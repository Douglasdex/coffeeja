using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CoffeeJa.Api.Common;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using WebPush;

namespace CoffeeJa.Api.Services;

public class PushOptions
{
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
    public string Subject { get; set; } = "mailto:contato@coffeeja.com.br";
    public bool Habilitado => PublicKey.Length > 0 && PrivateKey.Length > 0;
}

/// <summary>Chat da negociação em tempo real. A escrita continua pela API REST; o hub só entrega eventos.</summary>
[Authorize]
public class ChatHub(CoffeeJaDbContext db) : Hub
{
    public static string Grupo(Guid propostaId) => $"proposta:{propostaId}";

    public async Task EntrarProposta(Guid propostaId)
    {
        var fid = Context.User?.FornecedorId();
        var cid = Context.User?.ContratanteId();
        var participa = await db.Propostas.AnyAsync(p => p.Id == propostaId
            && ((fid != null && p.FornecedorId == fid) || (cid != null && p.Demanda.ContratanteId == cid)));
        if (!participa) throw new HubException("Sem acesso a esta conversa.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(propostaId));
    }

    public Task SairProposta(Guid propostaId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo(propostaId));
}

/// <summary>Depois de gravar no banco, avisa o navegador (SignalR) e o aparelho (Web Push). Falhas aqui nunca derrubam a requisição.</summary>
public class PosSalvarInterceptor(IHubContext<ChatHub> hub, IServiceScopeFactory scopes, IOptions<PushOptions> push, ILogger<PosSalvarInterceptor> log) : SaveChangesInterceptor
{
    private record Pendentes(List<(Guid UsuarioId, string Titulo, string Texto, string? Link)> Notificacoes, List<Guid> Mensagens);
    private readonly ConditionalWeakTable<DbContext, Pendentes> _pendentes = new();

    private void Capturar(DbContext? ctx)
    {
        if (ctx is null) return;
        var n = ctx.ChangeTracker.Entries<Notificacao>().Where(e => e.State == EntityState.Added)
            .Select(e => (e.Entity.UsuarioId, e.Entity.Titulo, e.Entity.Texto, e.Entity.Link)).ToList();
        var m = ctx.ChangeTracker.Entries<Mensagem>().Where(e => e.State == EntityState.Added).Select(e => e.Entity.PropostaId).ToList();
        _pendentes.Remove(ctx);
        if (n.Count > 0 || m.Count > 0) _pendentes.Add(ctx, new Pendentes(n, m));
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> r) { Capturar(e.Context); return r; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> r, CancellationToken ct = default)
    { Capturar(e.Context); return ValueTask.FromResult(r); }

    public override int SavedChanges(SaveChangesCompletedEventData e, int result)
    { _ = DispararAsync(e.Context); return result; }
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int result, CancellationToken ct = default)
    { _ = DispararAsync(e.Context); return ValueTask.FromResult(result); }

    public override void SaveChangesFailed(DbContextErrorEventData e) { if (e.Context is { } c) _pendentes.Remove(c); }
    public override Task SaveChangesFailedAsync(DbContextErrorEventData e, CancellationToken ct = default) { SaveChangesFailed(e); return Task.CompletedTask; }

    private async Task DispararAsync(DbContext? ctx)
    {
        if (ctx is null || !_pendentes.TryGetValue(ctx, out var p)) return;
        _pendentes.Remove(ctx);
        try
        {
            foreach (var propostaId in p.Mensagens.Distinct())
                await hub.Clients.Group(ChatHub.Grupo(propostaId)).SendAsync("mensagem", new { propostaId });
            foreach (var uid in p.Notificacoes.Select(n => n.UsuarioId).Distinct())
                await hub.Clients.User(uid.ToString()).SendAsync("notificacao");
        }
        catch (Exception ex) { log.LogWarning(ex, "Falha ao publicar evento em tempo real"); }

        if (push.Value.Habilitado && p.Notificacoes.Count > 0) _ = Task.Run(() => EnviarPushAsync(p.Notificacoes));
    }

    private async Task EnviarPushAsync(List<(Guid UsuarioId, string Titulo, string Texto, string? Link)> itens)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoffeeJaDbContext>();
            var ids = itens.Select(i => i.UsuarioId).Distinct().ToList();
            var inscricoes = await db.PushInscricoes.Where(x => ids.Contains(x.UsuarioId)).ToListAsync();
            if (inscricoes.Count == 0) return;

            var o = push.Value;
            var client = new WebPushClient();
            var vapid = new VapidDetails(o.Subject, o.PublicKey, o.PrivateKey);
            var mortas = new List<PushInscricao>();
            foreach (var i in inscricoes)
            foreach (var n in itens.Where(n => n.UsuarioId == i.UsuarioId))
            {
                var payload = JsonSerializer.Serialize(new { titulo = n.Titulo, texto = n.Texto, link = n.Link ?? "/" });
                try { await client.SendNotificationAsync(new PushSubscription(i.Endpoint, i.P256dh, i.Auth), payload, vapid); }
                catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound) { mortas.Add(i); }
                catch (Exception ex) { log.LogWarning(ex, "Falha no envio de push"); }
            }
            if (mortas.Count > 0) { db.PushInscricoes.RemoveRange(mortas.Distinct()); await db.SaveChangesAsync(); }
        }
        catch (Exception ex) { log.LogWarning(ex, "Falha ao processar push"); }
    }
}

public record InscreverPushRequest(string Endpoint, PushChaves Keys);
public record PushChaves(string P256dh, string Auth);

public static class PushEndpoints
{
    public static void MapPush(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/push").WithTags("Push").RequireAuthorization();

        g.MapGet("/chave", (IOptions<PushOptions> o) => o.Value.Habilitado ? Results.Ok(new { chave = o.Value.PublicKey }) : Results.NotFound());

        g.MapPost("/inscrever", async (InscreverPushRequest r, System.Security.Claims.ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            if (Validacao.Erros(
                    (string.IsNullOrWhiteSpace(r.Endpoint) || r.Endpoint.Length > 450 || !r.Endpoint.StartsWith("https://"), nameof(r.Endpoint), "Inscrição inválida."),
                    (string.IsNullOrWhiteSpace(r.Keys?.P256dh) || string.IsNullOrWhiteSpace(r.Keys?.Auth), nameof(r.Keys), "Chaves inválidas.")) is { } erro) return erro;
            var uid = u.UsuarioId();
            var atual = await db.PushInscricoes.FirstOrDefaultAsync(x => x.Endpoint == r.Endpoint);
            if (atual is null) db.PushInscricoes.Add(new PushInscricao { UsuarioId = uid, Endpoint = r.Endpoint, P256dh = r.Keys.P256dh, Auth = r.Keys.Auth });
            else { atual.UsuarioId = uid; atual.P256dh = r.Keys.P256dh; atual.Auth = r.Keys.Auth; }
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/cancelar", async (InscreverPushRequest r, System.Security.Claims.ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var uid = u.UsuarioId();
            await db.PushInscricoes.Where(x => x.Endpoint == r.Endpoint && x.UsuarioId == uid).ExecuteDeleteAsync();
            return Results.NoContent();
        });
    }
}
