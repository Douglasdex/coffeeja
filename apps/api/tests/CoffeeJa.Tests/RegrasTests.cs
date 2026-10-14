using CoffeeJa.Api.Common;
using CoffeeJa.Api.Features;
using CoffeeJa.Api.Services;
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

    [Theory]
    [InlineData("curta1!", false)]
    [InlineData("senha12345", false)]
    [InlineData("aaaaaaaaaa1", false)]
    [InlineData("somenteletras", false)]
    [InlineData("Cafe#Forte2026", true)]
    [InlineData("uma frase longa 7", true)]
    public void PoliticaSenha_aceita_apenas_senhas_fortes(string senha, bool ok) => Assert.Equal(ok, PoliticaSenha.Validar(senha) is null);

    [Fact]
    public void PoliticaSenha_rejeita_senha_com_nome_ou_email()
    {
        Assert.NotNull(PoliticaSenha.Validar("Fernanda#2026x", "fernanda@empresa.com", "Fernanda Lima"));
        Assert.Null(PoliticaSenha.Validar("Cafe#Forte2026", "fernanda@empresa.com", "Fernanda Lima"));
    }

    [Fact]
    public void Totp_confere_com_vetor_do_RFC6238()
    {
        // RFC 6238, apêndice B (SHA-1, segredo "12345678901234567890"), T = 59 s → 94287082 (6 últimos dígitos: 287082)
        var segredo = Totp.Base32(System.Text.Encoding.ASCII.GetBytes("12345678901234567890"));
        Assert.Equal("287082", Totp.Gerar(Totp.Decodificar(segredo), 1));
        Assert.NotNull(Totp.Verificar(segredo, "287082", DateTime.UnixEpoch.AddSeconds(59)));
        Assert.Null(Totp.Verificar(segredo, "000000", DateTime.UnixEpoch.AddSeconds(59)));
    }

    [Fact]
    public void Totp_aceita_deriva_de_um_passo_mas_nao_de_dois()
    {
        var segredo = Totp.NovoSegredo();
        var agora = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var codigoPassoAnterior = Totp.Gerar(Totp.Decodificar(segredo), Totp.Passo(agora) - 1);
        var codigoDoisPassos = Totp.Gerar(Totp.Decodificar(segredo), Totp.Passo(agora) - 2);
        Assert.NotNull(Totp.Verificar(segredo, codigoPassoAnterior, agora));
        Assert.Null(Totp.Verificar(segredo, codigoDoisPassos, agora));
    }

    [Fact]
    public void Codigos_de_recuperacao_sao_de_uso_unico()
    {
        var (claros, hashes) = Totp.NovosCodigosRecuperacao();
        var u = new CoffeeJa.Domain.Usuario { CodigosRecuperacaoHash = hashes };
        Assert.True(Totp.ConsumirRecuperacao(u, claros[0]));
        Assert.False(Totp.ConsumirRecuperacao(u, claros[0]));
        Assert.True(Totp.ConsumirRecuperacao(u, claros[1].ToLowerInvariant()));
    }

    [Theory]
    [InlineData(".pdf", "%PDF-1.7 abc", true)]
    [InlineData(".pdf", "MZ programa", false)]
    [InlineData(".xml", "<?xml version=\"1.0\"?><a/>", true)]
    [InlineData(".xml", "MZ programa", false)]
    [InlineData(".exe", "%PDF-1.7 abc", false)]
    public async Task AssinaturaArquivo_confere_o_conteudo_real(string ext, string conteudo, bool esperado)
    {
        using var ms = new MemoryStream(System.Text.Encoding.Latin1.GetBytes(conteudo));
        Assert.Equal(esperado, await AssinaturaArquivo.ConfereAsync(ms, ext, CancellationToken.None));
    }
}
