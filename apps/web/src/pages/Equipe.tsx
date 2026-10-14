import { api, type AcaoLink, type Acesso, type Funcionario, type StatusUsuario } from "@coffeeja/api-client";
import { Badge, Button, Card, CardContent, EmptyState, Field, Input, PageTitle, type Tone } from "@coffeeja/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Clock, KeyRound, MessageCircle, Plus, UserPlus, Users } from "lucide-react";
import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { AcaoLinkCard } from "../components/AcaoLinkCard";
import { Carregando, ErroBox } from "../components/common";
import { celularValido, cpfValido, mascaraCpf, mascaraTelefone, telefoneValido } from "../lib/docs";
import { haQuanto } from "../lib/format";
import { useToast } from "../lib/toast";

const statusInfo: Record<StatusUsuario, [string, Tone]> = {
  Ativo: ["Ativo", "success"],
  PendenteAprovacao: ["Aguardando superior", "warning"],
  AguardandoAtivacao: ["Aprovado · enviar acesso", "accent"],
  Recusado: ["Recusado", "danger"],
  Inativo: ["Desativado", "neutral"],
};
export const StatusUsuarioBadge = ({ s }: { s: StatusUsuario }) => <Badge tone={statusInfo[s][1]}>{statusInfo[s][0]}</Badge>;

const f0 = { nome: "", cpf: "", setor: "", telefoneComercial: "", whatsapp: "", superiorNome: "", superiorWhatsapp: "" };

/** Gestão de funcionários da empresa (somente o administrador da conta PJ). */
export function Equipe() {
  const qc = useQueryClient();
  const toast = useToast();
  const lista = useQuery({ queryKey: ["equipe"], queryFn: api.equipe.listar });
  const [novo, setNovo] = useState(false);
  const [f, setF] = useState(f0);
  const [tentou, setTentou] = useState(false);
  const [acao, setAcao] = useState<{ titulo: string; link: AcaoLink } | null>(null);
  const set = (k: keyof typeof f0, v: string) => setF({ ...f, [k]: v });
  const refresh = () => qc.invalidateQueries({ queryKey: ["equipe"] });

  const erros = {
    nome: f.nome.trim().length < 5 ? "Informe o nome completo" : undefined,
    cpf: cpfValido(f.cpf) ? undefined : "CPF inválido",
    setor: f.setor.trim() ? undefined : "Informe o setor",
    tel: telefoneValido(f.telefoneComercial) ? undefined : "Telefone comercial com DDD",
    whatsapp: celularValido(f.whatsapp) ? undefined : "WhatsApp com DDD",
    supNome: f.superiorNome.trim().length < 3 ? "Informe o nome do superior" : undefined,
    supWhats: celularValido(f.superiorWhatsapp) ? undefined : "WhatsApp do superior com DDD",
  };
  const temErro = Object.values(erros).some(Boolean);
  const e = (k: keyof typeof erros) => (tentou ? erros[k] : undefined);

  const criar = useMutation({
    mutationFn: () => api.equipe.criar(f),
    onSuccess: (link) => { setNovo(false); setF(f0); setTentou(false); setAcao({ titulo: "Funcionário cadastrado — peça a aprovação do superior", link }); void refresh(); },
    onError: (x) => toast(x.message, true),
  });
  const reenviar = useMutation({ mutationFn: (id: string) => api.equipe.reenviarAprovacao(id), onSuccess: (link) => setAcao({ titulo: "Novo link para o superior imediato", link }), onError: (x) => toast(x.message, true) });
  const acesso = useMutation({ mutationFn: (id: string) => api.equipe.enviarAcesso(id), onSuccess: (link) => setAcao({ titulo: "Acesso do funcionário", link }), onError: (x) => toast(x.message, true) });
  const desativar = useMutation({ mutationFn: (id: string) => api.equipe.desativar(id), onSuccess: () => void refresh() });
  const reativar = useMutation({ mutationFn: (id: string) => api.equipe.reativar(id), onSuccess: () => void refresh() });

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <Link to="/contratante/conta" className="inline-flex items-center gap-1 text-sm text-muted-foreground"><ArrowLeft className="h-4 w-4" />Conta</Link>
      <PageTitle title="Minha equipe" subtitle="Funcionários que podem pedir coffee break em nome da empresa." action={!novo && <Button onClick={() => setNovo(true)}><Plus className="h-4 w-4" />Novo</Button>} />

      <div className="flex items-start gap-3 rounded-2xl bg-accent-soft p-4 text-sm text-accent">
        <Clock className="mt-0.5 h-5 w-5 shrink-0" />
        <span>Como funciona: você cadastra o funcionário, o <b>superior imediato aprova pelo WhatsApp</b> e, depois, você envia o acesso para ele criar a senha. Cada pedido fica registrado com nome, setor, data e hora.</span>
      </div>

      {acao && <AcaoLinkCard titulo={acao.titulo} acao={acao.link} onFechar={() => setAcao(null)} />}

      {novo && (
        <Card><CardContent>
          <form className="space-y-4" noValidate onSubmit={(ev: FormEvent) => { ev.preventDefault(); setTentou(true); if (!temErro) criar.mutate(); }}>
            <div className="text-base font-semibold">Novo funcionário</div>
            <Field label="Nome completo" error={e("nome")}><Input value={f.nome} onChange={(x) => set("nome", x.target.value)} /></Field>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="CPF" error={e("cpf")}><Input value={f.cpf} inputMode="numeric" onChange={(x) => set("cpf", mascaraCpf(x.target.value))} /></Field>
              <Field label="Setor" error={e("setor")}><Input value={f.setor} onChange={(x) => set("setor", x.target.value)} placeholder="Ex.: Recursos Humanos" /></Field>
              <Field label="Telefone comercial" error={e("tel")}><Input value={f.telefoneComercial} inputMode="tel" onChange={(x) => set("telefoneComercial", mascaraTelefone(x.target.value))} /></Field>
              <Field label="WhatsApp" error={e("whatsapp")}><Input value={f.whatsapp} inputMode="tel" onChange={(x) => set("whatsapp", mascaraTelefone(x.target.value))} /></Field>
            </div>
            <div className="space-y-3 rounded-2xl border border-border/70 p-4">
              <div className="text-sm font-semibold text-muted-foreground">Superior imediato (quem vai aprovar)</div>
              <Field label="Nome do superior" error={e("supNome")}><Input value={f.superiorNome} onChange={(x) => set("superiorNome", x.target.value)} /></Field>
              <Field label="WhatsApp do superior" error={e("supWhats")}><Input value={f.superiorWhatsapp} inputMode="tel" onChange={(x) => set("superiorWhatsapp", mascaraTelefone(x.target.value))} /></Field>
            </div>
            <div className="flex gap-2"><Button disabled={criar.isPending}><UserPlus className="h-4 w-4" />{criar.isPending ? "Salvando..." : "Cadastrar e pedir aprovação"}</Button><Button type="button" variant="ghost" onClick={() => setNovo(false)}>Cancelar</Button></div>
          </form>
        </CardContent></Card>
      )}

      {lista.isLoading ? <Carregando /> : lista.error ? <ErroBox erro={lista.error} /> : lista.data?.length ? (
        <div className="space-y-3">{lista.data.map((x) => (
          <FuncionarioCard key={x.id} f={x} ocupado={reenviar.isPending || acesso.isPending}
            onReenviar={() => reenviar.mutate(x.id)} onAcesso={() => acesso.mutate(x.id)} onDesativar={() => desativar.mutate(x.id)} onReativar={() => reativar.mutate(x.id)} />
        ))}</div>
      ) : !novo && <EmptyState icon={<Users className="h-6 w-6" />} title="Nenhum funcionário ainda" action={<Button onClick={() => setNovo(true)}>Cadastrar o primeiro</Button>}>Você pode pedir coffee break sozinho ou liberar outras pessoas da empresa.</EmptyState>}
    </div>
  );
}

function FuncionarioCard({ f, ocupado, onReenviar, onAcesso, onDesativar, onReativar }: {
  f: Funcionario; ocupado: boolean; onReenviar: () => void; onAcesso: () => void; onDesativar: () => void; onReativar: () => void;
}) {
  return (
    <Card><CardContent className="space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="truncate font-semibold">{f.nome}</div>
          <div className="text-sm text-muted-foreground">{f.setor} · CPF {mascaraCpf(f.cpf)}</div>
          <div className="text-sm text-muted-foreground">Superior: {f.superiorNome} · cadastrado {haQuanto(f.criadoEm)}</div>
        </div>
        <StatusUsuarioBadge s={f.status} />
      </div>
      {f.status === "Recusado" && f.motivoRecusa && <p className="rounded-xl bg-destructive-soft p-3 text-sm text-destructive">Motivo: {f.motivoRecusa}</p>}
      <div className="flex flex-wrap gap-2">
        {f.status === "PendenteAprovacao" && <Button variant="success" size="sm" disabled={ocupado} onClick={onReenviar}><MessageCircle className="h-4 w-4" />Enviar ao superior no WhatsApp</Button>}
        {(f.status === "AguardandoAtivacao" || f.status === "Ativo") && <Button size="sm" variant={f.status === "Ativo" ? "outline" : "success"} disabled={ocupado} onClick={onAcesso}><KeyRound className="h-4 w-4" />{f.status === "Ativo" ? "Redefinir senha" : "Enviar acesso por WhatsApp"}</Button>}
        {f.status === "Inativo" ? <Button size="sm" variant="outline" onClick={onReativar}>Reativar</Button> : f.status !== "Recusado" && <Button size="sm" variant="ghost" className="text-destructive" onClick={onDesativar}>Desativar</Button>}
      </div>
    </CardContent></Card>
  );
}

/** Acessos adicionais do buffet ("gerar outro acesso"). Hoje gratuitos; no futuro, licença adicional. */
export function AcessosFornecedor() {
  const qc = useQueryClient();
  const toast = useToast();
  const { data, isLoading, error } = useQuery({ queryKey: ["acessos"], queryFn: api.acessos.listar });
  const [novo, setNovo] = useState(false);
  const [f, setF] = useState({ nome: "", cpf: "", cargo: "", telefone: "" });
  const [tentou, setTentou] = useState(false);
  const [acao, setAcao] = useState<{ titulo: string; link: AcaoLink } | null>(null);
  const refresh = () => qc.invalidateQueries({ queryKey: ["acessos"] });

  const erros = {
    nome: f.nome.trim().length < 5 ? "Informe o nome completo" : undefined,
    cpf: cpfValido(f.cpf) ? undefined : "CPF inválido",
    cargo: f.cargo.trim() ? undefined : "Informe o cargo",
    telefone: celularValido(f.telefone) ? undefined : "Celular/WhatsApp com DDD",
  };
  const temErro = Object.values(erros).some(Boolean);
  const e = (k: keyof typeof erros) => (tentou ? erros[k] : undefined);

  const criar = useMutation({
    mutationFn: () => api.acessos.criar(f),
    onSuccess: (link) => { setNovo(false); setF({ nome: "", cpf: "", cargo: "", telefone: "" }); setTentou(false); setAcao({ titulo: "Acesso criado — envie o link para a pessoa", link }); void refresh(); },
    onError: (x) => toast(x.message, true),
  });
  const enviar = useMutation({ mutationFn: (id: string) => api.acessos.enviarAcesso(id), onSuccess: (link) => setAcao({ titulo: "Acesso", link }), onError: (x) => toast(x.message, true) });
  const desativar = useMutation({ mutationFn: (id: string) => api.acessos.desativar(id), onSuccess: () => void refresh() });
  const reativar = useMutation({ mutationFn: (id: string) => api.acessos.reativar(id), onSuccess: () => void refresh() });

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-base font-semibold">Acessos da equipe</h2>
        {!novo && <Button variant="soft" size="sm" onClick={() => setNovo(true)}><UserPlus className="h-4 w-4" />Gerar outro acesso</Button>}
      </div>
      {data && <p className="text-xs text-muted-foreground">{data.aviso}</p>}
      {acao && <AcaoLinkCard titulo={acao.titulo} acao={acao.link} onFechar={() => setAcao(null)} />}

      {novo && (
        <Card><CardContent>
          <form className="space-y-3" noValidate onSubmit={(ev: FormEvent) => { ev.preventDefault(); setTentou(true); if (!temErro) criar.mutate(); }}>
            <Field label="Nome completo" error={e("nome")}><Input value={f.nome} onChange={(x) => setF({ ...f, nome: x.target.value })} /></Field>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="CPF" error={e("cpf")}><Input value={f.cpf} inputMode="numeric" onChange={(x) => setF({ ...f, cpf: mascaraCpf(x.target.value) })} /></Field>
              <Field label="Cargo" error={e("cargo")}><Input value={f.cargo} onChange={(x) => setF({ ...f, cargo: x.target.value })} placeholder="Ex.: Atendente comercial" /></Field>
            </div>
            <Field label="Telefone / WhatsApp" error={e("telefone")}><Input value={f.telefone} inputMode="tel" onChange={(x) => setF({ ...f, telefone: mascaraTelefone(x.target.value) })} /></Field>
            <div className="flex gap-2"><Button disabled={criar.isPending}>{criar.isPending ? "Gerando..." : "Gerar acesso"}</Button><Button type="button" variant="ghost" onClick={() => setNovo(false)}>Cancelar</Button></div>
          </form>
        </CardContent></Card>
      )}

      {isLoading ? <Carregando /> : error ? <ErroBox erro={error} /> : data?.acessos.map((a) => (
        <AcessoLinha key={a.id} a={a} onEnviar={() => enviar.mutate(a.id)} onDesativar={() => desativar.mutate(a.id)} onReativar={() => reativar.mutate(a.id)} />
      ))}
    </div>
  );
}

function AcessoLinha({ a, onEnviar, onDesativar, onReativar }: { a: Acesso; onEnviar: () => void; onDesativar: () => void; onReativar: () => void }) {
  const titular = a.funcao === "AdminConta";
  return (
    <Card><CardContent className="flex flex-wrap items-center justify-between gap-3">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2 font-semibold">{a.nome}{titular && <Badge tone="lavender">Titular</Badge>}</div>
        <div className="text-sm text-muted-foreground">{a.cargo ? `${a.cargo} · ` : ""}CPF {mascaraCpf(a.cpf)}</div>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <StatusUsuarioBadge s={a.status} />
        {!titular && (a.status === "AguardandoAtivacao" || a.status === "Ativo") && <Button size="sm" variant="outline" onClick={onEnviar}><KeyRound className="h-4 w-4" />{a.status === "Ativo" ? "Redefinir senha" : "Enviar acesso"}</Button>}
        {!titular && (a.status === "Inativo" ? <Button size="sm" variant="outline" onClick={onReativar}>Reativar</Button> : <Button size="sm" variant="ghost" className="text-destructive" onClick={onDesativar}>Desativar</Button>)}
      </div>
    </CardContent></Card>
  );
}
