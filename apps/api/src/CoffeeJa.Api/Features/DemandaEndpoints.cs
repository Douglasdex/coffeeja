using System.Security.Claims;
using CoffeeJa.Api.Common;
using CoffeeJa.Api.Services;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoffeeJa.Api.Features;

public record CriarDemandaRequest(string Titulo, DateOnly DataEvento, TimeOnly HoraInicio, string Endereco, string Cidade, string Uf,
    double Latitude, double Longitude, int NumeroPessoas, Restricoes Restricoes, EstiloCoffee Estilo, string? Observacoes, DateTime? PrazoPropostas,
    Descartaveis Descartaveis = Descartaveis.Nenhum);

public record DemandaResumoDto(Guid Id, string Titulo, DateOnly DataEvento, TimeOnly HoraInicio, string Cidade, string Uf, int NumeroPessoas,
    Restricoes Restricoes, EstiloCoffee Estilo, StatusDemanda Status, int TotalPropostas, decimal? MenorValor, DateTime CriadoEm,
    double? DistanciaKm, Guid? MinhaPropostaId, bool? AtendeRestricoes, string? Empresa, Descartaveis Descartaveis = Descartaveis.Nenhum,
    int MensagensNaoLidas = 0);

public record DemandaDetalheDto(Guid Id, string Titulo, DateOnly DataEvento, TimeOnly HoraInicio, string? Endereco, string Cidade, string Uf,
    double Latitude, double Longitude, int NumeroPessoas, Restricoes Restricoes, EstiloCoffee Estilo, string? Observacoes, DateTime? PrazoPropostas,
    StatusDemanda Status, string Empresa, DateTime CriadoEm, Guid? PedidoId, Descartaveis Descartaveis,
    string? SolicitadoPor, string? SolicitanteSetor);

public static class DemandaEndpoints
{
    public static DateTime Hoje => DateTime.UtcNow.AddHours(-3).Date;

    public static void MapDemandas(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/demandas").WithTags("Demandas").RequireAuthorization();

        g.MapPost("/", async (CriarDemandaRequest r, ClaimsPrincipal u, CoffeeJaDbContext db, INotificador notif, IOptions<PlataformaOptions> plat) =>
        {
            var data = r.DataEvento.ToDateTime(TimeOnly.MinValue);
            if (Validacao.Erros(
                    (string.IsNullOrWhiteSpace(r.Titulo), nameof(r.Titulo), "Informe um título."),
                    (data < Hoje, nameof(r.DataEvento), "A data não pode estar no passado."),
                    (r.NumeroPessoas is < 5 or > 5000, nameof(r.NumeroPessoas), "Informe entre 5 e 5000 pessoas."),
                    (string.IsNullOrWhiteSpace(r.Endereco) || string.IsNullOrWhiteSpace(r.Cidade) || r.Uf?.Length != 2, nameof(r.Endereco), "Informe o local do evento."),
                    (r.Latitude is < -34 or > 6 || r.Longitude is < -74 or > -32, nameof(r.Latitude), "Localização inválida.")) is { } erro)
                return erro;

            var d = new Demanda
            {
                ContratanteId = u.ContratanteId()!.Value,
                Titulo = r.Titulo.Trim(), DataEvento = data, HoraInicio = r.HoraInicio.ToTimeSpan(),
                Endereco = r.Endereco.Trim(), Cidade = r.Cidade.Trim(), Uf = r.Uf!.ToUpperInvariant(),
                Latitude = r.Latitude, Longitude = r.Longitude, NumeroPessoas = r.NumeroPessoas,
                SolicitanteId = u.UsuarioId(), SolicitanteNome = u.NomeUsuario(), SolicitanteSetor = u.SetorUsuario(),
                Restricoes = r.Restricoes, Descartaveis = r.Descartaveis, Estilo = r.Estilo, Observacoes = r.Observacoes, PrazoPropostas = r.PrazoPropostas,
            };
            db.Demandas.Add(d);

            // Distribui a demanda: fornecedores aprovados dentro do raio (pré-filtro por caixa + distância exata).
            var maxKm = plat.Value.RaioMaximoKm;
            var (minLat, maxLat, minLon, maxLon) = Geo.CaixaDelimitadora(d.Latitude, d.Longitude, maxKm);
            var candidatos = await db.Fornecedores.AsNoTracking()
                .Where(f => f.Aprovado && f.Latitude >= minLat && f.Latitude <= maxLat && f.Longitude >= minLon && f.Longitude <= maxLon)
                .Select(f => new { f.Id, f.Latitude, f.Longitude, f.RaioAtendimentoKm }).ToListAsync();
            var proximos = candidatos.Where(f => Geo.DistanciaKm(d.Latitude, d.Longitude, f.Latitude, f.Longitude) <= Math.Min(f.RaioAtendimentoKm, maxKm)).Select(f => f.Id).ToList();
            foreach (var fid in proximos)
                await notif.NotificarFornecedorAsync(fid, $"Novo coffee break para {d.NumeroPessoas} pessoas",
                    $"{d.Cidade} · {r.DataEvento:dd/MM} às {r.HoraInicio:HH\\:mm}", $"/fornecedor/demandas/{d.Id}");

            await db.SaveChangesAsync();
            return Results.Created($"/api/demandas/{d.Id}", new { d.Id, notificados = proximos.Count });
        }).RequireAuthorization(Politicas.Contratante);

        // Contratante: minhas demandas
        g.MapGet("/", async (ClaimsPrincipal u, CoffeeJaDbContext db, StatusDemanda? status) =>
        {
            var cid = u.ContratanteId();
            var q = db.Demandas.AsNoTracking().Where(d => d.ContratanteId == cid);
            if (status is not null) q = q.Where(d => d.Status == status);
            var lista = await q.OrderByDescending(d => d.CriadoEm).Select(d => new DemandaResumoDto(d.Id, d.Titulo,
                DateOnly.FromDateTime(d.DataEvento), TimeOnly.FromTimeSpan(d.HoraInicio), d.Cidade, d.Uf, d.NumeroPessoas, d.Restricoes, d.Estilo, d.Status,
                d.Propostas.Count(p => p.Status != StatusProposta.Retirada), d.Propostas.Where(p => p.Status != StatusProposta.Retirada).Min(p => (decimal?)p.ValorTotal),
                d.CriadoEm, null, null, null, null, d.Descartaveis, 0)).ToListAsync();
            var naoLidas = await Conversas.PorDemandaAsync(db, u.UsuarioId(), cid, null);
            return lista.Select(d => d with { MensagensNaoLidas = naoLidas.GetValueOrDefault(d.Id) }).ToList();
        }).RequireAuthorization(Politicas.Contratante);

        // Fornecedor: demandas abertas dentro do raio
        g.MapGet("/abertas", async (ClaimsPrincipal u, CoffeeJaDbContext db, IOptions<PlataformaOptions> plat) =>
        {
            var f = await db.Fornecedores.AsNoTracking().FirstAsync(x => x.Id == u.FornecedorId());
            if (!f.Aprovado) return Results.Ok(Array.Empty<DemandaResumoDto>());
            var raio = Math.Min(f.RaioAtendimentoKm, plat.Value.RaioMaximoKm);
            var (minLat, maxLat, minLon, maxLon) = Geo.CaixaDelimitadora(f.Latitude, f.Longitude, raio);
            var hoje = Hoje;
            var agora = DateTime.UtcNow;

            var lista = await db.Demandas.AsNoTracking()
                .Where(d => (d.Status == StatusDemanda.Aberta || d.Status == StatusDemanda.EmNegociacao) && d.DataEvento >= hoje
                            && (d.PrazoPropostas == null || d.PrazoPropostas > agora)
                            && d.Latitude >= minLat && d.Latitude <= maxLat && d.Longitude >= minLon && d.Longitude <= maxLon)
                .OrderBy(d => d.DataEvento).ThenBy(d => d.HoraInicio)
                .Select(d => new
                {
                    d.Id, d.Titulo, d.DataEvento, d.HoraInicio, d.Cidade, d.Uf, d.NumeroPessoas, d.Restricoes, d.Estilo, d.Status, d.CriadoEm,
                    d.Latitude, d.Longitude, d.Descartaveis, Empresa = d.Contratante.NomeFantasia ?? d.Contratante.Empresa,
                    Total = d.Propostas.Count(p => p.Status != StatusProposta.Retirada),
                    Minha = d.Propostas.Where(p => p.FornecedorId == f.Id).Select(p => (Guid?)p.Id).FirstOrDefault(),
                }).ToListAsync();

            var naoLidas = await Conversas.PorDemandaAsync(db, u.UsuarioId(), null, f.Id);
            var res = lista
                .Select(d => (d, km: Geo.DistanciaKm(f.Latitude, f.Longitude, d.Latitude, d.Longitude)))
                .Where(x => x.km <= raio)
                .Select(x => new DemandaResumoDto(x.d.Id, x.d.Titulo, DateOnly.FromDateTime(x.d.DataEvento), TimeOnly.FromTimeSpan(x.d.HoraInicio),
                    x.d.Cidade, x.d.Uf, x.d.NumeroPessoas, x.d.Restricoes, x.d.Estilo, x.d.Status, x.d.Total, null, x.d.CriadoEm,
                    Math.Round(x.km, 1), x.d.Minha, (f.AtendeRestricoes & x.d.Restricoes) == x.d.Restricoes, x.d.Empresa, x.d.Descartaveis, naoLidas.GetValueOrDefault(x.d.Id)))
                .ToList();
            return Results.Ok(res);
        }).RequireAuthorization(Politicas.Fornecedor);

        g.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db, IOptions<PlataformaOptions> plat) =>
        {
            var d = await db.Demandas.AsNoTracking().Include(x => x.Contratante).FirstOrDefaultAsync(x => x.Id == id);
            if (d is null) return Results.NotFound();
            var pedidoId = await db.Pedidos.Where(p => p.DemandaId == id).Select(p => (Guid?)p.Id).FirstOrDefaultAsync();

            var cid = u.ContratanteId();
            var fid = u.FornecedorId();
            var ehDono = cid is not null && d.ContratanteId == cid;
            var ehVencedor = false;
            if (fid is not null)
            {
                var f = await db.Fornecedores.AsNoTracking().FirstAsync(x => x.Id == fid);
                var dentro = Geo.DistanciaKm(f.Latitude, f.Longitude, d.Latitude, d.Longitude) <= Math.Min(f.RaioAtendimentoKm, plat.Value.RaioMaximoKm);
                var temProposta = await db.Propostas.AnyAsync(p => p.DemandaId == id && p.FornecedorId == fid);
                if (!dentro && !temProposta) return Results.NotFound();
                ehVencedor = await db.Propostas.AnyAsync(p => p.DemandaId == id && p.FornecedorId == fid && p.Status == StatusProposta.Aceita);
            }
            else if (!ehDono && !u.IsInRole(nameof(Papel.Admin))) return Results.NotFound();

            // Endereço exato só é revelado ao dono e ao fornecedor contratado.
            var endereco = ehDono || ehVencedor || u.IsInRole(nameof(Papel.Admin)) ? d.Endereco : null;
            var (lat, lon) = endereco is null ? (Math.Round(d.Latitude, 2), Math.Round(d.Longitude, 2)) : (d.Latitude, d.Longitude);
            return Results.Ok(new DemandaDetalheDto(d.Id, d.Titulo, DateOnly.FromDateTime(d.DataEvento), TimeOnly.FromTimeSpan(d.HoraInicio), endereco,
                d.Cidade, d.Uf, lat, lon, d.NumeroPessoas, d.Restricoes, d.Estilo, d.Observacoes, d.PrazoPropostas, d.Status, d.Contratante.NomeFantasia ?? d.Contratante.Empresa, d.CriadoEm, pedidoId, d.Descartaveis, d.SolicitanteNome, d.SolicitanteSetor));
        });

        g.MapPost("/{id:guid}/cancelar", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db, INotificador notif) =>
        {
            var cid = u.ContratanteId();
            var d = await db.Demandas.Include(x => x.Propostas).ThenInclude(p => p.Fornecedor)
                .FirstOrDefaultAsync(x => x.Id == id && x.ContratanteId == cid);
            if (d is null) return Results.NotFound();
            if (d.Status is not (StatusDemanda.Aberta or StatusDemanda.EmNegociacao))
                return Results.Conflict(new { detail = "Só é possível cancelar demandas ainda em aberto." });
            d.Status = StatusDemanda.Cancelada;
            foreach (var p in d.Propostas.Where(p => p.Status is StatusProposta.Enviada or StatusProposta.EmAjuste))
            {
                p.Status = StatusProposta.Recusada;
                await notif.NotificarFornecedorAsync(p.FornecedorId, "Demanda cancelada", d.Titulo);
            }
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Politicas.Contratante);
    }
}
