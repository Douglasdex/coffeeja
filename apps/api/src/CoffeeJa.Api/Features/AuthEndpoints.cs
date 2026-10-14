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

public record LoginRequest(string Email, string Senha, string? Codigo = null);

/// <summary>Quem cria a conta (admin PJ / titular do buffet). Telefone comercial da empresa + WhatsApp pessoal.</summary>
public record ResponsavelDto(string Nome, string Cpf, string TelefoneComercial, string Whatsapp, string Email, string Senha);
public record RegistrarPjRequest(string RazaoSocial, string NomeFantasia, string Cnpj, Endereco Endereco, ResponsavelDto Responsavel, string? ConviteCodigo,
    bool AceiteTermos = false);
public record RegistrarFornecedorRequest(string RazaoSocial, string NomeFantasia, string Cnpj, Endereco Endereco, ResponsavelDto Responsavel,
    string? ConviteCodigo, double? Latitude, double? Longitude, bool AceiteTermos = false);
public record RegistrarPfRequest(string Nome, string Cpf, string Whatsapp, string Email, string Senha, Endereco Endereco,
    bool NotaFiscalMesmoEndereco, Endereco? EnderecoNotaFiscal, string? ConviteCodigo, bool AceiteTermos = false);

public record SessaoDto(string Token, UsuarioDto Usuario);
public record UsuarioDto(Guid Id, string Nome, string? Email, string Papel, string Funcao, string? Empresa, bool? Aprovado, string? TipoConta, string? Setor,
    bool TotpAtivo, bool MfaPendente);

public static class AuthEndpoints
{
    // Hash de uma senha qualquer, usado para gastar o mesmo tempo quando o usuário não existe (evita descobrir contas pelo tempo de resposta).
    private static readonly string HashFalso = new PasswordHasher<Usuario>().HashPassword(new Usuario(), "senha-falsa-para-igualar-tempo");

    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth").RequireRateLimiting("auth");

        // Login por e-mail ou CPF (funcionários e acessos adicionais não têm e-mail).
        g.MapPost("/login", async (LoginRequest r, HttpContext http, CoffeeJaDbContext db, TokenService tokens, AuditoriaService audit, SegredoProtetor protetor,
            SessaoCache sessoes, IOptions<PlataformaOptions> plat) =>
        {
            var o = plat.Value;
            var id = (r.Email ?? "").Trim().ToLowerInvariant();
            var cpf = id.Contains('@') ? "" : Documentos.Digitos(id);
            var u = await db.Usuarios.Include(x => x.Contratante).Include(x => x.Fornecedor)
                .FirstOrDefaultAsync(x => id.Contains('@') ? x.Email == id : (cpf.Length == 11 && x.Cpf == cpf));
            var hasher = new PasswordHasher<Usuario>();

            if (u?.BloqueadoAte > DateTime.UtcNow)
            {
                await audit.RegistrarAsync("LOGIN_BLOQUEADO", u.Id, detalhe: "tentativa durante bloqueio");
                var min = (int)Math.Ceiling((u.BloqueadoAte.Value - DateTime.UtcNow).TotalMinutes);
                return Results.Problem($"Muitas tentativas incorretas. Tente novamente em {min} min.", statusCode: 429);
            }

            var alvo = u?.SenhaHash is { Length: > 0 } h ? h : HashFalso;
            var senhaOk = hasher.VerifyHashedPassword(u ?? new Usuario(), alvo, r.Senha ?? "") != PasswordVerificationResult.Failed && u is { SenhaHash.Length: > 0 };

            async Task<IResult> Falha(string motivo)
            {
                if (u is not null)
                {
                    u.FalhasLogin++;
                    if (u.FalhasLogin >= o.LoginTentativasMax)
                    {
                        u.BloqueadoAte = DateTime.UtcNow.AddMinutes(o.LoginBloqueioMinutos);
                        u.FalhasLogin = 0;
                        await audit.RegistrarAsync("LOGIN_BLOQUEIO", u.Id, detalhe: $"{o.LoginTentativasMax} falhas seguidas");
                    }
                    await db.SaveChangesAsync();
                }
                await audit.RegistrarAsync("LOGIN_FALHA", u?.Id, alvo: id.Contains('@') ? id : MascararId(id), detalhe: motivo);
                return Results.Problem("E-mail/CPF ou senha inválidos.", statusCode: 401);
            }

            if (!senhaOk) return await Falha("senha");

            var bloqueio = u!.Status switch
            {
                StatusUsuario.PendenteAprovacao => "Seu cadastro aguarda a aprovação do seu superior imediato.",
                StatusUsuario.AguardandoAtivacao => "Seu acesso foi aprovado. Use o link de primeiro acesso para criar sua senha.",
                StatusUsuario.Recusado => "Seu cadastro não foi aprovado. Fale com o administrador da sua empresa.",
                StatusUsuario.Inativo => "Este acesso está desativado.",
                _ => u.Contratante is { Ativo: false } ? "A conta da sua empresa está suspensa. Entre em contato com o suporte." : null,
            };
            if (bloqueio is not null)
            {
                await audit.RegistrarAsync("LOGIN_NEGADO", u.Id, detalhe: u.Status.ToString());
                return Results.Problem(bloqueio, statusCode: 403);
            }

            // Segundo fator (TOTP ou código de recuperação)
            if (u.TotpAtivo)
            {
                if (string.IsNullOrWhiteSpace(r.Codigo))
                    return Results.Json(new { title = "Código de verificação", detail = "Informe o código do aplicativo autenticador.", status = 401, requer2fa = true }, statusCode: 401);
                var segredo = protetor.Revelar(u.TotpSegredoProtegido);
                var passo = segredo is null ? null : Totp.Verificar(segredo, r.Codigo);
                if (passo is { } p && p > u.TotpUltimoPasso) u.TotpUltimoPasso = p;
                else if (!Totp.ConsumirRecuperacao(u, r.Codigo)) return await Falha("2fa");
                else await audit.RegistrarAsync("2FA_RECUPERACAO_USADO", u.Id);
            }

            u.FalhasLogin = 0;
            u.BloqueadoAte = null;
            await db.SaveChangesAsync();
            await audit.RegistrarAsync("LOGIN_OK", u.Id);
            return Results.Ok(Abrir(http, tokens, u));
        });

        g.MapPost("/logout", async (HttpContext http, AuditoriaService audit, ClaimsPrincipal user) =>
        {
            SessaoCookie.Remover(http);
            if (user.Identity?.IsAuthenticated == true) await audit.RegistrarAsync("LOGOUT", user.UsuarioId());
            return Results.NoContent();
        }).AllowAnonymous();

        g.MapPost("/registrar-contratante-pj", async (RegistrarPjRequest r, HttpContext http, CoffeeJaDbContext db, TokenService tokens, AuditoriaService audit) =>
        {
            if (ValidarPj(r.RazaoSocial, r.NomeFantasia, r.Cnpj, r.Endereco, r.Responsavel, r.AceiteTermos) is { } erro) return erro;
            if (await Duplicado(db, r.Responsavel.Email, r.Responsavel.Cpf, r.Cnpj) is { } dup) return dup;

            var u = NovoUsuario(r.Responsavel.Nome, r.Responsavel.Email, Documentos.Telefone(r.Responsavel.TelefoneComercial), r.Responsavel.Senha, Papel.Contratante);
            u.Cpf = Documentos.Digitos(r.Responsavel.Cpf);
            u.Whatsapp = Documentos.Telefone(r.Responsavel.Whatsapp);
            RegistrarAceite(u);
            var endereco = Documentos.Normalizar(r.Endereco);
            u.Contratante = new Contratante
            {
                Tipo = TipoConta.PessoaJuridica, Empresa = r.RazaoSocial.Trim(), NomeFantasia = r.NomeFantasia.Trim(), Cnpj = Documentos.Digitos(r.Cnpj),
                Endereco = endereco, EnderecoNotaFiscal = endereco.Copiar(), NotaFiscalMesmoEndereco = true, UsuarioId = u.Id,
            };
            await AplicarConvite(db, u, r.ConviteCodigo);
            db.Usuarios.Add(u);
            await db.SaveChangesAsync();
            await audit.RegistrarAsync("CADASTRO", u.Id, "contratante-pj");
            return Results.Ok(Abrir(http, tokens, u));
        });

        g.MapPost("/registrar-contratante-pf", async (RegistrarPfRequest r, HttpContext http, CoffeeJaDbContext db, TokenService tokens, AuditoriaService audit) =>
        {
            var nfEnd = r.NotaFiscalMesmoEndereco ? r.Endereco : r.EnderecoNotaFiscal;
            if (Validacao.Erros(
                    (string.IsNullOrWhiteSpace(r.Nome) || r.Nome.Trim().Length < 5, "nome", "Informe o nome completo."),
                    (!Documentos.CpfValido(r.Cpf), "cpf", "CPF inválido."),
                    (!Documentos.CelularValido(r.Whatsapp), "whatsapp", "Informe um WhatsApp válido com DDD."),
                    (string.IsNullOrWhiteSpace(r.Email) || !r.Email.Contains('@') || r.Email.Length > 200, "email", "E-mail inválido."),
                    (PoliticaSenha.Validar(r.Senha, r.Email, r.Nome) is not null, "senha", PoliticaSenha.Validar(r.Senha, r.Email, r.Nome) ?? ""),
                    (!r.AceiteTermos, "aceiteTermos", "Aceite os Termos de Uso e a Política de Privacidade.")) is { } e1) return e1;
            var msg = Documentos.ValidarEndereco(r.Endereco, "Endereço") ?? Documentos.ValidarEndereco(nfEnd, "Endereço da nota fiscal");
            if (msg is not null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["endereco"] = [msg] });
            if (await Duplicado(db, r.Email, r.Cpf, null) is { } dup) return dup;

            var u = NovoUsuario(r.Nome, r.Email, null, r.Senha, Papel.Contratante);
            u.Cpf = Documentos.Digitos(r.Cpf);
            u.Whatsapp = Documentos.Telefone(r.Whatsapp);
            RegistrarAceite(u);
            u.Contratante = new Contratante
            {
                Tipo = TipoConta.PessoaFisica, Empresa = r.Nome.Trim(), Cpf = u.Cpf, Endereco = Documentos.Normalizar(r.Endereco),
                EnderecoNotaFiscal = Documentos.Normalizar(nfEnd!), NotaFiscalMesmoEndereco = r.NotaFiscalMesmoEndereco, UsuarioId = u.Id,
            };
            await AplicarConvite(db, u, r.ConviteCodigo);
            db.Usuarios.Add(u);
            await db.SaveChangesAsync();
            await audit.RegistrarAsync("CADASTRO", u.Id, "contratante-pf");
            return Results.Ok(Abrir(http, tokens, u));
        });

        g.MapPost("/registrar-fornecedor", async (RegistrarFornecedorRequest r, HttpContext http, CoffeeJaDbContext db, TokenService tokens, GeocodingService geo,
            IOptions<PlataformaOptions> plat, AuditoriaService audit, CancellationToken ct) =>
        {
            if (ValidarPj(r.RazaoSocial, r.NomeFantasia, r.Cnpj, r.Endereco, r.Responsavel, r.AceiteTermos) is { } erro) return erro;
            if (await Duplicado(db, r.Responsavel.Email, r.Responsavel.Cpf, r.Cnpj) is { } dup) return dup;

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
            RegistrarAceite(u);
            u.Fornecedor = new Fornecedor
            {
                UsuarioId = u.Id, NomeFantasia = r.NomeFantasia.Trim(), RazaoSocial = r.RazaoSocial.Trim(), Cnpj = Documentos.Digitos(r.Cnpj),
                Endereco = endereco, Cidade = endereco.Cidade, Uf = endereco.Uf, Latitude = lat.Value, Longitude = lon.Value,
                RaioAtendimentoKm = plat.Value.RaioMaximoKm, Aprovado = plat.Value.AprovarFornecedoresAutomaticamente,
            };
            await AplicarConvite(db, u, r.ConviteCodigo);
            db.Usuarios.Add(u);
            await db.SaveChangesAsync(ct);
            await audit.RegistrarAsync("CADASTRO", u.Id, "fornecedor");
            return Results.Ok(Abrir(http, tokens, u));
        });

        app.MapGet("/api/auth/me", [Authorize] async (ClaimsPrincipal user, CoffeeJaDbContext db) =>
        {
            var u = await db.Usuarios.Include(x => x.Contratante).Include(x => x.Fornecedor).FirstOrDefaultAsync(x => x.Id == user.UsuarioId());
            return u is null || u.Status != StatusUsuario.Ativo ? Results.Unauthorized() : Results.Ok(ParaDto(u));
        }).WithTags("Auth");
    }

    /// <summary>Emite a sessão: cookie HttpOnly para o navegador e o mesmo token no corpo (clientes de API/testes).</summary>
    public static SessaoDto Abrir(HttpContext http, TokenService tokens, Usuario u)
    {
        var token = tokens.Gerar(u);
        SessaoCookie.Emitir(http, token, tokens.Validade);
        return new SessaoDto(token, ParaDto(u));
    }

    private static string MascararId(string id) => id.Length <= 4 ? "***" : new string('*', id.Length - 3) + id[^3..];

    private static void RegistrarAceite(Usuario u)
    {
        u.AceiteTermosEm = DateTime.UtcNow;
        u.VersaoTermos = Termos.Versao;
    }

    private static IResult? ValidarPj(string razao, string fantasia, string cnpj, Endereco endereco, ResponsavelDto? resp, bool aceite)
    {
        if (resp is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["responsavel"] = ["Informe os dados do responsável."] });
        var erroSenha = PoliticaSenha.Validar(resp.Senha, resp.Email, resp.Nome);
        if (Validacao.Erros(
                (string.IsNullOrWhiteSpace(razao), "razaoSocial", "Informe a razão social."),
                (string.IsNullOrWhiteSpace(fantasia), "nomeFantasia", "Informe o nome fantasia."),
                (!Documentos.CnpjValido(cnpj), "cnpj", "CNPJ inválido."),
                (string.IsNullOrWhiteSpace(resp.Nome) || resp.Nome.Trim().Length < 5, "responsavel.nome", "Informe o nome completo do responsável."),
                (!Documentos.CpfValido(resp.Cpf), "responsavel.cpf", "CPF do responsável inválido."),
                (Documentos.Telefone(resp.TelefoneComercial) is null, "responsavel.telefoneComercial", "Informe o telefone comercial da empresa com DDD."),
                (!Documentos.CelularValido(resp.Whatsapp), "responsavel.whatsapp", "Informe o WhatsApp do responsável com DDD."),
                (string.IsNullOrWhiteSpace(resp.Email) || !resp.Email.Contains('@') || resp.Email.Length > 200, "responsavel.email", "E-mail inválido."),
                (erroSenha is not null, "responsavel.senha", erroSenha ?? ""),
                (!aceite, "aceiteTermos", "Aceite os Termos de Uso e a Política de Privacidade.")) is { } e1) return e1;
        var msg = Documentos.ValidarEndereco(endereco, "Endereço da empresa");
        return msg is null ? null : Results.ValidationProblem(new Dictionary<string, string[]> { ["endereco"] = [msg] });
    }

    private static async Task<IResult?> Duplicado(CoffeeJaDbContext db, string email, string cpf, string? cnpj)
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
    public static bool ConfereSenha(Usuario u, string senha) =>
        !string.IsNullOrEmpty(u.SenhaHash) && new PasswordHasher<Usuario>().VerifyHashedPassword(u, u.SenhaHash, senha) != PasswordVerificationResult.Failed;

    public static UsuarioDto ParaDto(Usuario u) => new(u.Id, u.Nome, u.Email, u.Papel.ToString(), u.Funcao.ToString(),
        u.Contratante?.NomeFantasia ?? u.Contratante?.Empresa ?? u.Fornecedor?.NomeFantasia, u.Fornecedor?.Aprovado, u.Contratante?.Tipo.ToString(), u.Setor,
        u.TotpAtivo, u.Papel == Papel.Admin && !u.TotpAtivo);
}
