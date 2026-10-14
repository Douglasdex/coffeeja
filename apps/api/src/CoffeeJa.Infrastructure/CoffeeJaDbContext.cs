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

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Ids são gerados no domínio; sem isso o EF trata filhos novos de entidades rastreadas como existentes.
        foreach (var t in b.Model.GetEntityTypes().Where(t => typeof(Entidade).IsAssignableFrom(t.ClrType)))
            b.Entity(t.ClrType).Property(nameof(Entidade.Id)).ValueGeneratedNever();

        b.Entity<Usuario>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Nome).HasMaxLength(150);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Telefone).HasMaxLength(30);
            e.HasOne(x => x.Contratante).WithOne(x => x.Usuario).HasForeignKey<Contratante>(x => x.UsuarioId);
            e.HasOne(x => x.Fornecedor).WithOne(x => x.Usuario).HasForeignKey<Fornecedor>(x => x.UsuarioId);
        });

        b.Entity<Contratante>(e =>
        {
            e.Property(x => x.Empresa).HasMaxLength(200);
            e.Property(x => x.Cnpj).HasMaxLength(18);
        });

        b.Entity<Fornecedor>(e =>
        {
            e.Property(x => x.NomeFantasia).HasMaxLength(200);
            e.Property(x => x.Cnpj).HasMaxLength(18);
            e.Property(x => x.Descricao).HasMaxLength(1000);
            e.Property(x => x.Cidade).HasMaxLength(100);
            e.Property(x => x.Uf).HasMaxLength(2);
            e.HasIndex(x => new { x.Latitude, x.Longitude });
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
}
