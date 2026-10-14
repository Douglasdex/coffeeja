// Service worker mínimo: torna o app instalável e mostra push quando o backend passar a enviá-lo.
// Não faz cache de chamadas /api (dados sempre frescos).
const SHELL = "coffeeja-shell-v1";

self.addEventListener("install", (e) => {
  e.waitUntil(caches.open(SHELL).then((c) => c.addAll(["/", "/icon.svg"])).then(() => self.skipWaiting()));
});

self.addEventListener("activate", (e) => {
  e.waitUntil(
    caches.keys().then((keys) => Promise.all(keys.filter((k) => k !== SHELL).map((k) => caches.delete(k)))).then(() => self.clients.claim()),
  );
});

self.addEventListener("fetch", (e) => {
  const url = new URL(e.request.url);
  if (e.request.method !== "GET" || url.origin !== location.origin || url.pathname.startsWith("/api/")) return;
  // Navegação: rede primeiro, shell em cache como fallback offline.
  if (e.request.mode === "navigate") {
    e.respondWith(fetch(e.request).catch(() => caches.match("/")));
  }
});

self.addEventListener("push", (e) => {
  const d = e.data ? e.data.json() : { titulo: "CoffeeJá", texto: "Você tem uma novidade." };
  e.waitUntil(self.registration.showNotification(d.titulo, { body: d.texto, icon: "/icon.svg", data: { link: d.link || "/" } }));
});

self.addEventListener("notificationclick", (e) => {
  e.notification.close();
  e.waitUntil(clients.openWindow(e.notification.data.link));
});
