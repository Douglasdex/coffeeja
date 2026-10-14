import { api, aoExpirarSessao, type Sessao, type Usuario } from "@coffeeja/api-client";
import { useQueryClient } from "@tanstack/react-query";
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";

interface AuthCtx {
  usuario: Usuario | null;
  carregando: boolean;
  entrar: (s: Sessao) => void;
  sair: () => void;
}
const Ctx = createContext<AuthCtx>(null!);
export const useAuth = () => useContext(Ctx);

export function AuthProvider({ children }: { children: ReactNode }) {
  const qc = useQueryClient();
  const [usuario, setUsuario] = useState<Usuario | null>(null);
  const [carregando, setCarregando] = useState(true);

  const sair = useCallback(() => {
    void api.auth.logout().catch(() => {});
    setUsuario(null);
    qc.clear();
  }, [qc]);

  useEffect(() => {
    aoExpirarSessao(sair);
    // A sessão está num cookie HttpOnly: só o servidor sabe se ela vale.
    api.auth.me().then(setUsuario).catch(() => setUsuario(null)).finally(() => setCarregando(false));
  }, [sair]);

  const entrar = useCallback((s: Sessao) => {
    qc.clear();
    setUsuario(s.usuario);
  }, [qc]);

  const value = useMemo(() => ({ usuario, carregando, entrar, sair }), [usuario, carregando, entrar, sair]);
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export const rotaInicial = (u: Usuario) => (u.papel === "Fornecedor" ? "/fornecedor" : u.papel === "Contratante" ? "/contratante" : "/admin");
