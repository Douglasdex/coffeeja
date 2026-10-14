import { api } from "@coffeeja/api-client";
import { Button, Input, cn } from "@coffeeja/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Send } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { haQuanto } from "../lib/format";
import { useRealtime } from "../lib/realtime";
import { useToast } from "../lib/toast";

export function PropostaChat({ propostaId, fechado }: { propostaId: string; fechado?: boolean }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [texto, setTexto] = useState("");
  const fim = useRef<HTMLDivElement>(null);
  const { entrar, sair } = useRealtime();
  useEffect(() => { entrar(propostaId); return () => sair(propostaId); }, [propostaId, entrar, sair]);
  const { data, dataUpdatedAt } = useQuery({ queryKey: ["mensagens", propostaId], queryFn: () => api.propostas.mensagens(propostaId), refetchInterval: 60_000 });
  const enviar = useMutation({
    mutationFn: () => api.propostas.enviarMensagem(propostaId, texto.trim()),
    onSuccess: () => { setTexto(""); void qc.invalidateQueries({ queryKey: ["mensagens", propostaId] }); },
    onError: (e) => toast(e.message, true),
  });
  useEffect(() => { fim.current?.scrollIntoView({ block: "nearest" }); }, [data?.length]);
  // A leitura é registrada no servidor ao buscar as mensagens; atualiza os contadores exibidos nas demais telas.
  useEffect(() => {
    if (!dataUpdatedAt) return;
    for (const k of ["dash", "propostas", "minhas-propostas", "demandas", "abertas"]) void qc.invalidateQueries({ queryKey: [k] });
  }, [dataUpdatedAt, qc]);

  return (
    <div className="space-y-3">
      <div className="max-h-72 space-y-2 overflow-y-auto rounded-xl bg-muted/60 p-3">
        {data?.length === 0 && <p className="py-4 text-center text-sm text-muted-foreground">Nenhuma mensagem ainda. Tire dúvidas sobre cardápio, entrega e montagem.</p>}
        {data?.map((m) => (
          <div key={m.id} className={cn("flex", m.minha ? "justify-end" : "justify-start")}>
            <div className={cn("max-w-[85%] rounded-2xl px-3.5 py-2 text-sm", m.minha ? "rounded-br-md bg-primary text-primary-foreground" : "rounded-bl-md bg-card")}>
              <p className="whitespace-pre-wrap break-words">{m.texto}</p>
              <span className={cn("mt-0.5 block text-[11px]", m.minha ? "text-primary-foreground/80" : "text-muted-foreground")}>{haQuanto(m.criadoEm)}</span>
            </div>
          </div>
        ))}
        <div ref={fim} />
      </div>
      {fechado ? (
        <p className="text-center text-sm text-muted-foreground">Conversa encerrada.</p>
      ) : (
        <form className="flex gap-2" onSubmit={(e) => { e.preventDefault(); if (texto.trim()) enviar.mutate(); }}>
          <Input value={texto} onChange={(e) => setTexto(e.target.value)} placeholder="Escreva uma mensagem" maxLength={2000} />
          <Button size="icon" disabled={!texto.trim() || enviar.isPending} aria-label="Enviar"><Send className="h-5 w-5" /></Button>
        </form>
      )}
      <p className="text-xs text-muted-foreground">Telefones e e-mails ficam ocultos até o fechamento do pedido.</p>
    </div>
  );
}
