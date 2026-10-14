using System.Net;
using System.Security.Cryptography;
using System.Text;
using CoffeeJa.Api.Common;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CoffeeJa.Api.Services;

public static class Termos
{
    /// <summary>Versão vigente dos Termos de Uso / Política de Privacidade (registrada no aceite de cada usuário).</summary>
    public const string Versao = "1.0";
}

/// <summary>Política de senha: tamanho mínimo, sem senhas óbvias e sem dados do próprio usuário (orientação NIST SP 800-63B).</summary>
public static class PoliticaSenha
{
    public const int Minimo = 10;
    public const int Maximo = 128;

    private static readonly HashSet<string> Comuns = new(StringComparer.OrdinalIgnoreCase)
    {
        "1234567890", "12345678910", "0123456789", "123456789a", "qwertyuiop", "qwerty12345", "senha12345", "senha123456", "senha1234567",
        "password123", "password1234", "passw0rd123", "administrador", "admin12345", "admin123456", "abc1234567", "abcd123456", "cafe123456",
        "coffeeja123", "coffeeja1234", "mudar12345", "trocar12345", "brasil12345", "brasil2026", "empresa1234", "empresa12345", "1q2w3e4r5t",
        "iloveyou123", "letmein1234", "welcome1234", "minhasenha", "minhasenha1", "senhasegura", "senhasegura1", "corinthians1", "flamengo123",
    };

    public static string? Validar(string? senha, params string?[] referencias)
    {
        if (string.IsNullOrEmpty(senha) || senha.Length < Minimo) return $"A senha deve ter ao menos {Minimo} caracteres.";
        if (senha.Length > Maximo) return $"A senha pode ter no máximo {Maximo} caracteres.";
        if (!senha.Any(char.IsLetter) || !senha.Any(c => char.IsDigit(c) || !char.IsLetterOrDigit(c)))
            return "Use letras e ao menos um número ou símbolo.";
        if (senha.Distinct().Count() < 4) return "Essa senha é previsível. Escolha outra.";
        var s = senha.ToLowerInvariant();
        if (Comuns.Contains(senha) || Comuns.Contains(s.TrimEnd('!', '@', '#', '$', '.'))) return "Essa senha é muito comum. Escolha outra.";
        foreach (var r in referencias.Where(r => !string.IsNullOrWhiteSpace(r)))
            foreach (var parte in r!.ToLowerInvariant().Split(' ', '@', '.', '-', '_').Where(p => p.Length >= 4))
                if (s.Contains(parte)) return "A senha não deve conter seu nome ou e-mail.";
        return null;
    }
}

/// <summary>TOTP (RFC 6238) compatível com Google Authenticator, Microsoft Authenticator, Authy etc.</summary>
public static class Totp
{
    private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string NovoSegredo() => Base32(RandomNumberGenerator.GetBytes(20));

    public static string Uri(string conta, string segredo) =>
        $"otpauth://totp/CoffeeJa:{System.Uri.EscapeDataString(conta)}?secret={segredo}&issuer=CoffeeJa&digits=6&period=30";

    public static long Passo(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds() / 30;

    /// <summary>Retorna o passo (janela de 30 s) em que o código é válido, ou null. Aceita ±1 passo para tolerar diferença de relógio.</summary>
    public static long? Verificar(string segredo, string? codigo, DateTime? agora = null)
    {
        var limpo = new string((codigo ?? "").Where(char.IsDigit).ToArray());
        if (limpo.Length != 6) return null;
        var chave = Decodificar(segredo);
        var passo = Passo(agora ?? DateTime.UtcNow);
        for (var d = -1; d <= 1; d++)
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Gerar(chave, passo + d)), Encoding.ASCII.GetBytes(limpo))) return passo + d;
        return null;
    }

    public static string Gerar(byte[] chave, long passo)
    {
        var msg = BitConverter.GetBytes(passo);
        if (BitConverter.IsLittleEndian) Array.Reverse(msg);
        var h = HMACSHA1.HashData(chave, msg);
        var o = h[^1] & 0x0F;
        var bin = ((h[o] & 0x7F) << 24) | (h[o + 1] << 16) | (h[o + 2] << 8) | h[o + 3];
        return (bin % 1_000_000).ToString("D6");
    }

    public static string Base32(byte[] dados)
    {
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in dados)
        {
            buffer = (buffer << 8) | b; bits += 8;
            while (bits >= 5) { sb.Append(Alfabeto[(buffer >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) sb.Append(Alfabeto[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    public static byte[] Decodificar(string base32)
    {
        var saida = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in base32.ToUpperInvariant().Where(c => c != '='))
        {
            var v = Alfabeto.IndexOf(c);
            if (v < 0) continue;
            buffer = (buffer << 5) | v; bits += 5;
            if (bits >= 8) { saida.Add((byte)((buffer >> (bits - 8)) & 0xFF)); bits -= 8; }
        }
        return saida.ToArray();
    }

    /// <summary>Códigos de uso único para quem perder o aplicativo autenticador.</summary>
    public static (string[] Claros, string Hashes) NovosCodigosRecuperacao(int quantidade = 8)
    {
        var claros = Enumerable.Range(0, quantidade).Select(_ => $"{Tokens.Codigo(5)}-{Tokens.Codigo(5)}").ToArray();
        return (claros, string.Join(',', claros.Select(c => Tokens.Hash(c.Replace("-", "")))));
    }

    public static bool ConsumirRecuperacao(Usuario u, string? codigo)
    {
        var limpo = new string((codigo ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (limpo.Length != 10 || string.IsNullOrEmpty(u.CodigosRecuperacaoHash)) return false;
        var h = Tokens.Hash(limpo);
        var lista = u.CodigosRecuperacaoHash.Split(',').ToList();
        if (!lista.Remove(h)) return false;
        u.CodigosRecuperacaoHash = string.Join(',', lista);
        return true;
    }
}

/// <summary>Protege o segredo TOTP em repouso (Data Protection API; as chaves ficam em disco, fora do banco).</summary>
public class SegredoProtetor(IDataProtectionProvider provider)
{
    private readonly IDataProtector _p = provider.CreateProtector("CoffeeJa.Totp.v1");
    public string Proteger(string segredo) => _p.Protect(segredo);
    public string? Revelar(string? protegido)
    {
        if (string.IsNullOrEmpty(protegido)) return null;
        try { return _p.Unprotect(protegido); } catch (CryptographicException) { return null; }
    }
}

/// <summary>Trilha de auditoria gravada em contexto próprio (não depende do SaveChanges da requisição e nunca derruba a operação).</summary>
public class AuditoriaService(IServiceScopeFactory scopes, IHttpContextAccessor http, ILogger<AuditoriaService> log)
{
    public async Task RegistrarAsync(string acao, Guid? usuarioId, string? alvo = null, string? detalhe = null)
    {
        try
        {
            var ctx = http.HttpContext;
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoffeeJaDbContext>();
            var ua = ctx?.Request.Headers.UserAgent.ToString();
            db.LogsAuditoria.Add(new LogAuditoria
            {
                UsuarioId = usuarioId, Acao = acao, Alvo = alvo?[..Math.Min(100, alvo.Length)], Detalhe = detalhe?[..Math.Min(500, detalhe.Length)],
                Ip = ctx?.Connection.RemoteIpAddress?.ToString(), UserAgent = ua is { Length: > 200 } ? ua[..200] : ua,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { log.LogWarning(ex, "Falha ao gravar auditoria {Acao}", acao); }
    }
}

public static class SessaoCookie
{
    public const string Nome = "coffeeja_sessao";
    public const string CabecalhoCsrf = "X-Requested-With";
    public const string ValorCsrf = "CoffeeJa";

    public static void Emitir(HttpContext ctx, string token, TimeSpan validade) =>
        ctx.Response.Cookies.Append(Nome, token, new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = ctx.Request.IsHttps, Path = "/", MaxAge = validade, IsEssential = true,
        });

    public static void Remover(HttpContext ctx) =>
        ctx.Response.Cookies.Delete(Nome, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = ctx.Request.IsHttps, Path = "/" });
}

public record InfoSessao(StatusUsuario Status, string Stamp, bool ContaSuspensa);

/// <summary>Confere a cada requisição se a sessão ainda vale (usuário ativo, senha não trocada, conta não suspensa). Cache curto evita um SELECT por chamada.</summary>
public class SessaoCache(IMemoryCache cache)
{
    private static string Chave(Guid id) => $"sessao:{id}";
    public void Invalidar(Guid usuarioId) => cache.Remove(Chave(usuarioId));

    public async Task<InfoSessao?> ObterAsync(Guid id, CoffeeJaDbContext db) =>
        await cache.GetOrCreateAsync(Chave(id), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            return await db.Usuarios.AsNoTracking().Where(u => u.Id == id)
                .Select(u => new InfoSessao(u.Status, u.SecurityStamp, u.Contratante != null && !u.Contratante.Ativo)).FirstOrDefaultAsync();
        });
}

public static class SegurancaHttp
{
    /// <summary>Lê o JWT do cookie HttpOnly (navegador) ou do cabeçalho Authorization (clientes de API).</summary>
    public static Task LerTokenDoCookie(MessageReceivedContext ctx)
    {
        if (string.IsNullOrEmpty(ctx.Request.Headers.Authorization) && ctx.Request.Cookies.TryGetValue(SessaoCookie.Nome, out var t)) ctx.Token = t;
        return Task.CompletedTask;
    }

    public static async Task ValidarSessao(TokenValidatedContext ctx)
    {
        var sub = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var sv = ctx.Principal?.FindFirst("sv")?.Value;
        if (!Guid.TryParse(sub, out var uid)) { ctx.Fail("sessão inválida"); return; }
        var db = ctx.HttpContext.RequestServices.GetRequiredService<CoffeeJaDbContext>();
        var info = await ctx.HttpContext.RequestServices.GetRequiredService<SessaoCache>().ObterAsync(uid, db);
        if (info is null || info.Status != StatusUsuario.Ativo || info.ContaSuspensa || !string.Equals(info.Stamp, sv, StringComparison.Ordinal))
            ctx.Fail("sessão encerrada");
    }

    /// <summary>Cabeçalhos de segurança em todas as respostas.</summary>
    public static IApplicationBuilder UseCabecalhosSeguranca(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "camera=(), microphone=(), payment=(), geolocation=(self)";
        h["Cross-Origin-Opener-Policy"] = "same-origin";
        h["Cross-Origin-Resource-Policy"] = "same-origin";
        h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
        // Dados pessoais da API nunca devem ficar em cache de navegador/proxy.
        if (ctx.Request.Path.StartsWithSegments("/api")) h.CacheControl = "no-store";
        await next();
    });

    /// <summary>Proteção CSRF: requisições que mudam estado e autenticadas por cookie precisam do cabeçalho X-Requested-With
    /// (que um site de terceiros não consegue enviar sem passar pelo CORS). Soma-se ao SameSite=Strict do cookie.</summary>
    public static IApplicationBuilder UseProtecaoCsrf(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var m = ctx.Request.Method;
        var seguro = HttpMethods.IsGet(m) || HttpMethods.IsHead(m) || HttpMethods.IsOptions(m) || HttpMethods.IsTrace(m);
        if (!seguro && ctx.Request.Cookies.ContainsKey(SessaoCookie.Nome) && string.IsNullOrEmpty(ctx.Request.Headers.Authorization)
            && ctx.Request.Headers[SessaoCookie.CabecalhoCsrf] != SessaoCookie.ValorCsrf)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { title = "Requisição não autorizada", detail = "Cabeçalho de proteção ausente.", status = 403 });
            return;
        }
        await next();
    });

    /// <summary>Corpos JSON grandes não fazem sentido na API (uploads usam multipart): corta cedo para evitar abuso de memória.</summary>
    public static IApplicationBuilder UseLimiteJson(this IApplicationBuilder app, long maxBytes = 256 * 1024) => app.Use(async (ctx, next) =>
    {
        if (ctx.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true && ctx.Request.ContentLength > maxBytes)
        {
            ctx.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        await next();
    });
}

public static class Rede
{
    public static string Ip(HttpContext ctx) => ctx.Connection.RemoteIpAddress is { } ip ? (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString() : "?";
}

/// <summary>Verifica a assinatura (magic bytes) do arquivo enviado — a extensão informada pelo cliente não é confiável.</summary>
public static class AssinaturaArquivo
{
    public static async Task<bool> ConfereAsync(Stream s, string extensao, CancellationToken ct)
    {
        var buf = new byte[16];
        var n = await s.ReadAsync(buf.AsMemory(0, buf.Length), ct);
        s.Position = 0;
        if (n < 4) return false;
        return extensao switch
        {
            ".pdf" => buf[0] == 0x25 && buf[1] == 0x50 && buf[2] == 0x44 && buf[3] == 0x46,                  // %PDF
            ".png" => buf[0] == 0x89 && buf[1] == 0x50 && buf[2] == 0x4E && buf[3] == 0x47,
            ".jpg" or ".jpeg" => buf[0] == 0xFF && buf[1] == 0xD8 && buf[2] == 0xFF,
            ".xml" => await ComecaComXmlAsync(s, ct),
            _ => false,
        };
    }

    private static async Task<bool> ComecaComXmlAsync(Stream s, CancellationToken ct)
    {
        var buf = new byte[256];
        var n = await s.ReadAsync(buf.AsMemory(0, buf.Length), ct);
        s.Position = 0;
        var txt = Encoding.UTF8.GetString(buf, 0, n).TrimStart('﻿', ' ', '\r', '\n', '\t');
        return txt.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) || txt.StartsWith('<');
    }
}
