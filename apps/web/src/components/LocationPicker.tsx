import { ApiError, api } from "@coffeeja/api-client";
import { Button, Field, Input, Spinner } from "@coffeeja/ui";
import { CheckCircle2, LocateFixed, Search } from "lucide-react";
import { useState } from "react";

export interface Local { endereco: string; cidade: string; uf: string; latitude: number | null; longitude: number | null }
export const localVazio: Local = { endereco: "", cidade: "", uf: "", latitude: null, longitude: null };
export const localValido = (l: Local) => l.latitude !== null && l.longitude !== null && !!l.cidade && l.uf.length === 2;

/** Define o local por endereço/CEP (geocodificação no servidor) ou pela localização do aparelho. */
export function LocationPicker({ value, onChange, rotulo = "Endereço do evento" }: { value: Local; onChange: (l: Local) => void; rotulo?: string }) {
  const [buscando, setBuscando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function buscar() {
    setErro(null);
    setBuscando(true);
    try {
      const c = await api.geo.buscar(value.endereco);
      onChange({ ...value, latitude: c.latitude, longitude: c.longitude, cidade: c.cidade ?? value.cidade, uf: c.uf ?? value.uf });
    } catch (e) {
      setErro(e instanceof ApiError && e.status === 404 ? "Não encontramos esse endereço. Inclua cidade e número ou informe o CEP." : "Não foi possível buscar agora.");
    } finally {
      setBuscando(false);
    }
  }

  function usarGps() {
    setErro(null);
    if (!navigator.geolocation) return setErro("Seu aparelho não permite localização.");
    setBuscando(true);
    navigator.geolocation.getCurrentPosition(
      (p) => { onChange({ ...value, latitude: p.coords.latitude, longitude: p.coords.longitude }); setBuscando(false); },
      () => { setErro("Permissão de localização negada."); setBuscando(false); },
      { timeout: 8000 },
    );
  }

  const ok = value.latitude !== null;
  return (
    <div className="space-y-3">
      <Field label={rotulo} hint="Rua, número, bairro e cidade — ou apenas o CEP">
        <div className="flex gap-2">
          <Input value={value.endereco} onChange={(e) => onChange({ ...value, endereco: e.target.value, latitude: null, longitude: null })}
            onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); if (value.endereco.length >= 4) void buscar(); } }}
            placeholder="Av. Paulista, 1000, São Paulo" autoComplete="street-address" />
          <Button type="button" variant="accent" size="icon" onClick={buscar} disabled={buscando || value.endereco.length < 4} aria-label="Buscar endereço">
            {buscando ? <Spinner className="border-white border-t-transparent" /> : <Search className="h-5 w-5" />}
          </Button>
        </div>
      </Field>
      <Button type="button" variant="ghost" size="sm" onClick={usarGps} disabled={buscando}><LocateFixed className="h-4 w-4" />Usar minha localização atual</Button>
      {erro && <p className="text-sm font-medium text-destructive">{erro}</p>}
      {ok && (
        <div className="space-y-3">
          <p className="flex items-center gap-1.5 text-sm font-medium text-success"><CheckCircle2 className="h-4 w-4" />Local confirmado</p>
          <div className="grid grid-cols-[1fr_5rem] gap-3">
            <Field label="Cidade"><Input value={value.cidade} onChange={(e) => onChange({ ...value, cidade: e.target.value })} /></Field>
            <Field label="UF"><Input value={value.uf} maxLength={2} onChange={(e) => onChange({ ...value, uf: e.target.value.toUpperCase() })} /></Field>
          </div>
        </div>
      )}
    </div>
  );
}
