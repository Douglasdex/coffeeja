using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoffeeJa.Api.Common;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoffeeJa.Api.Services;

public class TokenService(IOptions<JwtOptions> opt)
{
    public string Gerar(Usuario u)
    {
        var o = opt.Value;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.Nome),
            new(ClaimTypes.Email, u.Email),
            new(ClaimTypes.Role, u.Papel.ToString()),
        };
        if (u.Contratante is not null) claims.Add(new("contratante_id", u.Contratante.Id.ToString()));
        if (u.Fornecedor is not null) claims.Add(new("fornecedor_id", u.Fornecedor.Id.ToString()));

        var cred = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, expires: DateTime.UtcNow.AddMinutes(o.ExpiraEmMinutos), signingCredentials: cred);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>Canal de notificação. Hoje grava no banco; push/WhatsApp/e-mail entram aqui sem mexer nos endpoints.</summary>
public interface INotificador
{
    void Notificar(Guid usuarioId, string titulo, string texto, string? link = null);
}

public class NotificadorEmBanco(CoffeeJaDbContext db) : INotificador
{
    public void Notificar(Guid usuarioId, string titulo, string texto, string? link = null)
        => db.Notificacoes.Add(new Notificacao { UsuarioId = usuarioId, Titulo = titulo, Texto = texto, Link = link });
}

/// <summary>Abstração do meio de pagamento. No início o pagamento é feito fora da plataforma (Manual).
/// Para ativar escrow/split, crie um provider (Pagar.me, Asaas...) e troque o registro no DI.</summary>
public interface IPagamentoProvider
{
    string Nome { get; }
    StatusPagamento StatusInicial { get; }
}

public class PagamentoManualProvider : IPagamentoProvider
{
    public string Nome => "Manual";
    public StatusPagamento StatusInicial => StatusPagamento.PagoForaDaPlataforma;
}

public interface IArquivoStorage
{
    Task<string> SalvarAsync(Stream conteudo, string extensao, CancellationToken ct);
    Stream Abrir(string caminho);
}

public class ArquivoStorageLocal(IOptions<PlataformaOptions> opt, IHostEnvironment env) : IArquivoStorage
{
    private string Raiz => Path.IsPathRooted(opt.Value.PastaArquivos)
        ? opt.Value.PastaArquivos
        : Path.Combine(env.ContentRootPath, opt.Value.PastaArquivos);

    public async Task<string> SalvarAsync(Stream conteudo, string extensao, CancellationToken ct)
    {
        var pasta = Path.Combine(Raiz, "notas-fiscais", DateTime.UtcNow.ToString("yyyyMM"));
        Directory.CreateDirectory(pasta);
        var nome = $"{Guid.NewGuid():N}{extensao}";
        await using var fs = File.Create(Path.Combine(pasta, nome));
        await conteudo.CopyToAsync(fs, ct);
        return Path.Combine("notas-fiscais", DateTime.UtcNow.ToString("yyyyMM"), nome).Replace(Path.DirectorySeparatorChar, '/');
    }

    public Stream Abrir(string caminho)
    {
        var full = Path.GetFullPath(Path.Combine(Raiz, caminho));
        if (!full.StartsWith(Path.GetFullPath(Raiz), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho inválido.");
        return File.OpenRead(full);
    }
}

public record Coordenadas(double Latitude, double Longitude, string? Cidade, string? Uf);

public class GeocodingService(HttpClient http)
{
    public async Task<Coordenadas?> BuscarAsync(string consulta, CancellationToken ct)
    {
        var url = $"search?format=jsonv2&addressdetails=1&limit=1&countrycodes=br&q={Uri.EscapeDataString(consulta)}";
        try
        {
            var r = await http.GetFromJsonAsync<List<NominatimItem>>(url, ct);
            var i = r?.FirstOrDefault();
            if (i is null) return null;
            var a = i.address;
            return new Coordenadas(double.Parse(i.lat, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(i.lon, System.Globalization.CultureInfo.InvariantCulture),
                a?.city ?? a?.town ?? a?.village ?? a?.municipality,
                a?.Iso?.Split('-').LastOrDefault());
        }
        catch (HttpRequestException) { return null; }
    }

    private record NominatimItem(string lat, string lon, NominatimAddress? address);
    private record NominatimAddress(string? city, string? town, string? village, string? municipality,
        [property: System.Text.Json.Serialization.JsonPropertyName("ISO3166-2-lvl4")] string? Iso);
}
