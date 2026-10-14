using System.Security.Cryptography;
using System.Text;
using CoffeeJa.Domain;

namespace CoffeeJa.Api.Common;

public static class Documentos
{
    public static string Digitos(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    public static bool CpfValido(string? valor)
    {
        var c = Digitos(valor);
        if (c.Length != 11 || c.Distinct().Count() == 1) return false;
        return Dv(c, 9, 10) == c[9] - '0' && Dv(c, 10, 11) == c[10] - '0';

        static int Dv(string c, int n, int peso)
        {
            var soma = 0;
            for (var i = 0; i < n; i++) soma += (c[i] - '0') * (peso - i);
            var r = soma * 10 % 11;
            return r == 10 ? 0 : r;
        }
    }

    public static bool CnpjValido(string? valor)
    {
        var c = Digitos(valor);
        if (c.Length != 14 || c.Distinct().Count() == 1) return false;
        int[] p1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2], p2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        return Dv(c, p1) == c[12] - '0' && Dv(c, p2) == c[13] - '0';

        static int Dv(string c, int[] pesos)
        {
            var soma = 0;
            for (var i = 0; i < pesos.Length; i++) soma += (c[i] - '0') * pesos[i];
            var r = soma % 11;
            return r < 2 ? 0 : 11 - r;
        }
    }

    /// <summary>Telefone brasileiro com DDD (10 ou 11 dígitos), aceitando prefixo 55. Retorna só dígitos sem o 55.</summary>
    public static string? Telefone(string? valor)
    {
        var d = Digitos(valor);
        if (d.Length is 12 or 13 && d.StartsWith("55")) d = d[2..];
        if (d.Length is not (10 or 11)) return null;
        if (d[0] == '0' || d[1] == '0') return null;
        return d;
    }

    public static bool CelularValido(string? valor) => Telefone(valor) is { Length: 11 } t && t[2] == '9';

    public static string MascararCpf(string? cpf)
    {
        var d = Digitos(cpf);
        return d.Length == 11 ? $"***.{d[3..6]}.{d[6..9]}-**" : "";
    }

    public static string? ValidarEndereco(Endereco? e, string rotulo = "Endereço")
    {
        if (e is null) return $"{rotulo}: preencha todos os campos.";
        if (Digitos(e.Cep).Length != 8) return $"{rotulo}: CEP inválido.";
        if (string.IsNullOrWhiteSpace(e.Logradouro) || string.IsNullOrWhiteSpace(e.Numero) || string.IsNullOrWhiteSpace(e.Bairro) || string.IsNullOrWhiteSpace(e.Cidade))
            return $"{rotulo}: preencha logradouro, número, bairro e cidade.";
        if (e.Uf?.Trim().Length != 2) return $"{rotulo}: informe a UF.";
        return null;
    }

    public static Endereco Normalizar(Endereco e) => new()
    {
        Cep = Digitos(e.Cep), Logradouro = e.Logradouro.Trim(), Numero = e.Numero.Trim(), Complemento = string.IsNullOrWhiteSpace(e.Complemento) ? null : e.Complemento.Trim(),
        Bairro = e.Bairro.Trim(), Cidade = e.Cidade.Trim(), Uf = e.Uf.Trim().ToUpperInvariant(),
    };
}

public static class Tokens
{
    /// <summary>Token aleatório (url-safe) e seu hash SHA-256. Só o hash é guardado no banco.</summary>
    public static (string Token, string Hash) Novo()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return (token, Hash(token));
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sem O/0/I/1
    public static string Codigo(int tamanho = 8)
    {
        var sb = new StringBuilder(tamanho);
        foreach (var b in RandomNumberGenerator.GetBytes(tamanho)) sb.Append(Alfabeto[b % Alfabeto.Length]);
        return sb.ToString();
    }
}

public static class WhatsApp
{
    /// <summary>Link que abre o WhatsApp (app ou web) com a mensagem pronta para o número informado.</summary>
    public static string Url(string? telefone, string mensagem)
    {
        var d = Documentos.Telefone(telefone);
        return d is null ? $"https://wa.me/?text={Uri.EscapeDataString(mensagem)}" : $"https://wa.me/55{d}?text={Uri.EscapeDataString(mensagem)}";
    }
}
