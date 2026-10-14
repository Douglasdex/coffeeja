import type { AcaoLink } from "@coffeeja/api-client";
import { Button, Card, CardContent } from "@coffeeja/ui";
import { Check, Copy, MessageCircle, X } from "lucide-react";
import { useState } from "react";

/** Resultado de uma ação que gera link: abre o WhatsApp com a mensagem pronta ou copia o link. */
export function AcaoLinkCard({ titulo, acao, onFechar }: { titulo: string; acao: AcaoLink; onFechar: () => void }) {
  const [copiado, setCopiado] = useState(false);
  async function copiar() {
    try { await navigator.clipboard.writeText(acao.link); setCopiado(true); setTimeout(() => setCopiado(false), 2500); } catch { /* sem permissão */ }
  }
  return (
    <Card className="border-success/40"><CardContent className="space-y-3">
      <div className="flex items-start justify-between gap-2">
        <div className="font-semibold">{titulo}</div>
        <button onClick={onFechar} aria-label="Fechar" className="text-muted-foreground"><X className="h-5 w-5" /></button>
      </div>
      <p className="whitespace-pre-wrap rounded-xl bg-muted/60 p-3 text-sm">{acao.mensagem}</p>
      <div className="flex flex-wrap gap-2">
        <Button asChild variant="success"><a href={acao.whatsappUrl} target="_blank" rel="noopener noreferrer"><MessageCircle className="h-4 w-4" />Enviar pelo WhatsApp</a></Button>
        <Button variant="outline" onClick={copiar}>{copiado ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}{copiado ? "Copiado" : "Copiar link"}</Button>
      </div>
      <p className="text-xs text-muted-foreground">O WhatsApp abre com a mensagem pronta; é só tocar em enviar. O link vale por 7 dias e só pode ser usado uma vez.</p>
    </CardContent></Card>
  );
}
