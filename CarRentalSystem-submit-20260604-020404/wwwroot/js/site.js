(() => {
    const chatKey = 'carrental_chat_session_id';
    let pendingAiAction = null;

    function getChatSessionId() {
        let id = localStorage.getItem(chatKey);
        if (!id) {
            id = `web_${Date.now()}_${Math.random().toString(36).slice(2)}`;
            localStorage.setItem(chatKey, id);
        }
        return id;
    }

    window.CarRentalUi = {
        getChatSessionId
    };

    function escapeHtml(value) {
        return (value || '').toString().replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function formatMoney(value) {
        const number = Number(value || 0);
        return number.toLocaleString('vi-VN') + 'đ';
    }

    function appendChatBubble(messages, html, isUser) {
        const style = isUser
            ? 'margin-left:auto;background:#2563eb;color:#fff;border-radius:16px 16px 4px 16px;'
            : 'background:linear-gradient(135deg,#e0e7ff,#ede9fe);color:#0f172a;border-radius:16px 16px 16px 4px;';
        messages.innerHTML += `<div style="${style}padding:12px 16px;max-width:85%;font-size:.85rem;line-height:1.5;white-space:pre-line;">${html}</div>`;
        messages.scrollTop = messages.scrollHeight;
    }

    function flattenAiData(data) {
        if (!Array.isArray(data)) return [];
        return data.flatMap(item => Array.isArray(item) ? item : [item]).filter(Boolean);
    }

    function getCarFromItem(item) {
        if (!item || typeof item !== 'object') return null;
        const car = item.car || item;
        const carId = car.carId ?? car.carID ?? car.CarID;
        const name = car.name ?? car.carName ?? car.CarName;
        if (!carId || !name) return null;
        return { ...car, carId, name, reason: item.reason };
    }

    function appendDataCards(messages, data) {
        const cars = flattenAiData(data).map(getCarFromItem).filter(Boolean);
        if (!cars.length) return;

        const cards = cars.slice(0, 8).map((car, index) => {
            const location = car.location ?? car.Location ?? 'Chưa rõ khu vực';
            const seats = car.seats ?? car.Seats ?? '-';
            const fuel = car.fuelType ?? car.FuelType ?? '-';
            const transmission = car.transmission ?? car.Transmission ?? '-';
            const price = car.pricePerDay ?? car.PricePerDay ?? 0;
            const reason = car.reason ? `<div style="margin-top:6px;color:#475569;">${escapeHtml(car.reason)}</div>` : '';
            return `
                <div style="background:#fff;border:1px solid #e2e8f0;border-radius:10px;padding:10px;margin-top:8px;box-shadow:0 1px 4px rgba(15,23,42,.06);">
                    <div style="display:flex;justify-content:space-between;gap:8px;align-items:flex-start;">
                        <div>
                            <div style="font-weight:700;color:#0f172a;">${index + 1}. ${escapeHtml(car.name)}</div>
                            <div style="font-size:.78rem;color:#64748b;margin-top:3px;">${escapeHtml(location)} · ${seats} chỗ · ${escapeHtml(fuel)} · ${escapeHtml(transmission)}</div>
                        </div>
                        <div style="white-space:nowrap;color:#0f766e;font-weight:700;font-size:.8rem;">${formatMoney(price)}/ngày</div>
                    </div>
                    ${reason}
                    <div style="display:flex;gap:6px;flex-wrap:wrap;margin-top:10px;">
                        <button type="button" onclick="sendAiQuickMessage('Xem chi tiết xe ${car.carId}')" style="border:1px solid #bfdbfe;background:#eff6ff;color:#1d4ed8;border-radius:8px;padding:6px 8px;font-size:.75rem;cursor:pointer;">Xem chi tiết</button>
                        <button type="button" onclick="sendAiQuickMessage('Tính giá xe ${car.carId}')" style="border:1px solid #bbf7d0;background:#f0fdf4;color:#15803d;border-radius:8px;padding:6px 8px;font-size:.75rem;cursor:pointer;">Tính giá</button>
                        <button type="button" onclick="sendAiQuickMessage('Đặt xe ${car.carId}')" style="border:1px solid #fed7aa;background:#fff7ed;color:#c2410c;border-radius:8px;padding:6px 8px;font-size:.75rem;cursor:pointer;">Đặt xe</button>
                    </div>
                </div>`;
        }).join('');

        messages.innerHTML += `<div style="max-width:95%;font-size:.85rem;">${cards}</div>`;
        messages.scrollTop = messages.scrollHeight;
    }

    function getQuoteFromItem(item) {
        if (!item || typeof item !== 'object') return null;
        const carId = item.carId ?? item.CarId;
        const total = item.estimatedTotal ?? item.EstimatedTotal;
        if (!carId || total === undefined) return null;
        return item;
    }

    function getComparisonFromItem(item) {
        if (!item || typeof item !== 'object') return null;
        if ((item.type ?? item.Type) !== 'carComparison') return null;
        return item;
    }

    function appendSmartDataCards(messages, data) {
        const flat = flattenAiData(data);
        let html = '';

        flat.map(getComparisonFromItem).filter(Boolean).forEach(comparison => {
            const comparedCars = comparison.cars ?? comparison.Cars ?? [];
            const rows = comparedCars.map((car, index) => {
                const name = car.name ?? car.Name;
                const seats = car.seats ?? car.Seats ?? '-';
                const price = car.pricePerDay ?? car.PricePerDay ?? 0;
                const location = car.location ?? car.Location ?? 'Chưa rõ';
                return `<tr>
                    <td style="padding:7px 6px;border-bottom:1px solid #e2e8f0;">${index + 1}. ${escapeHtml(name)}</td>
                    <td style="padding:7px 6px;border-bottom:1px solid #e2e8f0;text-align:center;">${seats}</td>
                    <td style="padding:7px 6px;border-bottom:1px solid #e2e8f0;white-space:nowrap;">${formatMoney(price)}</td>
                    <td style="padding:7px 6px;border-bottom:1px solid #e2e8f0;">${escapeHtml(location)}</td>
                </tr>`;
            }).join('');
            html += `<div style="background:#fff;border:1px solid #c7d2fe;border-radius:10px;padding:10px;margin-top:8px;box-shadow:0 1px 4px rgba(15,23,42,.06);">
                <div style="font-weight:700;color:#1e3a8a;margin-bottom:8px;">Bảng so sánh nhanh</div>
                <table style="width:100%;border-collapse:collapse;font-size:.76rem;color:#0f172a;">
                    <thead><tr style="color:#64748b;text-align:left;"><th>Xe</th><th>Chỗ</th><th>Giá/ngày</th><th>Khu vực</th></tr></thead>
                    <tbody>${rows}</tbody>
                </table>
            </div>`;
        });

        flat.map(getQuoteFromItem).filter(Boolean).forEach(quote => {
            const carId = quote.carId ?? quote.CarId;
            const carName = quote.carName ?? quote.CarName ?? `Xe ${carId}`;
            const rental = quote.rentalAmount ?? quote.RentalAmount ?? 0;
            const driverFee = quote.driverFee ?? quote.DriverFee ?? 0;
            const platformFee = quote.platformFee ?? quote.PlatformFee ?? 0;
            const deposit = quote.deposit ?? quote.Deposit ?? 0;
            const total = quote.estimatedTotal ?? quote.EstimatedTotal ?? 0;
            html += `<div style="background:#fff;border:1px solid #bbf7d0;border-radius:10px;padding:10px;margin-top:8px;box-shadow:0 1px 4px rgba(15,23,42,.06);">
                <div style="font-weight:700;color:#166534;margin-bottom:8px;">Báo giá dự kiến - ${escapeHtml(carName)}</div>
                <div style="display:grid;grid-template-columns:1fr auto;gap:6px;font-size:.8rem;color:#334155;">
                    <span>Tiền thuê</span><strong>${formatMoney(rental)}</strong>
                    <span>Phí tài xế</span><strong>${formatMoney(driverFee)}</strong>
                    <span>Phí nền tảng</span><strong>${formatMoney(platformFee)}</strong>
                    <span>Tiền cọc</span><strong>${formatMoney(deposit)}</strong>
                    <span style="border-top:1px solid #dcfce7;padding-top:6px;">Tổng dự kiến</span><strong style="border-top:1px solid #dcfce7;padding-top:6px;color:#15803d;">${formatMoney(total)}</strong>
                </div>
                <button type="button" onclick="sendAiQuickMessage('Đặt xe ${carId}')" style="margin-top:10px;border:1px solid #bbf7d0;background:#f0fdf4;color:#15803d;border-radius:8px;padding:7px 10px;font-size:.78rem;font-weight:700;cursor:pointer;">Đặt xe này</button>
            </div>`;
        });

        if (html) {
            messages.innerHTML += `<div style="max-width:95%;font-size:.85rem;">${html}</div>`;
            messages.scrollTop = messages.scrollHeight;
        }
        appendDataCards(messages, data);
    }

    function appendSuggestedReplies(messages, replies) {
        if (!Array.isArray(replies) || !replies.length) return;
        const buttons = replies.slice(0, 4).map(reply =>
            `<button type="button" onclick="sendAiQuickMessage('${escapeHtml(reply).replace(/'/g, '&#39;')}')" style="border:1px solid #c7d2fe;background:#eef2ff;color:#3730a3;border-radius:999px;padding:7px 10px;font-size:.76rem;cursor:pointer;">${escapeHtml(reply)}</button>`
        ).join('');
        messages.innerHTML += `<div style="display:flex;gap:6px;flex-wrap:wrap;max-width:95%;margin-top:8px;">${buttons}</div>`;
        messages.scrollTop = messages.scrollHeight;
    }

    function appendPendingAction(messages, pendingAction) {
        pendingAiAction = pendingAction;
        messages.innerHTML += `
            <div style="background:#fff7ed;border:1px solid #fed7aa;color:#7c2d12;padding:12px 14px;border-radius:12px;max-width:90%;font-size:.85rem;">
                <div style="font-weight:700;margin-bottom:6px;">Cần xác nhận</div>
                <div style="line-height:1.45;margin-bottom:10px;">${escapeHtml(pendingAction.summary)}</div>
                <div style="display:flex;gap:8px;">
                    <button type="button" onclick="confirmAiAction()" style="border:none;background:#16a34a;color:white;border-radius:8px;padding:8px 12px;font-weight:600;cursor:pointer;">Xác nhận</button>
                    <button type="button" onclick="cancelAiAction()" style="border:1px solid #fdba74;background:white;color:#9a3412;border-radius:8px;padding:8px 12px;font-weight:600;cursor:pointer;">Hủy</button>
                </div>
            </div>`;
        messages.scrollTop = messages.scrollHeight;
    }

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

    async function postAiMessage(payload) {
        const response = await fetch('/api/ai/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                sessionId: getChatSessionId(),
                ...payload
            })
        });
        return response.json();
    }

    async function sendMessageText(text) {
        const messages = document.getElementById('chat-messages');
        if (!messages || !text) return;

        appendChatBubble(messages, escapeHtml(text), true);
        appendChatBubble(messages, 'Đang xử lý...', false);
        const loadingNode = messages.lastElementChild;

        try {
            const data = await postAiMessage({ message: text });
            loadingNode?.remove();
            appendChatBubble(messages, escapeHtml(data.reply || ''), false);
            appendSmartDataCards(messages, data.data);
            appendSuggestedReplies(messages, data.suggestedReplies);
            if (data.pendingAction) appendPendingAction(messages, data.pendingAction);
        } catch {
            loadingNode?.remove();
            appendChatBubble(messages, '<span style="color:#dc2626;">Lỗi kết nối. Vui lòng thử lại.</span>', false);
        }
    }

    window.sendChat = async function sendChat() {
        const input = document.getElementById('chat-input');
        const text = input?.value.trim();
        if (!input || !text) return;
        input.value = '';
        await sendMessageText(text);
    };

    window.sendAiQuickMessage = async function sendAiQuickMessage(text) {
        await sendMessageText(text);
    };

    window.confirmAiAction = async function confirmAiAction() {
        if (!pendingAiAction) return;
        const messages = document.getElementById('chat-messages');
        appendChatBubble(messages, 'Tôi xác nhận thao tác này.', true);
        const confirmationId = pendingAiAction.confirmationId;
        pendingAiAction = null;

        try {
            const data = await postAiMessage({ message: '', confirmationId });
            appendChatBubble(messages, escapeHtml(data.reply || ''), false);
            appendSmartDataCards(messages, data.data);
            appendSuggestedReplies(messages, data.suggestedReplies);
            if (data.pendingAction) appendPendingAction(messages, data.pendingAction);
        } catch {
            appendChatBubble(messages, '<span style="color:#dc2626;">Không thể xác nhận thao tác. Vui lòng thử lại.</span>', false);
        }
    };

    window.cancelAiAction = function cancelAiAction() {
        pendingAiAction = null;
        const messages = document.getElementById('chat-messages');
        appendChatBubble(messages, 'Đã hủy thao tác đang chờ xác nhận.', false);
    };

    document.addEventListener('DOMContentLoaded', () => {
        initAdminTableSearch();
    });
})();
