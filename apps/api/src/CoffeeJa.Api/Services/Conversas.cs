using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoffeeJa.Api.Services;

/// <summary>Consultas de mensagens não lidas / conversas aguardando resposta.</summary>
public static class Conversas
{
    /// <summary>Mensagens da outra parte, em propostas ainda ativas, enviadas depois da última leitura do usuário.</summary>
    public static IQueryable<Mensagem> NaoLidas(CoffeeJaDbContext db, Guid usuarioId, Guid? contratanteId, Guid? fornecedorId) =>
        db.Mensagens.Where(m => m.RemetenteId != usuarioId
            && m.Proposta.Status != StatusProposta.Recusada && m.Proposta.Status != StatusProposta.Retirada && m.Proposta.Status != StatusProposta.Expirada
            && ((contratanteId != null && m.Proposta.Demanda.ContratanteId == contratanteId && m.CriadoEm > (m.Proposta.ContratanteLeuEm ?? DateTime.MinValue))
             || (fornecedorId != null && m.Proposta.FornecedorId == fornecedorId && m.CriadoEm > (m.Proposta.FornecedorLeuEm ?? DateTime.MinValue))));

    public static async Task<Dictionary<Guid, int>> PorPropostaAsync(CoffeeJaDbContext db, Guid uid, Guid? cid, Guid? fid) =>
        (await NaoLidas(db, uid, cid, fid).GroupBy(m => m.PropostaId).Select(g => new { g.Key, N = g.Count() }).ToListAsync()).ToDictionary(x => x.Key, x => x.N);

    public static async Task<Dictionary<Guid, int>> PorDemandaAsync(CoffeeJaDbContext db, Guid uid, Guid? cid, Guid? fid) =>
        (await NaoLidas(db, uid, cid, fid).GroupBy(m => m.Proposta.DemandaId).Select(g => new { g.Key, N = g.Count() }).ToListAsync()).ToDictionary(x => x.Key, x => x.N);

    /// <summary>Propostas cuja última mensagem veio da outra parte — "a bola está com você".</summary>
    public static async Task<HashSet<Guid>> AguardandoRespostaAsync(CoffeeJaDbContext db, Guid uid, IReadOnlyCollection<Guid> propostaIds)
    {
        if (propostaIds.Count == 0) return [];
        var ultimos = await db.Mensagens.Where(m => propostaIds.Contains(m.PropostaId))
            .GroupBy(m => m.PropostaId)
            .Select(g => new { g.Key, Rem = g.OrderByDescending(m => m.CriadoEm).Select(m => m.RemetenteId).FirstOrDefault() })
            .ToListAsync();
        return ultimos.Where(x => x.Rem != uid).Select(x => x.Key).ToHashSet();
    }
}
