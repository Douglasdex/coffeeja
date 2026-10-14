using System.Security.Claims;
using System.Text.RegularExpressions;
using CoffeeJa.Api.Common;
using CoffeeJa.Api.Services;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoffeeJa.Api.Features;

public record EnviarPropostaRequest(decimal ValorTotal, string Cardapio, string? Observacao);
public record AjusteRequest(string Mensagem);
public record MensagemRequest(string Texto);

public record PropostaDto(Guid Id, Guid DemandaId, Guid FornecedorId, string Fornecedor, double NotaMedia, int TotalAvaliacoes, bool Fundador,
    decimal ValorTotal, decimal ValorPorPessoa, string Cardapio, string? Observacao, int Versao, StatusProposta Status, string? AjusteSolicitado,
    double Score, bool MelhorCustoBeneficio, DateTime CriadoEm, int MensagensNaoLidas = 0, bool AguardandoMinhaResposta = false);

public record MinhaPropostaDto(Guid Id, Guid DemandaId, string Titulo, DateOnly DataEvento, string Cidade, int NumeroPessoas, decimal ValorTotal,
    int Versao, StatusProposta Status, string? AjusteSolicitado, StatusDemanda StatusDemanda, Guid? PedidoId, DateTime CriadoEm, int MensagensNaoLidas = 0, bool AguardandoMinhaResposta = false);

public record MensagemDto(Guid Id, Guid RemetenteId, bool Minha, string Texto, DateTime CriadoEm);

public static partial class PropostaEndpoints
{
    public static void MapPropostas(this IEndpointRouteBuilder app)
    {
        // Fornecedor envia proposta a uma demanda
        app.MapPost("/api/demandas/{demandaId:guid}/propostas", async (Guid demandaId, EnviarPropostaRequest r, ClaimsPrincipal u,
            CoffeeJaDbContext db, INotificador notif, IOptions<PlataformaOptions> plat) =>
        {
            if (Validacao.Erros(
                    (r.ValorTotal <= 0 || r.ValorTotal > 10_000_000, nameof(r.ValorTotal), "Informe um valor válido."),
                    (string.IsNullOrWhiteSpace(r.Cardapio), nameof(r.Cardapio), "Descreva o cardápio.")) is { } erro) return erro;

            var f = await db.Fornecedores.AsNoTracking().FirstAsync(x => x.Id == u.FornecedorId());
            if (!f.Aprovado) return Results.Problem("Seu cadastro ainda está em análise.", statusCode: 403);

            var d = await db.Demandas.Include(x => x.Contratante).FirstOrDefaultAsync(x => x.Id == demandaId);
            if (d is null) return Results.NotFound();
            if (d.Status is not (StatusDemanda.Aberta or StatusDemanda.EmNegociacao) || (d.PrazoPropostas is { } p && p < DateTime.UtcNow))
                return Results.Conflict(new { detail = "Esta demanda não está mais recebendo propostas." });
            if (Geo.DistanciaKm(f.Latitude, f.Longitude, d.Latitude, d.Longitude) > Math.Min(f.RaioAtendimentoKm, plat.Value.RaioMaximoKm))
                return Results.Problem("Demanda fora da sua área de atendimento.", statusCode: 403);
            if (await db.Propostas.AnyAsync(x => x.DemandaId == demandaId && x.FornecedorId == f.Id))
                return Results.Conflict(new { detail = "Você já enviou uma proposta para esta demanda." });

            var prop = new Proposta { DemandaId = d.Id, FornecedorId = f.Id, ValorTotal = r.ValorTotal, Cardapio = r.Cardapio.Trim(), Observacao = r.Observacao };
            db.Propostas.Add(prop);
            await notif.NotificarContratanteAsync(d.ContratanteId, d.SolicitanteId, "Nova proposta recebida", $"{f.NomeFantasia} · R$ {r.ValorTotal:N2}", $"/contratante/demandas/{d.Id}");
            await db.SaveChangesAsync();
            return Results.Created($"/api/propostas/{prop.Id}", new { prop.Id });
        }).WithTags("Propostas").RequireAuthorization(Politicas.Fornecedor);

        // Contratante compara propostas (ordenadas por custo-benefício)
        app.MapGet("/api/demandas/{demandaId:guid}/propostas", async (Guid demandaId, ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var cid = u.ContratanteId();
            var d = await db.Demandas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == demandaId && x.ContratanteId == cid);
            if (d is null) return Results.NotFound();
            var props = await db.Propostas.AsNoTracking().Include(p => p.Fornecedor)
                .Where(p => p.DemandaId == demandaId && p.Status != StatusProposta.Retirada).ToListAsync();
            var uid = u.UsuarioId();
            var naoLidas = await Conversas.PorPropostaAsync(db, uid, cid, null);
            var aguardando = await Conversas.AguardandoRespostaAsync(db, uid, props.Select(p => p.Id).ToList());
            return Results.Ok(Ranquear(props, d.NumeroPessoas).Select(p => p with
            {
                MensagensNaoLidas = naoLidas.GetValueOrDefault(p.Id),
                AguardandoMinhaResposta = aguardando.Contains(p.Id) && p.Status is not (StatusProposta.Recusada or StatusProposta.Retirada or StatusProposta.Expirada),
            }).ToList());
        }).WithTags("Propostas").RequireAuthorization(Politicas.Contratante);

        var g = app.MapGroup("/api/propostas").WithTags("Propostas").RequireAuthorization();

        g.MapGet("/minhas", async (ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var fid = u.FornecedorId();
            var lista = await db.Propostas.AsNoTracking().Where(p => p.FornecedorId == fid).OrderByDescending(p => p.CriadoEm).Take(300)
                .Select(p => new MinhaPropostaDto(p.Id, p.DemandaId, p.Demanda.Titulo, DateOnly.FromDateTime(p.Demanda.DataEvento), p.Demanda.Cidade,
                    p.Demanda.NumeroPessoas, p.ValorTotal, p.Versao, p.Status, p.AjusteSolicitado, p.Demanda.Status,
                    db.Pedidos.Where(x => x.PropostaId == p.Id).Select(x => (Guid?)x.Id).FirstOrDefault(), p.CriadoEm, 0, false)).ToListAsync();
            var uid = u.UsuarioId();
            var naoLidas = await Conversas.PorPropostaAsync(db, uid, null, fid);
            var aguardando = await Conversas.AguardandoRespostaAsync(db, uid, lista.Select(p => p.Id).ToList());
            return lista.Select(p => p with
            {
                MensagensNaoLidas = naoLidas.GetValueOrDefault(p.Id),
                AguardandoMinhaResposta = aguardando.Contains(p.Id) && p.Status is not (StatusProposta.Recusada or StatusProposta.Retirada or StatusProposta.Expirada),
            }).ToList();
        }).RequireAuthorization(Politicas.Fornecedor);

        // Contratante pede ajuste ("troque o suco, adicione mais 10 pães de queijo...")
        g.MapPost("/{id:guid}/ajuste", async (Guid id, AjusteRequest r, ClaimsPrincipal u, CoffeeJaDbContext db, INotificador notif) =>
        {
            if (Validacao.Erros((string.IsNullOrWhiteSpace(r.Mensagem) || r.Mensagem.Length > 2000, nameof(r.Mensagem), "Descreva o ajuste desejado.")) is { } erro) return erro;
            var p = await CarregarDoContratante(db, id, u);
            if (p is null) return Results.NotFound();
            if (p.Status is not (StatusProposta.Enviada or StatusProposta.EmAjuste) || p.Demanda.Status is not (StatusDemanda.Aberta or StatusDemanda.EmNegociacao))
                return Results.Conflict(new { detail = "Esta proposta não aceita mais ajustes." });
            p.Status = StatusProposta.EmAjuste;
            p.AjusteSolicitado = r.Mensagem.Trim();
            p.AtualizadoEm = DateTime.UtcNow;
            p.Demanda.Status = StatusDemanda.EmNegociacao;
            await notif.NotificarFornecedorAsync(p.FornecedorId, "Ajuste solicitado", p.Demanda.Titulo, "/fornecedor/propostas");
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Politicas.Contratante);

        // Fornecedor responde com nova versão da proposta
        g.MapPost("/{id:guid}/revisar", async (Guid id, EnviarPropostaRequest r, ClaimsPrincipal u, CoffeeJaDbContext db, INotificador notif) =>
        {
            if (Validacao.Erros(
                    (r.ValorTotal <= 0 || r.ValorTotal > 10_000_000, nameof(r.ValorTotal), "Informe um valor válido."),
                    (string.IsNullOrWhiteSpace(r.Cardapio), nameof(r.Cardapio), "Descreva o cardápio.")) is { } erro) return erro;
            var fid = u.FornecedorId();
            var p = await db.Propostas.Include(x => x.Demanda).ThenInclude(d => d.Contratante)
                .FirstOrDefaultAsync(x => x.Id == id && x.FornecedorId == fid);
            if (p is null) return Results.NotFound();
            if (p.Status is not (StatusProposta.Enviada or StatusProposta.EmAjuste)) return Results.Conflict(new { detail = "Esta proposta não pode mais ser alterada." });
            p.ValorTotal = r.ValorTotal;
            p.Cardapio = r.Cardapio.Trim();
            p.Observacao = r.Observacao;
            p.Versao++;
            p.Status = StatusProposta.Enviada;
            p.AtualizadoEm = DateTime.UtcNow;
            await notif.NotificarContratanteAsync(p.Demanda.ContratanteId, p.Demanda.SolicitanteId, "Proposta atualizada", $"{p.Demanda.Titulo} · nova versão (v{p.Versao})", $"/contratante/demandas/{p.DemandaId}");
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Politicas.Fornecedor);

        g.MapPost("/{id:guid}/retirar", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var fid = u.FornecedorId();
            var p = await db.Propostas.FirstOrDefaultAsync(x => x.Id == id && x.FornecedorId == fid);
            if (p is null) return Results.NotFound();
            if (p.Status is not (StatusProposta.Enviada or StatusProposta.EmAjuste)) return Results.Conflict(new { detail = "Não é possível retirar esta proposta." });
            p.Status = StatusProposta.Retirada;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Politicas.Fornecedor);

        // Aceite final: gera o pedido
        g.MapPost("/{id:guid}/aceitar", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db, INotificador notif,
            IPagamentoProvider pagamento, IOptions<PlataformaOptions> plat) =>
        {
            var p = await CarregarDoContratante(db, id, u);
            if (p is null) return Results.NotFound();
            if (p.Status != StatusProposta.Enviada || p.Demanda.Status is not (StatusDemanda.Aberta or StatusDemanda.EmNegociacao))
                return Results.Conflict(new { detail = "Esta proposta não pode ser aceita (verifique se há ajuste pendente)." });

            var outras = await db.Propostas.Include(x => x.Fornecedor)
                .Where(x => x.DemandaId == p.DemandaId && x.Id != p.Id && (x.Status == StatusProposta.Enviada || x.Status == StatusProposta.EmAjuste)).ToListAsync();
            foreach (var o in outras)
            {
                o.Status = StatusProposta.Recusada;
                await notif.NotificarFornecedorAsync(o.FornecedorId, "Proposta não selecionada", p.Demanda.Titulo);
            }

            p.Status = StatusProposta.Aceita;
            p.Demanda.Status = StatusDemanda.Contratada;
            var pedido = new Pedido
            {
                DemandaId = p.DemandaId, PropostaId = p.Id, ContratanteId = p.Demanda.ContratanteId, FornecedorId = p.FornecedorId,
                Valor = p.ValorTotal, ComissaoPercentual = plat.Value.ComissaoPercentual,
                StatusPagamento = pagamento.StatusInicial, ProvedorPagamento = pagamento.Nome,
                ContratadoPorId = u.UsuarioId(), ContratadoPorNome = u.NomeUsuario(), ContratadoPorSetor = u.SetorUsuario(),
            };
            pedido.Eventos.Add(new EventoPedido { Descricao = "Pedido confirmado" });
            db.Pedidos.Add(pedido);
            await notif.NotificarFornecedorAsync(p.FornecedorId, "Proposta aceita! 🎉", p.Demanda.Titulo, $"/fornecedor/pedidos/{pedido.Id}");
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { return Results.Conflict(new { detail = "Esta demanda já foi contratada." }); }
            return Results.Ok(new { PedidoId = pedido.Id });
        }).RequireAuthorization(Politicas.Contratante);

        // Chat da negociação
        g.MapGet("/{id:guid}/mensagens", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            if (!await ParticipaAsync(db, id, u)) return Results.NotFound();
            var uid = u.UsuarioId();
            var msgs = await db.Mensagens.AsNoTracking().Where(m => m.PropostaId == id).OrderBy(m => m.CriadoEm).ToListAsync();
            // Abrir a conversa marca como lida para quem está lendo.
            var agora = DateTime.UtcNow;
            if (u.FornecedorId() is not null) await db.Propostas.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.FornecedorLeuEm, agora));
            else await db.Propostas.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.ContratanteLeuEm, agora));
            return Results.Ok(msgs.Select(m => new MensagemDto(m.Id, m.RemetenteId, m.RemetenteId == uid, m.Texto, m.CriadoEm)));
        });

        g.MapPost("/{id:guid}/mensagens", async (Guid id, MensagemRequest r, ClaimsPrincipal u, CoffeeJaDbContext db, INotificador notif) =>
        {
            if (Validacao.Erros((string.IsNullOrWhiteSpace(r.Texto) || r.Texto.Length > 2000, nameof(r.Texto), "Mensagem inválida.")) is { } erro) return erro;
            var p = await db.Propostas.Include(x => x.Fornecedor).Include(x => x.Demanda).ThenInclude(d => d.Contratante).FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            var fid = u.FornecedorId();
            var cid = u.ContratanteId();
            var ehFornecedor = fid is not null && p.FornecedorId == fid;
            var ehContratante = cid is not null && p.Demanda.ContratanteId == cid;
            if (!ehFornecedor && !ehContratante) return Results.NotFound();
            if (p.Status is StatusProposta.Recusada or StatusProposta.Retirada or StatusProposta.Expirada)
                return Results.Conflict(new { detail = "Esta conversa foi encerrada." });

            // Antes do fechamento, contatos externos são ocultados (evita desintermediação).
            var texto = p.Status == StatusProposta.Aceita ? r.Texto.Trim() : OcultarContatos(r.Texto.Trim());
            var m = new Mensagem { PropostaId = id, RemetenteId = u.UsuarioId(), Texto = texto };
            db.Mensagens.Add(m);
            if (ehFornecedor) await notif.NotificarContratanteAsync(p.Demanda.ContratanteId, p.Demanda.SolicitanteId, "Nova mensagem", p.Demanda.Titulo, $"/contratante/demandas/{p.DemandaId}");
            else await notif.NotificarFornecedorAsync(p.FornecedorId, "Nova mensagem", p.Demanda.Titulo, "/fornecedor/propostas");
            await db.SaveChangesAsync();
            return Results.Ok(new MensagemDto(m.Id, m.RemetenteId, true, m.Texto, m.CriadoEm));
        }).RequireRateLimiting("mensagens");
    }

    private static Task<Proposta?> CarregarDoContratante(CoffeeJaDbContext db, Guid id, ClaimsPrincipal u)
    {
        var cid = u.ContratanteId();
        return db.Propostas.Include(x => x.Demanda).Include(x => x.Fornecedor)
            .FirstOrDefaultAsync(x => x.Id == id && x.Demanda.ContratanteId == cid);
    }

    private static async Task<bool> ParticipaAsync(CoffeeJaDbContext db, Guid propostaId, ClaimsPrincipal u)
    {
        var fid = u.FornecedorId();
        var cid = u.ContratanteId();
        return await db.Propostas.AnyAsync(p => p.Id == propostaId && ((fid != null && p.FornecedorId == fid) || (cid != null && p.Demanda.ContratanteId == cid)));
    }

    /// <summary>Score 0..1: 60% preço relativo ao menor valor, 40% reputação (nota 0..5; sem avaliações = 3).</summary>
    public static List<PropostaDto> Ranquear(List<Proposta> props, int pessoas)
    {
        var ativas = props.Where(p => p.Status is StatusProposta.Enviada or StatusProposta.EmAjuste or StatusProposta.Aceita).ToList();
        var menor = ativas.Count == 0 ? 0 : ativas.Min(p => p.ValorTotal);
        var lista = props.Select(p =>
        {
            var preco = p.ValorTotal > 0 && menor > 0 ? (double)(menor / p.ValorTotal) : 0;
            var nota = (p.Fornecedor.TotalAvaliacoes > 0 ? p.Fornecedor.NotaMedia : 3.0) / 5.0;
            var score = Math.Round(0.6 * preco + 0.4 * nota, 3);
            return (p, score);
        }).OrderByDescending(x => x.score).ToList();
        var melhor = lista.FirstOrDefault(x => x.p.Status == StatusProposta.Enviada).p?.Id;
        return lista.Select(x => new PropostaDto(x.p.Id, x.p.DemandaId, x.p.FornecedorId, x.p.Fornecedor.NomeFantasia, x.p.Fornecedor.NotaMedia,
            x.p.Fornecedor.TotalAvaliacoes, x.p.Fornecedor.Fundador, x.p.ValorTotal, pessoas > 0 ? Math.Round(x.p.ValorTotal / pessoas, 2) : 0,
            x.p.Cardapio, x.p.Observacao, x.p.Versao, x.p.Status, x.p.AjusteSolicitado, x.score, lista.Count > 1 && x.p.Id == melhor, x.p.CriadoEm)).ToList();
    }

    public static string OcultarContatos(string texto)
    {
        texto = Email().Replace(texto, "[contato oculto]");
        texto = Telefone().Replace(texto, "[contato oculto]");
        return texto;
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.IgnoreCase)]
    private static partial Regex Email();

    [GeneratedRegex(@"(?<!\d)(?:\+?55[\s.-]?)?\(?\d{2}\)?[\s.-]?9?[\s.-]?\d{4}[\s.-]?\d{4}(?!\d)")]
    private static partial Regex Telefone();
}
