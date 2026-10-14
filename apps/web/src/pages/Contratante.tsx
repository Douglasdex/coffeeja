import { api, type DemandaResumo, type Estilo, type Proposta } from "@coffeeja/api-client";
import { Badge, Button, Card, CardContent, Chip, EmptyState, Field, Input, PageTitle, Select, Stars, StatCard, Textarea, cn } from "@coffeeja/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, ArrowLeft, ArrowRight, Check, ClipboardList, CreditCard, FileText, Inbox, MessageCircle, Minus, Plus, Sparkles, Trophy, Wallet } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Carregando, DemandaCard, ErroBox, Restricoes, Secao, opcoesRestricao, restricaoLabels } from "../components/common";
import { LocationPicker, localValido, localVazio, type Local } from "../components/LocationPicker";
import { PushToggle } from "../components/PushToggle";
import { PropostaChat } from "../components/PropostaChat";
import { useAuth } from "../lib/auth";
import { dataLonga, hojeIso, hora, moeda } from "../lib/format";
import { StatusDemandaBadge, StatusPropostaBadge, labelEstilo } from "../lib/labels";
import { useToast } from "../lib/toast";

/* ------------------------------------------------------------------ Início */
export function ContratanteInicio() {
  const { usuario } = useAuth();
  const dash = useQuery({ queryKey: ["dash"], queryFn: api.dashboard.contratante });
  const dem = useQuery({ queryKey: ["demandas"], queryFn: api.demandas.minhas });
  const ativas = dem.data?.filter((d) => d.status === "Aberta" || d.status === "EmNegociacao" || d.status === "Contratada").slice(0, 4);
  const d = dash.data;

  return (
    <div className="space-y-6">
      <PageTitle title={`Olá, ${usuario?.nome.split(" ")[0]}`} subtitle="Peça propostas e compare em minutos." />

      <Card className="flex flex-col gap-3 bg-primary p-5 text-primary-foreground sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="text-lg font-bold">Precisa de um coffee break?</div>
          <p className="text-sm text-white/85">Descreva o evento e receba propostas dos buffets da sua região.</p>
        </div>
        <Button asChild variant="outline" size="lg" className="border-0 text-primary"><Link to="/contratante/nova"><Plus className="h-5 w-5" />Nova demanda</Link></Button>
      </Card>

      {d && (
        <div className="no-scrollbar -mx-4 flex gap-3 overflow-x-auto px-4 pb-1 sm:mx-0 sm:grid sm:grid-cols-2 sm:overflow-visible sm:px-0 lg:grid-cols-4">
          <StatCard label="Demandas abertas" value={d.demandasAbertas} ring={Math.min(1, d.demandasAbertas / 5)} icon={<ClipboardList className="h-5 w-5" />} />
          <StatCard label="Propostas p/ avaliar" value={d.propostasRecebidas} ring={Math.min(1, d.propostasRecebidas / 10)} icon={<Inbox className="h-5 w-5" />} color="var(--accent)" />
          <StatCard label="Pedidos" value={d.pedidos} ring={Math.min(1, d.pedidos / 20)} icon={<FileText className="h-5 w-5" />} color="var(--lavender)" />
          <StatCard label="Total contratado" value={moeda(d.gastoTotal)} ring={d.gastoTotal > 0 ? 1 : 0} icon={<Wallet className="h-5 w-5" />} color="var(--success)" />
        </div>
      )}

      {d && d.pedidosSemNota > 0 && (
        <Link to="/contratante/pedidos">
          <div className="flex items-center gap-3 rounded-2xl bg-warning-soft p-4 text-sm font-medium text-warning">
            <AlertTriangle className="h-5 w-5 shrink-0" />
            {d.pedidosSemNota} {d.pedidosSemNota === 1 ? "pedido está" : "pedidos estão"} sem nota fiscal anexada.
          </div>
        </Link>
      )}

      <Secao titulo="Suas demandas" acao={<Link to="/contratante/demandas" className="text-sm font-semibold text-primary">Ver todas</Link>}>
        {dem.isLoading ? <Carregando /> : dem.error ? <ErroBox erro={dem.error} /> : ativas?.length ? (
          <div className="grid gap-3 md:grid-cols-2">{ativas.map((x) => <DemandaCard key={x.id} d={x} to={`/contratante/demandas/${x.id}`} />)}</div>
        ) : (
          <EmptyState icon={<Sparkles className="h-6 w-6" />} title="Nenhuma demanda ainda" action={<Button asChild><Link to="/contratante/nova">Criar a primeira</Link></Button>}>
            Leva menos de 2 minutos e os buffets próximos são avisados na hora.
          </EmptyState>
        )}
      </Secao>
    </div>
  );
}

/* ----------------------------------------------------------- Lista de demandas */
const abas = [
  { id: "abertas", label: "Em aberto", filtro: (d: DemandaResumo) => d.status === "Aberta" || d.status === "EmNegociacao" },
  { id: "contratadas", label: "Contratadas", filtro: (d: DemandaResumo) => d.status === "Contratada" },
  { id: "encerradas", label: "Encerradas", filtro: (d: DemandaResumo) => ["Concluida", "Cancelada", "Expirada"].includes(d.status) },
];

export function ContratanteDemandas() {
  const [aba, setAba] = useState("abertas");
  const { data, isLoading, error } = useQuery({ queryKey: ["demandas"], queryFn: api.demandas.minhas });
  const lista = data?.filter(abas.find((a) => a.id === aba)!.filtro);
  return (
    <div className="space-y-4">
      <PageTitle title="Demandas" action={<Button asChild size="sm"><Link to="/contratante/nova"><Plus className="h-4 w-4" />Nova</Link></Button>} />
      <div className="flex gap-2 overflow-x-auto no-scrollbar" role="tablist">
        {abas.map((a) => <Chip key={a.id} active={aba === a.id} onClick={() => setAba(a.id)}>{a.label}{data ? ` (${data.filter(a.filtro).length})` : ""}</Chip>)}
      </div>
      {isLoading ? <Carregando /> : error ? <ErroBox erro={error} /> : lista?.length ? (
        <div className="grid gap-3 md:grid-cols-2">{lista.map((d) => <DemandaCard key={d.id} d={d} to={`/contratante/demandas/${d.id}`} />)}</div>
      ) : <EmptyState icon={<ClipboardList className="h-6 w-6" />} title="Nada por aqui" />}
    </div>
  );
}

/* -------------------------------------------------------------- Nova demanda */
const presets = [10, 20, 30, 50, 100];
const estilos: { id: Estilo; nome: string; desc: string }[] = [
  { id: "Economico", nome: "Econômico", desc: "Café, sucos e salgados simples" },
  { id: "Padrao", nome: "Padrão", desc: "Variedade de salgados, doces e frutas" },
  { id: "Premium", nome: "Premium", desc: "Itens especiais, montagem elaborada" },
];
const prazos = [
  { v: "2", l: "2 horas" }, { v: "6", l: "6 horas" }, { v: "24", l: "24 horas" }, { v: "", l: "Sem prazo" },
];

export function NovaDemanda() {
  const nav = useNavigate();
  const toast = useToast();
  const qc = useQueryClient();
  const [passo, setPasso] = useState(0);
  const [titulo, setTitulo] = useState("");
  const [data, setData] = useState("");
  const [horaIni, setHoraIni] = useState("08:30");
  const [local, setLocal] = useState<Local>(localVazio);
  const [pessoas, setPessoas] = useState(20);
  const [restr, setRestr] = useState(0);
  const [estilo, setEstilo] = useState<Estilo>("Padrao");
  const [obs, setObs] = useState("");
  const [prazo, setPrazo] = useState("24");

  const validoPasso = [
    titulo.trim().length >= 3 && data >= hojeIso() && !!data && !!horaIni && localValido(local),
    pessoas >= 5 && pessoas <= 5000,
    true,
    true,
  ];

  const criar = useMutation({
    mutationFn: () => api.demandas.criar({
      titulo: titulo.trim(), dataEvento: data, horaInicio: horaIni + ":00", endereco: local.endereco, cidade: local.cidade, uf: local.uf,
      latitude: local.latitude!, longitude: local.longitude!, numeroPessoas: pessoas, restricoes: restr, estilo, observacoes: obs || undefined,
      prazoPropostas: prazo ? new Date(Date.now() + Number(prazo) * 3600_000).toISOString() : null,
    }),
    onSuccess: (r) => {
      void qc.invalidateQueries({ queryKey: ["demandas"] });
      void qc.invalidateQueries({ queryKey: ["dash"] });
      toast(r.notificados > 0 ? `Demanda publicada! ${r.notificados} ${r.notificados === 1 ? "buffet avisado" : "buffets avisados"}.` : "Demanda publicada! Ainda não há buffets na sua região, avisaremos quando houver.");
      nav(`/contratante/demandas/${r.id}`, { replace: true });
    },
    onError: (e) => toast(e.message, true),
  });

  const titulos = ["Quando e onde", "Quantas pessoas", "Preferências", "Revisar e publicar"];
  return (
    <div className="mx-auto max-w-xl space-y-5">
      <div>
        <button onClick={() => (passo ? setPasso(passo - 1) : nav(-1))} className="mb-2 inline-flex items-center gap-1 text-sm text-muted-foreground"><ArrowLeft className="h-4 w-4" />Voltar</button>
        <PageTitle title="Nova demanda" subtitle={`Passo ${passo + 1} de 4 · ${titulos[passo]}`} />
        <div className="mt-3 flex gap-1.5" aria-hidden>{titulos.map((_, i) => <div key={i} className={cn("h-1.5 flex-1 rounded-full", i <= passo ? "bg-primary" : "bg-muted")} />)}</div>
      </div>

      <Card><CardContent className="space-y-5">
        {passo === 0 && <>
          <Field label="Nome do evento"><Input value={titulo} onChange={(e) => setTitulo(e.target.value)} placeholder="Ex.: Treinamento de vendas" maxLength={150} /></Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Data"><Input type="date" min={hojeIso()} value={data} onChange={(e) => setData(e.target.value)} /></Field>
            <Field label="Chegada do coffee"><Input type="time" value={horaIni} onChange={(e) => setHoraIni(e.target.value)} /></Field>
          </div>
          <LocationPicker value={local} onChange={setLocal} />
        </>}

        {passo === 1 && <>
          <div className="flex items-center justify-center gap-4 py-2">
            <Button type="button" variant="outline" size="icon" onClick={() => setPessoas(Math.max(5, pessoas - 5))} aria-label="Menos"><Minus className="h-5 w-5" /></Button>
            <div className="w-28 text-center">
              <input aria-label="Número de pessoas" inputMode="numeric" value={pessoas} onChange={(e) => setPessoas(Number(e.target.value.replace(/\D/g, "")) || 0)} className="w-full bg-transparent text-center text-5xl font-bold outline-none" />
              <div className="text-sm text-muted-foreground">pessoas</div>
            </div>
            <Button type="button" variant="outline" size="icon" onClick={() => setPessoas(pessoas + 5)} aria-label="Mais"><Plus className="h-5 w-5" /></Button>
          </div>
          <div className="flex flex-wrap justify-center gap-2">{presets.map((p) => <Chip key={p} active={pessoas === p} onClick={() => setPessoas(p)}>{p}</Chip>)}</div>
          {!validoPasso[1] && <p className="text-center text-sm text-destructive">Informe entre 5 e 5000 pessoas.</p>}
        </>}

        {passo === 2 && <>
          <div className="space-y-2">
            <div className="text-sm font-medium">Restrições alimentares</div>
            <div className="flex flex-wrap gap-2">
              {opcoesRestricao.map((b) => <Chip key={b} active={(restr & b) === b} onClick={() => setRestr(restr ^ b)}>{restricaoLabels[b]}</Chip>)}
            </div>
            <p className="text-xs text-muted-foreground">Só informe o que houver. Os buffets verão apenas as categorias, nunca nomes.</p>
          </div>
          <div className="space-y-2">
            <div className="text-sm font-medium">Estilo</div>
            <div className="grid gap-2 sm:grid-cols-3">
              {estilos.map((e) => (
                <button key={e.id} type="button" aria-pressed={estilo === e.id} onClick={() => setEstilo(e.id)}
                  className={cn("rounded-xl border p-3 text-left transition", estilo === e.id ? "border-primary bg-primary-soft" : "border-border hover:bg-muted")}>
                  <div className="font-semibold">{e.nome}</div><div className="text-xs text-muted-foreground">{e.desc}</div>
                </button>
              ))}
            </div>
          </div>
        </>}

        {passo === 3 && <>
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
            <dt className="text-muted-foreground">Evento</dt><dd className="font-medium">{titulo}</dd>
            <dt className="text-muted-foreground">Quando</dt><dd className="font-medium">{data && dataLonga(data)} às {horaIni}</dd>
            <dt className="text-muted-foreground">Local</dt><dd className="font-medium">{local.endereco} ({local.cidade}/{local.uf})</dd>
            <dt className="text-muted-foreground">Pessoas</dt><dd className="font-medium">{pessoas}</dd>
            <dt className="text-muted-foreground">Estilo</dt><dd className="font-medium">{labelEstilo[estilo]}</dd>
            <dt className="text-muted-foreground">Restrições</dt><dd className="flex flex-wrap gap-1"><Restricoes mask={restr} />{restr === 0 && "Nenhuma"}</dd>
          </dl>
          <Field label="Prazo para receber propostas"><Select value={prazo} onChange={(e) => setPrazo(e.target.value)}>{prazos.map((p) => <option key={p.v} value={p.v}>{p.l}</option>)}</Select></Field>
          <Field label="Observações (opcional)" hint="Ex.: copos descartáveis, acesso pela portaria B, montagem 30 min antes"><Textarea value={obs} onChange={(e) => setObs(e.target.value)} maxLength={2000} /></Field>
        </>}
      </CardContent></Card>

      <div className="sticky bottom-24 lg:bottom-4">
        {passo < 3
          ? <Button size="lg" className="w-full" disabled={!validoPasso[passo]} onClick={() => setPasso(passo + 1)}>Continuar<ArrowRight className="h-5 w-5" /></Button>
          : <Button size="lg" className="w-full" disabled={criar.isPending} onClick={() => criar.mutate()}><Check className="h-5 w-5" />{criar.isPending ? "Publicando..." : "Publicar demanda"}</Button>}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------ Detalhe + propostas */
export function ContratanteDemanda() {
  const { id } = useParams();
  const qc = useQueryClient();
  const toast = useToast();
  const nav = useNavigate();
  const [confirmarCancelar, setConfirmarCancelar] = useState(false);
  const d = useQuery({ queryKey: ["demanda", id], queryFn: () => api.demandas.obter(id!) });
  const props = useQuery({ queryKey: ["propostas", id], queryFn: () => api.demandas.propostas(id!), refetchInterval: 15_000 });

  const cancelar = useMutation({
    mutationFn: () => api.demandas.cancelar(id!),
    onSuccess: () => { toast("Demanda cancelada."); void qc.invalidateQueries(); nav("/contratante/demandas"); },
    onError: (e) => toast(e.message, true),
  });

  if (d.isLoading) return <Carregando />;
  if (d.error || !d.data) return <ErroBox erro={d.error} />;
  const x = d.data;
  const aberta = x.status === "Aberta" || x.status === "EmNegociacao";

  return (
    <div className="space-y-5">
      <Link to="/contratante/demandas" className="inline-flex items-center gap-1 text-sm text-muted-foreground"><ArrowLeft className="h-4 w-4" />Demandas</Link>
      <Card><CardContent className="space-y-3">
        <div className="flex items-start justify-between gap-3"><h1 className="text-xl font-bold">{x.titulo}</h1><StatusDemandaBadge s={x.status} /></div>
        <div className="grid gap-1 text-sm text-muted-foreground">
          <div>{dataLonga(x.dataEvento)} às {hora(x.horaInicio)} · {x.numeroPessoas} pessoas · {labelEstilo[x.estilo]}</div>
          <div>{x.endereco} — {x.cidade}/{x.uf}</div>
        </div>
        <div className="flex flex-wrap gap-2"><Restricoes mask={x.restricoes} /></div>
        {x.observacoes && <p className="rounded-xl bg-muted/60 p-3 text-sm">{x.observacoes}</p>}
        {x.pedidoId && <Button asChild><Link to={`/contratante/pedidos/${x.pedidoId}`}>Ver pedido</Link></Button>}
      </CardContent></Card>

      <Secao titulo={`Propostas${props.data ? ` (${props.data.filter((p) => p.status !== "Recusada").length})` : ""}`}>
        {props.isLoading ? <Carregando /> : props.error ? <ErroBox erro={props.error} /> : props.data?.length ? (
          <div className="space-y-3">{props.data.map((p) => <PropostaCard key={p.id} p={p} demandaAberta={aberta} onAceita={(pid) => nav(`/contratante/pedidos/${pid}`)} />)}</div>
        ) : (
          <EmptyState icon={<Inbox className="h-6 w-6" />} title="Aguardando propostas">Os buffets próximos já foram avisados. Você será notificado a cada nova proposta.</EmptyState>
        )}
      </Secao>

      {aberta && (confirmarCancelar
        ? <div className="flex items-center gap-2"><Button variant="destructive" onClick={() => cancelar.mutate()} disabled={cancelar.isPending}>Confirmar cancelamento</Button><Button variant="ghost" onClick={() => setConfirmarCancelar(false)}>Voltar</Button></div>
        : <Button variant="ghost" className="text-destructive" onClick={() => setConfirmarCancelar(true)}>Cancelar demanda</Button>)}
    </div>
  );
}

function PropostaCard({ p, demandaAberta, onAceita }: { p: Proposta; demandaAberta: boolean; onAceita: (pedidoId: string) => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [modo, setModo] = useState<null | "ajuste" | "chat" | "aceitar">(null);
  const [msg, setMsg] = useState("");
  const inativa = p.status === "Recusada" || p.status === "Retirada" || p.status === "Expirada";

  const refresh = () => qc.invalidateQueries({ queryKey: ["propostas"] });
  const ajuste = useMutation({ mutationFn: () => api.propostas.pedirAjuste(p.id, msg), onSuccess: () => { toast("Ajuste enviado ao fornecedor."); setModo(null); setMsg(""); void refresh(); }, onError: (e) => toast(e.message, true) });
  const aceitar = useMutation({ mutationFn: () => api.propostas.aceitar(p.id), onSuccess: (r) => { toast("Proposta aceita! Pedido criado."); void qc.invalidateQueries(); onAceita(r.pedidoId); }, onError: (e) => toast(e.message, true) });

  return (
    <Card className={cn(inativa && "opacity-60", p.melhorCustoBeneficio && "ring-2 ring-primary/50")}>
      <CardContent className="space-y-3">
        {p.melhorCustoBeneficio && <Badge tone="primary"><Trophy className="h-3.5 w-3.5" />Melhor custo-benefício</Badge>}
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2"><span className="truncate font-semibold">{p.fornecedor}</span>{p.fundador && <Badge tone="lavender">Fundador</Badge>}</div>
            <Stars value={p.notaMedia} count={p.totalAvaliacoes} />
          </div>
          <div className="text-right">
            <div className="text-xl font-bold">{moeda(p.valorTotal)}</div>
            <div className="text-xs text-muted-foreground">{moeda(p.valorPorPessoa)} por pessoa</div>
          </div>
        </div>
        <p className="whitespace-pre-wrap rounded-xl bg-muted/60 p-3 text-sm">{p.cardapio}</p>
        {p.observacao && <p className="text-sm text-muted-foreground">{p.observacao}</p>}
        <div className="flex flex-wrap items-center gap-2"><StatusPropostaBadge s={p.status} />{p.versao > 1 && <Badge>versão {p.versao}</Badge>}</div>
        {p.status === "EmAjuste" && p.ajusteSolicitado && <p className="rounded-xl bg-warning-soft p-3 text-sm"><b>Você pediu:</b> {p.ajusteSolicitado}</p>}

        {!inativa && (
          <div className="flex flex-wrap gap-2">
            {demandaAberta && p.status === "Enviada" && <Button onClick={() => setModo(modo === "aceitar" ? null : "aceitar")}><Check className="h-4 w-4" />Aceitar</Button>}
            {demandaAberta && p.status !== "Aceita" && <Button variant="outline" onClick={() => setModo(modo === "ajuste" ? null : "ajuste")}>Pedir ajuste</Button>}
            <Button variant="ghost" onClick={() => setModo(modo === "chat" ? null : "chat")}><MessageCircle className="h-4 w-4" />Conversar</Button>
          </div>
        )}

        {modo === "aceitar" && (
          <div className="space-y-2 rounded-xl bg-primary-soft p-3 text-sm">
            <p>Confirmar <b>{p.fornecedor}</b> por <b>{moeda(p.valorTotal)}</b>? As demais propostas serão encerradas. O pagamento é combinado diretamente com o fornecedor.</p>
            <div className="flex gap-2"><Button onClick={() => aceitar.mutate()} disabled={aceitar.isPending}>{aceitar.isPending ? "Confirmando..." : "Sim, contratar"}</Button><Button variant="ghost" onClick={() => setModo(null)}>Voltar</Button></div>
          </div>
        )}
        {modo === "ajuste" && (
          <div className="space-y-2">
            <Textarea value={msg} onChange={(e) => setMsg(e.target.value)} placeholder="Ex.: trocar o suco de caixinha por suco natural e adicionar mais 10 pães de queijo" maxLength={2000} />
            <div className="flex gap-2"><Button onClick={() => ajuste.mutate()} disabled={!msg.trim() || ajuste.isPending}>Enviar pedido de ajuste</Button><Button variant="ghost" onClick={() => setModo(null)}>Cancelar</Button></div>
          </div>
        )}
        {modo === "chat" && <PropostaChat propostaId={p.id} />}
      </CardContent>
    </Card>
  );
}

/* ----------------------------------------------------------------- Conta */
export function Conta() {
  const { usuario, sair } = useAuth();
  const nav = useNavigate();
  return (
    <div className="mx-auto max-w-xl space-y-4">
      <PageTitle title="Minha conta" />
      <Card><CardContent className="space-y-1">
        <div className="text-lg font-semibold">{usuario?.nome}</div>
        <div className="text-sm text-muted-foreground">{usuario?.email}</div>
        {usuario?.empresa && <div className="flex items-center gap-2 pt-2 text-sm"><CreditCard className="h-4 w-4 text-muted-foreground" />{usuario.empresa}</div>}
      </CardContent></Card>
      <PushToggle />
      <Button variant="outline" className="w-full" onClick={() => { sair(); nav("/login"); }}>Sair da conta</Button>
    </div>
  );
}
