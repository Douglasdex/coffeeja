using System.Security.Claims;
using CoffeeJa.Api.Common;
using CoffeeJa.Api.Services;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoffeeJa.Api.Features;

public record AtualizarStatusRequest(StatusPedido Status);
public record AvaliacaoRequest(int Nota, bool Pontual, string? Comentario);
public record EventoDto(string Descricao, DateTime Em);
public record ContatoDto(string Nome, string? Telefone, string? Email);

public record PedidoResumoDto(Guid Id, Guid DemandaId, string Titulo, DateOnly DataEvento, TimeOnly HoraInicio, string Cidade, int NumeroPessoas,
    decimal Valor, StatusPedido Status, StatusPagamento StatusPagamento, bool TemNotaFiscal, string Contraparte, DateTime CriadoEm);

public record PedidoDetalheDto(Guid Id, Guid DemandaId, Guid PropostaId, string Titulo, DateOnly DataEvento, TimeOnly HoraInicio, string Endereco,
    string Cidade, int NumeroPessoas, string Cardapio, decimal Valor, StatusPedido Status, StatusPagamento StatusPagamento, string ProvedorPagamento,
    string? NotaFiscalNumero, string? NotaFiscalNomeOriginal, DateTime? NotaFiscalEnviadaEm, ContatoDto Contraparte, bool Avaliado,
    IEnumerable<EventoDto> Eventos, StatusPedido[] ProximosStatus);

public static class PedidoEndpoints
{
    private static readonly string[] ExtensoesNota = [".pdf", ".xml", ".png", ".jpg", ".jpeg"];

    public static void MapPedidos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/pedidos").WithTags("Pedidos").RequireAuthorization();

        g.MapGet("/", async (ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var cid = u.ContratanteId();
            var fid = u.FornecedorId();
            var ehForn = fid is not null;
            return await db.Pedidos.AsNoTracking()
                .Where(p => (cid != null && p.ContratanteId == cid) || (fid != null && p.FornecedorId == fid))
                .OrderByDescending(p => p.CriadoEm)
                .Select(p => new PedidoResumoDto(p.Id, p.DemandaId, p.Demanda.Titulo, DateOnly.FromDateTime(p.Demanda.DataEvento),
                    TimeOnly.FromTimeSpan(p.Demanda.HoraInicio), p.Demanda.Cidade, p.Demanda.NumeroPessoas, p.Valor, p.Status, p.StatusPagamento,
                    p.NotaFiscalArquivo != null, ehForn ? p.Demanda.Contratante.Empresa : p.Proposta.Fornecedor.NomeFantasia, p.CriadoEm)).ToListAsync();
        });

        g.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            var p = await Carregar(db, id, u);
            if (p is null) return Results.NotFound();
            var souForn = u.FornecedorId() is not null;
            var contato = souForn
                ? new ContatoDto(p.Demanda.Contratante.Empresa, p.Demanda.Contratante.Usuario.Telefone, p.Demanda.Contratante.Usuario.Email)
                : new ContatoDto(p.Proposta.Fornecedor.NomeFantasia, p.Proposta.Fornecedor.Usuario.Telefone, p.Proposta.Fornecedor.Usuario.Email);
            var avaliado = await db.Avaliacoes.AnyAsync(a => a.PedidoId == id);
            return Results.Ok(new PedidoDetalheDto(p.Id, p.DemandaId, p.PropostaId, p.Demanda.Titulo, DateOnly.FromDateTime(p.Demanda.DataEvento),
                TimeOnly.FromTimeSpan(p.Demanda.HoraInicio), p.Demanda.Endereco, p.Demanda.Cidade, p.Demanda.NumeroPessoas, p.Proposta.Cardapio, p.Valor,
                p.Status, p.StatusPagamento, p.ProvedorPagamento, p.NotaFiscalNumero, p.NotaFiscalNomeOriginal, p.NotaFiscalEnviadaEm, contato, avaliado,
                p.Eventos.OrderBy(e => e.CriadoEm).Select(e => new EventoDto(e.Descricao, e.CriadoEm)), ProximosPermitidos(p, souForn)));
        });

        // Fornecedor avança o status (preparo → saiu → entregue); contratante conclui.
        g.MapPost("/{id:guid}/status", async (Guid id, AtualizarStatusRequest r, ClaimsPrincipal u, CoffeeJaDbContext db, INotificador notif) =>
        {
            var p = await Carregar(db, id, u);
            if (p is null) return Results.NotFound();
            var souForn = u.FornecedorId() is not null;
            if (!ProximosPermitidos(p, souForn).Contains(r.Status))
                return Results.Conflict(new { detail = "Mudança de status não permitida." });

            p.Status = r.Status;
            p.Eventos.Add(new EventoPedido { Descricao = DescricaoStatus(r.Status) });
            if (r.Status == StatusPedido.Concluido) p.Demanda.Status = StatusDemanda.Concluida;
            if (r.Status == StatusPedido.Cancelado) p.Demanda.Status = StatusDemanda.Cancelada;
            var destino = souForn ? p.Demanda.Contratante.UsuarioId : p.Proposta.Fornecedor.UsuarioId;
            notif.Notificar(destino, DescricaoStatus(r.Status), p.Demanda.Titulo, souForn ? $"/contratante/pedidos/{p.Id}" : $"/fornecedor/pedidos/{p.Id}");
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Inclusão da NF no sistema (fornecedor emite; contratante também pode anexar a sua cópia)
        g.MapPost("/{id:guid}/nota-fiscal", async ([FromForm] string? numero, IFormFile arquivo, Guid id, ClaimsPrincipal u, CoffeeJaDbContext db,
            IArquivoStorage storage, IOptions<PlataformaOptions> plat, INotificador notif, CancellationToken ct) =>
        {
            var p = await Carregar(db, id, u);
            if (p is null) return Results.NotFound();
            var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            if (Validacao.Erros(
                    (arquivo.Length == 0, nameof(arquivo), "Arquivo vazio."),
                    (arquivo.Length > plat.Value.TamanhoMaximoNotaFiscalBytes, nameof(arquivo), "Arquivo acima de 10 MB."),
                    (!ExtensoesNota.Contains(ext), nameof(arquivo), "Envie PDF, XML, PNG ou JPG."),
                    (numero is { Length: > 50 }, nameof(numero), "Número muito longo.")) is { } erro) return erro;

            await using var stream = arquivo.OpenReadStream();
            p.NotaFiscalArquivo = await storage.SalvarAsync(stream, ext, ct);
            p.NotaFiscalNumero = numero?.Trim();
            p.NotaFiscalNomeOriginal = Path.GetFileName(arquivo.FileName);
            p.NotaFiscalEnviadaEm = DateTime.UtcNow;
            p.Eventos.Add(new EventoPedido { Descricao = "Nota fiscal anexada" });
            var souForn = u.FornecedorId() is not null;
            notif.Notificar(souForn ? p.Demanda.Contratante.UsuarioId : p.Proposta.Fornecedor.UsuarioId, "Nota fiscal anexada", p.Demanda.Titulo,
                souForn ? $"/contratante/pedidos/{p.Id}" : $"/fornecedor/pedidos/{p.Id}");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).DisableAntiforgery();

        g.MapGet("/{id:guid}/nota-fiscal", async (Guid id, ClaimsPrincipal u, CoffeeJaDbContext db, IArquivoStorage storage) =>
        {
            var p = await Carregar(db, id, u);
            if (p?.NotaFiscalArquivo is null) return Results.NotFound();
            var ext = Path.GetExtension(p.NotaFiscalArquivo);
            var tipo = ext switch { ".pdf" => "application/pdf", ".xml" => "application/xml", ".png" => "image/png", _ => "image/jpeg" };
            return Results.File(storage.Abrir(p.NotaFiscalArquivo), tipo, p.NotaFiscalNomeOriginal ?? $"nota-fiscal{ext}");
        });

        g.MapPost("/{id:guid}/avaliacao", async (Guid id, AvaliacaoRequest r, ClaimsPrincipal u, CoffeeJaDbContext db) =>
        {
            if (Validacao.Erros((r.Nota is < 1 or > 5, nameof(r.Nota), "A nota vai de 1 a 5.")) is { } erro) return erro;
            var cid = u.ContratanteId();
            var p = await db.Pedidos.Include(x => x.Proposta).ThenInclude(x => x.Fornecedor)
                .FirstOrDefaultAsync(x => x.Id == id && x.ContratanteId == cid);
            if (p is null) return Results.NotFound();
            if (p.Status is not (StatusPedido.Entregue or StatusPedido.Concluido)) return Results.Conflict(new { detail = "Avalie após a entrega." });
            if (await db.Avaliacoes.AnyAsync(a => a.PedidoId == id)) return Results.Conflict(new { detail = "Pedido já avaliado." });

            db.Avaliacoes.Add(new Avaliacao { PedidoId = id, FornecedorId = p.FornecedorId, ContratanteId = p.ContratanteId, Nota = r.Nota, Pontual = r.Pontual, Comentario = r.Comentario });
            var f = p.Proposta.Fornecedor;
            f.NotaMedia = Math.Round((f.NotaMedia * f.TotalAvaliacoes + r.Nota) / (f.TotalAvaliacoes + 1), 2);
            f.TotalAvaliacoes++;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Politicas.Contratante);
    }

    private static Task<Pedido?> Carregar(CoffeeJaDbContext db, Guid id, ClaimsPrincipal u)
    {
        var cid = u.ContratanteId();
        var fid = u.FornecedorId();
        return db.Pedidos
            .Include(p => p.Eventos)
            .Include(p => p.Demanda).ThenInclude(d => d.Contratante).ThenInclude(c => c.Usuario)
            .Include(p => p.Proposta).ThenInclude(x => x.Fornecedor).ThenInclude(f => f.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id && ((cid != null && p.ContratanteId == cid) || (fid != null && p.FornecedorId == fid)));
    }

    private static StatusPedido[] ProximosPermitidos(Pedido p, bool fornecedor) => (p.Status, fornecedor) switch
    {
        (StatusPedido.Confirmado, true) => [StatusPedido.EmPreparo],
        (StatusPedido.EmPreparo, true) => [StatusPedido.SaiuParaEntrega],
        (StatusPedido.SaiuParaEntrega, true) => [StatusPedido.Entregue],
        (StatusPedido.Entregue, false) => [StatusPedido.Concluido],
        (StatusPedido.Confirmado, false) => [StatusPedido.Cancelado],
        _ => [],
    };

    private static string DescricaoStatus(StatusPedido s) => s switch
    {
        StatusPedido.EmPreparo => "Em preparo",
        StatusPedido.SaiuParaEntrega => "Saiu para entrega",
        StatusPedido.Entregue => "Entregue no local",
        StatusPedido.Concluido => "Evento concluído",
        StatusPedido.Cancelado => "Pedido cancelado",
        _ => "Pedido confirmado",
    };
}
