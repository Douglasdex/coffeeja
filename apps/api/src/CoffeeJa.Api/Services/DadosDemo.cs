using CoffeeJa.Api.Features;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoffeeJa.Api.Services;

/// <summary>Dados fictícios para testar localmente (Banco:DadosDemo=true). Só roda se não houver usuários demo.</summary>
public static class DadosDemo
{
    public const string Senha = "Demo@12345";

    public static async Task GerarAsync(CoffeeJaDbContext db)
    {
        if (await db.Usuarios.AnyAsync(u => u.Email == "contratante@demo.local")) return;

        var cu = AuthEndpoints.NovoUsuario("Ana Souza", "contratante@demo.local", "(11) 91234-5678", Senha, Papel.Contratante);
        cu.Contratante = new Contratante { Empresa = "ACME Ltda" };

        Usuario Buffet(string nome, string email, string fantasia, double lat, double lng, string cidade, Restricoes r, bool fundador, params (string, EstiloCoffee, string, decimal)[] cardapios)
        {
            var u = AuthEndpoints.NovoUsuario(nome, email, "(11) 98888-0000", Senha, Papel.Fornecedor);
            u.Fornecedor = new Fornecedor
            {
                NomeFantasia = fantasia, Cidade = cidade, Uf = "SP", Latitude = lat, Longitude = lng, Aprovado = true, Fundador = fundador,
                AtendeRestricoes = r, NotaMedia = fundador ? 4.8 : 4.5, TotalAvaliacoes = fundador ? 23 : 9,
                Descricao = "Buffet de demonstração para testes locais.",
            };
            foreach (var (n, e, itens, v) in cardapios)
                u.Fornecedor.Cardapios.Add(new ModeloCardapio { Nome = n, Estilo = e, Itens = itens, ValorPorPessoa = v, MinimoPessoas = 10 });
            return u;
        }

        var b1 = Buffet("Beto Silva", "buffet1@demo.local", "Sabor & Arte", -23.55, -46.63, "São Paulo",
            Restricoes.Vegano | Restricoes.SemGluten | Restricoes.SemLactose, true,
            ("Bronze", EstiloCoffee.Economico, "Café, leite, suco de caixinha, pão de queijo, mini sanduíche", 32m),
            ("Prata", EstiloCoffee.Padrao, "Café, suco natural, pão de queijo, mini quiche, bolo, frutas", 48m),
            ("Ouro", EstiloCoffee.Premium, "Café especial, sucos, salgados folhados, tortinhas, mesa de frutas", 72m));
        var b2 = Buffet("Cris Lima", "buffet2@demo.local", "Casa do Café", -23.58, -46.66, "São Paulo",
            Restricoes.Vegetariano | Restricoes.SemLactose, false,
            ("Executivo", EstiloCoffee.Padrao, "Café, chás, croissants, mini sanduíches, bolo de cenoura", 55m));
        var b3 = Buffet("Davi Rocha", "buffet3@demo.local", "Delícias do Vale", -22.90, -47.06, "Campinas", Restricoes.Nenhuma, false,
            ("Básico", EstiloCoffee.Economico, "Café, leite, pão de queijo, bolo", 28m)); // ~90 km: fora do raio

        db.Usuarios.AddRange(cu, b1, b2, b3);
        await db.SaveChangesAsync();

        var hoje = DateTime.UtcNow.AddHours(-3).Date;
        var d1 = new Demanda
        {
            ContratanteId = cu.Contratante.Id, Titulo = "Treinamento de vendas Q4", DataEvento = hoje.AddDays(5), HoraInicio = new TimeSpan(8, 30, 0),
            Endereco = "Av. Paulista, 1000", Cidade = "São Paulo", Uf = "SP", Latitude = -23.56, Longitude = -46.65, NumeroPessoas = 50,
            Restricoes = Restricoes.Vegano | Restricoes.SemLactose, Estilo = EstiloCoffee.Padrao,
        };
        var d2 = new Demanda
        {
            ContratanteId = cu.Contratante.Id, Titulo = "Reunião de diretoria", DataEvento = hoje.AddDays(9), HoraInicio = new TimeSpan(14, 0, 0),
            Endereco = "R. Augusta, 500", Cidade = "São Paulo", Uf = "SP", Latitude = -23.55, Longitude = -46.65, NumeroPessoas = 12,
            Estilo = EstiloCoffee.Premium,
        };
        db.Demandas.AddRange(d1, d2);
        db.Propostas.AddRange(
            new Proposta { DemandaId = d1.Id, FornecedorId = b1.Fornecedor!.Id, ValorTotal = 2400m, Cardapio = "Prata: café, suco natural, pão de queijo, mini quiche, bolo de cenoura, opções veganas" },
            new Proposta { DemandaId = d1.Id, FornecedorId = b2.Fornecedor!.Id, ValorTotal = 2750m, Cardapio = "Executivo: café, chás, croissants, mini sanduíches, bolo" });
        db.Notificacoes.Add(new Notificacao { UsuarioId = cu.Id, Titulo = "Nova proposta recebida", Texto = "Sabor & Arte · R$ 2.400,00", Link = $"/contratante/demandas/{d1.Id}" });
        await db.SaveChangesAsync();
    }
}
