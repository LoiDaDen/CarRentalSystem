const CACHE_NAME = 'carrental-pwa-v1';
const APP_SHELL = [
    '/',
    '/offline.html',
    '/css/site.css',
    '/js/site.js',
    '/js/pwa.js',
    '/images/car_rental_logo.png',
    '/favicon.ico',
    '/icons/pwa-192.png',
    '/icons/pwa-512.png',
    '/icons/maskable-512.png'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(APP_SHELL))
            .catch(() => undefined)
    );
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))))
    );
    self.clients.claim();
});

self.addEventListener('fetch', event => {
    const request = event.request;
    if (request.method !== 'GET') return;

    const url = new URL(request.url);
    if (url.origin !== self.location.origin) return;

    if (request.mode === 'navigate') {
        event.respondWith(
            fetch(request)
                .then(response => response)
                .catch(() => caches.match('/offline.html'))
        );
        return;
    }

    const isStaticAsset = /\.(css|js|png|jpg|jpeg|webp|gif|ico|svg|woff2?)$/i.test(url.pathname);
    if (!isStaticAsset) return;

    event.respondWith(
        caches.match(request).then(cached => {
            if (cached) return cached;
            return fetch(request).then(response => {
                const copy = response.clone();
                caches.open(CACHE_NAME).then(cache => cache.put(request, copy));
                return response;
            });
        })
    );
});
