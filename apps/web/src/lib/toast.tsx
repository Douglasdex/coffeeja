import { CheckCircle2, XCircle } from "lucide-react";
import { createContext, useCallback, useContext, useState, type ReactNode } from "react";

type Toast = { id: number; texto: string; erro?: boolean };
const Ctx = createContext<(texto: string, erro?: boolean) => void>(() => {});
export const useToast = () => useContext(Ctx);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [itens, setItens] = useState<Toast[]>([]);
  const push = useCallback((texto: string, erro?: boolean) => {
    const id = Date.now() + Math.random();
    setItens((l) => [...l, { id, texto, erro }]);
    setTimeout(() => setItens((l) => l.filter((t) => t.id !== id)), 4000);
  }, []);
  return (
    <Ctx.Provider value={push}>
      {children}
      <div className="pointer-events-none fixed inset-x-0 top-0 z-50 flex flex-col items-center gap-2 p-4 safe-top" aria-live="polite">
        {itens.map((t) => (
          <div key={t.id} className="pointer-events-auto flex max-w-md items-center gap-2 rounded-xl border border-border bg-card px-4 py-3 text-sm font-medium shadow-[var(--elevation-card)]">
            {t.erro ? <XCircle className="h-5 w-5 shrink-0 text-destructive" /> : <CheckCircle2 className="h-5 w-5 shrink-0 text-success" />}
            {t.texto}
          </div>
        ))}
      </div>
    </Ctx.Provider>
  );
}
