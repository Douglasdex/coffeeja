import type { Endereco } from "@coffeeja/api-client";
import { Field, Input } from "@coffeeja/ui";
import { useState } from "react";
import { mascaraCep } from "../lib/docs";

export const enderecoVazio: Endereco = { cep: "", logradouro: "", numero: "", complemento: "", bairro: "", cidade: "", uf: "" };

export const enderecoCompleto = (e: Endereco) =>
  e.cep.replace(/\D/g, "").length === 8 && !!e.logradouro.trim() && !!e.numero.trim() && !!e.bairro.trim() && !!e.cidade.trim() && e.uf.trim().length === 2;

/** Campos de endereço com preenchimento automático pelo CEP (ViaCEP). Se o serviço falhar, o usuário digita manualmente. */
export function EnderecoFields({ value, onChange }: { value: Endereco; onChange: (e: Endereco) => void }) {
  const [buscando, setBuscando] = useState(false);
  const [aviso, setAviso] = useState<string | null>(null);
  const set = (k: keyof Endereco, v: string) => onChange({ ...value, [k]: v });

  async function buscarCep(cepMascarado: string) {
    const cep = cepMascarado.replace(/\D/g, "");
    onChange({ ...value, cep: mascaraCep(cepMascarado) });
    setAviso(null);
    if (cep.length !== 8) return;
    setBuscando(true);
    try {
      const r = await fetch(`https://viacep.com.br/ws/${cep}/json/`);
      const d = await r.json();
      if (d.erro) setAviso("CEP não encontrado. Preencha o endereço manualmente.");
      else onChange({ ...value, cep: mascaraCep(cep), logradouro: d.logradouro || value.logradouro, bairro: d.bairro || value.bairro, cidade: d.localidade || value.cidade, uf: d.uf || value.uf });
    } catch {
      setAviso("Não foi possível consultar o CEP agora. Preencha manualmente.");
    } finally {
      setBuscando(false);
    }
  }

  return (
    <div className="grid grid-cols-6 gap-3">
      <Field label="CEP" className="col-span-3 sm:col-span-2" hint={buscando ? "Buscando..." : undefined} error={aviso ?? undefined}>
        <Input value={value.cep} onChange={(e) => void buscarCep(e.target.value)} inputMode="numeric" autoComplete="postal-code" placeholder="00000-000" required />
      </Field>
      <Field label="Logradouro" className="col-span-6 sm:col-span-4"><Input value={value.logradouro} onChange={(e) => set("logradouro", e.target.value)} autoComplete="address-line1" required /></Field>
      <Field label="Número" className="col-span-2"><Input value={value.numero} onChange={(e) => set("numero", e.target.value)} required /></Field>
      <Field label="Complemento" className="col-span-4"><Input value={value.complemento ?? ""} onChange={(e) => set("complemento", e.target.value)} /></Field>
      <Field label="Bairro" className="col-span-6 sm:col-span-3"><Input value={value.bairro} onChange={(e) => set("bairro", e.target.value)} required /></Field>
      <Field label="Cidade" className="col-span-4 sm:col-span-2"><Input value={value.cidade} onChange={(e) => set("cidade", e.target.value)} required /></Field>
      <Field label="UF" className="col-span-2 sm:col-span-1"><Input value={value.uf} maxLength={2} onChange={(e) => set("uf", e.target.value.toUpperCase())} required /></Field>
    </div>
  );
}
