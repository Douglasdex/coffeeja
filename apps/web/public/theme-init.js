// Aplica o tema antes da primeira pintura (evita "flash" claro). Arquivo externo para permitir CSP sem 'unsafe-inline'.
try {
  var t = localStorage.getItem("coffeeja.tema");
  if (t === "dark" || (!t && matchMedia("(prefers-color-scheme: dark)").matches)) document.documentElement.classList.add("dark");
} catch (e) {}
