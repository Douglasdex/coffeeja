import type { Estilo, StatusDemanda, StatusPagamento, StatusPedido, StatusProposta } from "@coffeeja/api-client";
import { Badge, type Tone } from "@coffeeja/ui";

const demanda: Record<StatusDemanda, [string, Tone]> = {
  Aberta: ["Recebendo propostas", "accent"],
  EmNegociacao: ["Em negociação", "lavender"],
  Contratada: ["Contratada", "success"],
  Concluida: ["Concluída", "neutral"],
  Cancelada: ["Cancelada", "danger"],
  Expirada: ["Expirada", "neutral"],
};
const proposta: Record<StatusProposta, [string, Tone]> = {
  Enviada: ["Enviada", "accent"],
  EmAjuste: ["Ajuste solicitado", "warning"],
  Aceita: ["Aceita", "success"],
  Recusada: ["Não selecionada", "neutral"],
  Expirada: ["Expirada", "neutral"],
  Retirada: ["Retirada", "neutral"],
};
const pedido: Record<StatusPedido, [string, Tone]> = {
  Confirmado: ["Confirmado", "accent"],
  EmPreparo: ["Em preparo", "lavender"],
  SaiuParaEntrega: ["A caminho", "warning"],
  Entregue: ["Entregue", "success"],
  Concluido: ["Concluído", "neutral"],
  Cancelado: ["Cancelado", "danger"],
};

export const StatusDemandaBadge = ({ s }: { s: StatusDemanda }) => <Badge tone={demanda[s][1]}>{demanda[s][0]}</Badge>;
export const StatusPropostaBadge = ({ s }: { s: StatusProposta }) => <Badge tone={proposta[s][1]}>{proposta[s][0]}</Badge>;
export const StatusPedidoBadge = ({ s }: { s: StatusPedido }) => <Badge tone={pedido[s][1]}>{pedido[s][0]}</Badge>;

export const labelEstilo: Record<Estilo, string> = { Economico: "Econômico", Padrao: "Padrão", Premium: "Premium" };
export const labelPagamento: Record<StatusPagamento, string> = {
  Pendente: "Pagamento pendente",
  Retido: "Pagamento retido",
  Liberado: "Pagamento liberado",
  Reembolsado: "Reembolsado",
  PagoForaDaPlataforma: "Pagamento combinado direto com o fornecedor",
};
export const labelProximoStatus: Record<StatusPedido, string> = {
  Confirmado: "Confirmar",
  EmPreparo: "Iniciar preparo",
  SaiuParaEntrega: "Saí para entrega",
  Entregue: "Marcar como entregue",
  Concluido: "Confirmar recebimento",
  Cancelado: "Cancelar pedido",
};
