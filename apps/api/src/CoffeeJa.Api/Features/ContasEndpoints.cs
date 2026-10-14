using System.Security.Claims;
using CoffeeJa.Api.Common;
using CoffeeJa.Api.Services;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoffeeJa.Api.Features;

public record AcaoLinkDto(Guid Id, string Link, string Mensagem, string WhatsappUrl);

public record FuncionarioDto(Guid Id, string Nome, string Cpf, string? Setor, string? Telefone, string? Whatsapp, string? SuperiorNome,
    string? SuperiorWhatsapp, StatusUsuario Status, DateTime CriadoEm, DateTime? AprovadoEm, string? MotivoRecusa);
public record NovoFuncionarioRequest(string Nome, string Cpf, string Setor, string TelefoneComercial, string Whatsapp, string SuperiorNome, string SuperiorWhatsapp);

public record AcessoDto(Guid Id, string Nome, string Cpf, string? Cargo, string? Telefone, string Funcao, StatusUsuario Status, DateTime CriadoEm);
public record NovoAcessoRequest(string Nome, string Cpf, string Cargo, string Telefone);

public record AprovacaoInfoDto(string Funcionario, string CpfMascarado, string? Setor, string Empresa, string SuperiorNome, string SolicitadoPor, DateTime CriadoEm);
public record DecisaoRequest(bool Aprovar, string? Motivo);
public record AtivacaoInfoDto(string Nome, string Empresa, string Login);
public record DefinirSenhaRequest(string Senha);

public record CriarConviteRequest(string? Telefone, string? Nome);
public record ConviteEnviadoDto(Guid Id, string? Telefone, string? Nome, StatusConvite Status, DateTime CriadoEm, DateTime? CadastradoEm);
public record ConviteResultadoDto(string Link, string Mensagem, string WhatsappUrl, Guid? ConviteId);
public record MeusConvitesDto(string LinkPessoal, string MensagemPessoal, int Enviados, int Cadastrados, List<ConviteEnviadoDto> Convites);

public static class ContasEndpoints
{
    private static string Url(IOptions<PlataformaOptions> p, string caminho) => $"{p.Value.UrlPublica.TrimEnd('/')}{caminho}";

    public static string MensagemConvite(string convidante, string? empresa, string link, string? convidado)
    {
        var quem = string.IsNullOrWhiteSpace(empresa) ? convidante : $"{convidante} ({empresa})";
        var saudacao = string.IsNullOrWhiteSpace(convidado) ? "Olá! 👋" : $"Olá, {convidado.Trim().Split(' ')[0]}! 👋";
        return $"""
            {saudacao} Aqui é {quem}.

            Estou usando o *CoffeeJá*, a plataforma que conecta empresas e buffets para *coffee break corporativo*, e achei que vale a pena você conhecer:

            ✅ Receba várias propostas de buffets em minutos, sem ligar para ninguém
            ✅ Compare preço, cardápio e avaliações lado a lado
            ✅ Peça ajustes e converse direto com o buffet pelo app
            ✅ Acompanhe cada pedido e guarde as notas fiscais num só lugar
            ✅ Buffets: receba pedidos reais de empresas da sua região (até 50 km)

            Cadastro gratuito, direto pelo celular:
            {link}
            """;
    }

    public static void MapContas(this IEndpointRouteBuilder app)
    {
        MapEquipe(app);
        MapAcessosFornecedor(app);
        MapPublico(app);
        MapConvites(app);
    }

    // ------------------------------------------------------------------ Equipe do contratante PJ
    private static void MapEquipe(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/equipe").WithTags("Equipe").RequireAuthorization(Politicas.Contratante, Politicas.AdminConta);

        g.MapGet("/", async (ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var cid = u.ContratanteId();
            return await db.Usuarios.AsNoTracking().Where(x => x.ContratanteId == cid && x.Funcao == FuncaoUsuario.Membro)
                .OrderBy(x => x.Nome).Select(x => new FuncionarioDto(x.Id, x.Nome, x.Cpf ?? "", x.Setor, x.Telefone, x.Whatsapp, x.SuperiorNome,
                    x.SuperiorWhatsapp, x.Status, x.CriadoEm, x.AprovadoEm, x.MotivoRecusa)).ToListAsync();
        });

        g.MapPost("/", async (NovoFuncionarioRequest r, ClaimsPrincipal u, CoffeeJaDbContext db, IOptions<PlataformaOptions> plat) =>
        {
            var cid = u.ContratanteId()!.Value;
            var conta = await db.Contratantes.AsNoTracking().FirstAsync(c => c.Id == cid);
            if (conta.Tipo != TipoConta.PessoaJuridica) return Results.Problem("Somente contas de empresa podem cadastrar funcionários.", statusCode: 403);
            if (Validacao.Erros(
                    (string.IsNullOrWhiteSpace(r.Nome) || r.Nome.Trim().Length < 5, "nome", "Informe o nome completo."),
                    (!Documentos.CpfValido(r.Cpf), "cpf", "CPF inválido."),
                    (string.IsNullOrWhiteSpace(r.Setor), "setor", "Informe o setor."),
                    (Documentos.Telefone(r.TelefoneComercial) is null, "telefoneComercial", "Informe o telefone comercial com DDD."),
                    (!Documentos.CelularValido(r.Whatsapp), "whatsapp", "Informe o WhatsApp com DDD."),
                    (string.IsNullOrWhiteSpace(r.SuperiorNome) || r.SuperiorNome.Trim().Length < 3, "superiorNome", "Informe o nome do superior imediato."),
                    (!Documentos.CelularValido(r.SuperiorWhatsapp), "superiorWhatsapp", "Informe o WhatsApp do superior com DDD."),
                    (Documentos.Telefone(r.SuperiorWhatsapp) == Documentos.Telefone(r.Whatsapp), "superiorWhatsapp", "O superior deve ser outra pessoa.")) is { } erro) return erro;
            var cpf = Documentos.Digitos(r.Cpf);
            if (await db.Usuarios.AnyAsync(x => x.Cpf == cpf)) return Results.Conflict(new { detail = "Já existe um acesso com esse CPF." });

            var f = AuthEndpoints.NovoUsuario(r.Nome, null, Documentos.Telefone(r.TelefoneComercial), null, Papel.Contratante);
            f.Cpf = cpf;
            f.Setor = r.Setor.Trim();
            f.Whatsapp = Documentos.Telefone(r.Whatsapp);
            f.Funcao = FuncaoUsuario.Membro;
            f.Status = StatusUsuario.PendenteAprovacao;
            f.ContratanteId = cid;
            f.CriadoPorId = u.UsuarioId();
            f.SuperiorNome = r.SuperiorNome.Trim();
            f.SuperiorWhatsapp = Documentos.Telefone(r.SuperiorWhatsapp);
            var link = GerarLinkAprovacao(f, plat, u.NomeUsuario(), conta);
            db.Usuarios.Add(f);
            await db.SaveChangesAsync();
            return Results.Created($"/api/equipe/{f.Id}", link);
        });

        g.MapPost("/{id:guid}/reenviar-aprovacao", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db, IOptions<PlataformaOptions> plat) =>
        {
            var cid = u.ContratanteId();
            var f = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id && x.ContratanteId == cid && x.Funcao == FuncaoUsuario.Membro);
            if (f is null) return Results.NotFound();
            if (f.Status != StatusUsuario.PendenteAprovacao) return Results.Conflict(new { detail = "Este funcionário não está aguardando aprovação." });
            var conta = await db.Contratantes.AsNoTracking().FirstAsync(c => c.Id == cid);
            var link = GerarLinkAprovacao(f, plat, u.NomeUsuario(), conta);
            await db.SaveChangesAsync();
            return Results.Ok(link);
        });

        g.MapPost("/{id:guid}/enviar-acesso", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db, IOptions<PlataformaOptions> plat) =>
        {
            var cid = u.ContratanteId();
            var f = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id && x.ContratanteId == cid && x.Funcao == FuncaoUsuario.Membro);
            if (f is null) return Results.NotFound();
            if (f.Status is not (StatusUsuario.AguardandoAtivacao or StatusUsuario.Ativo))
                return Results.Conflict(new { detail = "O acesso só pode ser enviado depois da aprovação do superior." });
            var conta = await db.Contratantes.AsNoTracking().FirstAsync(c => c.Id == cid);
            var link = GerarLinkAtivacao(f, plat, conta.NomeFantasia ?? conta.Empresa);
            await db.SaveChangesAsync();
            return Results.Ok(link);
        });

        g.MapPost("/{id:guid}/desativar", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var cid = u.ContratanteId();
            var n = await db.Usuarios.Where(x => x.Id == id && x.ContratanteId == cid && x.Funcao == FuncaoUsuario.Membro)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, StatusUsuario.Inativo));
            return n == 0 ? Results.NotFound() : Results.NoContent();
        });

        g.MapPost("/{id:guid}/reativar", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var cid = u.ContratanteId();
            var f = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id && x.ContratanteId == cid && x.Funcao == FuncaoUsuario.Membro);
            if (f is null) return Results.NotFound();
            f.Status = f.AprovadoEm is null ? StatusUsuario.PendenteAprovacao : string.IsNullOrEmpty(f.SenhaHash) ? StatusUsuario.AguardandoAtivacao : StatusUsuario.Ativo;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static AcaoLinkDto GerarLinkAprovacao(Usuario f, IOptions<PlataformaOptions> plat, string adminNome, Contratante conta)
    {
        var (token, hash) = Tokens.Novo();
        f.AprovacaoTokenHash = hash;
        f.AprovacaoExpiraEm = DateTime.UtcNow.AddDays(plat.Value.ValidadeLinkDias);
        var link = Url(plat, $"/aprovar/{token}");
        var msg = $"Olá, {f.SuperiorNome}! Aqui é {adminNome}, da {conta.NomeFantasia ?? conta.Empresa}. " +
                  $"Cadastrei {f.Nome} (setor {f.Setor}) para solicitar coffee breaks em nome da empresa pela plataforma CoffeeJá e preciso da sua aprovação como superior imediato. " +
                  $"Para aprovar ou recusar, acesse: {link}";
        return new AcaoLinkDto(f.Id, link, msg, WhatsApp.Url(f.SuperiorWhatsapp, msg));
    }

    private static AcaoLinkDto GerarLinkAtivacao(Usuario f, IOptions<PlataformaOptions> plat, string empresa)
    {
        var (token, hash) = Tokens.Novo();
        f.AtivacaoTokenHash = hash;
        f.AtivacaoExpiraEm = DateTime.UtcNow.AddDays(plat.Value.ValidadeLinkDias);
        var link = Url(plat, $"/ativar/{token}");
        var msg = $"Olá, {f.Nome.Split(' ')[0]}! Seu acesso ao CoffeeJá da {empresa} está liberado. " +
                  $"Crie sua senha por este link: {link}\nSeu login é o seu CPF.";
        return new AcaoLinkDto(f.Id, link, msg, WhatsApp.Url(f.Whatsapp ?? f.Telefone, msg));
    }

    // ------------------------------------------------------------------ Acessos adicionais do buffet
    private static void MapAcessosFornecedor(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/fornecedor/acessos").WithTags("Fornecedor").RequireAuthorization(Politicas.Fornecedor, Politicas.AdminConta);

        g.MapGet("/", async (ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var fid = u.FornecedorId();
            var lista = await db.Usuarios.AsNoTracking().Where(x => x.FornecedorId == fid).OrderBy(x => x.Funcao).ThenBy(x => x.Nome)
                .Select(x => new AcessoDto(x.Id, x.Nome, x.Cpf ?? "", x.Cargo, x.Whatsapp ?? x.Telefone, x.Funcao.ToString(), x.Status, x.CriadoEm)).ToListAsync();
            return Results.Ok(new { acessos = lista, aviso = "No futuro, cada acesso adicional terá uma licença mensal. Por enquanto, a liberação é gratuita." });
        });

        g.MapPost("/", async (NovoAcessoRequest r, ClaimsPrincipal u, CoffeeJaDbContext db, IOptions<PlataformaOptions> plat) =>
        {
            if (Validacao.Erros(
                    (string.IsNullOrWhiteSpace(r.Nome) || r.Nome.Trim().Length < 5, "nome", "Informe o nome completo."),
                    (!Documentos.CpfValido(r.Cpf), "cpf", "CPF inválido."),
                    (string.IsNullOrWhiteSpace(r.Cargo), "cargo", "Informe o cargo."),
                    (!Documentos.CelularValido(r.Telefone), "telefone", "Informe o WhatsApp/celular com DDD.")) is { } erro) return erro;
            var cpf = Documentos.Digitos(r.Cpf);
            if (await db.Usuarios.AnyAsync(x => x.Cpf == cpf)) return Results.Conflict(new { detail = "Já existe um acesso com esse CPF." });

            var fid = u.FornecedorId()!.Value;
            var forn = await db.Fornecedores.AsNoTracking().FirstAsync(x => x.Id == fid);
            var a = AuthEndpoints.NovoUsuario(r.Nome, null, null, null, Papel.Fornecedor);
            a.Cpf = cpf;
            a.Cargo = r.Cargo.Trim();
            a.Whatsapp = Documentos.Telefone(r.Telefone);
            a.Funcao = FuncaoUsuario.Membro;
            a.Status = StatusUsuario.AguardandoAtivacao;
            a.FornecedorId = fid;
            a.CriadoPorId = u.UsuarioId();
            var link = GerarLinkAtivacao(a, plat, forn.NomeFantasia);
            db.Usuarios.Add(a);
            await db.SaveChangesAsync();
            return Results.Created($"/api/fornecedor/acessos/{a.Id}", link);
        });

        g.MapPost("/{id:guid}/enviar-acesso", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db, IOptions<PlataformaOptions> plat) =>
        {
            var fid = u.FornecedorId();
            var a = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id && x.FornecedorId == fid && x.Funcao == FuncaoUsuario.Membro);
            if (a is null) return Results.NotFound();
            var forn = await db.Fornecedores.AsNoTracking().FirstAsync(x => x.Id == fid);
            var link = GerarLinkAtivacao(a, plat, forn.NomeFantasia);
            await db.SaveChangesAsync();
            return Results.Ok(link);
        });

        g.MapPost("/{id:guid}/desativar", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var fid = u.FornecedorId();
            var n = await db.Usuarios.Where(x => x.Id == id && x.FornecedorId == fid && x.Funcao == FuncaoUsuario.Membro)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, StatusUsuario.Inativo));
            return n == 0 ? Results.NotFound() : Results.NoContent();
        });

        g.MapPost("/{id:guid}/reativar", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var fid = u.FornecedorId();
            var a = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id && x.FornecedorId == fid && x.Funcao == FuncaoUsuario.Membro);
            if (a is null) return Results.NotFound();
            a.Status = string.IsNullOrEmpty(a.SenhaHash) ? StatusUsuario.AguardandoAtivacao : StatusUsuario.Ativo;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    // ------------------------------------------------------------------ Páginas públicas (sem login): aprovação, ativação, convite
    private static void MapPublico(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/publico").WithTags("Público").AllowAnonymous().RequireRateLimiting("auth");
        var invalido = () => Results.NotFound(new { detail = "Link inválido ou expirado." });

        g.MapGet("/aprovacao/{token}", async (string token, CoffeeJaDbContext db) =>
        {
            var h = Tokens.Hash(token);
            var f = await db.Usuarios.AsNoTracking().Include(x => x.Contratante)
                .FirstOrDefaultAsync(x => x.AprovacaoTokenHash == h && x.Status == StatusUsuario.PendenteAprovacao && x.AprovacaoExpiraEm > DateTime.UtcNow);
            if (f is null) return invalido();
            var admin = f.CriadoPorId is { } cp ? await db.Usuarios.AsNoTracking().Where(x => x.Id == cp).Select(x => x.Nome).FirstOrDefaultAsync() : null;
            return Results.Ok(new AprovacaoInfoDto(f.Nome, Documentos.MascararCpf(f.Cpf), f.Setor, f.Contratante?.NomeFantasia ?? f.Contratante?.Empresa ?? "",
                f.SuperiorNome ?? "", admin ?? "", f.CriadoEm));
        });

        g.MapPost("/aprovacao/{token}", async (string token, DecisaoRequest r, CoffeeJaDbContext db, INotificador notif) =>
        {
            var h = Tokens.Hash(token);
            var f = await db.Usuarios.Include(x => x.Contratante)
                .FirstOrDefaultAsync(x => x.AprovacaoTokenHash == h && x.Status == StatusUsuario.PendenteAprovacao && x.AprovacaoExpiraEm > DateTime.UtcNow);
            if (f is null) return invalido();
            f.AprovacaoTokenHash = null;
            f.AprovacaoExpiraEm = null;
            if (r.Aprovar)
            {
                f.Status = StatusUsuario.AguardandoAtivacao;
                f.AprovadoEm = DateTime.UtcNow;
            }
            else
            {
                f.Status = StatusUsuario.Recusado;
                f.MotivoRecusa = string.IsNullOrWhiteSpace(r.Motivo) ? null : r.Motivo.Trim()[..Math.Min(500, r.Motivo.Trim().Length)];
            }
            await notif.NotificarContratanteAsync(f.ContratanteId!.Value, f.CriadoPorId,
                r.Aprovar ? "Funcionário aprovado" : "Funcionário recusado",
                r.Aprovar ? $"{f.SuperiorNome} aprovou {f.Nome}. Envie o acesso por WhatsApp." : $"{f.SuperiorNome} recusou {f.Nome}.",
                "/contratante/equipe");
            await db.SaveChangesAsync();
            return Results.Ok(new { aprovado = r.Aprovar });
        });

        g.MapGet("/ativacao/{token}", async (string token, CoffeeJaDbContext db) =>
        {
            var h = Tokens.Hash(token);
            var a = await db.Usuarios.AsNoTracking().Include(x => x.Contratante).Include(x => x.Fornecedor)
                .FirstOrDefaultAsync(x => x.AtivacaoTokenHash == h && x.AtivacaoExpiraEm > DateTime.UtcNow
                    && (x.Status == StatusUsuario.AguardandoAtivacao || x.Status == StatusUsuario.Ativo));
            if (a is null) return invalido();
            return Results.Ok(new AtivacaoInfoDto(a.Nome, a.Contratante?.NomeFantasia ?? a.Contratante?.Empresa ?? a.Fornecedor?.NomeFantasia ?? "", Documentos.MascararCpf(a.Cpf)));
        });

        g.MapPost("/ativacao/{token}", async (string token, DefinirSenhaRequest r, CoffeeJaDbContext db, TokenService tokens) =>
        {
            if (Validacao.Erros((r.Senha is null || r.Senha.Length < 8, "senha", "A senha deve ter ao menos 8 caracteres.")) is { } erro) return erro;
            var h = Tokens.Hash(token);
            var a = await db.Usuarios.Include(x => x.Contratante).Include(x => x.Fornecedor)
                .FirstOrDefaultAsync(x => x.AtivacaoTokenHash == h && x.AtivacaoExpiraEm > DateTime.UtcNow
                    && (x.Status == StatusUsuario.AguardandoAtivacao || x.Status == StatusUsuario.Ativo));
            if (a is null) return invalido();
            a.SenhaHash = AuthEndpoints.HashSenha(a, r.Senha!);
            a.Status = StatusUsuario.Ativo;
            a.AtivacaoTokenHash = null;
            a.AtivacaoExpiraEm = null;
            await db.SaveChangesAsync();
            return Results.Ok(new SessaoDto(tokens.Gerar(a), AuthEndpoints.ParaDto(a)));
        });

        g.MapGet("/convite/{codigo}", async (string codigo, CoffeeJaDbContext db) =>
        {
            codigo = codigo.Trim().ToUpperInvariant();
            var convidanteId = await db.Convites.Where(c => c.Codigo == codigo).Select(c => (Guid?)c.ConvidanteId).FirstOrDefaultAsync()
                               ?? await db.Usuarios.Where(x => x.CodigoConvite == codigo).Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
            if (convidanteId is null) return Results.NotFound();
            var u = await db.Usuarios.AsNoTracking().Include(x => x.Contratante).Include(x => x.Fornecedor).FirstAsync(x => x.Id == convidanteId);
            return Results.Ok(new { convidante = u.Nome.Split(' ')[0], empresa = u.Contratante?.NomeFantasia ?? u.Contratante?.Empresa ?? u.Fornecedor?.NomeFantasia });
        });
    }

    // ------------------------------------------------------------------ Convites (qualquer perfil)
    private static void MapConvites(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/convites").WithTags("Convites").RequireAuthorization();

        async Task<(Usuario U, string? Empresa)> Quem(ClaimsPrincipal p, CoffeeJaDbContext db)
        {
            var u = await db.Usuarios.AsNoTracking().Include(x => x.Contratante).Include(x => x.Fornecedor).FirstAsync(x => x.Id == p.UsuarioId());
            return (u, u.Contratante?.NomeFantasia ?? u.Contratante?.Empresa ?? u.Fornecedor?.NomeFantasia);
        }

        g.MapGet("/", async (ClaimsPrincipal p, CoffeeJaDbContext db, IOptions<PlataformaOptions> plat) =>
        {
            var (u, empresa) = await Quem(p, db);
            var lista = await db.Convites.AsNoTracking().Where(c => c.ConvidanteId == u.Id).OrderByDescending(c => c.CriadoEm).Take(100)
                .Select(c => new ConviteEnviadoDto(c.Id, c.Telefone, c.NomeConvidado, c.Status, c.CriadoEm, c.CadastradoEm)).ToListAsync();
            var enviados = await db.Convites.CountAsync(c => c.ConvidanteId == u.Id);
            var cadastrados = await db.Usuarios.CountAsync(x => x.IndicadoPorId == u.Id && x.Funcao == FuncaoUsuario.AdminConta);
            var link = Url(plat, $"/cadastro?convite={u.CodigoConvite}");
            return Results.Ok(new MeusConvitesDto(link, MensagemConvite(u.Nome, empresa, link, null), enviados, cadastrados, lista));
        });

        // Com telefone: registra o convite individual e devolve o link do WhatsApp. Sem telefone: devolve o link pessoal para compartilhar.
        g.MapPost("/", async (CriarConviteRequest r, ClaimsPrincipal p, CoffeeJaDbContext db, IOptions<PlataformaOptions> plat) =>
        {
            var (u, empresa) = await Quem(p, db);
            if (string.IsNullOrWhiteSpace(r.Telefone))
            {
                var l = Url(plat, $"/cadastro?convite={u.CodigoConvite}");
                var m = MensagemConvite(u.Nome, empresa, l, r.Nome);
                return Results.Ok(new ConviteResultadoDto(l, m, WhatsApp.Url(null, m), null));
            }
            if (!Documentos.CelularValido(r.Telefone)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["telefone"] = ["Informe um celular válido com DDD."] });
            var tel = Documentos.Telefone(r.Telefone)!;
            var convite = new Convite { ConvidanteId = u.Id, Codigo = Tokens.Codigo(), Telefone = tel, NomeConvidado = string.IsNullOrWhiteSpace(r.Nome) ? null : r.Nome.Trim() };
            db.Convites.Add(convite);
            await db.SaveChangesAsync();
            var link = Url(plat, $"/cadastro?convite={convite.Codigo}");
            var msg = MensagemConvite(u.Nome, empresa, link, convite.NomeConvidado);
            return Results.Ok(new ConviteResultadoDto(link, msg, WhatsApp.Url(tel, msg), convite.Id));
        });
    }
}
