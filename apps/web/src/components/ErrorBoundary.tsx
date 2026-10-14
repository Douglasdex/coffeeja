import { Component, type ReactNode } from "react";

/** Evita a "tela em branco": mostra o erro e uma saída (recarregar / limpar dados locais). */
export class ErrorBoundary extends Component<{ children: ReactNode }, { erro: Error | null }> {
  state = { erro: null as Error | null };

  static getDerivedStateFromError(erro: Error) {
    return { erro };
  }

  limparERecarregar = async () => {
    try {
      localStorage.removeItem("coffeeja.token");
      const regs = await navigator.serviceWorker?.getRegistrations();
      await Promise.all((regs ?? []).map((r) => r.unregister()));
      for (const k of await caches.keys()) await caches.delete(k);
    } catch { /* ignore */ }
    location.href = "/login";
  };

  render() {
    if (!this.state.erro) return this.props.children;
    return (
      <div style={{ maxWidth: 480, margin: "15vh auto", padding: 24, fontFamily: "system-ui, sans-serif" }}>
        <h1 style={{ fontSize: 22, marginBottom: 8 }}>Ops, algo deu errado</h1>
        <p style={{ color: "#6b7290", marginBottom: 16 }}>Não foi possível carregar esta tela. Tente recarregar; se persistir, limpe os dados locais.</p>
        <pre style={{ whiteSpace: "pre-wrap", background: "#fdebec", color: "#e5484d", padding: 12, borderRadius: 12, fontSize: 13 }}>{this.state.erro.message}</pre>
        <div style={{ display: "flex", gap: 8, marginTop: 16 }}>
          <button onClick={() => location.reload()} style={{ padding: "10px 16px", borderRadius: 12, border: 0, background: "#e8612d", color: "#fff", fontWeight: 600 }}>Recarregar</button>
          <button onClick={this.limparERecarregar} style={{ padding: "10px 16px", borderRadius: 12, border: "1px solid #ccc", background: "#fff" }}>Limpar dados e entrar</button>
        </div>
      </div>
    );
  }
}
