using System.Security.Claims;
using CoffeeJa.Api.Common;
using CoffeeJa.Api.Services;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoffeeJa.Api.Features;

public record LoginRequest(string Email, string Senha);
public record RegistrarContratanteRequest(string Nome, string Email, string? Telefone, string Senha, string Empresa, string? Cnpj);
public record RegistrarFornecedorRequest(string Nome, string Email, string? Telefone, string Senha, string NomeFantasia, string? Cnpj,
    string Cidade, string Uf, double Latitude, double Longitude);
public record SessaoDto(string Token, UsuarioDto Usuario);
public record UsuarioDto(Guid Id, string Nome, string Email, string Papel, string? Empresa, bool? Aprovado);

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth").RequireRateLimiting("auth");

        g.MapPost("/login", async (LoginRequest r, CoffeeJaDbContext db, TokenService tokens) =>
        {
            var email = r.Email.Trim().ToLowerInvariant();
            var u = await db.Usuarios.Include(x => x.Contratante).Include(x => x.Fornecedor)
                .FirstOrDefaultAsync(x => x.Email == email && x.Ativo);
            var hasher = new PasswordHasher<Usuario>();
            if (u is null || hasher.VerifyHashedPassword(u, u.SenhaHash, r.Senha) == PasswordVerificationResult.Failed)
                return Results.Problem("E-mail ou senha inválidos.", statusCode: 401);
            return Results.Ok(new SessaoDto(tokens.Gerar(u), ParaDto(u)));
        });

        g.MapPost("/registrar-contratante", async (RegistrarContratanteRequest r, CoffeeJaDbContext db, TokenService tokens) =>
        {
            if (Validar(r.Nome, r.Email, r.Senha) is { } erro) return erro;
            if (Validacao.Erros((string.IsNullOrWhiteSpace(r.Empresa), nameof(r.Empresa), "Informe a empresa.")) is { } e2) return e2;
            var email = r.Email.Trim().ToLowerInvariant();
            if (await db.Usuarios.AnyAsync(x => x.Email == email))
                return Results.Conflict(new { detail = "Já existe uma conta com esse e-mail." });

            var u = NovoUsuario(r.Nome, r.Email, r.Telefone, r.Senha, Papel.Contratante);
            u.Contratante = new Contratante { Empresa = r.Empresa.Trim(), Cnpj = r.Cnpj };
            db.Usuarios.Add(u);
            await db.SaveChangesAsync();
            return Results.Ok(new SessaoDto(tokens.Gerar(u), ParaDto(u)));
        });

        g.MapPost("/registrar-fornecedor", async (RegistrarFornecedorRequest r, CoffeeJaDbContext db, TokenService tokens, IOptions<PlataformaOptions> plat) =>
        {
            if (Validar(r.Nome, r.Email, r.Senha) is { } erro) return erro;
            if (Validacao.Erros(
                    (string.IsNullOrWhiteSpace(r.NomeFantasia), nameof(r.NomeFantasia), "Informe o nome do buffet."),
                    (string.IsNullOrWhiteSpace(r.Cidade) || r.Uf?.Length != 2, nameof(r.Cidade), "Informe cidade e UF."),
                    (r.Latitude is < -34 or > 6 || r.Longitude is < -74 or > -32, nameof(r.Latitude), "Localização inválida.")) is { } e2) return e2;
            var email = r.Email.Trim().ToLowerInvariant();
            if (await db.Usuarios.AnyAsync(x => x.Email == email))
                return Results.Conflict(new { detail = "Já existe uma conta com esse e-mail." });

            var u = NovoUsuario(r.Nome, r.Email, r.Telefone, r.Senha, Papel.Fornecedor);
            u.Fornecedor = new Fornecedor
            {
                NomeFantasia = r.NomeFantasia.Trim(), Cnpj = r.Cnpj, Cidade = r.Cidade.Trim(), Uf = r.Uf!.ToUpperInvariant(),
                Latitude = r.Latitude, Longitude = r.Longitude, RaioAtendimentoKm = plat.Value.RaioMaximoKm,
                Aprovado = plat.Value.AprovarFornecedoresAutomaticamente,
            };
            db.Usuarios.Add(u);
            await db.SaveChangesAsync();
            return Results.Ok(new SessaoDto(tokens.Gerar(u), ParaDto(u)));
        });

        app.MapGet("/api/auth/me", [Authorize] async (ClaimsPrincipal user, CoffeeJaDbContext db) =>
        {
            var u = await db.Usuarios.Include(x => x.Contratante).Include(x => x.Fornecedor).FirstOrDefaultAsync(x => x.Id == user.UsuarioId());
            return u is null ? Results.Unauthorized() : Results.Ok(ParaDto(u));
        }).WithTags("Auth");
    }

    public static Usuario NovoUsuario(string nome, string email, string? tel, string senha, Papel papel)
    {
        var u = new Usuario { Nome = nome.Trim(), Email = email.Trim().ToLowerInvariant(), Telefone = tel, Papel = papel };
        u.SenhaHash = new PasswordHasher<Usuario>().HashPassword(u, senha);
        return u;
    }

    private static IResult? Validar(string nome, string email, string senha) => Validacao.Erros(
        (string.IsNullOrWhiteSpace(nome), "nome", "Informe o nome."),
        (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.Length > 200, "email", "E-mail inválido."),
        (senha is null || senha.Length < 8, "senha", "A senha deve ter ao menos 8 caracteres."));

    private static UsuarioDto ParaDto(Usuario u) => new(u.Id, u.Nome, u.Email, u.Papel.ToString(),
        u.Contratante?.Empresa ?? u.Fornecedor?.NomeFantasia, u.Fornecedor?.Aprovado);
}
