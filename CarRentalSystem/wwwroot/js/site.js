(() => {
    const themeKey = 'ui_theme';
    const chatKey = 'carrental_chat_session_id';

    function setCookie(name, value, days) {
        const expires = new Date(Date.now() + days * 864e5).toUTCString();
        document.cookie = `${name}=${encodeURIComponent(value)}; expires=${expires}; path=/; SameSite=Lax`;
    }

    function applyTheme(theme) {
        document.documentElement.setAttribute('data-bs-theme', theme);
        localStorage.setItem(themeKey, theme);
        setCookie(themeKey, theme, 365);
        document.querySelectorAll('[data-theme-toggle] i').forEach(icon => {
            icon.className = theme === 'dark' ? 'bi bi-sun' : 'bi bi-moon-stars';
        });
    }

    function initTheme() {
        const cookieTheme = document.cookie.match(/(?:^|; )ui_theme=([^;]+)/)?.[1];
        const current = localStorage.getItem(themeKey)
            || (cookieTheme ? decodeURIComponent(cookieTheme) : null)
            || document.documentElement.getAttribute('data-bs-theme')
            || 'light';

        applyTheme(current);
        document.querySelectorAll('[data-theme-toggle]').forEach(btn => {
            btn.addEventListener('click', () => {
                const next = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
                applyTheme(next);
            });
        });
    }

    function getChatSessionId() {
        let id = localStorage.getItem(chatKey);
        if (!id) {
            id = `web_${Date.now()}_${Math.random().toString(36).slice(2)}`;
            localStorage.setItem(chatKey, id);
        }
        return id;
    }

    window.CarRentalUi = {
        getChatSessionId,
        getCulture: () => document.documentElement.lang || navigator.language || 'vi'
    };

    function initAdminTableSearch() {
        const input = document.querySelector('[data-admin-table-search]');
        if (!input) return;
        const rows = Array.from(document.querySelectorAll('main table tbody tr'));
        const wrapper = document.querySelector('[data-admin-global-search]');
        if (!rows.length) {
            if (wrapper) wrapper.style.display = 'none';
            return;
        }
        input.addEventListener('input', () => {
            const term = input.value.trim().toLowerCase();
            rows.forEach(row => {
                row.style.display = !term || row.innerText.toLowerCase().includes(term) ? '' : 'none';
            });
        });
    }

    window.sendChat = async function sendChat() {
        const input = document.getElementById('chat-input');
        const messages = document.getElementById('chat-messages');
        const text = input?.value.trim();
        if (!input || !messages || !text) return;

        const escape = value => value.replace(/</g, '&lt;').replace(/>/g, '&gt;');
        messages.innerHTML += `<div style="background:#2563eb;color:#fff;padding:10px 16px;border-radius:16px 16px 4px 16px;max-width:85%;margin-left:auto;font-size:.85rem;">${escape(text)}</div>`;
        input.value = '';
        messages.scrollTop = messages.scrollHeight;

        try {
            const response = await fetch('/api/Chat', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    message: text,
                    sessionId: getChatSessionId(),
                    culture: window.CarRentalUi.getCulture()
                })
            });
            const data = await response.json();
            messages.innerHTML += `<div style="background:linear-gradient(135deg,#e0e7ff,#ede9fe);color:#0f172a;padding:12px 16px;border-radius:16px 16px 16px 4px;max-width:85%;font-size:.85rem;line-height:1.5;white-space:pre-line;">${escape(data.reply || '')}</div>`;
        } catch {
            messages.innerHTML += '<div style="background:#fee2e2;padding:10px 16px;border-radius:16px;max-width:85%;font-size:.85rem;color:#dc2626;">Loi ket noi. Vui long thu lai.</div>';
        }
        messages.scrollTop = messages.scrollHeight;
    };

    document.addEventListener('DOMContentLoaded', () => {
        initTheme();
        initAdminTableSearch();
    });
})();
