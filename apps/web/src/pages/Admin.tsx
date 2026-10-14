import { api, type ContratanteAdmin, type FornecedorAdmin } from "@coffeeja/api-client";
import { Badge, Button, Card, CardContent, Chip, EmptyState, PageTitle, Stars, StatCard, cn } from "@coffeeja/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Award, Building2, Check, ClipboardList, FileText, Gift, ShieldCheck, Trophy, User, Users, Utensils, Wallet } from "lucide-react";
import { useState } from "react";
import { Carregando, ErroBox } from "../components/common";
import { mascaraCnpj, mascaraCpf, mascaraTelefone } from "../lib/docs";
import { haQuanto, moeda } from "../lib/format";
import { useToast } from "../lib/toast";

type Aba = "buffets" | "contratantes" | "convites";

export function AdminPainel() {
  const [aba, setAba] = useState<Aba>("buffets");
  const resumo = useQuery({ queryKey: ["admin-resumo"], queryFn: api.admin.resumo });
  const r = resumo.data;

  return (
    <div className="space-y-6">
      <PageTitle title="Painel administrativo" subtitle="Gerencie a plataforma CoffeeJá." />
      {r && (
        <div className="no-scrollbar -mx-4 flex gap-3 overflow-x-auto px-4 pb-1 sm:mx-0 sm:grid sm:grid-cols-2 sm:overflow-visible sm:px-0 lg:grid-cols-4">
          <StatCard label="Aguardando aprovação" value={r.fornecedoresPendentes} ring={r.fornecedoresPendentes ? 1 : 0} icon={<ShieldCheck className="h-5 w-5" />} />
          <StatCard label="Buffets ativos" value={r.fornecedoresAtivos} ring={Math.min(1, r.fornecedoresAtivos / 10)} icon={<Utensils className="h-5 w-5" />} color="var(--accent)" />
          <StatCard label="Contas contratantes" value={r.contratantes} ring={Math.min(1, r.contratantes / 50)} icon={<Users className="h-5 w-5" />} color="var(--lavender)" />
          <StatCard label="Demandas abertas" value={r.demandasAbertas} ring={Math.min(1, r.demandasAbertas / 10)} icon={<ClipboardList className="h-5 w-5" />} />
          <StatCard label="Pedidos" value={r.pedidos} ring={Math.min(1, r.pedidos / 50)} icon={<FileText className="h-5 w-5" />} color="var(--accent)" />
          <StatCard label="Volume contratado" value={moeda(r.volume)} ring={r.volume > 0 ? 1 : 0} icon={<Wallet className="h-5 w-5" />} color="var(--success)" />
          <StatCard label="Convites enviados" value={r.convitesEnviados} ring={Math.min(1, r.convitesEnviados / 30)} icon={<Gift className="h-5 w-5" />} color="var(--lavender)" />
          <StatCard label="Cadastros por convite" value={r.cadastrosPorConvite} ring={Math.min(1, r.cadastrosPorConvite / 20)} icon={<Check className="h-5 w-5" />} color="var(--success)" />
        </div>
      )}

      <div className="flex gap-2 overflow-x-auto no-scrollbar" role="tablist">
        <Chip active={aba === "buffets"} onClick={() => setAba("buffets")}>Buffets</Chip>
        <Chip active={aba === "contratantes"} onClick={() => setAba("contratantes")}>Contratantes</Chip>
        <Chip active={aba === "convites"} onClick={() => setAba("convites")}>Ranking de convites</Chip>
      </div>

      {aba === "buffets" && <Buffets />}
      {aba === "contratantes" && <Contratantes />}
      {aba === "convites" && <Ranking />}
    </div>
  );
}

/* ------------------------------------------------------------------ Buffets */
function Buffets() {
  const [filtro, setFiltro] = useState<"pendentes" | "ativos" | "todos">("pendentes");
  const valor = filtro === "pendentes" ? false : filtro === "ativos" ? true : undefined;
  const lista = useQuery({ queryKey: ["admin-fornecedores", filtro], queryFn: () => api.admin.fornecedores(valor) });
  return (
    <div className="space-y-4">
      <div className="flex gap-2">{(["pendentes", "ativos", "todos"] as const).map((f) => <Chip key={f} active={filtro === f} onClick={() => setFiltro(f)}>{f[0].toUpperCase() + f.slice(1)}</Chip>)}</div>
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
          <div className="truncate text-sm text-muted-foreground">{f.cidade}/{f.uf}{f.cnpj ? ` · ${mascaraCnpj(f.cnpj)}` : ""}</div>
          <div className="truncate text-sm text-muted-foreground">{f.email}{f.telefone ? ` · ${mascaraTelefone(f.telefone)}` : ""}</div>
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

/* ------------------------------------------------------------ Contratantes */
function Contratantes() {
  const { data, isLoading, error } = useQuery({ queryKey: ["admin-contratantes"], queryFn: api.admin.contratantes });
  if (isLoading) return <Carregando />;
  if (error) return <ErroBox erro={error} />;
  return data?.length
    ? <div className="grid gap-3 md:grid-cols-2">{data.map((c) => <ContratanteLinha key={c.id} c={c} />)}</div>
    : <EmptyState icon={<Building2 className="h-6 w-6" />} title="Nenhum contratante cadastrado" />;
}

function ContratanteLinha({ c }: { c: ContratanteAdmin }) {
  const qc = useQueryClient();
  const toast = useToast();
  const alternar = useMutation({
    mutationFn: () => (c.ativo ? api.admin.suspenderContratante(c.id) : api.admin.ativarContratante(c.id)),
    onSuccess: () => { toast(c.ativo ? "Conta suspensa." : "Conta reativada."); void qc.invalidateQueries({ queryKey: ["admin-contratantes"] }); },
    onError: (e) => toast(e.message, true),
  });
  const pj = c.tipo === "PessoaJuridica";
  const doc = c.documento ? (pj ? mascaraCnpj(c.documento) : mascaraCpf(c.documento)) : "";
  return (
    <Card className={cn(!c.ativo && "opacity-70")}><CardContent className="space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2 font-semibold">{pj ? <Building2 className="h-4 w-4 shrink-0 text-muted-foreground" /> : <User className="h-4 w-4 shrink-0 text-muted-foreground" />}<span className="truncate">{c.nome}</span></div>
          <div className="truncate text-sm text-muted-foreground">{doc}{c.cidade ? ` · ${c.cidade}/${c.uf}` : ""}</div>
          <div className="truncate text-sm text-muted-foreground">{c.responsavelNome}{c.responsavelEmail ? ` · ${c.responsavelEmail}` : ""}</div>
        </div>
        {c.ativo ? <Badge tone="success">Ativa</Badge> : <Badge tone="danger">Suspensa</Badge>}
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap gap-2 text-xs"><Badge>{c.usuarios} {c.usuarios === 1 ? "usuário" : "usuários"}</Badge><Badge tone="accent">{c.demandas} demandas</Badge><Badge tone="lavender">{c.pedidos} pedidos</Badge></div>
        <Button size="sm" variant={c.ativo ? "destructive" : "success"} disabled={alternar.isPending} onClick={() => alternar.mutate()}>{c.ativo ? "Suspender" : "Reativar"}</Button>
      </div>
    </CardContent></Card>
  );
}

/* ------------------------------------------------------- Ranking de convites */
function Ranking() {
  const { data, isLoading, error } = useQuery({ queryKey: ["admin-ranking"], queryFn: api.admin.ranking });
  if (isLoading) return <Carregando />;
  if (error) return <ErroBox erro={error} />;
  if (!data?.length) return <EmptyState icon={<Gift className="h-6 w-6" />} title="Ainda não há convites">Quando os usuários convidarem outras pessoas, o ranking aparece aqui.</EmptyState>;
  const medalha = ["text-warning", "text-muted-foreground", "text-primary"];
  return (
    <div className="space-y-2">
      <p className="text-sm text-muted-foreground">Ordenado por cadastros concluídos e, em seguida, por convites enviados.</p>
      {data.map((x, i) => (
        <Card key={x.usuarioId} className="flex items-center gap-3 p-4">
          <div className={cn("grid h-10 w-10 shrink-0 place-items-center rounded-full bg-muted font-bold", medalha[i])}>{i < 3 ? <Trophy className="h-5 w-5" /> : i + 1}</div>
          <div className="min-w-0 flex-1">
            <div className="truncate font-semibold">{x.nome}</div>
            <div className="truncate text-sm text-muted-foreground">{x.empresa ? `${x.empresa} · ` : ""}{x.perfil}</div>
          </div>
          <div className="text-right text-sm">
            <div className="font-bold text-success">{x.cadastrados} {x.cadastrados === 1 ? "cadastro" : "cadastros"}</div>
            <div className="text-muted-foreground">{x.enviados} {x.enviados === 1 ? "convite" : "convites"}</div>
          </div>
        </Card>
      ))}
    </div>
  );
}
