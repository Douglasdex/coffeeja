using CoffeeJa.Api.Common;
using CoffeeJa.Api.Features;
using CoffeeJa.Domain;

namespace CoffeeJa.Tests;

public class RegrasTests
{
    [Fact]
    public void Distancia_SaoPaulo_Campinas_fica_fora_de_50km()
    {
        var km = Geo.DistanciaKm(-23.55, -46.63, -22.90, -47.06);
        Assert.InRange(km, 80, 100);
    }

    [Fact]
    public void Distancia_Paulista_Se_fica_dentro_de_50km()
        => Assert.True(Geo.DistanciaKm(-23.56, -46.65, -23.55, -46.63) < 5);

    [Theory]
    [InlineData("me ligue (11) 98765-4321", "me ligue [contato oculto]")]
    [InlineData("fale com joao@empresa.com.br", "fale com [contato oculto]")]
    [InlineData("50 pessoas, 30 pães", "50 pessoas, 30 pães")]
    public void OcultarContatos_mascara_telefone_e_email(string entrada, string esperado)
        => Assert.Equal(esperado, PropostaEndpoints.OcultarContatos(entrada));

    [Fact]
    public void CaixaDelimitadora_contem_pontos_dentro_do_raio()
    {
        var (minLat, maxLat, minLon, maxLon) = Geo.CaixaDelimitadora(-23.55, -46.63, 50);
        Assert.InRange(-23.55, minLat, maxLat);
        Assert.InRange(-46.63, minLon, maxLon);
        Assert.True(maxLat - minLat > 0.8);
    }

    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("11144477735", true)]
    [InlineData("111.111.111-11", false)]
    [InlineData("52998224726", false)]
    [InlineData("123", false)]
    public void Cpf_valida_digito_verificador(string cpf, bool esperado) => Assert.Equal(esperado, Documentos.CpfValido(cpf));

    [Theory]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("11222333000182", false)]
    [InlineData("00000000000000", false)]
    public void Cnpj_valida_digito_verificador(string cnpj, bool esperado) => Assert.Equal(esperado, Documentos.CnpjValido(cnpj));

    [Theory]
    [InlineData("(11) 91234-5678", "11912345678")]
    [InlineData("+55 11 3333-4444", "1133334444")]
    [InlineData("123", null)]
    public void Telefone_normaliza_e_valida(string entrada, string? esperado) => Assert.Equal(esperado, Documentos.Telefone(entrada));

    [Fact]
    public void Celular_exige_9_digitos()
    {
        Assert.True(Documentos.CelularValido("11912345678"));
        Assert.False(Documentos.CelularValido("1133334444"));
    }

    [Fact]
    public void Token_guarda_apenas_hash_e_nao_se_repete()
    {
        var (t1, h1) = Tokens.Novo();
        var (t2, _) = Tokens.Novo();
        Assert.NotEqual(t1, t2);
        Assert.Equal(h1, Tokens.Hash(t1));
        Assert.DoesNotContain(t1, h1);
    }

    [Fact]
    public void WhatsApp_monta_link_com_ddi_55()
    {
        Assert.StartsWith("https://wa.me/5511912345678?text=", WhatsApp.Url("(11) 91234-5678", "oi"));
        Assert.StartsWith("https://wa.me/?text=", WhatsApp.Url(null, "oi"));
    }

    [Fact]
    public void Mensagem_de_convite_destaca_vantagens_e_inclui_link()
    {
        var m = ContasEndpoints.MensagemConvite("Ana Souza", "ACME", "https://x/cadastro?convite=ABC", "Maria Silva");
        Assert.Contains("Olá, Maria!", m);
        Assert.Contains("propostas de buffets", m);
        Assert.Contains("https://x/cadastro?convite=ABC", m);
    }
}
