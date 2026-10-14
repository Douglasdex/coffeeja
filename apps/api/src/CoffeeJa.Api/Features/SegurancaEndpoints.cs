using System.Security.Claims;
using System.Text.Json;
using CoffeeJa.Api.Common;
using CoffeeJa.Api.Services;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoffeeJa.Api.Features;

public record TrocarSenhaRequest(string SenhaAtual, string NovaSenha);
public record ConfirmarTotpRequest(string Codigo);
public record DesativarTotpRequest(string Senha, string Codigo);
public record SolicitacaoExclusaoDto(Guid Id, string Nome, string? Email, string Papel, string? Empresa, DateTime SolicitadaEm, StatusUsuario Status);
public record LogAuditoriaDto(Guid Id, DateTime Em, string Acao, string? Usuario, Guid? UsuarioId, string? Alvo, string? Detalhe, string? Ip);

public static class SegurancaEndpoints
{
    public static void MapSeguranca(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/seguranca").WithTags("Segurança").RequireAuthorization();

        // ---- Troca de senha: exige a senha atual, derruba as outras sessões e mantém esta.
        g.MapPost("/senha", async (TrocarSenhaRequest r, ClaimsPrincipal p, HttpContext http, CoffeeJaDbContext db, TokenService tokens, SessaoCache sessoes, AuditoriaService audit) =>
        {
            var u = await db.Usuarios.Include(x => x.Contratante).Include(x => x.Fornecedor).FirstAsync(x => x.Id == p.UsuarioId());
            if (!AuthEndpoints.ConfereSenha(u, r.SenhaAtual ?? "")) { await audit.RegistrarAsync("SENHA_TROCA_FALHA", u.Id); return Results.Problem("A senha atual está incorreta.", statusCode: 400); }
            if (PoliticaSenha.Validar(r.NovaSenha, u.Email, u.Nome) is { } erro) return Results.ValidationProblem(new Dictionary<string, string[]> { ["novaSenha"] = [erro] });
            if (AuthEndpoints.ConfereSenha(u, r.NovaSenha)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["novaSenha"] = ["A nova senha deve ser diferente da atual."] });
            u.SenhaHash = AuthEndpoints.HashSenha(u, r.NovaSenha);
            u.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
            sessoes.Invalidar(u.Id);
            await audit.RegistrarAsync("SENHA_ALTERADA", u.Id);
            return Results.Ok(AuthEndpoints.Abrir(http, tokens, u));
        });

        // ---- Autenticação em dois fatores (TOTP)
        g.MapPost("/2fa/iniciar", async (ClaimsPrincipal p, CoffeeJaDbContext db, SegredoProtetor protetor) =>
        {
            var u = await db.Usuarios.FirstAsync(x => x.Id == p.UsuarioId());
            if (u.TotpAtivo) return Results.Conflict(new { detail = "A verificação em duas etapas já está ativa." });
            var segredo = Totp.NovoSegredo();
            u.TotpSegredoProtegido = protetor.Proteger(segredo);
            await db.SaveChangesAsync();
            var conta = u.Email ?? u.Nome;
            return Results.Ok(new { segredo, uri = Totp.Uri(conta, segredo) });
        });

        g.MapPost("/2fa/confirmar", async (ConfirmarTotpRequest r, ClaimsPrincipal p, HttpContext http, CoffeeJaDbContext db, SegredoProtetor protetor, TokenService tokens,
            SessaoCache sessoes, AuditoriaService audit) =>
        {
            var u = await db.Usuarios.Include(x => x.Contratante).Include(x => x.Fornecedor).FirstAsync(x => x.Id == p.UsuarioId());
            var segredo = protetor.Revelar(u.TotpSegredoProtegido);
            if (u.TotpAtivo || segredo is null) return Results.Conflict(new { detail = "Inicie a configuração novamente." });
            if (Totp.Verificar(segredo, r.Codigo) is not { } passo) return Results.Problem("Código incorreto. Confira o horário do celular e tente de novo.", statusCode: 400);
            var (claros, hashes) = Totp.NovosCodigosRecuperacao();
            u.TotpAtivo = true;
            u.TotpUltimoPasso = passo;
            u.CodigosRecuperacaoHash = hashes;
            u.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
            sessoes.Invalidar(u.Id);
            await audit.RegistrarAsync("2FA_ATIVADO", u.Id);
            return Results.Ok(new { codigosRecuperacao = claros, sessao = AuthEndpoints.Abrir(http, tokens, u) });
        });

        g.MapPost("/2fa/desativar", async (DesativarTotpRequest r, ClaimsPrincipal p, CoffeeJaDbContext db, SegredoProtetor protetor, SessaoCache sessoes, AuditoriaService audit) =>
        {
            var u = await db.Usuarios.FirstAsync(x => x.Id == p.UsuarioId());
            if (u.Papel == Papel.Admin) return Results.Problem("A verificação em duas etapas é obrigatória para administradores.", statusCode: 403);
            var segredo = protetor.Revelar(u.TotpSegredoProtegido);
            if (!u.TotpAtivo || segredo is null) return Results.NoContent();
            if (!AuthEndpoints.ConfereSenha(u, r.Senha ?? "") || Totp.Verificar(segredo, r.Codigo) is null) return Results.Problem("Senha ou código incorretos.", statusCode: 400);
            u.TotpAtivo = false; u.TotpSegredoProtegido = null; u.CodigosRecuperacaoHash = null;
            u.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
            sessoes.Invalidar(u.Id);
            await audit.RegistrarAsync("2FA_DESATIVADO", u.Id);
            return Results.NoContent();
        });

        // ---- LGPD: direitos do titular
        var l = app.MapGroup("/api/lgpd").WithTags("LGPD").RequireAuthorization();

        l.MapGet("/meus-dados", async (ClaimsPrincipal p, HttpContext http, CoffeeJaDbContext db, AuditoriaService audit) =>
        {
            var uid = p.UsuarioId();
            var u = await db.Usuarios.AsNoTracking().Include(x => x.Contratante).Include(x => x.Fornecedor).FirstAsync(x => x.Id == uid);
            var demandas = await db.Demandas.AsNoTracking().Where(d => d.SolicitanteId == uid)
                .Select(d => new { d.Id, d.Titulo, d.DataEvento, d.Cidade, d.NumeroPessoas, Status = d.Status.ToString(), d.CriadoEm }).ToListAsync();
            var pedidos = await db.Pedidos.AsNoTracking().Where(x => x.ContratadoPorId == uid)
                .Select(x => new { x.Id, x.Valor, Status = x.Status.ToString(), x.CriadoEm }).ToListAsync();
            var convites = await db.Convites.AsNoTracking().Where(c => c.ConvidanteId == uid)
                .Select(c => new { c.Telefone, c.NomeConvidado, Status = c.Status.ToString(), c.CriadoEm }).ToListAsync();
            var acessos = await db.LogsAuditoria.AsNoTracking().Where(x => x.UsuarioId == uid).OrderByDescending(x => x.CriadoEm).Take(200)
                .Select(x => new { x.CriadoEm, x.Acao, x.Ip }).ToListAsync();
            await audit.RegistrarAsync("LGPD_EXPORT", uid);
            var dados = new
            {
                geradoEm = DateTime.UtcNow,
                usuario = new { u.Id, u.Nome, u.Email, u.Cpf, telefoneComercial = u.Telefone, u.Whatsapp, u.Cargo, u.Setor, papel = u.Papel.ToString(), funcao = u.Funcao.ToString(), u.CriadoEm, u.AceiteTermosEm, u.VersaoTermos, doisFatores = u.TotpAtivo },
                conta = u.Contratante is null ? null : new { tipo = u.Contratante.Tipo.ToString(), nome = u.Contratante.Empresa, u.Contratante.NomeFantasia, u.Contratante.Cnpj, u.Contratante.Cpf, u.Contratante.Endereco, enderecoNotaFiscal = u.Contratante.EnderecoNotaFiscal },
                buffet = u.Fornecedor is null ? null : new { u.Fornecedor.NomeFantasia, u.Fornecedor.RazaoSocial, u.Fornecedor.Cnpj, u.Fornecedor.Endereco },
                demandasSolicitadas = demandas, pedidosContratados = pedidos, convitesEnviados = convites, ultimosAcessos = acessos,
            };
            http.Response.Headers.ContentDisposition = "attachment; filename=\"meus-dados-coffeeja.json\"";
            return Results.Json(dados, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        });

        l.MapPost("/solicitar-exclusao", async (ClaimsPrincipal p, CoffeeJaDbContext db, AuditoriaService audit, INotificador notif) =>
        {
            var u = await db.Usuarios.FirstAsync(x => x.Id == p.UsuarioId());
            if (u.Papel == Papel.Admin) return Results.Problem("Contas de administrador não podem ser removidas por aqui.", statusCode: 403);
            u.ExclusaoSolicitadaEm ??= DateTime.UtcNow;
            var admins = await db.Usuarios.Where(x => x.Papel == Papel.Admin && x.Status == StatusUsuario.Ativo).Select(x => x.Id).ToListAsync();
            foreach (var adminId in admins) notif.Notificar(adminId, "Solicitação de exclusão de dados (LGPD)", u.Nome, "/admin");
            await db.SaveChangesAsync();
            await audit.RegistrarAsync("LGPD_EXCLUSAO_SOLICITADA", u.Id);
            return Results.Accepted(value: new { detail = "Recebemos seu pedido. Ele será analisado em até 15 dias, e você será avisado." });
        });

        // ---- Administração (exige 2FA ativo)
        var ad = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization(Politicas.Admin);

        ad.MapGet("/lgpd/solicitacoes", async (CoffeeJaDbContext db) =>
            await db.Usuarios.AsNoTracking().Where(x => x.ExclusaoSolicitadaEm != null && x.Nome != "Usuário removido").OrderBy(x => x.ExclusaoSolicitadaEm)
                .Select(x => new SolicitacaoExclusaoDto(x.Id, x.Nome, x.Email, x.Papel.ToString(), x.Contratante != null ? x.Contratante.Empresa : x.Fornecedor != null ? x.Fornecedor.NomeFantasia : null,
                    x.ExclusaoSolicitadaEm!.Value, x.Status)).ToListAsync());

        // Anonimiza dados pessoais do usuário, preservando registros fiscais/contábeis da empresa (obrigação legal).
        ad.MapPost("/usuarios/{id:guid}/anonimizar", async (Guid id, ClaimsPrincipal p, CoffeeJaDbContext db, SessaoCache sessoes, AuditoriaService audit) =>
        {
            var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id);
            if (u is null) return Results.NotFound();
            if (u.Papel == Papel.Admin) return Results.Problem("Administradores não podem ser anonimizados.", statusCode: 403);
            var emAndamento = new[] { StatusPedido.Concluido, StatusPedido.Cancelado };
            var pendente = u.Funcao == FuncaoUsuario.AdminConta
                ? await db.Pedidos.AnyAsync(x => (x.ContratanteId == u.ContratanteId || x.FornecedorId == u.FornecedorId) && !emAndamento.Contains(x.Status))
                : await db.Pedidos.AnyAsync(x => x.ContratadoPorId == id && !emAndamento.Contains(x.Status));
            if (pendente) return Results.Conflict(new { detail = "Há pedidos em andamento. Conclua ou cancele antes de remover os dados." });

            u.Nome = "Usuário removido"; u.Email = null; u.Cpf = null; u.Telefone = null; u.Whatsapp = null; u.Cargo = null; u.Setor = null;
            u.SuperiorNome = null; u.SuperiorWhatsapp = null; u.SenhaHash = ""; u.TotpSegredoProtegido = null; u.TotpAtivo = false; u.CodigosRecuperacaoHash = null;
            u.AprovacaoTokenHash = null; u.AtivacaoTokenHash = null; u.Status = StatusUsuario.Inativo;
            u.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.Demandas.Where(d => d.SolicitanteId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.SolicitanteNome, "Usuário removido").SetProperty(d => d.SolicitanteSetor, (string?)null));
            await db.Pedidos.Where(x => x.ContratadoPorId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ContratadoPorNome, "Usuário removido").SetProperty(x => x.ContratadoPorSetor, (string?)null));
            await db.PushInscricoes.Where(x => x.UsuarioId == id).ExecuteDeleteAsync();
            await db.SaveChangesAsync();
            sessoes.Invalidar(id);
            await audit.RegistrarAsync("LGPD_ANONIMIZADO", p.UsuarioId(), alvo: id.ToString());
            return Results.NoContent();
        });

        ad.MapGet("/auditoria", async (CoffeeJaDbContext db, string? acao, Guid? usuarioId, int? pagina) =>
        {
            var q = db.LogsAuditoria.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(acao)) q = q.Where(x => x.Acao == acao);
            if (usuarioId is not null) q = q.Where(x => x.UsuarioId == usuarioId);
            var skip = Math.Max(0, (pagina ?? 1) - 1) * 100;
            return await q.OrderByDescending(x => x.CriadoEm).Skip(skip).Take(100)
                .Select(x => new LogAuditoriaDto(x.Id, x.CriadoEm, x.Acao, db.Usuarios.Where(u => u.Id == x.UsuarioId).Select(u => u.Nome).FirstOrDefault(), x.UsuarioId, x.Alvo, x.Detalhe, x.Ip))
                .ToListAsync();
        });
    }
}
