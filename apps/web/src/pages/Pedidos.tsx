import { api, type StatusPedido } from "@coffeeja/api-client";
import { Badge, Button, Card, CardContent, CardHeader, CardTitle, EmptyState, Field, Input, PageTitle, Textarea, cn } from "@coffeeja/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Banknote, Bell, CheckCircle2, Download, FileText, Phone, Star, Upload } from "lucide-react";
import { useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Carregando, ErroBox } from "../components/common";
import { useAuth } from "../lib/auth";
import { dataCurta, dataHora, dataLonga, haQuanto, hora, moeda } from "../lib/format";
import { StatusPedidoBadge, labelPagamento, labelProximoStatus } from "../lib/labels";
import { useToast } from "../lib/toast";

const base = (papel?: string) => (papel === "Fornecedor" ? "/fornecedor" : "/contratante");

export function PedidosLista() {
  const { usuario } = useAuth();
  const { data, isLoading, error } = useQuery({ queryKey: ["pedidos"], queryFn: api.pedidos.listar });
  return (
    <div className="space-y-4">
      <PageTitle title="Pedidos" />
      {isLoading ? <Carregando /> : error ? <ErroBox erro={error} /> : data?.length ? (
        <div className="grid gap-3 md:grid-cols-2">
          {data.map((p) => (
            <Link key={p.id} to={`${base(usuario?.papel)}/pedidos/${p.id}`}>
              <Card className="space-y-2 p-4 transition hover:-translate-y-0.5">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0"><div className="truncate font-semibold">{p.titulo}</div><div className="truncate text-sm text-muted-foreground">{p.contraparte}</div></div>
                  <div className="font-bold">{moeda(p.valor)}</div>
                </div>
                <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                  <StatusPedidoBadge s={p.status} /><span>{dataCurta(p.dataEvento)} · {hora(p.horaInicio)}</span>
                  {p.status !== "Cancelado" && (p.temNotaFiscal ? <Badge tone="success"><FileText className="h-3.5 w-3.5" />NF anexada</Badge> : <Badge tone="warning">Sem NF</Badge>)}
                </div>
              </Card>
            </Link>
          ))}
        </div>
      ) : <EmptyState icon={<FileText className="h-6 w-6" />} title="Nenhum pedido ainda">Quando uma proposta for aceita, o pedido aparece aqui.</EmptyState>}
    </div>
  );
}

const etapas: StatusPedido[] = ["Confirmado", "EmPreparo", "SaiuParaEntrega", "Entregue", "Concluido"];
const etapaLabel: Record<string, string> = { Confirmado: "Confirmado", EmPreparo: "Preparo", SaiuParaEntrega: "A caminho", Entregue: "Entregue", Concluido: "Concluído" };

export function PedidoDetalhe() {
  const { id } = useParams();
  const { usuario } = useAuth();
  const souForn = usuario?.papel === "Fornecedor";
  const qc = useQueryClient();
  const toast = useToast();
  const nav = useNavigate();
  const arquivo = useRef<HTMLInputElement>(null);
  const [numero, setNumero] = useState("");
  const [nota, setNota] = useState(0);
  const [pontual, setPontual] = useState(true);
  const [comentario, setComentario] = useState("");

  const { data: p, isLoading, error } = useQuery({ queryKey: ["pedido", id], queryFn: () => api.pedidos.obter(id!), refetchInterval: 15_000 });
  const refresh = () => qc.invalidateQueries();

  const status = useMutation({ mutationFn: (s: StatusPedido) => api.pedidos.status(id!, s), onSuccess: () => { toast("Status atualizado."); void refresh(); }, onError: (e) => toast(e.message, true) });
  const anexar = useMutation({
    mutationFn: (f: File) => api.pedidos.anexarNota(id!, f, numero || undefined),
    onSuccess: () => { toast("Nota fiscal anexada."); setNumero(""); void refresh(); },
    onError: (e) => toast(e.message, true),
  });
  const avaliar = useMutation({ mutationFn: () => api.pedidos.avaliar(id!, { nota, pontual, comentario: comentario || undefined }), onSuccess: () => { toast("Obrigado pela avaliação!"); void refresh(); }, onError: (e) => toast(e.message, true) });

  if (isLoading) return <Carregando />;
  if (error || !p) return <ErroBox erro={error} />;
  const idx = etapas.indexOf(p.status);
  const cancelado = p.status === "Cancelado";

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <button onClick={() => nav(`${base(usuario?.papel)}/pedidos`)} className="inline-flex items-center gap-1 text-sm text-muted-foreground"><ArrowLeft className="h-4 w-4" />Pedidos</button>

      <Card><CardContent className="space-y-3">
        <div className="flex items-start justify-between gap-3"><h1 className="text-xl font-bold">{p.titulo}</h1><StatusPedidoBadge s={p.status} /></div>
        <div className="grid gap-1 text-sm text-muted-foreground">
          <div>{dataLonga(p.dataEvento)} às {hora(p.horaInicio)} · {p.numeroPessoas} pessoas</div>
          <div>{p.endereco} — {p.cidade}</div>
        </div>
        <div className="text-2xl font-bold">{moeda(p.valor)}</div>
        <p className="whitespace-pre-wrap rounded-xl bg-muted/60 p-3 text-sm">{p.cardapio}</p>
      </CardContent></Card>

      {!cancelado && (
        <Card><CardContent className="space-y-4">
          <ol className="flex items-center justify-between" aria-label="Andamento do pedido">
            {etapas.map((e, i) => (
              <li key={e} className="flex flex-1 flex-col items-center gap-1 text-center">
                <span className={cn("grid h-8 w-8 place-items-center rounded-full text-xs font-bold", i <= idx ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground")}>{i <= idx ? <CheckCircle2 className="h-4 w-4" /> : i + 1}</span>
                <span className={cn("text-[11px]", i <= idx ? "font-semibold" : "text-muted-foreground")}>{etapaLabel[e]}</span>
              </li>
            ))}
          </ol>
          {p.proximosStatus.filter((s) => s !== "Cancelado").map((s) => <Button key={s} size="lg" className="w-full" disabled={status.isPending} onClick={() => status.mutate(s)}>{labelProximoStatus[s]}</Button>)}
          {p.proximosStatus.includes("Cancelado") && <Button variant="ghost" className="w-full text-destructive" disabled={status.isPending} onClick={() => status.mutate("Cancelado")}>Cancelar pedido</Button>}
        </CardContent></Card>
      )}

      <Card>
        <CardHeader><CardTitle>Nota fiscal</CardTitle>{p.notaFiscalNomeOriginal ? <Badge tone="success">Anexada</Badge> : <Badge tone="warning">Pendente</Badge>}</CardHeader>
        <CardContent className="space-y-3">
          {p.notaFiscalNomeOriginal && (
            <div className="flex items-center justify-between gap-3 rounded-xl bg-muted/60 p-3 text-sm">
              <div className="min-w-0"><div className="truncate font-medium">{p.notaFiscalNomeOriginal}</div><div className="text-muted-foreground">{p.notaFiscalNumero ? `NF ${p.notaFiscalNumero} · ` : ""}{p.notaFiscalEnviadaEm && haQuanto(p.notaFiscalEnviadaEm)}</div></div>
              <Button variant="outline" size="sm" onClick={() => api.pedidos.baixarNota(p.id, p.notaFiscalNomeOriginal!).catch((e) => toast(e.message, true))}><Download className="h-4 w-4" />Baixar</Button>
            </div>
          )}
          {!cancelado && (
            <>
              <Field label="Número da NF (opcional)"><Input value={numero} onChange={(e) => setNumero(e.target.value)} maxLength={50} /></Field>
              <input ref={arquivo} type="file" hidden accept=".pdf,.xml,.png,.jpg,.jpeg,application/pdf,image/*"
                onChange={(e) => { const f = e.target.files?.[0]; if (f) anexar.mutate(f); e.target.value = ""; }} />
              <Button variant={p.notaFiscalNomeOriginal ? "outline" : "default"} className="w-full" disabled={anexar.isPending} onClick={() => arquivo.current?.click()}>
                <Upload className="h-4 w-4" />{anexar.isPending ? "Enviando..." : p.notaFiscalNomeOriginal ? "Substituir arquivo" : "Anexar nota fiscal (PDF, XML ou imagem)"}
              </Button>
              <p className="text-xs text-muted-foreground">Até 10 MB.</p>
            </>
          )}
        </CardContent>
      </Card>

      <Card><CardContent className="space-y-3">
        <div className="flex items-center gap-2 font-semibold"><Banknote className="h-5 w-5 text-muted-foreground" />Pagamento</div>
        <p className="text-sm text-muted-foreground">{labelPagamento[p.statusPagamento]}. {p.provedorPagamento === "Manual" && "Neste momento o pagamento é feito fora da plataforma — inclua a nota fiscal aqui para manter o registro."}</p>
      </CardContent></Card>

      {(p.solicitadoPor || p.contratadoPor) && (
        <Card><CardHeader><CardTitle>Registro da contratação</CardTitle></CardHeader><CardContent className="space-y-3 text-sm">
          <p className="text-muted-foreground">Contratado em nome da empresa. Responsáveis pelo processo:</p>
          {p.solicitadoPor && <div><div className="text-xs text-muted-foreground">Solicitado por</div><div className="font-semibold">{p.solicitadoPor.nome}{p.solicitadoPor.setor ? ` · ${p.solicitadoPor.setor}` : ""}</div><div className="text-muted-foreground">{dataHora(p.solicitadoPor.em)}</div></div>}
          {p.contratadoPor && <div><div className="text-xs text-muted-foreground">Contratado por</div><div className="font-semibold">{p.contratadoPor.nome}{p.contratadoPor.setor ? ` · ${p.contratadoPor.setor}` : ""}</div><div className="text-muted-foreground">{dataHora(p.contratadoPor.em)}</div></div>}
        </CardContent></Card>
      )}

      <Card><CardContent className="space-y-2">
        <div className="text-sm text-muted-foreground">{souForn ? "Cliente" : "Fornecedor"}</div>
        <div className="font-semibold">{p.contraparte.nome}</div>
        {p.contraparte.telefone && <a href={`tel:${p.contraparte.telefone}`} className="flex items-center gap-2 text-sm text-primary"><Phone className="h-4 w-4" />{p.contraparte.telefone}</a>}
        {p.contraparte.email && <div className="text-sm text-muted-foreground">{p.contraparte.email}</div>}
      </CardContent></Card>

      <Card><CardHeader><CardTitle>Linha do tempo</CardTitle></CardHeader>
        <CardContent><ul className="space-y-3">
          {[...p.eventos].reverse().map((e, i) => (
            <li key={i} className="flex gap-3 text-sm"><Bell className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" /><div><div className="font-medium">{e.descricao}</div><div className="text-xs text-muted-foreground">{haQuanto(e.em)}</div></div></li>
          ))}
        </ul></CardContent>
      </Card>

      {!souForn && (p.status === "Entregue" || p.status === "Concluido") && !p.avaliado && (
        <Card><CardHeader><CardTitle>Como foi o serviço?</CardTitle></CardHeader><CardContent className="space-y-4">
          <div className="flex justify-center gap-1" role="radiogroup" aria-label="Nota">
            {[1, 2, 3, 4, 5].map((n) => (
              <button key={n} type="button" role="radio" aria-checked={nota === n} aria-label={`${n} estrelas`} onClick={() => setNota(n)} className="p-1"><Star className={cn("h-9 w-9", n <= nota ? "fill-warning text-warning" : "text-border")} /></button>
            ))}
          </div>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={pontual} onChange={(e) => setPontual(e.target.checked)} className="h-5 w-5 accent-[var(--primary)]" />Chegou no horário combinado</label>
          <Textarea value={comentario} onChange={(e) => setComentario(e.target.value)} placeholder="Comentário (opcional)" maxLength={1000} />
          <Button className="w-full" disabled={!nota || avaliar.isPending} onClick={() => avaliar.mutate()}>Enviar avaliação</Button>
        </CardContent></Card>
      )}
    </div>
  );
}
