import { sessao } from "@coffeeja/api-client";
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, type ReactNode } from "react";
import { useAuth } from "./auth";

interface Ctx { entrar: (propostaId: string) => void; sair: (propostaId: string) => void }
const RealtimeCtx = createContext<Ctx>({ entrar: () => {}, sair: () => {} });
export const useRealtime = () => useContext(RealtimeCtx);

/** Conexão SignalR única por sessão. Eventos do servidor apenas invalidam o cache — a fonte da verdade continua sendo a API REST. */
export function RealtimeProvider({ children }: { children: ReactNode }) {
  const { usuario } = useAuth();
  const qc = useQueryClient();
  const conn = useRef<HubConnection | null>(null);
  const salas = useRef(new Set<string>());

  const entrarNaSala = useCallback((id: string) => {
    const c = conn.current;
    if (c?.state === HubConnectionState.Connected) c.invoke("EntrarProposta", id).catch(() => {});
  }, []);

  useEffect(() => {
    if (!usuario) return;
    const c = new HubConnectionBuilder()
      .withUrl("/hubs/chat", { accessTokenFactory: () => sessao.token ?? "" })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.None)
      .build();
    conn.current = c;

    c.on("mensagem", (e: { propostaId: string }) => void qc.invalidateQueries({ queryKey: ["mensagens", e.propostaId] }));
    c.on("notificacao", () => void qc.invalidateQueries({ predicate: (q) => q.queryKey[0] !== "mensagens" }));
    c.onreconnected(() => {
      salas.current.forEach(entrarNaSala);
      void qc.invalidateQueries();
    });

    let ativo = true;
    c.start().then(() => { if (ativo) salas.current.forEach(entrarNaSala); }).catch(() => { /* polling de segurança cobre a falha */ });
    return () => { ativo = false; conn.current = null; void c.stop(); };
  }, [usuario, qc, entrarNaSala]);

  const value = useMemo<Ctx>(() => ({
    entrar: (id) => { salas.current.add(id); entrarNaSala(id); },
    sair: (id) => {
      salas.current.delete(id);
      if (conn.current?.state === HubConnectionState.Connected) conn.current.invoke("SairProposta", id).catch(() => {});
    },
  }), [entrarNaSala]);

  return <RealtimeCtx.Provider value={value}>{children}</RealtimeCtx.Provider>;
}
