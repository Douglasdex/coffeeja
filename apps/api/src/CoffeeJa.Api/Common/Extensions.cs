using System.Security.Claims;
using CoffeeJa.Domain;

namespace CoffeeJa.Api.Common;

public static class ClaimsExtensions
{
    public static Guid UsuarioId(this ClaimsPrincipal u) => Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static Guid? ContratanteId(this ClaimsPrincipal u) => Guid.TryParse(u.FindFirstValue("contratante_id"), out var g) ? g : null;
    public static bool EhAdminConta(this ClaimsPrincipal u) => u.FindFirstValue("funcao") == nameof(FuncaoUsuario.AdminConta);
    public static string NomeUsuario(this ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.Name) ?? "";
    public static string? SetorUsuario(this ClaimsPrincipal u) => u.FindFirstValue("setor");
    public static Guid? FornecedorId(this ClaimsPrincipal u) => Guid.TryParse(u.FindFirstValue("fornecedor_id"), out var g) ? g : null;
}

public static class Politicas
{
    public const string Contratante = nameof(Contratante);
    public const string Fornecedor = nameof(Fornecedor);
    public const string Admin = nameof(Admin);
    public const string AdminConta = nameof(AdminConta);
}

public static class Validacao
{
    public static IResult? Erros(params (bool invalido, string campo, string msg)[] regras)
    {
        var erros = regras.Where(r => r.invalido).ToDictionary(r => r.campo, r => new[] { r.msg });
        return erros.Count == 0 ? null : Results.ValidationProblem(erros);
    }
}

public static class Fila
{
    /// <summary>Descrição legível das restrições, para exibição.</summary>
    public static string[] Nomes(Restricoes r) => Enum.GetValues<Restricoes>()
        .Where(f => f != Restricoes.Nenhuma && r.HasFlag(f)).Select(f => f.ToString()).ToArray();
}
