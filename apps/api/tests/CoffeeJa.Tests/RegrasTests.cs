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
}
