import { api } from "@coffeeja/api-client";
import { Button, Card, CardContent, Field, Input, Spinner, Textarea } from "@coffeeja/ui";
import { useMutation, useQuery } from "@tanstack/react-query";
import { CheckCircle2, ShieldCheck, XCircle } from "lucide-react";
import { useState, type FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { rotaInicial, useAuth } from "../lib/auth";
import { AuthLayout } from "./Auth";

function LinkInvalido() {
  return (
    <AuthLayout titulo="Link inválido ou expirado">
      <Card><CardContent className="space-y-2">
        <XCircle className="h-8 w-8 text-destructive" />
        <p className="text-sm text-muted-foreground">Este link já foi utilizado ou passou do prazo. Peça um novo link a quem fez o seu cadastro.</p>
      </CardContent></Card>
    </AuthLayout>
  );
}

/** Página aberta pelo superior imediato (sem login) para aprovar ou recusar o cadastro do funcionário. */
export function AprovarFuncionario() {
  const { token } = useParams();
  const info = useQuery({ queryKey: ["aprovacao", token], queryFn: () => api.publico.aprovacao(token!), retry: false, refetchOnWindowFocus: false, staleTime: Infinity });
  const [recusando, setRecusando] = useState(false);
  const [motivo, setMotivo] = useState("");
  const decidir = useMutation({ mutationFn: (aprovar: boolean) => api.publico.decidir(token!, aprovar, aprovar ? undefined : motivo) });

  if (info.isLoading) return <div className="grid min-h-dvh place-items-center"><Spinner className="h-7 w-7" /></div>;
  // Depois de decidir o token é consumido: mantém os dados já carregados em vez de mostrar "link inválido".
  if (!info.data || (info.error && !decidir.isSuccess)) return <LinkInvalido />;
  const d = info.data;

  if (decidir.isSuccess) {
    const aprovado = decidir.data.aprovado;
    return (
      <AuthLayout titulo={aprovado ? "Cadastro aprovado" : "Cadastro recusado"}>
        <Card><CardContent className="space-y-2">
          {aprovado ? <CheckCircle2 className="h-8 w-8 text-success" /> : <XCircle className="h-8 w-8 text-destructive" />}
          <p className="text-sm text-muted-foreground">
            {aprovado ? `Obrigado, ${d.superiorNome}. ${d.funcionario} receberá o acesso da empresa ${d.empresa} em breve.` : `Registramos a recusa. O administrador da ${d.empresa} foi avisado.`}
          </p>
        </CardContent></Card>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout titulo="Aprovação de funcionário" subtitulo={`Olá, ${d.superiorNome}. Sua confirmação é necessária.`}>
      <Card><CardContent className="space-y-3">
        <p className="text-sm"><b>{d.solicitadoPor || "O administrador"}</b>, da <b>{d.empresa}</b>, cadastrou o funcionário abaixo para solicitar coffee breaks em nome da empresa, e indicou você como superior imediato.</p>
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 rounded-xl bg-muted/60 p-3 text-sm">
          <dt className="text-muted-foreground">Nome</dt><dd className="font-medium">{d.funcionario}</dd>
          <dt className="text-muted-foreground">CPF</dt><dd className="font-medium">{d.cpfMascarado}</dd>
          <dt className="text-muted-foreground">Setor</dt><dd className="font-medium">{d.setor}</dd>
        </dl>
        <p className="text-xs text-muted-foreground">Ao aprovar, você confirma que esta pessoa pode contratar coffee break em nome da empresa. Cada pedido ficará registrado com nome, setor, data e hora.</p>
        {recusando ? (
          <form className="space-y-3" onSubmit={(e: FormEvent) => { e.preventDefault(); decidir.mutate(false); }}>
            <Field label="Motivo (opcional)"><Textarea value={motivo} onChange={(e) => setMotivo(e.target.value)} maxLength={500} /></Field>
            <div className="flex gap-2"><Button variant="destructive" disabled={decidir.isPending}>Confirmar recusa</Button><Button type="button" variant="ghost" onClick={() => setRecusando(false)}>Voltar</Button></div>
          </form>
        ) : (
          <div className="flex flex-wrap gap-2">
            <Button variant="success" size="lg" disabled={decidir.isPending} onClick={() => decidir.mutate(true)}><CheckCircle2 className="h-5 w-5" />Aprovar</Button>
            <Button variant="outline" size="lg" onClick={() => setRecusando(true)}>Recusar</Button>
          </div>
        )}
        {decidir.error && <p role="alert" className="text-sm font-medium text-destructive">{decidir.error.message}</p>}
      </CardContent></Card>
    </AuthLayout>
  );
}

/** Primeiro acesso (ou redefinição): a pessoa cria a própria senha pelo link recebido por WhatsApp. */
export function AtivarAcesso() {
  const { token } = useParams();
  const { entrar } = useAuth();
  const nav = useNavigate();
  const info = useQuery({ queryKey: ["ativacao", token], queryFn: () => api.publico.ativacao(token!), retry: false, refetchOnWindowFocus: false, staleTime: Infinity });
  const [senha, setSenha] = useState("");
  const [conf, setConf] = useState("");
  const m = useMutation({ mutationFn: () => api.publico.definirSenha(token!, senha), onSuccess: (s) => { entrar(s); nav(rotaInicial(s.usuario), { replace: true }); } });

  if (info.isLoading) return <div className="grid min-h-dvh place-items-center"><Spinner className="h-7 w-7" /></div>;
  if (info.error || !info.data) return <LinkInvalido />;
  const invalida = senha.length < 8 || senha !== conf;

  return (
    <AuthLayout titulo={`Bem-vindo(a), ${info.data.nome.split(" ")[0]}!`} subtitulo={`Crie sua senha para acessar o CoffeeJá da ${info.data.empresa}.`}>
      <form className="space-y-4" onSubmit={(e: FormEvent) => { e.preventDefault(); if (!invalida) m.mutate(); }}>
        <div className="flex items-center gap-2 rounded-xl bg-accent-soft p-3 text-sm text-accent"><ShieldCheck className="h-5 w-5 shrink-0" />Seu login será o seu CPF ({info.data.login}).</div>
        <Field label="Nova senha" hint="Mínimo de 8 caracteres"><Input type="password" autoComplete="new-password" value={senha} onChange={(e) => setSenha(e.target.value)} /></Field>
        <Field label="Repita a senha" error={conf && senha !== conf ? "As senhas não conferem" : undefined}><Input type="password" autoComplete="new-password" value={conf} onChange={(e) => setConf(e.target.value)} /></Field>
        {m.error && <p role="alert" className="text-sm font-medium text-destructive">{m.error.message}</p>}
        <Button size="lg" className="w-full" disabled={invalida || m.isPending}>{m.isPending ? "Salvando..." : "Criar senha e entrar"}</Button>
      </form>
    </AuthLayout>
  );
}
