import { useCallback, useState } from "react";

export function useTema() {
  const [escuro, setEscuro] = useState(() => document.documentElement.classList.contains("dark"));
  const alternar = useCallback(() => {
    const novo = !document.documentElement.classList.contains("dark");
    document.documentElement.classList.toggle("dark", novo);
    try { localStorage.setItem("coffeeja.tema", novo ? "dark" : "light"); } catch { /* ignore */ }
    setEscuro(novo);
  }, []);
  return { escuro, alternar };
}
