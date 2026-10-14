import { api } from "@coffeeja/api-client";
import { Button, Card, CardContent } from "@coffeeja/ui";
import { BellOff, BellRing } from "lucide-react";
import { useEffect, useState } from "react";
import { useToast } from "../lib/toast";

type Estado = "carregando" | "indisponivel" | "bloqueado" | "desligado" | "ligado";

function chaveParaBytes(b64url: string) {
  const pad = "=".repeat((4 - (b64url.length % 4)) % 4);
  const raw = atob((b64url + pad).replace(/-/g, "+").replace(/_/g, "/"));
  return Uint8Array.from(raw, (c) => c.charCodeAt(0));
}
const b64url = (buf: ArrayBuffer | null) =>
  buf ? btoa(String.fromCharCode(...new Uint8Array(buf))).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "") : "";

/** Liga/desliga avisos no celular (Web Push). No iOS só funciona com o app instalado na tela inicial. */
export function PushToggle() {
  const toast = useToast();
  const [estado, setEstado] = useState<Estado>("carregando");
  const [ocupado, setOcupado] = useState(false);

  useEffect(() => {
    (async () => {
      if (!("serviceWorker" in navigator) || !("PushManager" in window) || !("Notification" in window)) return setEstado("indisponivel");
      try { await api.push.chave(); } catch { return setEstado("indisponivel"); }
      if (Notification.permission === "denied") return setEstado("bloqueado");
      const reg = await navigator.serviceWorker.ready;
      setEstado((await reg.pushManager.getSubscription()) ? "ligado" : "desligado");
    })().catch(() => setEstado("indisponivel"));
  }, []);

  async function ligar() {
    setOcupado(true);
    try {
      const perm = await Notification.requestPermission();
      if (perm !== "granted") return setEstado(perm === "denied" ? "bloqueado" : "desligado");
      const { chave } = await api.push.chave();
      const reg = await navigator.serviceWorker.ready;
      const sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: chaveParaBytes(chave) });
      await api.push.inscrever(sub.endpoint, b64url(sub.getKey("p256dh")), b64url(sub.getKey("auth")));
      setEstado("ligado");
      toast("Avisos ativados neste aparelho.");
    } catch (e) {
      toast(e instanceof Error ? e.message : "Não foi possível ativar os avisos.", true);
    } finally { setOcupado(false); }
  }

  async function desligar() {
    setOcupado(true);
    try {
      const reg = await navigator.serviceWorker.ready;
      const sub = await reg.pushManager.getSubscription();
      if (sub) { await api.push.cancelar(sub.endpoint).catch(() => {}); await sub.unsubscribe(); }
      setEstado("desligado");
    } finally { setOcupado(false); }
  }

  if (estado === "carregando") return null;
  return (
    <Card><CardContent className="flex items-center justify-between gap-3">
      <div className="min-w-0">
        <div className="font-semibold">Avisos no celular</div>
        <p className="text-sm text-muted-foreground">
          {estado === "indisponivel" && "Indisponível neste navegador. No iPhone, adicione o app à tela inicial primeiro."}
          {estado === "bloqueado" && "Bloqueado nas configurações do navegador."}
          {estado === "desligado" && "Receba propostas, mensagens e pedidos mesmo com o app fechado."}
          {estado === "ligado" && "Ativados neste aparelho."}
        </p>
      </div>
      {estado === "desligado" && <Button onClick={ligar} disabled={ocupado}><BellRing className="h-4 w-4" />Ativar</Button>}
      {estado === "ligado" && <Button variant="outline" onClick={desligar} disabled={ocupado}><BellOff className="h-4 w-4" />Desativar</Button>}
    </CardContent></Card>
  );
}
