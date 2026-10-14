using CoffeeJa.Api.Features;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoffeeJa.Api.Services;

/// <summary>Dados fictícios para testar localmente (Banco:DadosDemo=true). Só roda se não houver usuários demo.
/// CPFs/CNPJs são sintéticos, válidos apenas no dígito verificador.</summary>
public static class DadosDemo
{
    public const string Senha = "Demo@12345";

    private static Endereco End(string rua, string num, string bairro, string cidade, string cep) =>
        new() { Cep = cep, Logradouro = rua, Numero = num, Bairro = bairro, Cidade = cidade, Uf = "SP" };

    public static async Task GerarAsync(CoffeeJaDbContext db)
    {
        if (await db.Usuarios.AnyAsync(u => u.Email == "contratante@demo.local")) return;

        // Contratante PJ: admin + 1 funcionário ativo + 1 aguardando aprovação do superior
        var endAcme = End("Av. Paulista", "1000", "Bela Vista", "São Paulo", "01310100");
        var admin = AuthEndpoints.NovoUsuario("Ana Souza", "contratante@demo.local", "11300000001", Senha, Papel.Contratante);
        admin.Cpf = "52998224725"; admin.Whatsapp = "11912345678";
        admin.Contratante = new Contratante
        {
            Tipo = TipoConta.PessoaJuridica, Empresa = "ACME Comércio Ltda", NomeFantasia = "ACME", Cnpj = "11222333000181",
            Endereco = endAcme, EnderecoNotaFiscal = endAcme.Copiar(), UsuarioId = admin.Id,
        };

        var func = AuthEndpoints.NovoUsuario("Carlos Pereira", null, "11300000002", Senha, Papel.Contratante);
        func.Cpf = "11144477735"; func.Whatsapp = "11987654321"; func.Setor = "Recursos Humanos"; func.Funcao = FuncaoUsuario.Membro;
        func.SuperiorNome = "Marta Lima"; func.SuperiorWhatsapp = "11955554444"; func.AprovadoEm = DateTime.UtcNow; func.CriadoPorId = admin.Id;
        func.Contratante = admin.Contratante;

        var pendente = AuthEndpoints.NovoUsuario("Paula Nunes", null, "11300000003", null, Papel.Contratante);
        pendente.Cpf = "39053344705"; pendente.Whatsapp = "11977776666"; pendente.Setor = "Financeiro"; pendente.Funcao = FuncaoUsuario.Membro;
        pendente.Status = StatusUsuario.PendenteAprovacao; pendente.SuperiorNome = "Roberto Alves"; pendente.SuperiorWhatsapp = "11944443333"; pendente.CriadoPorId = admin.Id;
        pendente.Contratante = admin.Contratante;

        // Contratante pessoa física
        var endPf = End("Rua Augusta", "500", "Consolação", "São Paulo", "01305000");
        var pf = AuthEndpoints.NovoUsuario("Juliana Mendes", "pf@demo.local", null, Senha, Papel.Contratante);
        pf.Cpf = "86288366757"; pf.Whatsapp = "11933332222";
        pf.Contratante = new Contratante
        {
            Tipo = TipoConta.PessoaFisica, Empresa = "Juliana Mendes", Cpf = pf.Cpf, Endereco = endPf, EnderecoNotaFiscal = endPf.Copiar(),
            NotaFiscalMesmoEndereco = true, UsuarioId = pf.Id,
        };

        Usuario Buffet(string nome, string email, string cpf, string fantasia, string cnpj, double lat, double lng, string cidade, Restricoes r, bool fundador,
            params (string, EstiloCoffee, string, decimal)[] cardapios)
        {
            var u = AuthEndpoints.NovoUsuario(nome, email, "11300009999", Senha, Papel.Fornecedor);
            u.Cpf = cpf; u.Whatsapp = "11988880000";
            u.Fornecedor = new Fornecedor
            {
                UsuarioId = u.Id, NomeFantasia = fantasia, RazaoSocial = fantasia + " Ltda", Cnpj = cnpj,
                Endereco = End("Rua das Flores", "10", "Centro", cidade, "01000000"), Cidade = cidade, Uf = "SP", Latitude = lat, Longitude = lng,
                Aprovado = true, Fundador = fundador, AtendeRestricoes = r, NotaMedia = fundador ? 4.8 : 4.5, TotalAvaliacoes = fundador ? 23 : 9,
                Descricao = "Buffet de demonstração para testes locais.",
            };
            foreach (var (n, e, itens, v) in cardapios)
                u.Fornecedor.Cardapios.Add(new ModeloCardapio { Nome = n, Estilo = e, Itens = itens, ValorPorPessoa = v, MinimoPessoas = 10 });
            return u;
        }

        var b1 = Buffet("Beto Silva", "buffet1@demo.local", "15350946056", "Sabor & Arte", "45723174000110", -23.55, -46.63, "São Paulo",
            Restricoes.Vegano | Restricoes.SemGluten | Restricoes.SemLactose, true,
            ("Bronze", EstiloCoffee.Economico, "Café, leite, suco de caixinha, pão de queijo, mini sanduíche", 32m),
            ("Prata", EstiloCoffee.Padrao, "Café, suco natural, pão de queijo, mini quiche, bolo, frutas", 48m),
            ("Ouro", EstiloCoffee.Premium, "Café especial, sucos, salgados folhados, tortinhas, mesa de frutas", 72m));
        var b2 = Buffet("Cris Lima", "buffet2@demo.local", "93541134780", "Casa do Café", "34028316000103", -23.58, -46.66, "São Paulo",
            Restricoes.Vegetariano | Restricoes.SemLactose, false,
            ("Executivo", EstiloCoffee.Padrao, "Café, chás, croissants, mini sanduíches, bolo de cenoura", 55m));
        var b3 = Buffet("Davi Rocha", "buffet3@demo.local", "71428793860", "Delícias do Vale", "07526557000100", -22.90, -47.06, "Campinas", Restricoes.Nenhuma, false,
            ("Básico", EstiloCoffee.Economico, "Café, leite, pão de queijo, bolo", 28m)); // ~90 km: fora do raio

        db.Usuarios.AddRange(admin, func, pendente, pf, b1, b2, b3);
        await db.SaveChangesAsync();

        var hoje = DateTime.UtcNow.AddHours(-3).Date;
        var d1 = new Demanda
        {
            ContratanteId = admin.Contratante.Id, Titulo = "Treinamento de vendas Q4", DataEvento = hoje.AddDays(5), HoraInicio = new TimeSpan(8, 30, 0),
            Endereco = "Av. Paulista, 1000", Cidade = "São Paulo", Uf = "SP", Latitude = -23.56, Longitude = -46.65, NumeroPessoas = 50,
            Restricoes = Restricoes.Vegano | Restricoes.SemLactose, Estilo = EstiloCoffee.Padrao, Descartaveis = Descartaveis.Pratos | Descartaveis.Copos,
            SolicitanteId = func.Id, SolicitanteNome = func.Nome, SolicitanteSetor = func.Setor,
        };
        var d2 = new Demanda
        {
            ContratanteId = admin.Contratante.Id, Titulo = "Reunião de diretoria", DataEvento = hoje.AddDays(9), HoraInicio = new TimeSpan(14, 0, 0),
            Endereco = "R. Augusta, 500", Cidade = "São Paulo", Uf = "SP", Latitude = -23.55, Longitude = -46.65, NumeroPessoas = 12,
            Estilo = EstiloCoffee.Premium, SolicitanteId = admin.Id, SolicitanteNome = admin.Nome,
        };
        db.Demandas.AddRange(d1, d2);
        db.Propostas.AddRange(
            new Proposta { DemandaId = d1.Id, FornecedorId = b1.Fornecedor!.Id, ValorTotal = 2400m, Cardapio = "Prata: café, suco natural, pão de queijo, mini quiche, bolo de cenoura, opções veganas" },
            new Proposta { DemandaId = d1.Id, FornecedorId = b2.Fornecedor!.Id, ValorTotal = 2750m, Cardapio = "Executivo: café, chás, croissants, mini sanduíches, bolo" });
        db.Notificacoes.Add(new Notificacao { UsuarioId = func.Id, Titulo = "Nova proposta recebida", Texto = "Sabor & Arte · R$ 2.400,00", Link = $"/contratante/demandas/{d1.Id}" });
        await db.SaveChangesAsync();
    }
}
