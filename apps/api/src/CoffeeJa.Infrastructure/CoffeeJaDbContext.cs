using CoffeeJa.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoffeeJa.Infrastructure;

public class CoffeeJaDbContext(DbContextOptions<CoffeeJaDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Contratante> Contratantes => Set<Contratante>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<ModeloCardapio> ModelosCardapio => Set<ModeloCardapio>();
    public DbSet<Demanda> Demandas => Set<Demanda>();
    public DbSet<Proposta> Propostas => Set<Proposta>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<EventoPedido> EventosPedido => Set<EventoPedido>();
    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();
    public DbSet<Mensagem> Mensagens => Set<Mensagem>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<PushInscricao> PushInscricoes => Set<PushInscricao>();
    public DbSet<Convite> Convites => Set<Convite>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Ids são gerados no domínio; sem isso o EF trata filhos novos de entidades rastreadas como existentes.
        foreach (var t in b.Model.GetEntityTypes().Where(t => typeof(Entidade).IsAssignableFrom(t.ClrType)))
            b.Entity(t.ClrType).Property(nameof(Entidade.Id)).ValueGeneratedNever();

        b.Entity<Usuario>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
            e.HasIndex(x => x.Cpf).IsUnique().HasFilter("[Cpf] IS NOT NULL");
            e.HasIndex(x => x.CodigoConvite).IsUnique();
            e.HasIndex(x => x.ContratanteId);
            e.HasIndex(x => x.FornecedorId);
            e.HasIndex(x => x.IndicadoPorId);
            e.Property(x => x.Nome).HasMaxLength(150);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Cpf).HasMaxLength(11);
            e.Property(x => x.Telefone).HasMaxLength(20);
            e.Property(x => x.Whatsapp).HasMaxLength(20);
            e.Property(x => x.Cargo).HasMaxLength(100);
            e.Property(x => x.Setor).HasMaxLength(100);
            e.Property(x => x.SuperiorNome).HasMaxLength(150);
            e.Property(x => x.SuperiorWhatsapp).HasMaxLength(20);
            e.Property(x => x.AprovacaoTokenHash).HasMaxLength(64);
            e.Property(x => x.AtivacaoTokenHash).HasMaxLength(64);
            e.Property(x => x.MotivoRecusa).HasMaxLength(500);
            e.Property(x => x.CodigoConvite).HasMaxLength(12);
            e.HasIndex(x => x.AprovacaoTokenHash);
            e.HasIndex(x => x.AtivacaoTokenHash);
            e.HasOne(x => x.Contratante).WithMany().HasForeignKey(x => x.ContratanteId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<Contratante>(e =>
        {
            e.Property(x => x.Empresa).HasMaxLength(200);
            e.Property(x => x.NomeFantasia).HasMaxLength(200);
            e.Property(x => x.Cnpj).HasMaxLength(14);
            e.Property(x => x.Cpf).HasMaxLength(11);
            e.HasIndex(x => x.Cnpj).IsUnique().HasFilter("[Cnpj] IS NOT NULL");
            e.ComplexProperty(x => x.Endereco, ConfigEndereco);
            e.ComplexProperty(x => x.EnderecoNotaFiscal, ConfigEndereco);
        });

        b.Entity<Fornecedor>(e =>
        {
            e.Property(x => x.NomeFantasia).HasMaxLength(200);
            e.Property(x => x.RazaoSocial).HasMaxLength(200);
            e.Property(x => x.Cnpj).HasMaxLength(14);
            e.HasIndex(x => x.Cnpj).IsUnique().HasFilter("[Cnpj] IS NOT NULL");
            e.Property(x => x.Descricao).HasMaxLength(1000);
            e.Property(x => x.Cidade).HasMaxLength(100);
            e.Property(x => x.Uf).HasMaxLength(2);
            e.ComplexProperty(x => x.Endereco, ConfigEndereco);
            e.HasIndex(x => new { x.Latitude, x.Longitude });
        });

        b.Entity<Convite>(e =>
        {
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasIndex(x => x.ConvidanteId);
            e.Property(x => x.Codigo).HasMaxLength(12);
            e.Property(x => x.Telefone).HasMaxLength(20);
            e.Property(x => x.NomeConvidado).HasMaxLength(150);
        });

        b.Entity<ModeloCardapio>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(100);
            e.Property(x => x.Itens).HasMaxLength(2000);
            e.Property(x => x.ValorPorPessoa).HasPrecision(10, 2);
        });

        b.Entity<Demanda>(e =>
        {
            e.Property(x => x.Titulo).HasMaxLength(150);
            e.Property(x => x.Endereco).HasMaxLength(300);
            e.Property(x => x.Cidade).HasMaxLength(100);
            e.Property(x => x.Uf).HasMaxLength(2);
            e.Property(x => x.Observacoes).HasMaxLength(2000);
            e.Property(x => x.SolicitanteNome).HasMaxLength(150);
            e.Property(x => x.SolicitanteSetor).HasMaxLength(100);
            e.HasOne(x => x.Contratante).WithMany().HasForeignKey(x => x.ContratanteId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.Status, x.DataEvento });
            e.HasIndex(x => new { x.Latitude, x.Longitude });
        });

        b.Entity<Proposta>(e =>
        {
            e.Property(x => x.ValorTotal).HasPrecision(12, 2);
            e.Property(x => x.Cardapio).HasMaxLength(3000);
            e.Property(x => x.Observacao).HasMaxLength(2000);
            e.Property(x => x.AjusteSolicitado).HasMaxLength(2000);
            e.Ignore(x => x.ValorPorPessoa);
            e.HasOne(x => x.Demanda).WithMany(x => x.Propostas).HasForeignKey(x => x.DemandaId);
            e.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.DemandaId, x.FornecedorId }).IsUnique();
        });

        b.Entity<Pedido>(e =>
        {
            e.Property(x => x.Valor).HasPrecision(12, 2);
            e.Property(x => x.ComissaoPercentual).HasPrecision(5, 2);
            e.Property(x => x.ProvedorPagamento).HasMaxLength(30);
            e.Property(x => x.ContratadoPorNome).HasMaxLength(150);
            e.Property(x => x.ContratadoPorSetor).HasMaxLength(100);
            e.Property(x => x.NotaFiscalNumero).HasMaxLength(50);
            e.Property(x => x.NotaFiscalArquivo).HasMaxLength(300);
            e.Property(x => x.NotaFiscalNomeOriginal).HasMaxLength(260);
            e.HasOne(x => x.Demanda).WithMany().HasForeignKey(x => x.DemandaId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Proposta).WithMany().HasForeignKey(x => x.PropostaId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => x.DemandaId).IsUnique();
            e.HasIndex(x => x.ContratanteId);
            e.HasIndex(x => x.FornecedorId);
        });

        b.Entity<EventoPedido>(e =>
        {
            e.Property(x => x.Descricao).HasMaxLength(300);
            e.HasOne(x => x.Pedido).WithMany(x => x.Eventos).HasForeignKey(x => x.PedidoId);
        });

        b.Entity<Avaliacao>(e =>
        {
            e.Property(x => x.Comentario).HasMaxLength(1000);
            e.HasIndex(x => x.PedidoId).IsUnique();
            e.HasIndex(x => x.FornecedorId);
        });

        b.Entity<Mensagem>(e =>
        {
            e.Property(x => x.Texto).HasMaxLength(2000);
            e.HasOne(x => x.Proposta).WithMany().HasForeignKey(x => x.PropostaId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PropostaId, x.CriadoEm });
        });

        b.Entity<Notificacao>(e =>
        {
            e.Property(x => x.Titulo).HasMaxLength(150);
            e.Property(x => x.Texto).HasMaxLength(500);
            e.Property(x => x.Link).HasMaxLength(200);
            e.HasIndex(x => new { x.UsuarioId, x.Lida });
        });

        b.Entity<PushInscricao>(e =>
        {
            e.Property(x => x.Endpoint).HasMaxLength(450);
            e.Property(x => x.P256dh).HasMaxLength(200);
            e.Property(x => x.Auth).HasMaxLength(100);
            e.HasIndex(x => x.Endpoint).IsUnique();
            e.HasIndex(x => x.UsuarioId);
        });
    }

    private static void ConfigEndereco(Microsoft.EntityFrameworkCore.Metadata.Builders.ComplexPropertyBuilder<Endereco> c)
    {
        c.Property(x => x.Cep).HasMaxLength(8);
        c.Property(x => x.Logradouro).HasMaxLength(200);
        c.Property(x => x.Numero).HasMaxLength(20);
        c.Property(x => x.Complemento).HasMaxLength(100);
        c.Property(x => x.Bairro).HasMaxLength(100);
        c.Property(x => x.Cidade).HasMaxLength(100);
        c.Property(x => x.Uf).HasMaxLength(2);
    }
}
