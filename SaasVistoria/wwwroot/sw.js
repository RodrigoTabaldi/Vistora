/* Service worker do Vistora.

   O shell é servido em **network-first**: em campo, com rede, o vistoriador sempre recebe a
   versão atual do app; sem rede, cai para o cache e continua funcionando. Cache-first causava
   mistura de versões (HTML novo + JS antigo) e telas que não respondiam ao clique.

   Chamadas /api nunca passam pelo cache — as gravações offline vão para a fila em vistoria.js. */
const CACHE = 'vistora-v3';
const SHELL = ['/', '/app.css', '/app.js', '/vistoria.js', '/assinar', '/assinar.js', '/manifest.webmanifest'];

self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE).then(c => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);
  if (event.request.method !== 'GET' || url.origin !== location.origin) return;
  if (url.pathname.startsWith('/api/')) return;

  event.respondWith(
    fetch(event.request)
      .then(response => {
        if (response.ok) {
          const copy = response.clone();
          caches.open(CACHE).then(c => c.put(event.request, copy));
        }
        return response;
      })
      .catch(async () => await caches.match(event.request) || await caches.match('/'))
  );
});
