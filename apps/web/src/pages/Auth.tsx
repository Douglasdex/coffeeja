import { api } from "@coffeeja/api-client";
import { Button, Card, Field, Input, cn } from "@coffeeja/ui";
import { useMutation } from "@tanstack/react-query";
import { Building2, Clock, ShieldCheck, Utensils } from "lucide-react";
import { useState, type FormEvent, type ReactNode } from "react";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { Logo } from "../components/AppShell";
import { LocationPicker, localValido, localVazio, type Local } from "../components/LocationPicker";
import { rotaInicial, useAuth } from "../lib/auth";

function AuthLayout({ titulo, subtitulo, children }: { titulo: string; subtitulo: string; children: ReactNode }) {
  return (
    <div className="grid min-h-dvh lg:grid-cols-2">
      <aside className="hidden flex-col justify-between bg-primary p-12 text-primary-foreground lg:flex">
        <Logo className="text-primary-foreground [&_span]:!text-primary-foreground [&>span:first-child]:bg-white/20" />
        <div className="space-y-6">
          <h2 className="max-w-md text-4xl font-bold leading-tight">Chega de esperar 3 dias por um orçamento de café.</h2>
          <ul className="space-y-3 text-lg text-white/90">
            <li className="flex items-center gap-3"><Clock className="h-5 w-5" />Receba propostas de vários buffets em minutos</li>
            <li className="flex items-center gap-3"><Utensils className="h-5 w-5" />Compare cardápio, preço e avaliações lado a lado</li>
            <li className="flex items-center gap-3"><ShieldCheck className="h-5 w-5" />Fornecedores avaliados e aprovados</li>
          </ul>
        </div>
        <p className="text-sm text-white/70">© CoffeeJá</p>
      </aside>
      <main className="flex items-center justify-center px-4 py-8 safe-top">
        <div className="w-full max-w-md space-y-6">
          <Logo className="lg:hidden" />
          <div>
            <h1 className="text-2xl font-bold tracking-tight">{titulo}</h1>
            <p className="text-muted-foreground">{subtitulo}</p>
          </div>
          {children}
        </div>
      </main>
    </div>
  );
}

export function Login() {
  const { usuario, entrar } = useAuth();
  const [email, setEmail] = useState("");
  const [senha, setSenha] = useState("");
  const m = useMutation({ mutationFn: () => api.auth.login(email, senha), onSuccess: entrar });
  if (usuario) return <Navigate to={rotaInicial(usuario)} replace />;

  return (
    <AuthLayout titulo="Entrar" subtitulo="Acesse sua conta CoffeeJá">
      <form className="space-y-4" onSubmit={(e: FormEvent) => { e.preventDefault(); m.mutate(); }}>
        <Field label="E-mail"><Input type="email" autoComplete="email" inputMode="email" value={email} onChange={(e) => setEmail(e.target.value)} required /></Field>
        <Field label="Senha"><Input type="password" autoComplete="current-password" value={senha} onChange={(e) => setSenha(e.target.value)} required /></Field>
        {m.error && <p role="alert" className="text-sm font-medium text-destructive">{m.error.message}</p>}
        <Button size="lg" className="w-full" disabled={m.isPending}>{m.isPending ? "Entrando..." : "Entrar"}</Button>
      </form>
      <p className="text-center text-sm text-muted-foreground">Ainda não tem conta? <Link to="/cadastro" className="font-semibold text-primary">Cadastre-se</Link></p>
    </AuthLayout>
  );
}

type Perfil = "contratante" | "fornecedor";

export function Cadastro() {
  const { usuario, entrar } = useAuth();
  const nav = useNavigate();
  const [perfil, setPerfil] = useState<Perfil>("contratante");
  const [f, setF] = useState({ nome: "", email: "", telefone: "", senha: "", empresa: "", cnpj: "" });
  const [local, setLocal] = useState<Local>(localVazio);
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value });

  const m = useMutation({
    mutationFn: () => perfil === "contratante"
      ? api.auth.registrarContratante({ nome: f.nome, email: f.email, telefone: f.telefone || undefined, senha: f.senha, empresa: f.empresa, cnpj: f.cnpj || undefined })
      : api.auth.registrarFornecedor({ nome: f.nome, email: f.email, telefone: f.telefone || undefined, senha: f.senha, nomeFantasia: f.empresa, cnpj: f.cnpj || undefined, cidade: local.cidade, uf: local.uf, latitude: local.latitude!, longitude: local.longitude! }),
    onSuccess: (s) => { entrar(s); nav(rotaInicial(s.usuario), { replace: true }); },
  });
  if (usuario) return <Navigate to={rotaInicial(usuario)} replace />;
  const podeEnviar = perfil === "contratante" || localValido(local);

  return (
    <AuthLayout titulo="Criar conta" subtitulo="Escolha como você vai usar o CoffeeJá">
      <div className="grid grid-cols-2 gap-3" role="radiogroup" aria-label="Tipo de conta">
        {([["contratante", "Quero contratar", Building2], ["fornecedor", "Sou buffet", Utensils]] as const).map(([k, label, Icon]) => (
          <button key={k} type="button" role="radio" aria-checked={perfil === k} onClick={() => setPerfil(k)}>
            <Card className={cn("flex h-full flex-col items-center gap-2 p-4 text-sm font-semibold transition", perfil === k ? "border-primary ring-2 ring-primary/30" : "")}>
              <Icon className={cn("h-6 w-6", perfil === k ? "text-primary" : "text-muted-foreground")} />{label}
            </Card>
          </button>
        ))}
      </div>
      <form className="space-y-4" onSubmit={(e: FormEvent) => { e.preventDefault(); m.mutate(); }}>
        <Field label={perfil === "contratante" ? "Empresa" : "Nome do buffet"}><Input value={f.empresa} onChange={set("empresa")} required /></Field>
        <Field label="CNPJ (opcional)"><Input value={f.cnpj} onChange={set("cnpj")} inputMode="numeric" /></Field>
        <Field label="Seu nome"><Input value={f.nome} onChange={set("nome")} autoComplete="name" required /></Field>
        <Field label="WhatsApp / telefone"><Input value={f.telefone} onChange={set("telefone")} inputMode="tel" autoComplete="tel" /></Field>
        <Field label="E-mail"><Input type="email" value={f.email} onChange={set("email")} autoComplete="email" required /></Field>
        <Field label="Senha" hint="Mínimo de 8 caracteres"><Input type="password" value={f.senha} onChange={set("senha")} autoComplete="new-password" minLength={8} required /></Field>
        {perfil === "fornecedor" && (
          <div className="space-y-2 rounded-2xl bg-muted/60 p-4">
            <p className="text-sm text-muted-foreground">Informe de onde você atende. Você receberá demandas num raio de até 50 km.</p>
            <LocationPicker value={local} onChange={setLocal} rotulo="Endereço da cozinha / base" />
          </div>
        )}
        {m.error && <p role="alert" className="text-sm font-medium text-destructive">{m.error.message}</p>}
        <Button size="lg" className="w-full" disabled={m.isPending || !podeEnviar}>{m.isPending ? "Criando..." : "Criar conta"}</Button>
      </form>
      <p className="text-center text-sm text-muted-foreground">Já tem conta? <Link to="/login" className="font-semibold text-primary">Entrar</Link></p>
    </AuthLayout>
  );
}
