using System.Security.Claims;
using CoffeeJa.Api.Common;
using CoffeeJa.Api.Services;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoffeeJa.Api.Features;

public record NotificacaoDto(Guid Id, string Titulo, string Texto, string? Link, bool Lida, DateTime CriadoEm);
public record FornecedorAdminDto(Guid Id, string NomeFantasia, string? Cnpj, string Cidade, string Uf, string Email, string? Telefone,
    bool Aprovado, bool Fundador, double NotaMedia, int TotalAvaliacoes, DateTime CriadoEm);
public record FundadorRequest(bool Fundador);

public static class OutrosEndpoints
{
    public static void MapOutros(this IEndpointRouteBuilder app)
    {
        var n = app.MapGroup("/api/notificacoes").WithTags("Notificações").RequireAuthorization();

        n.MapGet("/", async (ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var uid = u.UsuarioId();
            return await db.Notificacoes.AsNoTracking().Where(x => x.UsuarioId == uid).OrderByDescending(x => x.CriadoEm).Take(50)
                .Select(x => new NotificacaoDto(x.Id, x.Titulo, x.Texto, x.Link, x.Lida, x.CriadoEm)).ToListAsync();
        });

        n.MapPost("/{id:guid}/lida", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var uid = u.UsuarioId();
            await db.Notificacoes.Where(x => x.Id == id && x.UsuarioId == uid).ExecuteUpdateAsync(s => s.SetProperty(x => x.Lida, true));
            return Results.NoContent();
        });

        n.MapPost("/lidas", async (ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var uid = u.UsuarioId();
            await db.Notificacoes.Where(x => x.UsuarioId == uid && !x.Lida).ExecuteUpdateAsync(s => s.SetProperty(x => x.Lida, true));
            return Results.NoContent();
        });

        app.MapGet("/api/geo/buscar", async (string q, GeocodingService geo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 4) return Results.BadRequest(new { detail = "Informe um endereço ou CEP." });
            var c = await geo.BuscarAsync(q, ct);
            return c is null ? Results.NotFound(new { detail = "Endereço não encontrado." }) : Results.Ok(c);
        }).WithTags("Geo").RequireAuthorization().RequireRateLimiting("geo");

        app.MapGet("/api/dashboard", async (ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var cid = u.ContratanteId();
            var fid = u.FornecedorId();
            if (cid is not null)
            {
                var abertas = await db.Demandas.CountAsync(d => d.ContratanteId == cid && (d.Status == StatusDemanda.Aberta || d.Status == StatusDemanda.EmNegociacao));
                var propostas = await db.Propostas.CountAsync(p => p.Demanda.ContratanteId == cid && p.Status == StatusProposta.Enviada
                    && (p.Demanda.Status == StatusDemanda.Aberta || p.Demanda.Status == StatusDemanda.EmNegociacao));
                var pedidos = await db.Pedidos.CountAsync(p => p.ContratanteId == cid);
                var gasto = await db.Pedidos.Where(p => p.ContratanteId == cid && p.Status != StatusPedido.Cancelado).SumAsync(p => (decimal?)p.Valor) ?? 0;
                var semNota = await db.Pedidos.CountAsync(p => p.ContratanteId == cid && p.NotaFiscalArquivo == null && p.Status != StatusPedido.Cancelado);
                return Results.Ok(new { demandasAbertas = abertas, propostasRecebidas = propostas, pedidos, gastoTotal = gasto, pedidosSemNota = semNota });
            }
            if (fid is not null)
            {
                var f = await db.Fornecedores.AsNoTracking().FirstAsync(x => x.Id == fid);
                var enviadas = await db.Propostas.CountAsync(p => p.FornecedorId == fid);
                var aceitas = await db.Propostas.CountAsync(p => p.FornecedorId == fid && p.Status == StatusProposta.Aceita);
                var emAjuste = await db.Propostas.CountAsync(p => p.FornecedorId == fid && p.Status == StatusProposta.EmAjuste);
                var faturamento = await db.Pedidos.Where(p => p.FornecedorId == fid && p.Status != StatusPedido.Cancelado).SumAsync(p => (decimal?)p.Valor) ?? 0;
                var semNota = await db.Pedidos.CountAsync(p => p.FornecedorId == fid && p.NotaFiscalArquivo == null && p.Status != StatusPedido.Cancelado);
                return Results.Ok(new
                {
                    propostasEnviadas = enviadas, propostasAceitas = aceitas, propostasEmAjuste = emAjuste,
                    taxaConversao = enviadas == 0 ? 0 : Math.Round(100.0 * aceitas / enviadas, 1),
                    faturamento, notaMedia = f.NotaMedia, totalAvaliacoes = f.TotalAvaliacoes, pedidosSemNota = semNota, aprovado = f.Aprovado,
                });
            }
            return Results.Forbid();
        }).WithTags("Dashboard").RequireAuthorization();

        var a = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization(Politicas.Admin);

        a.MapGet("/resumo", async (CoffeeJaDbContext db) => Results.Ok(new
        {
            fornecedoresPendentes = await db.Fornecedores.CountAsync(f => !f.Aprovado),
            fornecedoresAtivos = await db.Fornecedores.CountAsync(f => f.Aprovado),
            contratantes = await db.Contratantes.CountAsync(),
            demandasAbertas = await db.Demandas.CountAsync(d => d.Status == StatusDemanda.Aberta || d.Status == StatusDemanda.EmNegociacao),
            pedidos = await db.Pedidos.CountAsync(),
            volume = await db.Pedidos.Where(p => p.Status != StatusPedido.Cancelado).SumAsync(p => (decimal?)p.Valor) ?? 0,
        }));

        a.MapGet("/fornecedores", async (CoffeeJaDbContext db, bool? aprovado) =>
        {
            var q = db.Fornecedores.AsNoTracking().AsQueryable();
            if (aprovado is not null) q = q.Where(f => f.Aprovado == aprovado);
            return await q.OrderBy(f => f.Aprovado).ThenByDescending(f => f.CriadoEm).Take(200)
                .Select(f => new FornecedorAdminDto(f.Id, f.NomeFantasia, f.Cnpj, f.Cidade, f.Uf, f.Usuario.Email, f.Usuario.Telefone,
                    f.Aprovado, f.Fundador, f.NotaMedia, f.TotalAvaliacoes, f.CriadoEm)).ToListAsync();
        });

        a.MapPost("/fornecedores/{id:guid}/aprovar", async (Guid id, CoffeeJaDbContext db, INotificador notif) =>
        {
            var f = await db.Fornecedores.FirstOrDefaultAsync(x => x.Id == id);
            if (f is null) return Results.NotFound();
            if (!f.Aprovado)
            {
                f.Aprovado = true;
                notif.Notificar(f.UsuarioId, "Cadastro aprovado", "Você já pode receber demandas e enviar propostas.", "/fornecedor/demandas");
                await db.SaveChangesAsync();
            }
            return Results.NoContent();
        });

        a.MapPost("/fornecedores/{id:guid}/suspender", async (Guid id, CoffeeJaDbContext db) =>
        {
            var n = await db.Fornecedores.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Aprovado, false));
            return n == 0 ? Results.NotFound() : Results.NoContent();
        });

        a.MapPost("/fornecedores/{id:guid}/fundador", async (Guid id, FundadorRequest r, CoffeeJaDbContext db) =>
        {
            var n = await db.Fornecedores.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Fundador, r.Fundador));
            return n == 0 ? Results.NotFound() : Results.NoContent();
        });
    }
}
