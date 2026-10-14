export const moeda = (v: number) => v.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });

/** "2026-10-20" (ou ISO completo) → "20/10". Evita new Date() em datas puras para não deslocar pelo fuso. */
export function dataCurta(iso: string) {
  const [a, m, d] = iso.slice(0, 10).split("-");
  return a && m && d ? `${d}/${m}` : iso;
}
export function dataLonga(iso: string) {
  const [a, m, d] = iso.slice(0, 10).split("-").map(Number);
  const dt = new Date(a, m - 1, d);
  return dt.toLocaleDateString("pt-BR", { weekday: "short", day: "2-digit", month: "short" }).replace(".", "");
}
export const hora = (t: string) => t.slice(0, 5);

/** ISO em UTC (sem "Z" vindo do servidor) → "14/10 12:42" no fuso do navegador. */
export function dataHora(iso: string) {
  const d = new Date(iso.endsWith("Z") || /[+-]\d\d:\d\d$/.test(iso) ? iso : iso + "Z");
  return `${d.toLocaleDateString("pt-BR", { day: "2-digit", month: "2-digit" })} ${d.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })}`;
}

export function haQuanto(iso: string) {
  const s = Math.max(0, (Date.now() - new Date(iso.endsWith("Z") ? iso : iso + "Z").getTime()) / 1000);
  if (s < 60) return "agora";
  if (s < 3600) return `há ${Math.floor(s / 60)} min`;
  if (s < 86400) return `há ${Math.floor(s / 3600)} h`;
  return `há ${Math.floor(s / 86400)} d`;
}

export const soDigitos = (s: string) => s.replace(/\D/g, "");

export function hojeIso() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
