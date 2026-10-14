import { ApiError, api, type InicioTotp, type Sessao } from "@coffeeja/api-client";
import { Badge, Button, Card, CardContent, CardHeader, CardTitle, Field, Input } from "@coffeeja/ui";
import { useMutation } from "@tanstack/react-query";
import { Check, Copy, Download, KeyRound, ShieldAlert, ShieldCheck } from "lucide-react";
import QRCode from "qrcode";
import { useEffect, useState, type FormEvent } from "react";
import { AuthLayout } from "../pages/Auth";
import { useAuth } from "../lib/auth";
import { useToast } from "../lib/toast";

/** Regras de senha espelham o servidor (mínimo de 10, letras + número/símbolo). */
export function erroSenha(s: string): string | undefined {
  if (s.length < 10) return "Mínimo de 10 caracteres";
  if (!/[a-zA-ZÀ-ÿ]/.test(s) || !/[\d\W_]/.test(s)) return "Use letras e ao menos um número ou símbolo";
  return undefined;
}

/** Passo a passo do 2FA: QR code → código de 6 dígitos → códigos de recuperação. */
export function Configurar2fa({ onConcluido }: { onConcluido: () => void }) {
  const { entrar } = useAuth();
  const toast = useToast();
  const [info, setInfo] = useState<InicioTotp | null>(null);
  const [qr, setQr] = useState<string | null>(null);
  const [codigo, setCodigo] = useState("");
  const [recuperacao, setRecuperacao] = useState<string[] | null>(null);
  const [copiado, setCopiado] = useState(false);

  const iniciar = useMutation({ mutationFn: api.seguranca.totpIniciar, onSuccess: setInfo, onError: (e) => toast(e.message, true) });
  const [sessaoNova, setSessaoNova] = useState<Sessao | null>(null);
  const confirmar = useMutation({
    mutationFn: () => api.seguranca.totpConfirmar(codigo),
    // A sessão só é aplicada ao clicar em "continuar": se o usuário entrasse agora, esta tela sumiria antes de ele ver os códigos de recuperação.
    onSuccess: (r) => { setRecuperacao(r.codigosRecuperacao); setSessaoNova(r.sessao); },
  });
  useEffect(() => { if (info) void QRCode.toDataURL(info.uri, { width: 200, margin: 1 }).then(setQr); }, [info]);

  if (recuperacao) {
    const texto = recuperacao.join("\n");
    return (
      <div className="space-y-4">
        <div className="flex items-start gap-3 rounded-2xl bg-success-soft p-4 text-sm text-success"><ShieldCheck className="mt-0.5 h-5 w-5 shrink-0" />Verificação em duas etapas ativada.</div>
        <p className="text-sm">Guarde estes <b>códigos de recuperação</b> em local seguro. Cada um vale uma vez e é a única saída se você perder o celular. Eles não serão exibidos de novo.</p>
        <div className="grid grid-cols-2 gap-2 rounded-xl bg-muted/60 p-3 font-mono text-sm">{recuperacao.map((c) => <span key={c}>{c}</span>)}</div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={async () => { try { await navigator.clipboard.writeText(texto); setCopiado(true); } catch { /* sem permissão */ } }}>{copiado ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}{copiado ? "Copiado" : "Copiar"}</Button>
          <Button variant="outline" onClick={() => { const a = document.createElement("a"); a.href = URL.createObjectURL(new Blob([texto], { type: "text/plain" })); a.download = "codigos-recuperacao-coffeeja.txt"; a.click(); }}><Download className="h-4 w-4" />Baixar</Button>
        </div>
        <Button size="lg" className="w-full" onClick={() => { if (sessaoNova) entrar(sessaoNova); onConcluido(); }}>Já guardei, continuar</Button>
      </div>
    );
  }

  if (!info) {
    return (
      <div className="space-y-3">
        <p className="text-sm text-muted-foreground">Você vai usar um aplicativo autenticador (Google Authenticator, Microsoft Authenticator, Authy...) para gerar um código a cada login.</p>
        <Button onClick={() => iniciar.mutate()} disabled={iniciar.isPending}><KeyRound className="h-4 w-4" />Começar configuração</Button>
      </div>
    );
  }

  return (
    <form className="space-y-4" onSubmit={(e: FormEvent) => { e.preventDefault(); confirmar.mutate(); }}>
      <ol className="list-decimal space-y-1 pl-5 text-sm">
        <li>Abra o aplicativo autenticador e escaneie o QR code (ou digite a chave).</li>
        <li>Digite abaixo o código de 6 dígitos que o aplicativo mostrar.</li>
      </ol>
      <div className="flex flex-col items-center gap-2 rounded-2xl bg-white p-3">
        {qr && <img src={qr} alt="QR code para o aplicativo autenticador" width={200} height={200} />}
        <code className="break-all text-center text-xs text-neutral-700">{info.segredo}</code>
      </div>
      <Field label="Código de 6 dígitos"><Input value={codigo} onChange={(e) => setCodigo(e.target.value.replace(/\D/g, "").slice(0, 6))} inputMode="numeric" autoComplete="one-time-code" placeholder="000000" /></Field>
      {confirmar.error && <p role="alert" className="text-sm font-medium text-destructive">{confirmar.error.message}</p>}
      <Button size="lg" className="w-full" disabled={codigo.length !== 6 || confirmar.isPending}>{confirmar.isPending ? "Verificando..." : "Ativar"}</Button>
    </form>
  );
}

/** Tela obrigatória para administradores sem 2FA. */
export function TelaMfaObrigatoria() {
  const { sair } = useAuth();
  return (
    <AuthLayout titulo="Proteja a conta de administrador" subtitulo="A verificação em duas etapas é obrigatória para acessar o painel.">
      <Configurar2fa onConcluido={() => location.assign("/admin")} />
      <Button variant="ghost" className="w-full" onClick={sair}>Sair</Button>
    </AuthLayout>
  );
}

/** Cartão de segurança e privacidade: senha, 2FA opcional e direitos do titular (LGPD). */
export function SegurancaCard() {
  const { usuario, entrar } = useAuth();
  const toast = useToast();
  const [aba, setAba] = useState<null | "senha" | "2fa" | "2fa-off" | "lgpd">(null);
  const [atual, setAtual] = useState("");
  const [nova, setNova] = useState("");
  const [senha2fa, setSenha2fa] = useState("");
  const [cod2fa, setCod2fa] = useState("");
  const [confirmaExclusao, setConfirmaExclusao] = useState(false);

  const trocar = useMutation({
    mutationFn: () => api.seguranca.trocarSenha(atual, nova),
    onSuccess: (s) => { entrar(s); toast("Senha alterada. Outras sessões foram encerradas."); setAtual(""); setNova(""); setAba(null); },
    onError: (e) => toast(e.message, true),
  });
  const desativar2fa = useMutation({
    mutationFn: () => api.seguranca.totpDesativar(senha2fa, cod2fa),
    onSuccess: () => { toast("Verificação em duas etapas desativada."); setAba(null); location.reload(); },
    onError: (e) => toast(e.message, true),
  });
  const exclusao = useMutation({
    mutationFn: api.lgpd.solicitarExclusao,
    onSuccess: (r) => { toast(r.detail); setConfirmaExclusao(false); setAba(null); },
    onError: (e) => toast(e.message, true),
  });
  const baixar = useMutation({ mutationFn: api.lgpd.baixarMeusDados, onError: (e) => toast(e instanceof ApiError ? e.message : "Erro ao baixar.", true) });

  return (
    <Card>
      <CardHeader><CardTitle>Segurança e privacidade</CardTitle><ShieldCheck className="h-5 w-5 text-success" /></CardHeader>
      <CardContent className="space-y-3">
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" size="sm" onClick={() => setAba(aba === "senha" ? null : "senha")}>Alterar senha</Button>
          {usuario?.totpAtivo
            ? usuario.papel !== "Admin" && <Button variant="outline" size="sm" onClick={() => setAba(aba === "2fa-off" ? null : "2fa-off")}>Desativar 2 etapas</Button>
            : <Button variant="outline" size="sm" onClick={() => setAba(aba === "2fa" ? null : "2fa")}>Ativar verificação em 2 etapas</Button>}
          <Button variant="outline" size="sm" onClick={() => setAba(aba === "lgpd" ? null : "lgpd")}>Meus dados (LGPD)</Button>
          {usuario?.totpAtivo && <Badge tone="success"><ShieldCheck className="h-3.5 w-3.5" />2 etapas ativa</Badge>}
        </div>

        {aba === "senha" && (
          <form className="space-y-3" onSubmit={(e: FormEvent) => { e.preventDefault(); if (!erroSenha(nova)) trocar.mutate(); }}>
            <Field label="Senha atual"><Input type="password" autoComplete="current-password" value={atual} onChange={(e) => setAtual(e.target.value)} /></Field>
            <Field label="Nova senha" hint="Mínimo de 10 caracteres, com letras e número ou símbolo" error={nova ? erroSenha(nova) : undefined}><Input type="password" autoComplete="new-password" value={nova} onChange={(e) => setNova(e.target.value)} /></Field>
            <Button disabled={!atual || !nova || !!erroSenha(nova) || trocar.isPending}>Salvar nova senha</Button>
          </form>
        )}
        {aba === "2fa" && <Configurar2fa onConcluido={() => { setAba(null); location.reload(); }} />}
        {aba === "2fa-off" && (
          <form className="space-y-3" onSubmit={(e: FormEvent) => { e.preventDefault(); desativar2fa.mutate(); }}>
            <Field label="Senha"><Input type="password" value={senha2fa} onChange={(e) => setSenha2fa(e.target.value)} /></Field>
            <Field label="Código do aplicativo"><Input value={cod2fa} inputMode="numeric" onChange={(e) => setCod2fa(e.target.value.replace(/\D/g, "").slice(0, 6))} /></Field>
            <Button variant="destructive" disabled={!senha2fa || cod2fa.length !== 6 || desativar2fa.isPending}>Desativar</Button>
          </form>
        )}
        {aba === "lgpd" && (
          <div className="space-y-3 rounded-xl bg-muted/60 p-3 text-sm">
            <p>Você pode baixar uma cópia dos seus dados ou pedir a exclusão deles. Registros fiscais e contábeis podem ser mantidos pelo prazo legal.</p>
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" size="sm" onClick={() => baixar.mutate()} disabled={baixar.isPending}><Download className="h-4 w-4" />Baixar meus dados</Button>
              {!confirmaExclusao
                ? <Button variant="destructive" size="sm" onClick={() => setConfirmaExclusao(true)}><ShieldAlert className="h-4 w-4" />Solicitar exclusão</Button>
                : <>
                    <Button variant="destructive" size="sm" disabled={exclusao.isPending} onClick={() => exclusao.mutate()}>Confirmar pedido de exclusão</Button>
                    <Button variant="ghost" size="sm" onClick={() => setConfirmaExclusao(false)}>Cancelar</Button>
                  </>}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
