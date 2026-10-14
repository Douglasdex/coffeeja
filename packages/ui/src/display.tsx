import { Star } from "lucide-react";
import type { ReactNode } from "react";
import { Card } from "./card";
import { cn } from "./cn";

const tones = {
  neutral: "bg-muted text-muted-foreground",
  primary: "bg-primary-soft text-primary",
  accent: "bg-accent-soft text-accent",
  lavender: "bg-lavender-soft text-lavender",
  success: "bg-success-soft text-success",
  warning: "bg-warning-soft text-warning",
  danger: "bg-destructive-soft text-destructive",
} as const;
export type Tone = keyof typeof tones;

export function Badge({ tone = "neutral", children, className }: { tone?: Tone; children: ReactNode; className?: string }) {
  return <span className={cn("inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-semibold", tones[tone], className)}>{children}</span>;
}

export function Stars({ value, count }: { value: number; count?: number }) {
  return (
    <span className="inline-flex items-center gap-1 text-sm">
      <Star className="h-4 w-4 fill-warning text-warning" aria-hidden />
      <span className="font-semibold">{count === 0 ? "Novo" : value.toFixed(1)}</span>
      {count !== undefined && count > 0 && <span className="text-muted-foreground">({count})</span>}
    </span>
  );
}

/** Anel de progresso no estilo dos cards de KPI da referência. */
export function Ring({ value, icon, color = "var(--primary)" }: { value: number; icon?: ReactNode; color?: string }) {
  const r = 28;
  const c = 2 * Math.PI * r;
  const v = Math.max(0, Math.min(1, value));
  return (
    <div className="relative h-[4.5rem] w-[4.5rem] shrink-0">
      <svg viewBox="0 0 72 72" className="-rotate-90" aria-hidden>
        <circle cx="36" cy="36" r={r} fill="none" stroke="var(--muted)" strokeWidth="7" />
        <circle cx="36" cy="36" r={r} fill="none" stroke={color} strokeWidth="7" strokeLinecap="round" strokeDasharray={c} strokeDashoffset={c * (1 - v)} />
      </svg>
      <div className="absolute inset-0 grid place-items-center" style={{ color }}>{icon}</div>
    </div>
  );
}

export function StatCard({ label, value, ring, icon, color, className }: { label: string; value: ReactNode; ring?: number; icon?: ReactNode; color?: string; className?: string }) {
  return (
    <Card className={cn("flex min-w-[10.5rem] items-center justify-between gap-3 p-4", className)}>
      <div className="min-w-0">
        <div className="text-sm text-muted-foreground">{label}</div>
        <div className="mt-1 break-words text-xl font-bold tracking-tight sm:text-2xl">{value}</div>
      </div>
      {ring !== undefined && <Ring value={ring} icon={icon} color={color} />}
    </Card>
  );
}

export function EmptyState({ icon, title, children, action }: { icon?: ReactNode; title: string; children?: ReactNode; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-2xl border border-dashed border-border px-6 py-10 text-center">
      {icon && <div className="grid h-12 w-12 place-items-center rounded-full bg-primary-soft text-primary">{icon}</div>}
      <div className="font-semibold">{title}</div>
      {children && <p className="max-w-xs text-sm text-muted-foreground">{children}</p>}
      {action}
    </div>
  );
}

export function Spinner({ className }: { className?: string }) {
  return <span role="status" aria-label="Carregando" className={cn("inline-block h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent", className)} />;
}

export function PageTitle({ title, subtitle, action }: { title: string; subtitle?: string; action?: ReactNode }) {
  return (
    <div className="flex items-end justify-between gap-3">
      <div className="min-w-0">
        <h1 className="truncate text-xl font-bold tracking-tight sm:text-2xl">{title}</h1>
        {subtitle && <p className="text-sm text-muted-foreground">{subtitle}</p>}
      </div>
      {action}
    </div>
  );
}
