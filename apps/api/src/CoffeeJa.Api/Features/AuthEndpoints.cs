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

/// <summary>Quem cria a conta (admin PJ / titular do buffet). Telefone comercial da empresa + WhatsApp pessoal.</summary>
public record ResponsavelDto(string Nome, string Cpf, string TelefoneComercial, string Whatsapp, string Email, string Senha);
public record RegistrarPjRequest(string RazaoSocial, string NomeFantasia, string Cnpj, Endereco Endereco, ResponsavelDto Responsavel, string? ConviteCodigo);
public record RegistrarFornecedorRequest(string RazaoSocial, string NomeFantasia, string Cnpj, Endereco Endereco, ResponsavelDto Responsavel,
    string? ConviteCodigo, double? Latitude, double? Longitude);
public record RegistrarPfRequest(string Nome, string Cpf, string Whatsapp, string Email, string Senha, Endereco Endereco,
    bool NotaFiscalMesmoEndereco, Endereco? EnderecoNotaFiscal, string? ConviteCodigo);

public record SessaoDto(string Token, UsuarioDto Usuario);
public record UsuarioDto(Guid Id, string Nome, string? Email, string Papel, string Funcao, string? Empresa, bool? Aprovado, string? TipoConta, string? Setor);

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth").RequireRateLimiting("auth");

        // Login por e-mail ou CPF (funcionários e acessos adicionais não têm e-mail).
        g.MapPost("/login", async (LoginRequest r, CoffeeJaDbContext db, TokenService tokens) =>
        {
            var id = (r.Email ?? "").Trim().ToLowerInvariant();
            var cpf = id.Contains('@') ? "" : Documentos.Digitos(id);
            var u = await db.Usuarios.Include(x => x.Contratante).Include(x => x.Fornecedor)
                .FirstOrDefaultAsync(x => id.Contains('@') ? x.Email == id : (cpf.Length == 11 && x.Cpf == cpf));
            var hasher = new PasswordHasher<Usuario>();
            if (u is null || string.IsNullOrEmpty(u.SenhaHash) || hasher.VerifyHashedPassword(u, u.SenhaHash, r.Senha ?? "") == PasswordVerificationResult.Failed)
                return Results.Problem("E-mail/CPF ou senha inválidos.", statusCode: 401);

            var bloqueio = u.Status switch
            {
                StatusUsuario.PendenteAprovacao => "Seu cadastro aguarda a aprovação do seu superior imediato.",
                StatusUsuario.AguardandoAtivacao => "Seu acesso foi aprovado. Use o link de primeiro acesso para criar sua senha.",
                StatusUsuario.Recusado => "Seu cadastro não foi aprovado. Fale com o administrador da sua empresa.",
                StatusUsuario.Inativo => "Este acesso está desativado.",
                _ => u.Contratante is { Ativo: false } ? "A conta da sua empresa está suspensa. Entre em contato com o suporte." : null,
            };
            return bloqueio is not null ? Results.Problem(bloqueio, statusCode: 403) : Results.Ok(new SessaoDto(tokens.Gerar(u), ParaDto(u)));
        });

        g.MapPost("/registrar-contratante-pj", async (RegistrarPjRequest r, CoffeeJaDbContext db, TokenService tokens) =>
        {
            if (ValidarPj(r.RazaoSocial, r.NomeFantasia, r.Cnpj, r.Endereco, r.Responsavel) is { } erro) return erro;
            if (await Duplicado(db, r.Responsavel.Email, r.Responsavel.Cpf, r.Cnpj) is { } dup) return dup;

            var u = NovoUsuario(r.Responsavel.Nome, r.Responsavel.Email, Documentos.Telefone(r.Responsavel.TelefoneComercial), r.Responsavel.Senha, Papel.Contratante);
            u.Cpf = Documentos.Digitos(r.Responsavel.Cpf);
            u.Whatsapp = Documentos.Telefone(r.Responsavel.Whatsapp);
            var endereco = Documentos.Normalizar(r.Endereco);
            u.Contratante = new Contratante
            {
                Tipo = TipoConta.PessoaJuridica, Empresa = r.RazaoSocial.Trim(), NomeFantasia = r.NomeFantasia.Trim(), Cnpj = Documentos.Digitos(r.Cnpj),
                Endereco = endereco, EnderecoNotaFiscal = endereco.Copiar(), NotaFiscalMesmoEndereco = true, UsuarioId = u.Id,
            };
            await AplicarConvite(db, u, r.ConviteCodigo);
            db.Usuarios.Add(u);
            await db.SaveChangesAsync();
            return Results.Ok(new SessaoDto(tokens.Gerar(u), ParaDto(u)));
        });

        g.MapPost("/registrar-contratante-pf", async (RegistrarPfRequest r, CoffeeJaDbContext db, TokenService tokens) =>
        {
            var nfEnd = r.NotaFiscalMesmoEndereco ? r.Endereco : r.EnderecoNotaFiscal;
            if (Validacao.Erros(
                    (string.IsNullOrWhiteSpace(r.Nome) || r.Nome.Trim().Length < 5, "nome", "Informe o nome completo."),
                    (!Documentos.CpfValido(r.Cpf), "cpf", "CPF inválido."),
                    (!Documentos.CelularValido(r.Whatsapp), "whatsapp", "Informe um WhatsApp válido com DDD."),
                    (string.IsNullOrWhiteSpace(r.Email) || !r.Email.Contains('@') || r.Email.Length > 200, "email", "E-mail inválido."),
                    (r.Senha is null || r.Senha.Length < 8, "senha", "A senha deve ter ao menos 8 caracteres.")) is { } e1) return e1;
            var msg = Documentos.ValidarEndereco(r.Endereco, "Endereço") ?? Documentos.ValidarEndereco(nfEnd, "Endereço da nota fiscal");
            if (msg is not null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["endereco"] = [msg] });
            if (await Duplicado(db, r.Email, r.Cpf, null) is { } dup) return dup;

            var u = NovoUsuario(r.Nome, r.Email, null, r.Senha, Papel.Contratante);
            u.Cpf = Documentos.Digitos(r.Cpf);
            u.Whatsapp = Documentos.Telefone(r.Whatsapp);
            u.Contratante = new Contratante
            {
                Tipo = TipoConta.PessoaFisica, Empresa = r.Nome.Trim(), Cpf = u.Cpf, Endereco = Documentos.Normalizar(r.Endereco),
                EnderecoNotaFiscal = Documentos.Normalizar(nfEnd!), NotaFiscalMesmoEndereco = r.NotaFiscalMesmoEndereco, UsuarioId = u.Id,
            };
            await AplicarConvite(db, u, r.ConviteCodigo);
            db.Usuarios.Add(u);
            await db.SaveChangesAsync();
            return Results.Ok(new SessaoDto(tokens.Gerar(u), ParaDto(u)));
        });

        g.MapPost("/registrar-fornecedor", async (RegistrarFornecedorRequest r, CoffeeJaDbContext db, TokenService tokens, GeocodingService geo,
            IOptions<PlataformaOptions> plat, CancellationToken ct) =>
        {
            if (ValidarPj(r.RazaoSocial, r.NomeFantasia, r.Cnpj, r.Endereco, r.Responsavel) is { } erro) return erro;
            if (await Duplicado(db, r.Responsavel.Email, r.Responsavel.Cpf, r.Cnpj, fornecedor: true) is { } dup) return dup;

            var endereco = Documentos.Normalizar(r.Endereco);
            var (lat, lon) = (r.Latitude, r.Longitude);
            if (lat is null || lon is null)
            {
                var c = await geo.BuscarAsync($"{endereco.Logradouro}, {endereco.Numero}, {endereco.Bairro}, {endereco.Cidade}, {endereco.Uf}", ct)
                        ?? await geo.BuscarAsync($"{endereco.Cep}, {endereco.Cidade}, {endereco.Uf}", ct)
                        ?? await geo.BuscarAsync($"{endereco.Cidade}, {endereco.Uf}", ct);
                if (c is null) return Results.Problem("Não conseguimos localizar o endereço informado. Confira o CEP, o logradouro e a cidade.", statusCode: 422);
                (lat, lon) = (c.Latitude, c.Longitude);
            }
            if (lat is < -34 or > 6 || lon is < -74 or > -32) return Results.Problem("Localização fora do Brasil.", statusCode: 422);

            var u = NovoUsuario(r.Responsavel.Nome, r.Responsavel.Email, Documentos.Telefone(r.Responsavel.TelefoneComercial), r.Responsavel.Senha, Papel.Fornecedor);
            u.Cpf = Documentos.Digitos(r.Responsavel.Cpf);
            u.Whatsapp = Documentos.Telefone(r.Responsavel.Whatsapp);
            u.Fornecedor = new Fornecedor
            {
                UsuarioId = u.Id, NomeFantasia = r.NomeFantasia.Trim(), RazaoSocial = r.RazaoSocial.Trim(), Cnpj = Documentos.Digitos(r.Cnpj),
                Endereco = endereco, Cidade = endereco.Cidade, Uf = endereco.Uf, Latitude = lat.Value, Longitude = lon.Value,
                RaioAtendimentoKm = plat.Value.RaioMaximoKm, Aprovado = plat.Value.AprovarFornecedoresAutomaticamente,
            };
            await AplicarConvite(db, u, r.ConviteCodigo);
            db.Usuarios.Add(u);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new SessaoDto(tokens.Gerar(u), ParaDto(u)));
        });

        app.MapGet("/api/auth/me", [Authorize] async (ClaimsPrincipal user, CoffeeJaDbContext db) =>
        {
            var u = await db.Usuarios.Include(x => x.Contratante).Include(x => x.Fornecedor).FirstOrDefaultAsync(x => x.Id == user.UsuarioId());
            return u is null || u.Status != StatusUsuario.Ativo ? Results.Unauthorized() : Results.Ok(ParaDto(u));
        }).WithTags("Auth");
    }

    private static IResult? ValidarPj(string razao, string fantasia, string cnpj, Endereco endereco, ResponsavelDto? resp)
    {
        if (resp is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["responsavel"] = ["Informe os dados do responsável."] });
        if (Validacao.Erros(
                (string.IsNullOrWhiteSpace(razao), "razaoSocial", "Informe a razão social."),
                (string.IsNullOrWhiteSpace(fantasia), "nomeFantasia", "Informe o nome fantasia."),
                (!Documentos.CnpjValido(cnpj), "cnpj", "CNPJ inválido."),
                (string.IsNullOrWhiteSpace(resp.Nome) || resp.Nome.Trim().Length < 5, "responsavel.nome", "Informe o nome completo do responsável."),
                (!Documentos.CpfValido(resp.Cpf), "responsavel.cpf", "CPF do responsável inválido."),
                (Documentos.Telefone(resp.TelefoneComercial) is null, "responsavel.telefoneComercial", "Informe o telefone comercial da empresa com DDD."),
                (!Documentos.CelularValido(resp.Whatsapp), "responsavel.whatsapp", "Informe o WhatsApp do responsável com DDD."),
                (string.IsNullOrWhiteSpace(resp.Email) || !resp.Email.Contains('@') || resp.Email.Length > 200, "responsavel.email", "E-mail inválido."),
                (resp.Senha is null || resp.Senha.Length < 8, "responsavel.senha", "A senha deve ter ao menos 8 caracteres.")) is { } e1) return e1;
        var msg = Documentos.ValidarEndereco(endereco, "Endereço da empresa");
        return msg is null ? null : Results.ValidationProblem(new Dictionary<string, string[]> { ["endereco"] = [msg] });
    }

    private static async Task<IResult?> Duplicado(CoffeeJaDbContext db, string email, string cpf, string? cnpj, bool fornecedor = false)
    {
        var e = email.Trim().ToLowerInvariant();
        var c = Documentos.Digitos(cpf);
        if (await db.Usuarios.AnyAsync(x => x.Email == e)) return Results.Conflict(new { detail = "Já existe uma conta com esse e-mail." });
        if (await db.Usuarios.AnyAsync(x => x.Cpf == c)) return Results.Conflict(new { detail = "Já existe uma conta com esse CPF." });
        if (cnpj is not null)
        {
            var n = Documentos.Digitos(cnpj);
            if (await db.Contratantes.AnyAsync(x => x.Cnpj == n) || await db.Fornecedores.AnyAsync(x => x.Cnpj == n))
                return Results.Conflict(new { detail = "Este CNPJ já possui cadastro." });
        }
        return null;
    }

    /// <summary>Vincula o novo usuário a quem o convidou (código de convite individual ou código pessoal do convidante).</summary>
    private static async Task AplicarConvite(CoffeeJaDbContext db, Usuario novo, string? codigo)
    {
        codigo = codigo?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(codigo)) return;
        var convite = await db.Convites.FirstOrDefaultAsync(c => c.Codigo == codigo);
        if (convite is not null)
        {
            novo.IndicadoPorId = convite.ConvidanteId;
            if (convite.Status == StatusConvite.Enviado)
            {
                convite.Status = StatusConvite.Cadastrado;
                convite.CadastradoUsuarioId = novo.Id;
                convite.CadastradoEm = DateTime.UtcNow;
            }
            return;
        }
        var dono = await db.Usuarios.Where(x => x.CodigoConvite == codigo).Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
        if (dono is not null) novo.IndicadoPorId = dono;
    }

    public static Usuario NovoUsuario(string nome, string? email, string? tel, string? senha, Papel papel)
    {
        var u = new Usuario
        {
            Nome = nome.Trim(), Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(), Telefone = tel, Papel = papel,
            CodigoConvite = Tokens.Codigo(),
        };
        if (!string.IsNullOrEmpty(senha)) u.SenhaHash = new PasswordHasher<Usuario>().HashPassword(u, senha);
        return u;
    }

    public static string HashSenha(Usuario u, string senha) => new PasswordHasher<Usuario>().HashPassword(u, senha);

    public static UsuarioDto ParaDto(Usuario u) => new(u.Id, u.Nome, u.Email, u.Papel.ToString(), u.Funcao.ToString(),
        u.Contratante?.NomeFantasia ?? u.Contratante?.Empresa ?? u.Fornecedor?.NomeFantasia, u.Fornecedor?.Aprovado, u.Contratante?.Tipo.ToString(), u.Setor);
}
