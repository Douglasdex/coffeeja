import { api } from "@coffeeja/api-client";
import { cn } from "@coffeeja/ui";
import { useQuery } from "@tanstack/react-query";
import { Bell, ClipboardList, Coffee, FileText, Home, Inbox, LogOut, Moon, Plus, Send, ShieldCheck, Sun, User, Utensils, type LucideIcon } from "lucide-react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../lib/auth";
import { useTema } from "../lib/theme";

interface Item { to: string; label: string; icon: LucideIcon; end?: boolean; destaque?: boolean }

const contratante: Item[] = [
  { to: "/contratante", label: "Início", icon: Home, end: true },
  { to: "/contratante/demandas", label: "Demandas", icon: ClipboardList },
  { to: "/contratante/nova", label: "Nova", icon: Plus, destaque: true },
  { to: "/contratante/pedidos", label: "Pedidos", icon: FileText },
  { to: "/contratante/conta", label: "Conta", icon: User },
];
const admin: Item[] = [
  { to: "/admin", label: "Painel", icon: ShieldCheck, end: true },
]
const fornecedor: Item[] = [
  { to: "/fornecedor", label: "Início", icon: Home, end: true },
  { to: "/fornecedor/demandas", label: "Demandas", icon: Inbox },
  { to: "/fornecedor/propostas", label: "Propostas", icon: Send },
  { to: "/fornecedor/pedidos", label: "Pedidos", icon: FileText },
  { to: "/fornecedor/perfil", label: "Perfil", icon: Utensils },
];

export function Logo({ className }: { className?: string }) {
  return (
    <span className={cn("inline-flex items-center gap-2 text-lg font-bold tracking-tight", className)}>
      <span className="grid h-9 w-9 place-items-center rounded-xl bg-primary text-primary-foreground"><Coffee className="h-5 w-5" /></span>
      <span>Coffee<span className="text-primary">Já</span></span>
    </span>
  );
}

export function AppShell() {
  const { usuario, sair } = useAuth();
  const { escuro, alternar } = useTema();
  const nav = useNavigate();
  const itens = usuario?.papel === "Fornecedor" ? fornecedor : usuario?.papel === "Admin" ? admin : contratante;

  const { data: notifs } = useQuery({ queryKey: ["notificacoes"], queryFn: api.notificacoes.listar, refetchInterval: 60_000 });
  const naoLidas = notifs?.filter((n) => !n.lida).length ?? 0;

  const Acoes = (
    <div className="flex items-center gap-2">
      <button onClick={() => nav("/notificacoes")} aria-label={`Notificações${naoLidas ? `, ${naoLidas} novas` : ""}`}
        className="relative grid h-11 w-11 place-items-center rounded-xl bg-accent-soft text-accent">
        <Bell className="h-5 w-5" />
        {naoLidas > 0 && <span className="absolute -right-1 -top-1 grid h-5 min-w-5 place-items-center rounded-full bg-primary px-1 text-[11px] font-bold text-primary-foreground">{naoLidas > 9 ? "9+" : naoLidas}</span>}
      </button>
      <button onClick={alternar} aria-label={escuro ? "Tema claro" : "Tema escuro"} className="grid h-11 w-11 place-items-center rounded-xl bg-lavender-soft text-lavender">
        {escuro ? <Sun className="h-5 w-5" /> : <Moon className="h-5 w-5" />}
      </button>
    </div>
  );

  return (
    <div className="min-h-dvh lg:grid lg:grid-cols-[17rem_1fr]">
      {/* Menu lateral (desktop) — mesma disposição da referência */}
      <aside className="sticky top-0 hidden h-dvh flex-col gap-6 overflow-y-auto border-r border-border/60 bg-card p-5 lg:flex">
        <Logo />
        <nav className="flex flex-col gap-1" aria-label="Principal">
          {itens.map(({ to, label, icon: Icon, end }) => (
            <NavLink key={to} to={to} end={end}
              className={({ isActive }) => cn("flex h-11 items-center gap-3 rounded-full px-4 text-sm font-medium transition", isActive ? "bg-accent-soft text-accent" : "text-muted-foreground hover:bg-muted hover:text-foreground")}>
              <Icon className="h-5 w-5" />{label === "Nova" ? "Nova demanda" : label}
            </NavLink>
          ))}
        </nav>
        <button onClick={sair} className="mt-auto flex h-11 items-center gap-3 rounded-full px-4 text-sm font-medium text-muted-foreground hover:bg-muted">
          <LogOut className="h-5 w-5" />Sair
        </button>
      </aside>

      <div className="flex min-w-0 flex-col">
        <header className="sticky top-0 z-30 flex items-center justify-between gap-3 border-b border-border/60 bg-background/90 px-4 py-3 backdrop-blur safe-top sm:px-6">
          <Logo className="lg:hidden" />
          <div className="hidden text-sm text-muted-foreground lg:block">Olá, <span className="font-semibold text-foreground">{usuario?.nome.split(" ")[0]}</span>{usuario?.empresa ? ` · ${usuario.empresa}` : ""}</div>
          {Acoes}
        </header>

        <main className="mx-auto w-full max-w-5xl flex-1 px-4 pb-28 pt-5 sm:px-6 lg:pb-10">
          <Outlet />
        </main>

        {/* Barra inferior (celular) */}
        <nav aria-label="Principal" className="fixed inset-x-0 bottom-0 z-30 border-t border-border/60 bg-card/95 backdrop-blur safe-bottom lg:hidden">
          <ul className="mx-auto flex max-w-md items-end justify-around px-2 pt-1.5">
            {itens.map(({ to, label, icon: Icon, end, destaque }) => (
              <li key={to} className="flex-1">
                <NavLink to={to} end={end} className={({ isActive }) => cn("flex flex-col items-center gap-0.5 py-1.5 text-[11px] font-medium", isActive ? "text-primary" : "text-muted-foreground")}>
                  {destaque
                    ? <span className="-mt-6 grid h-14 w-14 place-items-center rounded-full bg-primary text-primary-foreground shadow-lg ring-4 ring-background"><Icon className="h-6 w-6" /></span>
                    : <Icon className="h-5 w-5" />}
                  {label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      </div>
    </div>
  );
}
