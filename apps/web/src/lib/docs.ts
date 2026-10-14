/** Máscaras e validações de documentos brasileiros (a validação definitiva é feita no servidor). */
export const digitos = (s: string) => s.replace(/\D/g, "");

export function mascaraCpf(v: string) {
  const d = digitos(v).slice(0, 11);
  return d.replace(/(\d{3})(\d)/, "$1.$2").replace(/(\d{3})(\d)/, "$1.$2").replace(/(\d{3})(\d{1,2})$/, "$1-$2");
}

export function mascaraCnpj(v: string) {
  const d = digitos(v).slice(0, 14);
  return d.replace(/(\d{2})(\d)/, "$1.$2").replace(/(\d{3})(\d)/, "$1.$2").replace(/(\d{3})(\d)/, "$1/$2").replace(/(\d{4})(\d{1,2})$/, "$1-$2");
}

export function mascaraTelefone(v: string) {
  const d = digitos(v).slice(0, 11);
  if (d.length <= 2) return d.length ? `(${d}` : "";
  if (d.length <= 6) return `(${d.slice(0, 2)}) ${d.slice(2)}`;
  if (d.length <= 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
  return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
}

export const mascaraCep = (v: string) => digitos(v).slice(0, 8).replace(/(\d{5})(\d)/, "$1-$2");

export function cpfValido(v: string) {
  const c = digitos(v);
  if (c.length !== 11 || /^(\d)\1+$/.test(c)) return false;
  const dv = (n: number) => {
    let s = 0;
    for (let i = 0; i < n; i++) s += Number(c[i]) * (n + 1 - i);
    const r = (s * 10) % 11;
    return r === 10 ? 0 : r;
  };
  return dv(9) === Number(c[9]) && dv(10) === Number(c[10]);
}

export function cnpjValido(v: string) {
  const c = digitos(v);
  if (c.length !== 14 || /^(\d)\1+$/.test(c)) return false;
  const dv = (pesos: number[]) => {
    const s = pesos.reduce((a, p, i) => a + Number(c[i]) * p, 0) % 11;
    return s < 2 ? 0 : 11 - s;
  };
  return dv([5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]) === Number(c[12]) && dv([6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]) === Number(c[13]);
}

/** Telefone com DDD; celular (WhatsApp) exige 11 dígitos começando com 9 após o DDD. */
export const telefoneValido = (v: string) => [10, 11].includes(digitos(v).length);
export const celularValido = (v: string) => digitos(v).length === 11 && digitos(v)[2] === "9";
export const emailValido = (v: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v);
