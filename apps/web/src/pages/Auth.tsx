import { ApiError, api, type CadastroPf, type CadastroPj, type Endereco } from "@coffeeja/api-client";
import { Button, Card, Field, Input, cn } from "@coffeeja/ui";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Building2, Clock, ShieldCheck, User, Utensils, Gift } from "lucide-react";
import { useState, type FormEvent, type ReactNode } from "react";
import { Link, Navigate, useNavigate, useSearchParams } from "react-router-dom";
import { erroSenha } from "../components/Seguranca";
import { EnderecoFields, enderecoCompleto, enderecoVazio } from "../components/EnderecoFields";
import { Logo } from "../components/AppShell";
import { rotaInicial, useAuth } from "../lib/auth";
import { celularValido, cnpjValido, cpfValido, emailValido, mascaraCnpj, mascaraCpf, mascaraTelefone, telefoneValido } from "../lib/docs";

export function AuthLayout({ titulo, subtitulo, children }: { titulo: string; subtitulo?: string; children: ReactNode }) {
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
      <main className="flex items-start justify-center px-4 py-8 safe-top lg:items-center">
        <div className="w-full max-w-lg space-y-6">
          <Logo className="lg:hidden" />
          <div>
            <h1 className="text-2xl font-bold tracking-tight">{titulo}</h1>
            {subtitulo && <p className="text-muted-foreground">{subtitulo}</p>}
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
  const [codigo, setCodigo] = useState("");
  const [pede2fa, setPede2fa] = useState(false);
  const m = useMutation({
    mutationFn: () => api.auth.login(email, senha, pede2fa ? codigo : undefined),
    onSuccess: entrar,
    onError: (e) => { if (e instanceof ApiError && e.requer2fa) setPede2fa(true); },
  });
  if (usuario) return <Navigate to={rotaInicial(usuario)} replace />;

  return (
    <AuthLayout titulo="Entrar" subtitulo="Acesse sua conta CoffeeJá">
      <form className="space-y-4" onSubmit={(e: FormEvent) => { e.preventDefault(); m.mutate(); }}>
        <Field label="E-mail ou CPF" hint="Funcionários entram com o CPF"><Input autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} required /></Field>
        <Field label="Senha"><Input type="password" autoComplete="current-password" value={senha} onChange={(e) => setSenha(e.target.value)} required /></Field>
        {pede2fa && (
          <Field label="Código de verificação" hint="Do aplicativo autenticador (ou um código de recuperação)">
            <Input value={codigo} onChange={(e) => setCodigo(e.target.value.trim())} inputMode="text" autoComplete="one-time-code" autoFocus />
          </Field>
        )}
        {m.error && !(m.error instanceof ApiError && m.error.requer2fa) && <p role="alert" className="text-sm font-medium text-destructive">{m.error.message}</p>}
        <Button size="lg" className="w-full" disabled={m.isPending}>{m.isPending ? "Entrando..." : "Entrar"}</Button>
      </form>
      <p className="text-center text-sm text-muted-foreground">Ainda não tem conta? <Link to="/cadastro" className="font-semibold text-primary">Cadastre-se</Link></p>
    </AuthLayout>
  );
}

type Perfil = "pj" | "pf" | "fornecedor";

function Secao({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <fieldset className="space-y-3 rounded-2xl border border-border/70 p-4">
      <legend className="px-2 text-sm font-semibold text-muted-foreground">{titulo}</legend>
      {children}
    </fieldset>
  );
}

const resp0 = { nome: "", cpf: "", telefoneComercial: "", whatsapp: "", email: "", senha: "" };

export function Cadastro() {
  const { usuario, entrar } = useAuth();
  const nav = useNavigate();
  const [params] = useSearchParams();
  const convite = params.get("convite");
  const conviteInfo = useQuery({ queryKey: ["convite", convite], queryFn: () => api.publico.convite(convite!), enabled: !!convite, retry: false });

  const [perfil, setPerfil] = useState<Perfil>("pj");
  // PJ / Buffet
  const [razao, setRazao] = useState("");
  const [fantasia, setFantasia] = useState("");
  const [cnpj, setCnpj] = useState("");
  const [endEmpresa, setEndEmpresa] = useState<Endereco>(enderecoVazio);
  const [resp, setResp] = useState(resp0);
  // PF
  const [pf, setPf] = useState({ nome: "", cpf: "", whatsapp: "", email: "", senha: "" });
  const [endPessoa, setEndPessoa] = useState<Endereco>(enderecoVazio);
  const [nfMesmo, setNfMesmo] = useState(true);
  const [endNf, setEndNf] = useState<Endereco>(enderecoVazio);
  const [tentou, setTentou] = useState(false);
  const [aceite, setAceite] = useState(false);

  const setR = (k: keyof typeof resp0) => (v: string) => setResp({ ...resp, [k]: v });
  const setP = (k: keyof typeof pf) => (v: string) => setPf({ ...pf, [k]: v });

  const m = useMutation({
    mutationFn: () => {
      const conviteCodigo = convite ?? null;
      if (perfil === "pf") {
        const body: CadastroPf = { ...pf, endereco: endPessoa, notaFiscalMesmoEndereco: nfMesmo, enderecoNotaFiscal: nfMesmo ? null : endNf, conviteCodigo, aceiteTermos: aceite } as CadastroPf;
        return api.auth.registrarContratantePf(body);
      }
      const body: CadastroPj = { razaoSocial: razao, nomeFantasia: fantasia, cnpj, endereco: endEmpresa, responsavel: resp, conviteCodigo, aceiteTermos: aceite } as CadastroPj;
      return perfil === "pj" ? api.auth.registrarContratantePj(body) : api.auth.registrarFornecedor(body);
    },
    onSuccess: (s) => { entrar(s); nav(rotaInicial(s.usuario), { replace: true }); },
  });
  if (usuario) return <Navigate to={rotaInicial(usuario)} replace />;

  const erros: Record<string, string | undefined> = perfil === "pf"
    ? {
        nome: pf.nome.trim().length < 5 ? "Informe o nome completo" : undefined,
        cpf: cpfValido(pf.cpf) ? undefined : "CPF inválido",
        whatsapp: celularValido(pf.whatsapp) ? undefined : "WhatsApp com DDD",
        email: emailValido(pf.email) ? undefined : "E-mail inválido",
        senha: erroSenha(pf.senha),
        endereco: enderecoCompleto(endPessoa) ? undefined : "Preencha todo o endereço",
        nf: nfMesmo || enderecoCompleto(endNf) ? undefined : "Preencha o endereço da nota fiscal",
      }
    : {
        razao: razao.trim() ? undefined : "Informe a razão social",
        fantasia: fantasia.trim() ? undefined : "Informe o nome fantasia",
        cnpj: cnpjValido(cnpj) ? undefined : "CNPJ inválido",
        endereco: enderecoCompleto(endEmpresa) ? undefined : "Preencha todo o endereço",
        nome: resp.nome.trim().length < 5 ? "Informe o nome completo" : undefined,
        cpf: cpfValido(resp.cpf) ? undefined : "CPF inválido",
        tel: telefoneValido(resp.telefoneComercial) ? undefined : "Telefone comercial com DDD",
        whatsapp: celularValido(resp.whatsapp) ? undefined : "WhatsApp com DDD",
        email: emailValido(resp.email) ? undefined : "E-mail inválido",
        senha: erroSenha(resp.senha),
      };
  const temErro = Object.values(erros).some(Boolean) || !aceite;
  const e = (k: string) => (tentou ? erros[k] : undefined);

  const opcoes = [
    { k: "pj" as const, label: "Empresa que contrata", desc: "Pessoa jurídica", Icon: Building2 },
    { k: "pf" as const, label: "Pessoa física", desc: "Contrato para mim", Icon: User },
    { k: "fornecedor" as const, label: "Sou buffet", desc: "Quero enviar propostas", Icon: Utensils },
  ];

  return (
    <AuthLayout titulo="Criar conta" subtitulo="Escolha como você vai usar o CoffeeJá">
      {convite && conviteInfo.data && (
        <div className="flex items-center gap-3 rounded-2xl bg-lavender-soft p-4 text-sm font-medium text-lavender">
          <Gift className="h-5 w-5 shrink-0" />{conviteInfo.data.convidante}{conviteInfo.data.empresa ? ` (${conviteInfo.data.empresa})` : ""} convidou você para o CoffeeJá.
        </div>
      )}

      <div className="grid grid-cols-3 gap-2" role="radiogroup" aria-label="Tipo de conta">
        {opcoes.map(({ k, label, desc, Icon }) => (
          <button key={k} type="button" role="radio" aria-checked={perfil === k} onClick={() => { setPerfil(k); setTentou(false); }}>
            <Card className={cn("flex h-full flex-col items-center gap-1 p-3 text-center transition", perfil === k ? "border-primary ring-2 ring-primary/30" : "")}>
              <Icon className={cn("h-6 w-6", perfil === k ? "text-primary" : "text-muted-foreground")} />
              <span className="text-sm font-semibold leading-tight">{label}</span>
              <span className="text-[11px] leading-tight text-muted-foreground">{desc}</span>
            </Card>
          </button>
        ))}
      </div>

      <form className="space-y-5" noValidate onSubmit={(ev: FormEvent) => { ev.preventDefault(); setTentou(true); if (!temErro) m.mutate(); }}>
        {perfil !== "pf" ? (
          <>
            <Secao titulo={perfil === "pj" ? "Dados da empresa" : "Dados do buffet"}>
              <Field label="Razão social" error={e("razao")}><Input value={razao} onChange={(x) => setRazao(x.target.value)} /></Field>
              <Field label="Nome fantasia" error={e("fantasia")}><Input value={fantasia} onChange={(x) => setFantasia(x.target.value)} /></Field>
              <Field label="CNPJ" error={e("cnpj")}><Input value={cnpj} inputMode="numeric" onChange={(x) => setCnpj(mascaraCnpj(x.target.value))} placeholder="00.000.000/0000-00" /></Field>
            </Secao>
            <Secao titulo={perfil === "pj" ? "Endereço da empresa" : "Endereço da cozinha / base"}>
              <EnderecoFields value={endEmpresa} onChange={setEndEmpresa} />
              {e("endereco") && <p className="text-xs font-medium text-destructive">{e("endereco")}</p>}
              {perfil === "fornecedor" && <p className="text-xs text-muted-foreground">Usamos este endereço para definir a região (até 50 km) das demandas que você receberá.</p>}
            </Secao>
            <Secao titulo={perfil === "pj" ? "Responsável pela conta (administrador)" : "Responsável pela conta"}>
              <Field label="Nome completo" error={e("nome")}><Input value={resp.nome} onChange={(x) => setR("nome")(x.target.value)} autoComplete="name" /></Field>
              <Field label="CPF" error={e("cpf")}><Input value={resp.cpf} inputMode="numeric" onChange={(x) => setR("cpf")(mascaraCpf(x.target.value))} placeholder="000.000.000-00" /></Field>
              <Field label="Telefone comercial da empresa" error={e("tel")}><Input value={resp.telefoneComercial} inputMode="tel" onChange={(x) => setR("telefoneComercial")(mascaraTelefone(x.target.value))} placeholder="(11) 3333-4444" /></Field>
              <Field label="WhatsApp do responsável" error={e("whatsapp")}><Input value={resp.whatsapp} inputMode="tel" onChange={(x) => setR("whatsapp")(mascaraTelefone(x.target.value))} placeholder="(11) 91234-5678" /></Field>
              <Field label="E-mail (seu login)" error={e("email")}><Input type="email" value={resp.email} onChange={(x) => setR("email")(x.target.value)} autoComplete="email" /></Field>
              <Field label="Senha" hint="Mínimo de 10 caracteres, com letras e número ou símbolo" error={e("senha")}><Input type="password" value={resp.senha} onChange={(x) => setR("senha")(x.target.value)} autoComplete="new-password" /></Field>
            </Secao>
            {perfil === "pj" && <p className="rounded-xl bg-accent-soft p-3 text-sm text-accent">Depois de criar a conta você poderá cadastrar funcionários para fazer pedidos em nome da empresa, com aprovação do superior imediato.</p>}
          </>
        ) : (
          <>
            <Secao titulo="Seus dados">
              <Field label="Nome completo" error={e("nome")}><Input value={pf.nome} onChange={(x) => setP("nome")(x.target.value)} autoComplete="name" /></Field>
              <Field label="CPF" error={e("cpf")}><Input value={pf.cpf} inputMode="numeric" onChange={(x) => setP("cpf")(mascaraCpf(x.target.value))} placeholder="000.000.000-00" /></Field>
              <Field label="WhatsApp" error={e("whatsapp")}><Input value={pf.whatsapp} inputMode="tel" onChange={(x) => setP("whatsapp")(mascaraTelefone(x.target.value))} placeholder="(11) 91234-5678" /></Field>
              <Field label="E-mail (seu login)" error={e("email")}><Input type="email" value={pf.email} onChange={(x) => setP("email")(x.target.value)} autoComplete="email" /></Field>
              <Field label="Senha" hint="Mínimo de 10 caracteres, com letras e número ou símbolo" error={e("senha")}><Input type="password" value={pf.senha} onChange={(x) => setP("senha")(x.target.value)} autoComplete="new-password" /></Field>
            </Secao>
            <Secao titulo="Seu endereço">
              <EnderecoFields value={endPessoa} onChange={(v) => { setEndPessoa(v); }} />
              {e("endereco") && <p className="text-xs font-medium text-destructive">{e("endereco")}</p>}
            </Secao>
            <Secao titulo="Endereço para emissão da nota fiscal">
              <div className="flex gap-2" role="radiogroup" aria-label="Endereço da nota fiscal">
                {[[true, "Mesmo endereço"], [false, "Outro endereço"]].map(([v, l]) => (
                  <button key={String(v)} type="button" role="radio" aria-checked={nfMesmo === v} onClick={() => setNfMesmo(v as boolean)}
                    className={cn("h-10 flex-1 rounded-full border px-4 text-sm font-medium transition", nfMesmo === v ? "border-primary bg-primary-soft text-primary" : "border-border bg-card")}>{l as string}</button>
                ))}
              </div>
              {nfMesmo
                ? <p className="text-sm text-muted-foreground">A nota fiscal será emitida no endereço informado acima.</p>
                : <><EnderecoFields value={endNf} onChange={setEndNf} />{e("nf") && <p className="text-xs font-medium text-destructive">{e("nf")}</p>}</>}
            </Secao>
          </>
        )}
        <label className="flex items-start gap-2 text-sm">
          <input type="checkbox" checked={aceite} onChange={(x) => setAceite(x.target.checked)} className="mt-0.5 h-5 w-5 shrink-0 accent-[var(--primary)]" />
          <span>Li e aceito os <Link to="/termos" target="_blank" className="font-semibold text-primary underline">Termos de Uso</Link> e a <Link to="/privacidade" target="_blank" className="font-semibold text-primary underline">Política de Privacidade</Link>.</span>
        </label>
        {tentou && !aceite && <p className="text-xs font-medium text-destructive">É necessário aceitar para criar a conta.</p>}
        {m.error && <p role="alert" className="text-sm font-medium text-destructive">{m.error.message}</p>}
        {tentou && temErro && <p role="alert" className="text-sm font-medium text-destructive">Revise os campos destacados.</p>}
        <Button size="lg" className="w-full" disabled={m.isPending}>{m.isPending ? "Criando..." : "Criar conta"}</Button>
      </form>
      <p className="text-center text-sm text-muted-foreground">Já tem conta? <Link to="/login" className="font-semibold text-primary">Entrar</Link></p>
    </AuthLayout>
  );
}
