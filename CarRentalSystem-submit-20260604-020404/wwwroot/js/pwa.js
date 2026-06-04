(function () {
    if ('serviceWorker' in navigator) {
        window.addEventListener('load', function () {
            navigator.serviceWorker.register('/sw.js').catch(function () { });
        });
    }

    var deferredPrompt;
    var installButton;

    window.addEventListener('beforeinstallprompt', function (event) {
        event.preventDefault();
        deferredPrompt = event;

        if (!installButton) {
            if (!document.getElementById('pwa-install-style')) {
                var style = document.createElement('style');
                style.id = 'pwa-install-style';
                style.textContent = [
                    '.pwa-install-btn{position:fixed;left:max(12px,env(safe-area-inset-left));bottom:max(84px,env(safe-area-inset-bottom));z-index:9997;font-size:.82rem;max-width:calc(100vw - 24px);white-space:nowrap;}',
                    '@media (min-width:768px){.pwa-install-btn{left:16px;bottom:16px;font-size:.875rem;}}'
                ].join('');
                document.head.appendChild(style);
            }

            installButton = document.createElement('button');
            installButton.type = 'button';
            installButton.className = 'btn btn-primary rounded-pill shadow fw-bold px-3 py-2 pwa-install-btn';
            installButton.setAttribute('aria-label', 'Cai app');
            installButton.innerHTML = '<i class="bi bi-phone me-1"></i>Cai app';
            document.body.appendChild(installButton);

            installButton.addEventListener('click', function () {
                if (!deferredPrompt) return;
                deferredPrompt.prompt();
                deferredPrompt.userChoice.finally(function () {
                    deferredPrompt = null;
                    installButton.remove();
                    installButton = null;
                });
            });
        }
    });

    window.addEventListener('appinstalled', function () {
        deferredPrompt = null;
        if (installButton) {
            installButton.remove();
            installButton = null;
        }
    });
})();
