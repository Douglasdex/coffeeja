namespace CoffeeJa.Api.Common;

public class JwtOptions
{
    public string Issuer { get; set; } = "coffeeja";
    public string Audience { get; set; } = "coffeeja-web";
    public string Key { get; set; } = "";
    public int ExpiraEmMinutos { get; set; } = 60 * 12;
}

public class PlataformaOptions
{
    /// <summary>Raio máximo de atendimento (km). O fornecedor pode reduzir, não ampliar.</summary>
    public int RaioMaximoKm { get; set; } = 50;
    public decimal ComissaoPercentual { get; set; } = 10m;
    public bool AprovarFornecedoresAutomaticamente { get; set; }
    public string PastaArquivos { get; set; } = "storage";
    public long TamanhoMaximoNotaFiscalBytes { get; set; } = 10 * 1024 * 1024;
    public string[] Origens { get; set; } = ["http://localhost:5173"];
    public string? AdminEmail { get; set; }
    public string? AdminSenha { get; set; }
}
