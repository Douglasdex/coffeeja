// Cliente tipado da API CoffeeJá. Os tipos espelham os DTOs de apps/api (OpenAPI em /openapi/v1.json no ambiente de desenvolvimento).

export type Papel = "Contratante" | "Fornecedor" | "Admin";
export type Estilo = "Economico" | "Padrao" | "Premium";
export type StatusDemanda = "Aberta" | "EmNegociacao" | "Contratada" | "Concluida" | "Cancelada" | "Expirada";
export type StatusProposta = "Enviada" | "EmAjuste" | "Aceita" | "Recusada" | "Expirada" | "Retirada";
export type StatusPedido = "Confirmado" | "EmPreparo" | "SaiuParaEntrega" | "Entregue" | "Concluido" | "Cancelado";
export type StatusPagamento = "Pendente" | "Retido" | "Liberado" | "Reembolsado" | "PagoForaDaPlataforma";

/** Restrições alimentares como bitmask (igual ao enum [Flags] do backend). */
export const Restricao = {
  Vegano: 1,
  Vegetariano: 2,
  SemGluten: 4,
  SemLactose: 8,
  SemAcucar: 16,
  SemOleaginosas: 32,
} as const;
export const restricaoLabels: Record<number, string> = {
  1: "Vegano",
  2: "Vegetariano",
  4: "Sem glúten",
  8: "Sem lactose",
  16: "Sem açúcar",
  32: "Sem oleaginosas",
};
/** Itens descartáveis (bitmask, igual ao enum [Flags] do backend). */
export const Descartavel = { Pratos: 1, Copos: 2, Talheres: 4, Guardanapos: 8 } as const;
export const descartavelLabels: Record<number, string> = { 1: "Pratos", 2: "Copos", 4: "Talheres", 8: "Guardanapos" };
export const descartaveisDe = (mask: number) => Object.keys(descartavelLabels).map(Number).filter((b) => (mask & b) === b);
export const restricoesDe = (mask: number) => Object.keys(restricaoLabels).map(Number).filter((b) => (mask & b) === b);

export type Funcao = "AdminConta" | "Membro";
export type TipoConta = "PessoaJuridica" | "PessoaFisica";
export type StatusUsuario = "Ativo" | "PendenteAprovacao" | "AguardandoAtivacao" | "Recusado" | "Inativo";

export interface Usuario {
  id: string; nome: string; email: string | null; papel: Papel; funcao: Funcao; empresa: string | null; aprovado: boolean | null;
  tipoConta: TipoConta | null; setor: string | null; totpAtivo: boolean; mfaPendente: boolean;
}

export interface Endereco { cep: string; logradouro: string; numero: string; complemento?: string | null; bairro: string; cidade: string; uf: string }
export interface Responsavel { nome: string; cpf: string; telefoneComercial: string; whatsapp: string; email: string; senha: string }
export interface CadastroPj { razaoSocial: string; nomeFantasia: string; cnpj: string; endereco: Endereco; responsavel: Responsavel; conviteCodigo?: string | null }
export interface CadastroPf {
  nome: string; cpf: string; whatsapp: string; email: string; senha: string; endereco: Endereco;
  notaFiscalMesmoEndereco: boolean; enderecoNotaFiscal: Endereco | null; conviteCodigo?: string | null;
}

export interface AcaoLink { id: string; link: string; mensagem: string; whatsappUrl: string }
export interface Funcionario {
  id: string; nome: string; cpf: string; setor: string | null; telefone: string | null; whatsapp: string | null; superiorNome: string | null;
  superiorWhatsapp: string | null; status: StatusUsuario; criadoEm: string; aprovadoEm: string | null; motivoRecusa: string | null;
}
export interface NovoFuncionario { nome: string; cpf: string; setor: string; telefoneComercial: string; whatsapp: string; superiorNome: string; superiorWhatsapp: string }
export interface Acesso { id: string; nome: string; cpf: string; cargo: string | null; telefone: string | null; funcao: Funcao; status: StatusUsuario; criadoEm: string }
export interface AprovacaoInfo { funcionario: string; cpfMascarado: string; setor: string | null; empresa: string; superiorNome: string; solicitadoPor: string; criadoEm: string }
export interface AtivacaoInfo { nome: string; empresa: string; login: string }
export interface ConviteEnviado { id: string; telefone: string | null; nome: string | null; status: "Enviado" | "Cadastrado"; criadoEm: string; cadastradoEm: string | null }
export interface ConviteResultado { link: string; mensagem: string; whatsappUrl: string; conviteId: string | null }
export interface MeusConvites { linkPessoal: string; mensagemPessoal: string; enviados: number; cadastrados: number; convites: ConviteEnviado[] }
export interface ContratanteAdmin {
  id: string; tipo: TipoConta; nome: string; documento: string | null; cidade: string; uf: string; usuarios: number; demandas: number; pedidos: number;
  ativo: boolean; responsavelNome: string | null; responsavelEmail: string | null; responsavelWhatsapp: string | null; criadoEm: string;
}
export interface RankingConvite { usuarioId: string; nome: string; empresa: string | null; perfil: string; enviados: number; cadastrados: number; ultimoConviteEm: string | null }
export interface Quem { nome: string; setor: string | null; em: string }
export interface Sessao { token: string; usuario: Usuario }

export interface DemandaResumo {
  id: string; titulo: string; dataEvento: string; horaInicio: string; cidade: string; uf: string; numeroPessoas: number;
  restricoes: number; estilo: Estilo; status: StatusDemanda; totalPropostas: number; menorValor: number | null; criadoEm: string;
  distanciaKm: number | null; minhaPropostaId: string | null; atendeRestricoes: boolean | null; empresa: string | null;
  descartaveis: number; mensagensNaoLidas: number;
}
export interface DemandaDetalhe {
  id: string; titulo: string; dataEvento: string; horaInicio: string; endereco: string | null; cidade: string; uf: string;
  latitude: number; longitude: number; numeroPessoas: number; restricoes: number; estilo: Estilo; observacoes: string | null;
  prazoPropostas: string | null; status: StatusDemanda; empresa: string; criadoEm: string; pedidoId: string | null; descartaveis: number;
  solicitadoPor: string | null; solicitanteSetor: string | null;
}
export interface NovaDemanda {
  titulo: string; dataEvento: string; horaInicio: string; endereco: string; cidade: string; uf: string; latitude: number; longitude: number;
  numeroPessoas: number; restricoes: number; estilo: Estilo; observacoes?: string; prazoPropostas?: string | null; descartaveis: number;
}

export interface Proposta {
  id: string; demandaId: string; fornecedorId: string; fornecedor: string; notaMedia: number; totalAvaliacoes: number; fundador: boolean;
  valorTotal: number; valorPorPessoa: number; cardapio: string; observacao: string | null; versao: number; status: StatusProposta;
  ajusteSolicitado: string | null; score: number; melhorCustoBeneficio: boolean; criadoEm: string;
  mensagensNaoLidas: number; aguardandoMinhaResposta: boolean;
}
export interface MinhaProposta {
  id: string; demandaId: string; titulo: string; dataEvento: string; cidade: string; numeroPessoas: number; valorTotal: number;
  versao: number; status: StatusProposta; ajusteSolicitado: string | null; statusDemanda: StatusDemanda; pedidoId: string | null; criadoEm: string;
  mensagensNaoLidas: number; aguardandoMinhaResposta: boolean;
}
export interface Mensagem { id: string; remetenteId: string; minha: boolean; texto: string; criadoEm: string }

export interface PedidoResumo {
  id: string; demandaId: string; titulo: string; dataEvento: string; horaInicio: string; cidade: string; numeroPessoas: number; valor: number;
  status: StatusPedido; statusPagamento: StatusPagamento; temNotaFiscal: boolean; contraparte: string; criadoEm: string;
}
export interface PedidoDetalhe {
  id: string; demandaId: string; propostaId: string; titulo: string; dataEvento: string; horaInicio: string; endereco: string; cidade: string;
  numeroPessoas: number; cardapio: string; valor: number; status: StatusPedido; statusPagamento: StatusPagamento; provedorPagamento: string;
  notaFiscalNumero: string | null; notaFiscalNomeOriginal: string | null; notaFiscalEnviadaEm: string | null;
  contraparte: { nome: string; telefone: string | null; email: string | null }; avaliado: boolean;
  eventos: { descricao: string; em: string }[]; proximosStatus: StatusPedido[]; solicitadoPor: Quem | null; contratadoPor: Quem | null;
}

export interface PerfilFornecedor {
  id: string; nomeFantasia: string; cnpj: string | null; descricao: string | null; cidade: string; uf: string; latitude: number; longitude: number;
  raioAtendimentoKm: number; atendeRestricoes: number; aprovado: boolean; fundador: boolean; notaMedia: number; totalAvaliacoes: number;
}
export interface Cardapio { id: string; nome: string; estilo: Estilo; itens: string; valorPorPessoa: number; minimoPessoas: number }
export interface Notificacao { id: string; titulo: string; texto: string; link: string | null; lida: boolean; criadoEm: string }
export interface Coordenadas { latitude: number; longitude: number; cidade: string | null; uf: string | null }

export interface DashboardContratante { mensagensNaoLidas: number; demandasAbertas: number; propostasRecebidas: number; pedidos: number; gastoTotal: number; pedidosSemNota: number }
export interface DashboardFornecedor {
  mensagensNaoLidas: number; propostasEnviadas: number; propostasAceitas: number; propostasEmAjuste: number; taxaConversao: number; faturamento: number;
  notaMedia: number; totalAvaliacoes: number; pedidosSemNota: number; aprovado: boolean;
}

export interface FornecedorAdmin {
  id: string; nomeFantasia: string; cnpj: string | null; cidade: string; uf: string; email: string; telefone: string | null;
  aprovado: boolean; fundador: boolean; notaMedia: number; totalAvaliacoes: number; criadoEm: string;
}
export interface ResumoAdmin { convitesEnviados: number; cadastrosPorConvite: number; fornecedoresPendentes: number; fornecedoresAtivos: number; contratantes: number; demandasAbertas: number; pedidos: number; volume: number }

export interface SolicitacaoExclusao { id: string; nome: string; email: string | null; papel: string; empresa: string | null; solicitadaEm: string; status: StatusUsuario }
export interface LogAuditoria { id: string; em: string; acao: string; usuario: string | null; usuarioId: string | null; alvo: string | null; detalhe: string | null; ip: string | null }
export interface InicioTotp { segredo: string; uri: string }
export interface ConfirmacaoTotp { codigosRecuperacao: string[]; sessao: Sessao }

export class ApiError extends Error {
  constructor(public status: number, message: string, public campos?: Record<string, string[]>, public requer2fa = false) {
    super(message);
  }
}

// A sessão vive em cookie HttpOnly (o JavaScript não consegue ler o token, o que reduz o estrago de uma falha XSS).
// Toda requisição que muda estado leva o cabeçalho abaixo, exigido pela API como proteção CSRF.
const CSRF = { "X-Requested-With": "CoffeeJa" };

let onUnauthorized: (() => void) | undefined;
export const aoExpirarSessao = (fn: () => void) => { onUnauthorized = fn; };

const BASE = (import.meta as unknown as { env?: { VITE_API_URL?: string } }).env?.VITE_API_URL ?? "";

async function req<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {};
  const isForm = typeof FormData !== "undefined" && body instanceof FormData;
  if (body !== undefined && !isForm) headers["Content-Type"] = "application/json";
  Object.assign(headers, CSRF);
  let res: Response;
  try {
    res = await fetch(BASE + path, { method, headers, credentials: "same-origin", body: body === undefined ? undefined : isForm ? (body as FormData) : JSON.stringify(body) });
  } catch {
    throw new ApiError(0, "Sem conexão com o servidor. Verifique sua internet.");
  }
  if (res.status === 401 && !path.startsWith("/api/auth/")) onUnauthorized?.();
  if (!res.ok) {
    let msg = "Algo deu errado. Tente novamente.";
    let campos: Record<string, string[]> | undefined;
    let requer2fa = false;
    let temDetalhe = false;
    try {
      const j = await res.json();
      if (j.errors) { campos = j.errors; msg = Object.values(j.errors as Record<string, string[]>)[0]?.[0] ?? msg; temDetalhe = true; }
      else if (j.detail ?? j.title) { msg = j.detail ?? j.title; temDetalhe = true; }
      requer2fa = j.requer2fa === true;
    } catch { /* corpo vazio */ }
    if (res.status === 429 && !temDetalhe) msg = "Muitas tentativas. Aguarde um instante.";
    throw new ApiError(res.status, msg, campos, requer2fa);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

const get = <T>(p: string) => req<T>("GET", p);
const post = <T = void>(p: string, b?: unknown) => req<T>("POST", p, b ?? {});
const put = <T>(p: string, b: unknown) => req<T>("PUT", p, b);
const del = (p: string) => req<void>("DELETE", p);

export const api = {
  auth: {
    login: (email: string, senha: string, codigo?: string) => post<Sessao>("/api/auth/login", { email, senha, codigo: codigo || null }),
    logout: () => post("/api/auth/logout"),
    registrarContratantePj: (b: CadastroPj) => post<Sessao>("/api/auth/registrar-contratante-pj", b),
    registrarContratantePf: (b: CadastroPf) => post<Sessao>("/api/auth/registrar-contratante-pf", b),
    registrarFornecedor: (b: CadastroPj) => post<Sessao>("/api/auth/registrar-fornecedor", b),
    me: () => get<Usuario>("/api/auth/me"),
  },
  dashboard: {
    contratante: () => get<DashboardContratante>("/api/dashboard"),
    fornecedor: () => get<DashboardFornecedor>("/api/dashboard"),
  },
  geo: { buscar: (q: string) => get<Coordenadas>(`/api/geo/buscar?q=${encodeURIComponent(q)}`) },
  demandas: {
    criar: (d: NovaDemanda) => post<{ id: string; notificados: number }>("/api/demandas", d),
    minhas: () => get<DemandaResumo[]>("/api/demandas"),
    abertas: () => get<DemandaResumo[]>("/api/demandas/abertas"),
    obter: (id: string) => get<DemandaDetalhe>(`/api/demandas/${id}`),
    cancelar: (id: string) => post(`/api/demandas/${id}/cancelar`),
    propostas: (id: string) => get<Proposta[]>(`/api/demandas/${id}/propostas`),
    enviarProposta: (id: string, b: { valorTotal: number; cardapio: string; observacao?: string }) => post<{ id: string }>(`/api/demandas/${id}/propostas`, b),
  },
  propostas: {
    minhas: () => get<MinhaProposta[]>("/api/propostas/minhas"),
    pedirAjuste: (id: string, mensagem: string) => post(`/api/propostas/${id}/ajuste`, { mensagem }),
    revisar: (id: string, b: { valorTotal: number; cardapio: string; observacao?: string }) => post(`/api/propostas/${id}/revisar`, b),
    retirar: (id: string) => post(`/api/propostas/${id}/retirar`),
    aceitar: (id: string) => post<{ pedidoId: string }>(`/api/propostas/${id}/aceitar`),
    mensagens: (id: string) => get<Mensagem[]>(`/api/propostas/${id}/mensagens`),
    enviarMensagem: (id: string, texto: string) => post<Mensagem>(`/api/propostas/${id}/mensagens`, { texto }),
  },
  pedidos: {
    listar: () => get<PedidoResumo[]>("/api/pedidos"),
    obter: (id: string) => get<PedidoDetalhe>(`/api/pedidos/${id}`),
    status: (id: string, status: StatusPedido) => post(`/api/pedidos/${id}/status`, { status }),
    anexarNota: (id: string, arquivo: File, numero?: string) => {
      const f = new FormData();
      f.append("arquivo", arquivo);
      if (numero) f.append("numero", numero);
      return req<void>("POST", `/api/pedidos/${id}/nota-fiscal`, f);
    },
    baixarNota: async (id: string, nome: string) => {
      const res = await fetch(`${BASE}/api/pedidos/${id}/nota-fiscal`, { credentials: "same-origin" });
      if (!res.ok) throw new ApiError(res.status, "Não foi possível baixar a nota fiscal.");
      const url = URL.createObjectURL(await res.blob());
      const a = document.createElement("a");
      a.href = url; a.download = nome; a.click();
      setTimeout(() => URL.revokeObjectURL(url), 5000);
    },
    avaliar: (id: string, b: { nota: number; pontual: boolean; comentario?: string }) => post(`/api/pedidos/${id}/avaliacao`, b),
  },
  fornecedor: {
    perfil: () => get<PerfilFornecedor>("/api/fornecedor/perfil"),
    salvarPerfil: (b: Omit<PerfilFornecedor, "id" | "cnpj" | "aprovado" | "fundador" | "notaMedia" | "totalAvaliacoes">) => put<PerfilFornecedor>("/api/fornecedor/perfil", b),
    cardapios: () => get<Cardapio[]>("/api/fornecedor/cardapios"),
    criarCardapio: (b: Omit<Cardapio, "id">) => post<Cardapio>("/api/fornecedor/cardapios", b),
    removerCardapio: (id: string) => del(`/api/fornecedor/cardapios/${id}`),
  },
  equipe: {
    listar: () => get<Funcionario[]>("/api/equipe"),
    criar: (b: NovoFuncionario) => post<AcaoLink>("/api/equipe", b),
    reenviarAprovacao: (id: string) => post<AcaoLink>(`/api/equipe/${id}/reenviar-aprovacao`),
    enviarAcesso: (id: string) => post<AcaoLink>(`/api/equipe/${id}/enviar-acesso`),
    desativar: (id: string) => post(`/api/equipe/${id}/desativar`),
    reativar: (id: string) => post(`/api/equipe/${id}/reativar`),
  },
  acessos: {
    listar: () => get<{ acessos: Acesso[]; aviso: string }>("/api/fornecedor/acessos"),
    criar: (b: { nome: string; cpf: string; cargo: string; telefone: string }) => post<AcaoLink>("/api/fornecedor/acessos", b),
    enviarAcesso: (id: string) => post<AcaoLink>(`/api/fornecedor/acessos/${id}/enviar-acesso`),
    desativar: (id: string) => post(`/api/fornecedor/acessos/${id}/desativar`),
    reativar: (id: string) => post(`/api/fornecedor/acessos/${id}/reativar`),
  },
  publico: {
    aprovacao: (token: string) => get<AprovacaoInfo>(`/api/publico/aprovacao/${token}`),
    decidir: (token: string, aprovar: boolean, motivo?: string) => post<{ aprovado: boolean }>(`/api/publico/aprovacao/${token}`, { aprovar, motivo }),
    ativacao: (token: string) => get<AtivacaoInfo>(`/api/publico/ativacao/${token}`),
    definirSenha: (token: string, senha: string) => post<Sessao>(`/api/publico/ativacao/${token}`, { senha }),
    convite: (codigo: string) => get<{ convidante: string; empresa: string | null }>(`/api/publico/convite/${codigo}`),
  },
  convites: {
    meus: () => get<MeusConvites>("/api/convites"),
    criar: (telefone?: string, nome?: string) => post<ConviteResultado>("/api/convites", { telefone: telefone || null, nome: nome || null }),
  },
  seguranca: {
    trocarSenha: (senhaAtual: string, novaSenha: string) => post<Sessao>("/api/seguranca/senha", { senhaAtual, novaSenha }),
    totpIniciar: () => post<InicioTotp>("/api/seguranca/2fa/iniciar"),
    totpConfirmar: (codigo: string) => post<ConfirmacaoTotp>("/api/seguranca/2fa/confirmar", { codigo }),
    totpDesativar: (senha: string, codigo: string) => post("/api/seguranca/2fa/desativar", { senha, codigo }),
  },
  lgpd: {
    solicitarExclusao: () => post<{ detail: string }>("/api/lgpd/solicitar-exclusao"),
    baixarMeusDados: async () => {
      const res = await fetch(`${BASE}/api/lgpd/meus-dados`, { credentials: "same-origin" });
      if (!res.ok) throw new ApiError(res.status, "Não foi possível gerar seus dados agora.");
      const url = URL.createObjectURL(await res.blob());
      const a = document.createElement("a");
      a.href = url; a.download = "meus-dados-coffeeja.json"; a.click();
      setTimeout(() => URL.revokeObjectURL(url), 5000);
    },
  },
  admin: {
    lgpdSolicitacoes: () => get<SolicitacaoExclusao[]>("/api/admin/lgpd/solicitacoes"),
    anonimizar: (id: string) => post(`/api/admin/usuarios/${id}/anonimizar`),
    auditoria: (acao?: string, pagina = 1) => get<LogAuditoria[]>(`/api/admin/auditoria?pagina=${pagina}${acao ? `&acao=${encodeURIComponent(acao)}` : ""}`),
    contratantes: () => get<ContratanteAdmin[]>("/api/admin/contratantes"),
    suspenderContratante: (id: string) => post(`/api/admin/contratantes/${id}/suspender`),
    ativarContratante: (id: string) => post(`/api/admin/contratantes/${id}/ativar`),
    ranking: () => get<RankingConvite[]>("/api/admin/convites/ranking"),
    resumo: () => get<ResumoAdmin>("/api/admin/resumo"),
    fornecedores: (aprovado?: boolean) => get<FornecedorAdmin[]>(`/api/admin/fornecedores${aprovado === undefined ? "" : `?aprovado=${aprovado}`}`),
    aprovar: (id: string) => post(`/api/admin/fornecedores/${id}/aprovar`),
    suspender: (id: string) => post(`/api/admin/fornecedores/${id}/suspender`),
    fundador: (id: string, fundador: boolean) => post(`/api/admin/fornecedores/${id}/fundador`, { fundador }),
  },
  push: {
    chave: () => get<{ chave: string }>("/api/push/chave"),
    inscrever: (endpoint: string, p256dh: string, auth: string) => post("/api/push/inscrever", { endpoint, keys: { p256dh, auth } }),
    cancelar: (endpoint: string) => post("/api/push/cancelar", { endpoint, keys: { p256dh: "-", auth: "-" } }),
  },
  notificacoes: {
    listar: () => get<Notificacao[]>("/api/notificacoes"),
    lida: (id: string) => post(`/api/notificacoes/${id}/lida`),
    todasLidas: () => post("/api/notificacoes/lidas"),
  },
};
