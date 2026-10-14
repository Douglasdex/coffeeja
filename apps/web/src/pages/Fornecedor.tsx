import { api, type Cardapio, type Estilo, type MinhaProposta } from "@coffeeja/api-client";
import { Badge, Button, Card, CardContent, Chip, EmptyState, Field, Input, PageTitle, Select, Stars, StatCard, Textarea } from "@coffeeja/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, ArrowLeft, Clock, FileText, Inbox, LogOut, MessageCircle, Percent, Send, Star, Trash2, Wallet } from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Carregando, ChatSinal, DemandaCard, Descartaveis, ErroBox, Restricoes, Secao, opcoesRestricao, restricaoLabels } from "../components/common";
import { LocationPicker, type Local } from "../components/LocationPicker";
import { PushToggle } from "../components/PushToggle";
import { SegurancaCard } from "../components/Seguranca";
import { AcessosFornecedor } from "./Equipe";
import { PropostaChat } from "../components/PropostaChat";
import { useAuth } from "../lib/auth";
import { dataCurta, dataHora, dataLonga, hora, moeda } from "../lib/format";
import { StatusDemandaBadge, StatusPropostaBadge, labelEstilo } from "../lib/labels";
import { useToast } from "../lib/toast";

/* ------------------------------------------------------------------ Início */
export function FornecedorInicio() {
  const { usuario } = useAuth();
  const dash = useQuery({ queryKey: ["dash"], queryFn: api.dashboard.fornecedor });
  const abertas = useQuery({ queryKey: ["abertas"], queryFn: api.demandas.abertas, refetchInterval: 30_000 });
  const d = dash.data;
  const novas = abertas.data?.filter((x) => !x.minhaPropostaId) ?? [];

  return (
    <div className="space-y-6">
      <PageTitle title={`Olá, ${usuario?.nome.split(" ")[0]}`} subtitle="Responda rápido: quem responde primeiro costuma fechar." />
      {d && !d.aprovado && (
        <div className="flex items-start gap-3 rounded-2xl bg-warning-soft p-4 text-sm font-medium text-warning">
          <Clock className="mt-0.5 h-5 w-5 shrink-0" />
          Seu cadastro está em análise. Assim que for aprovado você começa a receber demandas.
        </div>
      )}
      {d && (
        <div className="no-scrollbar -mx-4 flex gap-3 overflow-x-auto px-4 pb-1 sm:mx-0 sm:grid sm:grid-cols-2 sm:overflow-visible sm:px-0 lg:grid-cols-4">
          <StatCard label="Propostas enviadas" value={d.propostasEnviadas} ring={Math.min(1, d.propostasEnviadas / 20)} icon={<Send className="h-5 w-5" />} />
          <StatCard label="Taxa de conversão" value={`${d.taxaConversao}%`} ring={d.taxaConversao / 100} icon={<Percent className="h-5 w-5" />} color="var(--accent)" />
          <StatCard label="Faturamento" value={moeda(d.faturamento)} ring={d.faturamento > 0 ? 1 : 0} icon={<Wallet className="h-5 w-5" />} color="var(--success)" />
          <StatCard label="Nota média" value={d.totalAvaliacoes ? d.notaMedia.toFixed(1) : "—"} ring={d.notaMedia / 5} icon={<Star className="h-5 w-5" />} color="var(--lavender)" />
        </div>
      )}
      {d && d.mensagensNaoLidas > 0 && (
        <Link to="/fornecedor/propostas">
          <div className="flex items-center gap-3 rounded-2xl bg-primary-soft p-4 text-sm font-medium text-primary">
            <MessageCircle className="h-5 w-5 shrink-0" />
            {d.mensagensNaoLidas} {d.mensagensNaoLidas === 1 ? "mensagem nova" : "mensagens novas"} de clientes. Toque para responder.
          </div>
        </Link>
      )}
      {d && d.propostasEmAjuste > 0 && (
        <Link to="/fornecedor/propostas"><div className="flex items-center gap-3 rounded-2xl bg-primary-soft p-4 text-sm font-medium text-primary"><AlertTriangle className="h-5 w-5" />{d.propostasEmAjuste} {d.propostasEmAjuste === 1 ? "cliente pediu" : "clientes pediram"} ajuste na proposta.</div></Link>
      )}
      {d && d.pedidosSemNota > 0 && (
        <Link to="/fornecedor/pedidos"><div className="flex items-center gap-3 rounded-2xl bg-warning-soft p-4 text-sm font-medium text-warning"><FileText className="h-5 w-5" />{d.pedidosSemNota} {d.pedidosSemNota === 1 ? "pedido sem" : "pedidos sem"} nota fiscal anexada.</div></Link>
      )}
      <Secao titulo="Novas demandas perto de você" acao={<Link to="/fornecedor/demandas" className="text-sm font-semibold text-primary">Ver todas</Link>}>
        {abertas.isLoading ? <Carregando /> : novas.length ? (
          <div className="grid gap-3 md:grid-cols-2">{novas.slice(0, 4).map((x) => <DemandaCard key={x.id} d={x} to={`/fornecedor/demandas/${x.id}`} fornecedor />)}</div>
        ) : <EmptyState icon={<Inbox className="h-6 w-6" />} title="Sem novas demandas no momento">Avisaremos você assim que surgir um pedido na sua região.</EmptyState>}
      </Secao>
    </div>
  );
}

export function FornecedorDemandas() {
  const { data, isLoading, error } = useQuery({ queryKey: ["abertas"], queryFn: api.demandas.abertas, refetchInterval: 30_000 });
  return (
    <div className="space-y-4">
      <PageTitle title="Demandas abertas" subtitle="Pedidos num raio de até 50 km da sua cozinha." />
      {isLoading ? <Carregando /> : error ? <ErroBox erro={error} /> : data?.length ? (
        <div className="grid gap-3 md:grid-cols-2">{data.map((x) => <DemandaCard key={x.id} d={x} to={`/fornecedor/demandas/${x.id}`} fornecedor />)}</div>
      ) : <EmptyState icon={<Inbox className="h-6 w-6" />} title="Nenhuma demanda aberta">Volte em alguns minutos ou ajuste seu raio no perfil.</EmptyState>}
    </div>
  );
}

/* ---------------------------------------------------- Detalhe + envio de proposta */
export function FornecedorDemanda() {
  const { id } = useParams();
  const qc = useQueryClient();
  const toast = useToast();
  const nav = useNavigate();
  const d = useQuery({ queryKey: ["demanda", id], queryFn: () => api.demandas.obter(id!) });
  const abertas = useQuery({ queryKey: ["abertas"], queryFn: api.demandas.abertas });
  const cardapios = useQuery({ queryKey: ["cardapios"], queryFn: api.fornecedor.cardapios });
  const minha = abertas.data?.find((x) => x.id === id)?.minhaPropostaId;

  const [valor, setValor] = useState("");
  const [cardapio, setCardapio] = useState("");
  const [obs, setObs] = useState("");
  const [modelo, setModelo] = useState<string | null>(null);

  const enviar = useMutation({
    mutationFn: () => api.demandas.enviarProposta(id!, { valorTotal: Number(valor.replace(",", ".")), cardapio, observacao: obs || undefined }),
    onSuccess: () => { toast("Proposta enviada!"); void qc.invalidateQueries(); nav("/fornecedor/propostas"); },
    onError: (e) => toast(e.message, true),
  });

  if (d.isLoading) return <Carregando />;
  if (d.error || !d.data) return <ErroBox erro={d.error} />;
  const x = d.data;

  function aplicar(c: Cardapio) {
    setModelo(c.id);
    setCardapio(`${c.nome}\n${c.itens}`);
    setValor((c.valorPorPessoa * x.numeroPessoas).toFixed(2).replace(".", ","));
  }
  const total = Number(valor.replace(",", "."));
  const valido = total > 0 && cardapio.trim().length > 3;

  return (
    <div className="space-y-5">
      <Link to="/fornecedor/demandas" className="inline-flex items-center gap-1 text-sm text-muted-foreground"><ArrowLeft className="h-4 w-4" />Demandas</Link>
      <Card><CardContent className="space-y-3">
        <div className="flex items-start justify-between gap-3"><h1 className="text-xl font-bold">{x.titulo}</h1><StatusDemandaBadge s={x.status} /></div>
        <div className="grid gap-1 text-sm text-muted-foreground">
          <div>{x.empresa}</div>
          <div>{dataLonga(x.dataEvento)} às {hora(x.horaInicio)} · <b className="text-foreground">{x.numeroPessoas} pessoas</b> · {labelEstilo[x.estilo]}</div>
          <div>{x.cidade}/{x.uf} {x.endereco ? `— ${x.endereco}` : "· endereço exato liberado após a contratação"}</div>
        </div>
        <div className="flex flex-wrap gap-2"><Restricoes mask={x.restricoes} /><Descartaveis mask={x.descartaveis} /></div>
        {x.observacoes && <p className="rounded-xl bg-muted/60 p-3 text-sm">{x.observacoes}</p>}
        {x.solicitadoPor && <p className="text-xs text-muted-foreground">Solicitado por {x.solicitadoPor}{x.solicitanteSetor ? ` · ${x.solicitanteSetor}` : ""} em {dataHora(x.criadoEm)}</p>}
      </CardContent></Card>

      {minha ? (
        <Card><CardContent className="flex flex-col items-start gap-3"><Badge tone="success">Você já enviou proposta</Badge><Button asChild variant="outline"><Link to="/fornecedor/propostas">Ver minhas propostas</Link></Button></CardContent></Card>
      ) : x.status === "Aberta" || x.status === "EmNegociacao" ? (
        <Secao titulo="Sua proposta">
          <Card><CardContent className="space-y-4">
            {!!cardapios.data?.length && (
              <div className="space-y-2">
                <div className="text-sm font-medium">Responder com um cardápio-base</div>
                <div className="flex flex-wrap gap-2">{cardapios.data.map((c) => <Chip key={c.id} active={modelo === c.id} onClick={() => aplicar(c)}>{c.nome} · {moeda(c.valorPorPessoa)}/pessoa</Chip>)}</div>
              </div>
            )}
            {cardapios.data && !cardapios.data.length && <p className="rounded-xl bg-accent-soft p-3 text-sm text-accent">Dica: cadastre cardápios-base no seu <Link to="/fornecedor/perfil" className="font-semibold underline">perfil</Link> para responder em 2 toques.</p>}
            <Field label="Valor total (R$)" hint={total > 0 ? `${moeda(total / x.numeroPessoas)} por pessoa` : undefined}><Input inputMode="decimal" value={valor} onChange={(e) => setValor(e.target.value)} placeholder="0,00" /></Field>
            <Field label="Cardápio"><Textarea value={cardapio} onChange={(e) => setCardapio(e.target.value)} placeholder="Café, leite, sucos, pão de queijo, mini sanduíches, bolos..." maxLength={3000} /></Field>
            <Field label="Observações (opcional)"><Textarea className="min-h-16" value={obs} onChange={(e) => setObs(e.target.value)} placeholder="Montagem, descartáveis, equipe..." maxLength={2000} /></Field>
            <Button size="lg" className="w-full" disabled={!valido || enviar.isPending} onClick={() => enviar.mutate()}><Send className="h-5 w-5" />{enviar.isPending ? "Enviando..." : "Enviar proposta"}</Button>
          </CardContent></Card>
        </Secao>
      ) : <p className="text-sm text-muted-foreground">Esta demanda não recebe mais propostas.</p>}
    </div>
  );
}

/* ---------------------------------------------------------- Minhas propostas */
export function FornecedorPropostas() {
  const { data, isLoading, error } = useQuery({ queryKey: ["minhas-propostas"], queryFn: api.propostas.minhas, refetchInterval: 20_000 });
  return (
    <div className="space-y-4">
      <PageTitle title="Minhas propostas" />
      {isLoading ? <Carregando /> : error ? <ErroBox erro={error} /> : data?.length ? <div className="space-y-3">{data.map((p) => <MinhaPropostaCard key={p.id} p={p} />)}</div>
        : <EmptyState icon={<Send className="h-6 w-6" />} title="Você ainda não enviou propostas" action={<Button asChild><Link to="/fornecedor/demandas">Ver demandas</Link></Button>} />}
    </div>
  );
}

function MinhaPropostaCard({ p }: { p: MinhaProposta }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [aberto, setAberto] = useState<null | "revisar" | "chat">(p.mensagensNaoLidas > 0 ? "chat" : p.status === "EmAjuste" ? "revisar" : null);
  const [valor, setValor] = useState(String(p.valorTotal).replace(".", ","));
  const [cardapio, setCardapio] = useState("");
  const ativa = p.status === "Enviada" || p.status === "EmAjuste";

  const revisar = useMutation({
    mutationFn: () => api.propostas.revisar(p.id, { valorTotal: Number(valor.replace(",", ".")), cardapio }),
    onSuccess: () => { toast("Nova versão enviada."); setAberto(null); void qc.invalidateQueries(); },
    onError: (e) => toast(e.message, true),
  });
  const retirar = useMutation({ mutationFn: () => api.propostas.retirar(p.id), onSuccess: () => { toast("Proposta retirada."); void qc.invalidateQueries(); }, onError: (e) => toast(e.message, true) });

  return (
    <Card><CardContent className="space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0"><div className="truncate font-semibold">{p.titulo}</div><div className="text-sm text-muted-foreground">{dataCurta(p.dataEvento)} · {p.cidade} · {p.numeroPessoas} pessoas</div></div>
        <div className="text-right"><div className="font-bold">{moeda(p.valorTotal)}</div>{p.versao > 1 && <div className="text-xs text-muted-foreground">v{p.versao}</div>}</div>
      </div>
      <div className="flex flex-wrap items-center gap-2"><StatusPropostaBadge s={p.status} /><ChatSinal naoLidas={p.mensagensNaoLidas} aguardando={p.aguardandoMinhaResposta} /></div>
      {p.status === "EmAjuste" && p.ajusteSolicitado && <p className="rounded-xl bg-warning-soft p-3 text-sm"><b>O cliente pediu:</b> {p.ajusteSolicitado}</p>}
      <div className="flex flex-wrap gap-2">
        {p.pedidoId && <Button asChild><Link to={`/fornecedor/pedidos/${p.pedidoId}`}>Abrir pedido</Link></Button>}
        {ativa && <Button variant="outline" onClick={() => setAberto(aberto === "revisar" ? null : "revisar")}>Atualizar proposta</Button>}
        {p.status !== "Recusada" && p.status !== "Retirada" && <Button variant="ghost" onClick={() => setAberto(aberto === "chat" ? null : "chat")}>Conversar{p.mensagensNaoLidas > 0 && <span className="ml-1 grid h-5 min-w-5 place-items-center rounded-full bg-primary px-1 text-[11px] font-bold text-primary-foreground">{p.mensagensNaoLidas}</span>}</Button>}
        {ativa && <Button variant="ghost" className="text-destructive" onClick={() => retirar.mutate()} disabled={retirar.isPending}>Retirar</Button>}
      </div>
      {aberto === "revisar" && (
        <div className="space-y-3 rounded-xl bg-muted/60 p-3">
          <Field label="Novo valor total (R$)"><Input inputMode="decimal" value={valor} onChange={(e) => setValor(e.target.value)} /></Field>
          <Field label="Cardápio atualizado"><Textarea value={cardapio} onChange={(e) => setCardapio(e.target.value)} placeholder="Descreva o cardápio completo com os ajustes" /></Field>
          <Button onClick={() => revisar.mutate()} disabled={revisar.isPending || !cardapio.trim() || !Number(valor.replace(",", "."))}>Enviar nova versão</Button>
        </div>
      )}
      {aberto === "chat" && <PropostaChat propostaId={p.id} fechado={p.status === "Recusada" || p.status === "Retirada"} />}
    </CardContent></Card>
  );
}

/* ------------------------------------------------------------------- Perfil */
const estilosOpc: Estilo[] = ["Economico", "Padrao", "Premium"];

export function FornecedorPerfil() {
  const { sair, usuario } = useAuth();
  const nav = useNavigate();
  const qc = useQueryClient();
  const toast = useToast();
  const perfil = useQuery({ queryKey: ["perfil"], queryFn: api.fornecedor.perfil });
  const cardapios = useQuery({ queryKey: ["cardapios"], queryFn: api.fornecedor.cardapios });

  const [nome, setNome] = useState("");
  const [desc, setDesc] = useState("");
  const [raio, setRaio] = useState(50);
  const [restr, setRestr] = useState(0);
  const [local, setLocal] = useState<Local>({ endereco: "", cidade: "", uf: "", latitude: null, longitude: null });
  useEffect(() => {
    const p = perfil.data;
    if (!p) return;
    setNome(p.nomeFantasia); setDesc(p.descricao ?? ""); setRaio(p.raioAtendimentoKm); setRestr(p.atendeRestricoes);
    setLocal({ endereco: `${p.cidade}/${p.uf}`, cidade: p.cidade, uf: p.uf, latitude: p.latitude, longitude: p.longitude });
  }, [perfil.data]);

  const salvar = useMutation({
    mutationFn: () => api.fornecedor.salvarPerfil({ nomeFantasia: nome, descricao: desc || null, cidade: local.cidade, uf: local.uf, latitude: local.latitude!, longitude: local.longitude!, raioAtendimentoKm: raio, atendeRestricoes: restr }),
    onSuccess: () => { toast("Perfil salvo."); void qc.invalidateQueries(); },
    onError: (e) => toast(e.message, true),
  });

  const [c, setC] = useState({ nome: "", estilo: "Padrao" as Estilo, itens: "", valor: "", min: "10" });
  const criar = useMutation({
    mutationFn: () => api.fornecedor.criarCardapio({ nome: c.nome, estilo: c.estilo, itens: c.itens, valorPorPessoa: Number(c.valor.replace(",", ".")), minimoPessoas: Number(c.min) || 1 }),
    onSuccess: () => { toast("Cardápio adicionado."); setC({ ...c, nome: "", itens: "", valor: "" }); void qc.invalidateQueries({ queryKey: ["cardapios"] }); },
    onError: (e) => toast(e.message, true),
  });
  const remover = useMutation({ mutationFn: (id: string) => api.fornecedor.removerCardapio(id), onSuccess: () => qc.invalidateQueries({ queryKey: ["cardapios"] }) });

  if (perfil.isLoading) return <Carregando />;
  if (perfil.error || !perfil.data) return <ErroBox erro={perfil.error} />;

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <PageTitle title="Perfil do buffet" action={<Stars value={perfil.data.notaMedia} count={perfil.data.totalAvaliacoes} />} />
      {!perfil.data.aprovado && <p className="rounded-2xl bg-warning-soft p-4 text-sm font-medium text-warning">Cadastro em análise pela equipe CoffeeJá.</p>}

      <Card><CardContent className="space-y-4">
        <Field label="Nome do buffet"><Input value={nome} onChange={(e) => setNome(e.target.value)} /></Field>
        <Field label="Sobre o buffet"><Textarea value={desc} onChange={(e) => setDesc(e.target.value)} maxLength={1000} placeholder="Há quantos anos atende, diferenciais, capacidade..." /></Field>
        <Field label={`Raio de atendimento: ${raio} km`} hint="Máximo de 50 km"><input type="range" min={5} max={50} step={5} value={raio} onChange={(e) => setRaio(Number(e.target.value))} className="w-full accent-[var(--primary)]" /></Field>
        <div className="space-y-2">
          <div className="text-sm font-medium">Atendo restrições alimentares</div>
          <div className="flex flex-wrap gap-2">{opcoesRestricao.map((b) => <Chip key={b} active={(restr & b) === b} onClick={() => setRestr(restr ^ b)}>{restricaoLabels[b]}</Chip>)}</div>
        </div>
        <LocationPicker value={local} onChange={setLocal} rotulo="Endereço da cozinha / base" />
        <Button onClick={() => salvar.mutate()} disabled={salvar.isPending || !nome.trim() || local.latitude === null}>Salvar perfil</Button>
      </CardContent></Card>

      <Secao titulo="Cardápios-base">
        <div className="space-y-3">
          {cardapios.data?.map((x) => (
            <Card key={x.id}><CardContent className="flex items-start justify-between gap-3">
              <div className="min-w-0 space-y-1"><div className="flex flex-wrap items-center gap-2 font-semibold">{x.nome}<Badge tone="lavender">{labelEstilo[x.estilo]}</Badge></div>
                <p className="whitespace-pre-wrap text-sm text-muted-foreground">{x.itens}</p>
                <div className="text-sm font-medium">{moeda(x.valorPorPessoa)} / pessoa · mínimo {x.minimoPessoas}</div></div>
              <Button variant="ghost" size="icon" onClick={() => remover.mutate(x.id)} aria-label={`Remover ${x.nome}`}><Trash2 className="h-5 w-5 text-destructive" /></Button>
            </CardContent></Card>
          ))}
          <Card><CardContent className="space-y-3">
            <div className="text-sm font-semibold">Novo cardápio</div>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Nome"><Input value={c.nome} onChange={(e) => setC({ ...c, nome: e.target.value })} placeholder="Prata" /></Field>
              <Field label="Estilo"><Select value={c.estilo} onChange={(e) => setC({ ...c, estilo: e.target.value as Estilo })}>{estilosOpc.map((e) => <option key={e} value={e}>{labelEstilo[e]}</option>)}</Select></Field>
              <Field label="Valor por pessoa (R$)"><Input inputMode="decimal" value={c.valor} onChange={(e) => setC({ ...c, valor: e.target.value })} /></Field>
              <Field label="Mínimo de pessoas"><Input inputMode="numeric" value={c.min} onChange={(e) => setC({ ...c, min: e.target.value })} /></Field>
            </div>
            <Field label="Itens"><Textarea value={c.itens} onChange={(e) => setC({ ...c, itens: e.target.value })} placeholder="Café, suco, pão de queijo, mini quiche..." /></Field>
            <Button variant="soft" disabled={criar.isPending || !c.nome.trim() || !c.itens.trim() || !Number(c.valor.replace(",", "."))} onClick={() => criar.mutate()}>Adicionar cardápio</Button>
          </CardContent></Card>
        </div>
      </Secao>

      {usuario?.funcao === "AdminConta" && <AcessosFornecedor />}

      <PushToggle />
      <SegurancaCard />
      <div className="space-y-2 pt-2">
        <p className="text-center text-sm text-muted-foreground">{usuario?.email}</p>
        <Button variant="outline" className="w-full" onClick={() => { sair(); nav("/login"); }}><LogOut className="h-4 w-4" />Sair da conta</Button>
      </div>
    </div>
  );
}
