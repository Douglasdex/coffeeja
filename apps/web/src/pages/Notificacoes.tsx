import { api } from "@coffeeja/api-client";
import { Button, Card, EmptyState, PageTitle, cn } from "@coffeeja/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { Carregando, ErroBox } from "../components/common";
import { haQuanto } from "../lib/format";

export function Notificacoes() {
  const qc = useQueryClient();
  const nav = useNavigate();
  const { data, isLoading, error } = useQuery({ queryKey: ["notificacoes"], queryFn: api.notificacoes.listar });
  const lerTodas = useMutation({ mutationFn: api.notificacoes.todasLidas, onSuccess: () => qc.invalidateQueries({ queryKey: ["notificacoes"] }) });
  const ler = useMutation({ mutationFn: api.notificacoes.lida, onSuccess: () => qc.invalidateQueries({ queryKey: ["notificacoes"] }) });

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <PageTitle title="Notificações" action={data?.some((n) => !n.lida) ? <Button variant="ghost" size="sm" onClick={() => lerTodas.mutate()}>Marcar todas como lidas</Button> : undefined} />
      {isLoading ? <Carregando /> : error ? <ErroBox erro={error} /> : data?.length ? (
        <div className="space-y-2">
          {data.map((n) => (
            <button key={n.id} className="block w-full text-left" onClick={() => { if (!n.lida) ler.mutate(n.id); if (n.link) nav(n.link); }}>
              <Card className={cn("flex items-start gap-3 p-4", !n.lida && "border-primary/40")}>
                <span className={cn("mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full", n.lida ? "bg-transparent" : "bg-primary")} />
                <div className="min-w-0 flex-1"><div className="font-semibold">{n.titulo}</div><div className="text-sm text-muted-foreground">{n.texto}</div></div>
                <span className="shrink-0 text-xs text-muted-foreground">{haQuanto(n.criadoEm)}</span>
              </Card>
            </button>
          ))}
        </div>
      ) : <EmptyState icon={<Bell className="h-6 w-6" />} title="Tudo em dia">Você será avisado de novas propostas, mensagens e atualizações de pedido.</EmptyState>}
    </div>
  );
}
