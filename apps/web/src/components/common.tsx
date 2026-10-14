import { Restricao, descartavelLabels, descartaveisDe, restricaoLabels, restricoesDe, type DemandaResumo } from "@coffeeja/api-client";
import { Badge, Card, Spinner } from "@coffeeja/ui";
import { CalendarDays, ChevronRight, MapPin, MessageCircle, Users, UtensilsCrossed } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { dataLonga, hora, moeda } from "../lib/format";
import { StatusDemandaBadge, labelEstilo } from "../lib/labels";

export const opcoesRestricao = Object.values(Restricao);
export { restricaoLabels };

export function Carregando() {
  return <div className="grid place-items-center py-16"><Spinner className="h-7 w-7" /></div>;
}

export function ErroBox({ erro }: { erro: unknown }) {
  return <div role="alert" className="rounded-xl bg-destructive-soft p-4 text-sm font-medium text-destructive">{erro instanceof Error ? erro.message : "Erro ao carregar."}</div>;
}

export function Restricoes({ mask }: { mask: number }) {
  const l = restricoesDe(mask);
  if (!l.length) return null;
  return <>{l.map((b) => <Badge key={b} tone="success">{restricaoLabels[b]}</Badge>)}</>;
}

export function Descartaveis({ mask }: { mask: number }) {
  const l = descartaveisDe(mask);
  if (!l.length) return null;
  return <Badge tone="accent"><UtensilsCrossed className="h-3.5 w-3.5" />Descartáveis: {l.map((b) => descartavelLabels[b].toLowerCase()).join(", ")}</Badge>;
}

/** Sinalização de conversa: "N novas" quando há mensagens não lidas; "Responder" quando a última mensagem é da outra parte. */
export function ChatSinal({ naoLidas, aguardando }: { naoLidas: number; aguardando?: boolean }) {
  if (naoLidas > 0) return <Badge tone="primary" className="animate-pulse"><MessageCircle className="h-3.5 w-3.5" />{naoLidas} {naoLidas === 1 ? "mensagem nova" : "mensagens novas"}</Badge>;
  if (aguardando) return <Badge tone="warning"><MessageCircle className="h-3.5 w-3.5" />Aguardando sua resposta</Badge>;
  return null;
}

export function Secao({ titulo, acao, children }: { titulo: string; acao?: ReactNode; children: ReactNode }) {
  return (
    <section className="space-y-3">
      <div className="flex items-center justify-between"><h2 className="text-base font-semibold">{titulo}</h2>{acao}</div>
      {children}
    </section>
  );
}

export function DemandaCard({ d, to, fornecedor }: { d: DemandaResumo; to: string; fornecedor?: boolean }) {
  return (
    <Link to={to} className="block">
      <Card className="p-4 transition hover:-translate-y-0.5">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0 space-y-1">
            <div className="truncate font-semibold">{d.titulo}</div>
            <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted-foreground">
              <span className="inline-flex items-center gap-1"><CalendarDays className="h-4 w-4" />{dataLonga(d.dataEvento)} · {hora(d.horaInicio)}</span>
              <span className="inline-flex items-center gap-1"><Users className="h-4 w-4" />{d.numeroPessoas}</span>
              <span className="inline-flex items-center gap-1"><MapPin className="h-4 w-4" />{d.cidade}{d.distanciaKm !== null && ` · ${d.distanciaKm} km`}</span>
            </div>
          </div>
          <ChevronRight className="mt-1 h-5 w-5 shrink-0 text-muted-foreground" />
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          {fornecedor
            ? <>
                {d.minhaPropostaId ? <Badge tone="success">Proposta enviada</Badge> : <Badge tone="primary">Responder</Badge>}
                <ChatSinal naoLidas={d.mensagensNaoLidas} />
                {d.empresa && <Badge>{d.empresa}</Badge>}
                {d.atendeRestricoes === false && <Badge tone="warning">Restrições fora do seu perfil</Badge>}
              </>
            : <>
                <StatusDemandaBadge s={d.status} />
                <ChatSinal naoLidas={d.mensagensNaoLidas} />
                <Badge tone="accent">{d.totalPropostas} {d.totalPropostas === 1 ? "proposta" : "propostas"}</Badge>
                {d.menorValor !== null && <Badge>a partir de {moeda(d.menorValor)}</Badge>}
              </>}
          <Badge tone="lavender">{labelEstilo[d.estilo]}</Badge>
          <Restricoes mask={d.restricoes} />
          <Descartaveis mask={d.descartaveis} />
        </div>
      </Card>
    </Link>
  );
}
