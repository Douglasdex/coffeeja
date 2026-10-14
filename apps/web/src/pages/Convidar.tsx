import { api, type ConviteResultado } from "@coffeeja/api-client";
import { Badge, Button, Card, CardContent, CardHeader, CardTitle, EmptyState, Field, Input, PageTitle } from "@coffeeja/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, Copy, Gift, MessageCircle, Send, Share2 } from "lucide-react";
import { useState, type FormEvent } from "react";
import { Carregando, ErroBox } from "../components/common";
import { celularValido, mascaraTelefone } from "../lib/docs";
import { haQuanto } from "../lib/format";
import { useToast } from "../lib/toast";

/** Convite por WhatsApp — disponível para qualquer perfil. */
export function Convidar() {
  const qc = useQueryClient();
  const toast = useToast();
  const { data, isLoading, error } = useQuery({ queryKey: ["convites"], queryFn: api.convites.meus });
  const [tel, setTel] = useState("");
  const [nome, setNome] = useState("");
  const [tentou, setTentou] = useState(false);
  const [copiado, setCopiado] = useState(false);
  const [ultimo, setUltimo] = useState<ConviteResultado | null>(null);

  const enviar = useMutation({
    mutationFn: () => api.convites.criar(tel, nome),
    onSuccess: (r) => {
      setUltimo(r);
      setTel(""); setNome(""); setTentou(false);
      window.open(r.whatsappUrl, "_blank", "noopener,noreferrer");
      void qc.invalidateQueries({ queryKey: ["convites"] });
    },
    onError: (e) => toast(e.message, true),
  });

  async function compartilhar() {
    if (!data) return;
    try {
      if (navigator.share) await navigator.share({ title: "CoffeeJá", text: data.mensagemPessoal });
      else { await navigator.clipboard.writeText(data.mensagemPessoal); setCopiado(true); setTimeout(() => setCopiado(false), 2500); }
    } catch { /* usuário cancelou */ }
  }
  async function copiarLink() {
    if (!data) return;
    try { await navigator.clipboard.writeText(data.linkPessoal); setCopiado(true); setTimeout(() => setCopiado(false), 2500); } catch { /* sem permissão */ }
  }

  if (isLoading) return <Carregando />;
  if (error || !data) return <ErroBox erro={error} />;
  const telOk = celularValido(tel);

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <PageTitle title="Convidar para o CoffeeJá" subtitle="Indique outras empresas, pessoas e buffets." />

      <div className="grid grid-cols-2 gap-3">
        <Card className="p-4"><div className="text-sm text-muted-foreground">Convites enviados</div><div className="text-3xl font-bold">{data.enviados}</div></Card>
        <Card className="p-4"><div className="text-sm text-muted-foreground">Cadastros feitos</div><div className="text-3xl font-bold text-success">{data.cadastrados}</div></Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Convite por WhatsApp</CardTitle><Gift className="h-5 w-5 text-primary" /></CardHeader>
        <CardContent>
          <form className="space-y-3" onSubmit={(e: FormEvent) => { e.preventDefault(); setTentou(true); if (telOk) enviar.mutate(); }}>
            <Field label="Celular de quem será convidado" error={tentou && !telOk ? "Informe um celular com DDD" : undefined}>
              <Input value={tel} inputMode="tel" onChange={(e) => setTel(mascaraTelefone(e.target.value))} placeholder="(11) 91234-5678" />
            </Field>
            <Field label="Nome (opcional)" hint="Usado na saudação da mensagem"><Input value={nome} onChange={(e) => setNome(e.target.value)} /></Field>
            <Button size="lg" className="w-full" variant="success" disabled={enviar.isPending}><MessageCircle className="h-5 w-5" />Enviar convite pelo WhatsApp</Button>
          </form>
          {ultimo && <p className="mt-3 text-sm text-muted-foreground">Convite registrado. Se o WhatsApp não abriu, <a className="font-semibold text-primary underline" href={ultimo.whatsappUrl} target="_blank" rel="noopener noreferrer">toque aqui</a>.</p>}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Mensagem do convite</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <p className="whitespace-pre-wrap rounded-xl bg-muted/60 p-3 text-sm">{data.mensagemPessoal}</p>
          <div className="flex flex-wrap gap-2">
            <Button onClick={compartilhar}><Share2 className="h-4 w-4" />Compartilhar</Button>
            <Button variant="outline" onClick={copiarLink}>{copiado ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}{copiado ? "Copiado" : "Copiar meu link"}</Button>
          </div>
          <p className="text-xs text-muted-foreground">Seu link pessoal registra automaticamente quem se cadastrar por ele.</p>
        </CardContent>
      </Card>

      <section className="space-y-2">
        <h2 className="text-base font-semibold">Convites enviados</h2>
        {data.convites.length ? data.convites.map((c) => (
          <Card key={c.id} className="flex items-center justify-between gap-3 p-4">
            <div className="min-w-0">
              <div className="truncate font-medium">{c.nome || mascaraTelefone(c.telefone ?? "")}</div>
              <div className="text-xs text-muted-foreground">{c.nome ? `${mascaraTelefone(c.telefone ?? "")} · ` : ""}enviado {haQuanto(c.criadoEm)}</div>
            </div>
            {c.status === "Cadastrado" ? <Badge tone="success"><Check className="h-3.5 w-3.5" />Cadastrou-se</Badge> : <Badge tone="warning"><Send className="h-3.5 w-3.5" />Aguardando</Badge>}
          </Card>
        )) : <EmptyState icon={<Gift className="h-6 w-6" />} title="Nenhum convite ainda">Convide colegas, fornecedores e parceiros.</EmptyState>}
      </section>
    </div>
  );
}
