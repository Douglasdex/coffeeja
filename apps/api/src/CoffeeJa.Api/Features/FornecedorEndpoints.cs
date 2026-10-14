using System.Security.Claims;
using CoffeeJa.Api.Common;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoffeeJa.Api.Features;

public record PerfilFornecedorDto(Guid Id, string NomeFantasia, string? Cnpj, string? Descricao, string Cidade, string Uf,
    double Latitude, double Longitude, int RaioAtendimentoKm, Restricoes AtendeRestricoes, bool Aprovado, bool Fundador, double NotaMedia, int TotalAvaliacoes);
public record AtualizarPerfilRequest(string NomeFantasia, string? Descricao, string Cidade, string Uf, double Latitude, double Longitude,
    int RaioAtendimentoKm, Restricoes AtendeRestricoes);
public record CardapioDto(Guid Id, string Nome, EstiloCoffee Estilo, string Itens, decimal ValorPorPessoa, int MinimoPessoas);
public record CardapioRequest(string Nome, EstiloCoffee Estilo, string Itens, decimal ValorPorPessoa, int MinimoPessoas);

public static class FornecedorEndpoints
{
    public static void MapFornecedor(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/fornecedor").WithTags("Fornecedor").RequireAuthorization(Politicas.Fornecedor);

        g.MapGet("/perfil", async (ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var f = await db.Fornecedores.AsNoTracking().FirstAsync(x => x.Id == u.FornecedorId());
            return Results.Ok(Dto(f));
        });

        g.MapPut("/perfil", async (AtualizarPerfilRequest r, ClaimsPrincipal u, CoffeeJaDbContext db, IOptions<PlataformaOptions> plat) =>
        {
            if (Validacao.Erros(
                    (string.IsNullOrWhiteSpace(r.NomeFantasia), nameof(r.NomeFantasia), "Informe o nome."),
                    (r.RaioAtendimentoKm < 1, nameof(r.RaioAtendimentoKm), "Raio inválido.")) is { } erro) return erro;
            var f = await db.Fornecedores.FirstAsync(x => x.Id == u.FornecedorId());
            f.NomeFantasia = r.NomeFantasia.Trim();
            f.Descricao = r.Descricao;
            f.Cidade = r.Cidade.Trim();
            f.Uf = r.Uf!.ToUpperInvariant();
            f.Latitude = r.Latitude;
            f.Longitude = r.Longitude;
            f.RaioAtendimentoKm = Math.Min(r.RaioAtendimentoKm, plat.Value.RaioMaximoKm);
            f.AtendeRestricoes = r.AtendeRestricoes;
            await db.SaveChangesAsync();
            return Results.Ok(Dto(f));
        });

        g.MapGet("/cardapios", async (ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var fid = u.FornecedorId();
            return await db.ModelosCardapio.AsNoTracking().Where(x => x.FornecedorId == fid)
                .OrderBy(x => x.Estilo).Select(x => new CardapioDto(x.Id, x.Nome, x.Estilo, x.Itens, x.ValorPorPessoa, x.MinimoPessoas)).ToListAsync();
        });

        g.MapPost("/cardapios", async (CardapioRequest r, ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            if (Validacao.Erros(
                    (string.IsNullOrWhiteSpace(r.Nome), nameof(r.Nome), "Informe o nome."),
                    (string.IsNullOrWhiteSpace(r.Itens), nameof(r.Itens), "Liste os itens."),
                    (r.ValorPorPessoa <= 0, nameof(r.ValorPorPessoa), "Valor inválido.")) is { } erro) return erro;
            var c = new ModeloCardapio
            {
                FornecedorId = u.FornecedorId()!.Value, Nome = r.Nome.Trim(), Estilo = r.Estilo, Itens = r.Itens.Trim(),
                ValorPorPessoa = r.ValorPorPessoa, MinimoPessoas = Math.Max(1, r.MinimoPessoas),
            };
            db.ModelosCardapio.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/api/fornecedor/cardapios/{c.Id}", new CardapioDto(c.Id, c.Nome, c.Estilo, c.Itens, c.ValorPorPessoa, c.MinimoPessoas));
        });

        g.MapDelete("/cardapios/{id:guid}", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var fid = u.FornecedorId();
            var n = await db.ModelosCardapio.Where(x => x.Id == id && x.FornecedorId == fid).ExecuteDeleteAsync();
            return n == 0 ? Results.NotFound() : Results.NoContent();
        });
    }

    private static PerfilFornecedorDto Dto(Fornecedor f) => new(f.Id, f.NomeFantasia, f.Cnpj, f.Descricao, f.Cidade, f.Uf, f.Latitude,
        f.Longitude, f.RaioAtendimentoKm, f.AtendeRestricoes, f.Aprovado, f.Fundador, f.NotaMedia, f.TotalAvaliacoes);
}
