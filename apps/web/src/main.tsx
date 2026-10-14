import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { ErrorBoundary } from "./components/ErrorBoundary";
import { AuthProvider } from "./lib/auth";
import { RealtimeProvider } from "./lib/realtime";
import { ToastProvider } from "./lib/toast";
import "./styles.css";

const qc = new QueryClient({ defaultOptions: { queries: { staleTime: 10_000, retry: 1, refetchOnWindowFocus: true } } });

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <ErrorBoundary>
      <QueryClientProvider client={qc}>
        <BrowserRouter>
          <ToastProvider>
            <AuthProvider>
              <RealtimeProvider>
                <App />
              </RealtimeProvider>
            </AuthProvider>
          </ToastProvider>
        </BrowserRouter>
      </QueryClientProvider>
    </ErrorBoundary>
  </StrictMode>,
);

// Service worker só em produção. Em desenvolvimento removemos qualquer um registrado antes (evita página velha/em branco).
if ("serviceWorker" in navigator) {
  if (import.meta.env.PROD) {
    addEventListener("load", () => navigator.serviceWorker.register("/sw.js").catch(() => {}));
  } else {
    navigator.serviceWorker.getRegistrations().then((rs) => rs.forEach((r) => void r.unregister())).catch(() => {});
    caches?.keys().then((ks) => ks.forEach((k) => void caches.delete(k))).catch(() => {});
  }
}
