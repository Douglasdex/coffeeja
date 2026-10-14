import { api, type FornecedorAdmin } from "@coffeeja/api-client";
import { Badge, Button, Card, CardContent, Chip, EmptyState, PageTitle, Stars, StatCard } from "@coffeeja/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Award, Check, ClipboardList, FileText, ShieldCheck, Users, Utensils, Wallet } from "lucide-react";
import { useState } from "react";
import { Carregando, ErroBox } from "../components/common";
import { haQuanto, moeda } from "../lib/format";
import { useToast } from "../lib/toast";

type Filtro = "pendentes" | "ativos" | "todos";
const filtros: { id: Filtro; label: string; valor?: boolean }[] = [
  { id: "pendentes", label: "Pendentes", valor: false },
  { id: "ativos", label: "Ativos", valor: true },
  { id: "todos", label: "Todos" },
];

export function AdminPainel() {
  const [filtro, setFiltro] = useState<Filtro>("pendentes");
  const resumo = useQuery({ queryKey: ["admin-resumo"], queryFn: api.admin.resumo });
  const lista = useQuery({ queryKey: ["admin-fornecedores", filtro], queryFn: () => api.admin.fornecedores(filtros.find((f) => f.id === filtro)!.valor) });
  const r = resumo.data;

  return (
    <div className="space-y-6">
      <PageTitle title="Painel administrativo" subtitle="Aprove buffets e acompanhe a plataforma." />
      {r && (
        <div className="no-scrollbar -mx-4 flex gap-3 overflow-x-auto px-4 pb-1 sm:mx-0 sm:grid sm:grid-cols-2 sm:overflow-visible sm:px-0 lg:grid-cols-3">
          <StatCard label="Aguardando aprovação" value={r.fornecedoresPendentes} ring={r.fornecedoresPendentes ? 1 : 0} icon={<ShieldCheck className="h-5 w-5" />} />
          <StatCard label="Buffets ativos" value={r.fornecedoresAtivos} ring={Math.min(1, r.fornecedoresAtivos / 10)} icon={<Utensils className="h-5 w-5" />} color="var(--accent)" />
          <StatCard label="Contratantes" value={r.contratantes} ring={Math.min(1, r.contratantes / 50)} icon={<Users className="h-5 w-5" />} color="var(--lavender)" />
          <StatCard label="Demandas abertas" value={r.demandasAbertas} ring={Math.min(1, r.demandasAbertas / 10)} icon={<ClipboardList className="h-5 w-5" />} />
          <StatCard label="Pedidos" value={r.pedidos} ring={Math.min(1, r.pedidos / 50)} icon={<FileText className="h-5 w-5" />} color="var(--accent)" />
          <StatCard label="Volume contratado" value={moeda(r.volume)} ring={r.volume > 0 ? 1 : 0} icon={<Wallet className="h-5 w-5" />} color="var(--success)" />
        </div>
      )}

      <div className="flex gap-2" role="tablist">
        {filtros.map((f) => <Chip key={f.id} active={filtro === f.id} onClick={() => setFiltro(f.id)}>{f.label}</Chip>)}
      </div>

      {lista.isLoading ? <Carregando /> : lista.error ? <ErroBox erro={lista.error} /> : lista.data?.length ? (
        <div className="grid gap-3 md:grid-cols-2">{lista.data.map((f) => <FornecedorLinha key={f.id} f={f} />)}</div>
      ) : <EmptyState icon={<Check className="h-6 w-6" />} title="Nenhum buffet nesta lista" />}
    </div>
  );
}

function FornecedorLinha({ f }: { f: FornecedorAdmin }) {
  const qc = useQueryClient();
  const toast = useToast();
  const refresh = () => { void qc.invalidateQueries({ queryKey: ["admin-fornecedores"] }); void qc.invalidateQueries({ queryKey: ["admin-resumo"] }); };
  const onError = (e: Error) => toast(e.message, true);
  const aprovar = useMutation({ mutationFn: () => api.admin.aprovar(f.id), onSuccess: () => { toast(`${f.nomeFantasia} aprovado.`); refresh(); }, onError });
  const suspender = useMutation({ mutationFn: () => api.admin.suspender(f.id), onSuccess: () => { toast(`${f.nomeFantasia} suspenso.`); refresh(); }, onError });
  const fundador = useMutation({ mutationFn: () => api.admin.fundador(f.id, !f.fundador), onSuccess: refresh, onError });
  const ocupado = aprovar.isPending || suspender.isPending || fundador.isPending;

  return (
    <Card><CardContent className="space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="truncate font-semibold">{f.nomeFantasia}</div>
          <div className="truncate text-sm text-muted-foreground">{f.cidade}/{f.uf}{f.cnpj ? ` · ${f.cnpj}` : ""}</div>
          <div className="truncate text-sm text-muted-foreground">{f.email}{f.telefone ? ` · ${f.telefone}` : ""}</div>
        </div>
        <div className="flex flex-col items-end gap-1">
          {f.aprovado ? <Badge tone="success">Ativo</Badge> : <Badge tone="warning">Pendente</Badge>}
          {f.fundador && <Badge tone="lavender"><Award className="h-3.5 w-3.5" />Fundador</Badge>}
        </div>
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-3 text-xs text-muted-foreground"><Stars value={f.notaMedia} count={f.totalAvaliacoes} /><span>cadastro {haQuanto(f.criadoEm)}</span></div>
        <div className="flex flex-wrap gap-2">
          {!f.aprovado
            ? <Button size="sm" variant="success" disabled={ocupado} onClick={() => aprovar.mutate()}><Check className="h-4 w-4" />Aprovar</Button>
            : <Button size="sm" variant="destructive" disabled={ocupado} onClick={() => suspender.mutate()}>Suspender</Button>}
          <Button size="sm" variant="outline" disabled={ocupado} onClick={() => fundador.mutate()}>{f.fundador ? "Remover selo" : "Selo Fundador"}</Button>
        </div>
      </div>
    </CardContent></Card>
  );
}
