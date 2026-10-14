namespace CoffeeJa.Domain;

public abstract class Entidade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

public class Usuario : Entidade
{
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telefone { get; set; }
    public string SenhaHash { get; set; } = "";
    public Papel Papel { get; set; }
    public bool Ativo { get; set; } = true;

    public Contratante? Contratante { get; set; }
    public Fornecedor? Fornecedor { get; set; }
}

public class Contratante : Entidade
{
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string Empresa { get; set; } = "";
    public string? Cnpj { get; set; }
}

public class Fornecedor : Entidade
{
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string NomeFantasia { get; set; } = "";
    public string? Cnpj { get; set; }
    public string? Descricao { get; set; }
    public string Cidade { get; set; } = "";
    public string Uf { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    /// <summary>Raio de atendimento em km (padrão da plataforma: 50).</summary>
    public int RaioAtendimentoKm { get; set; } = 50;
    public Restricoes AtendeRestricoes { get; set; }
    public bool Aprovado { get; set; }
    public bool Fundador { get; set; }
    public double NotaMedia { get; set; }
    public int TotalAvaliacoes { get; set; }

    public List<ModeloCardapio> Cardapios { get; set; } = [];
}

/// <summary>Cardápio-base (ex.: Bronze/Prata/Ouro) usado para responder demandas em poucos toques.</summary>
public class ModeloCardapio : Entidade
{
    public Guid FornecedorId { get; set; }
    public Fornecedor Fornecedor { get; set; } = null!;
    public string Nome { get; set; } = "";
    public EstiloCoffee Estilo { get; set; }
    public string Itens { get; set; } = "";
    public decimal ValorPorPessoa { get; set; }
    public int MinimoPessoas { get; set; } = 10;
}

public class Demanda : Entidade
{
    public Guid ContratanteId { get; set; }
    public Contratante Contratante { get; set; } = null!;
    public string Titulo { get; set; } = "";
    public DateTime DataEvento { get; set; }
    public TimeSpan HoraInicio { get; set; }
    public string Endereco { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string Uf { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int NumeroPessoas { get; set; }
    public Restricoes Restricoes { get; set; }
    public Descartaveis Descartaveis { get; set; }
    public EstiloCoffee Estilo { get; set; }
    public string? Observacoes { get; set; }
    public DateTime? PrazoPropostas { get; set; }
    public StatusDemanda Status { get; set; } = StatusDemanda.Aberta;

    public List<Proposta> Propostas { get; set; } = [];
}

public class Proposta : Entidade
{
    public Guid DemandaId { get; set; }
    public Demanda Demanda { get; set; } = null!;
    public Guid FornecedorId { get; set; }
    public Fornecedor Fornecedor { get; set; } = null!;
    public decimal ValorTotal { get; set; }
    public string Cardapio { get; set; } = "";
    public string? Observacao { get; set; }
    public int Versao { get; set; } = 1;
    public StatusProposta Status { get; set; } = StatusProposta.Enviada;
    public string? AjusteSolicitado { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    /// <summary>Até quando cada parte já leu o chat (base do indicador de mensagens não lidas).</summary>
    public DateTime? ContratanteLeuEm { get; set; }
    public DateTime? FornecedorLeuEm { get; set; }

    public decimal ValorPorPessoa => Demanda is { NumeroPessoas: > 0 } ? Math.Round(ValorTotal / Demanda.NumeroPessoas, 2) : 0;
}

public class Pedido : Entidade
{
    public Guid DemandaId { get; set; }
    public Demanda Demanda { get; set; } = null!;
    public Guid PropostaId { get; set; }
    public Proposta Proposta { get; set; } = null!;
    public Guid ContratanteId { get; set; }
    public Guid FornecedorId { get; set; }
    public decimal Valor { get; set; }
    public decimal ComissaoPercentual { get; set; }
    public StatusPedido Status { get; set; } = StatusPedido.Confirmado;
    public StatusPagamento StatusPagamento { get; set; } = StatusPagamento.PagoForaDaPlataforma;
    public string ProvedorPagamento { get; set; } = "Manual";

    public string? NotaFiscalNumero { get; set; }
    public string? NotaFiscalArquivo { get; set; }
    public string? NotaFiscalNomeOriginal { get; set; }
    public DateTime? NotaFiscalEnviadaEm { get; set; }

    public List<EventoPedido> Eventos { get; set; } = [];
}

/// <summary>Linha do tempo do pedido.</summary>
public class EventoPedido : Entidade
{
    public Guid PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;
    public string Descricao { get; set; } = "";
}

public class Avaliacao : Entidade
{
    public Guid PedidoId { get; set; }
    public Guid FornecedorId { get; set; }
    public Guid ContratanteId { get; set; }
    public int Nota { get; set; }
    public bool Pontual { get; set; } = true;
    public string? Comentario { get; set; }
}

public class Mensagem : Entidade
{
    public Guid PropostaId { get; set; }
    public Proposta Proposta { get; set; } = null!;
    public Guid RemetenteId { get; set; }
    public string Texto { get; set; } = "";
}

public class Notificacao : Entidade
{
    public Guid UsuarioId { get; set; }
    public string Titulo { get; set; } = "";
    public string Texto { get; set; } = "";
    public string? Link { get; set; }
    public bool Lida { get; set; }
}

/// <summary>Inscrição Web Push de um aparelho do usuário.</summary>
public class PushInscricao : Entidade
{
    public Guid UsuarioId { get; set; }
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
}
