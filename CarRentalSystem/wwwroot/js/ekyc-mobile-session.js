(function () {
    if (window.DriveGoEkycMobileSession) return;

    const api = { initAll, init };
    window.DriveGoEkycMobileSession = api;
    document.addEventListener('DOMContentLoaded', initAll);

    function initAll(root = document) {
        const scope = root && typeof root.querySelectorAll === 'function' ? root : document;
        scope.querySelectorAll('[data-ekyc-mobile-session]').forEach(init);
    }

    function init(root) {
        if (!root || root.dataset.ekycMobileReady === 'true') return;
        root.dataset.ekycMobileReady = 'true';

        const createButton = root.querySelector('[data-ekyc-create-mobile]');
        const qrImage = root.querySelector('[data-ekyc-qr-image]');
        const link = root.querySelector('[data-ekyc-mobile-url]');
        const status = root.querySelector('[data-ekyc-mobile-status]');
        const countdown = root.querySelector('[data-ekyc-mobile-countdown]');
        const message = root.querySelector('[data-ekyc-mobile-message]');
        const warnings = root.querySelector('[data-ekyc-mobile-warnings]');

        let sessionId = null;
        let expiresAt = null;
        let pollTimer = null;
        let countdownTimer = null;

        if (!createButton) return;

        createButton.addEventListener('click', () => createSession());

        async function createSession() {
            setBusy(true);
            clearTimers();
            setMessage('');
            renderWarnings([]);

            const token = findAntiForgeryToken(root);
            const formData = new FormData();
            formData.append('returnUrl', root.dataset.returnUrl || window.location.pathname + window.location.search);

            try {
                const response = await fetch(root.dataset.createUrl || '/Ekyc/CreateMobileSession', {
                    method: 'POST',
                    headers: token ? { RequestVerificationToken: token } : {},
                    body: formData
                });

                const result = await response.json();
                if (!response.ok || !result.success) {
                    setMessage(result.message || 'Không tạo được mã QR. Vui lòng thử lại.');
                    return;
                }

                sessionId = result.sessionId;
                expiresAt = new Date(result.expiresAtUtc);
                if (qrImage) {
                    qrImage.src = result.qrDataUri;
                    qrImage.classList.remove('d-none');
                }
                if (link) {
                    link.href = result.mobileUrl;
                    link.textContent = 'Mở liên kết eKYC trên điện thoại';
                    link.classList.remove('d-none');
                }

                setStatus(result.statusText || 'Đang chờ thao tác trên điện thoại', result.status);
                setMessage('Quét mã bằng camera điện thoại. Trang này sẽ tự cập nhật khi bạn gửi hồ sơ.');
                startCountdown();
                pollTimer = window.setInterval(pollStatus, 3000);
            } catch {
                setMessage('Không tạo được mã QR. Vui lòng kiểm tra kết nối và thử lại.');
            } finally {
                setBusy(false);
            }
        }

        async function pollStatus() {
            if (!sessionId) return;

            try {
                const response = await fetch(`${root.dataset.statusUrl || '/Ekyc/MobileSessionStatus'}?id=${encodeURIComponent(sessionId)}`, {
                    headers: { Accept: 'application/json' }
                });
                if (!response.ok) return;

                const result = await response.json();
                setStatus(result.statusText || result.status, result.status);
                if (result.message) setMessage(result.message);
                renderWarnings(result.warnings || []);

                if (['Verified', 'PendingReview', 'Rejected', 'Expired'].includes(result.status)) {
                    clearTimers();
                    if (result.status === 'Verified') setMessage('Xác thực thành công. Bạn có thể quay lại bước thuê xe trên máy tính.');
                    if (result.status === 'PendingReview') setMessage('Hồ sơ đã gửi thành công và đang chờ đội ngũ DriveGo xác minh thêm.');
                    if (result.status === 'Rejected') setMessage(result.message || 'Hồ sơ eKYC bị từ chối. Vui lòng kiểm tra lại thông tin.');
                    if (result.status === 'Expired') setMessage('Mã QR đã hết hạn. Vui lòng tạo mã mới.');
                }
            } catch {
                setMessage('Đang chờ kết nối lại để cập nhật trạng thái.');
            }
        }

        function startCountdown() {
            updateCountdown();
            countdownTimer = window.setInterval(updateCountdown, 1000);
        }

        function updateCountdown() {
            if (!expiresAt || !countdown) return;
            const remaining = Math.max(0, expiresAt.getTime() - Date.now());
            const minutes = Math.floor(remaining / 60000);
            const seconds = Math.floor((remaining % 60000) / 1000);
            countdown.textContent = `${minutes}:${seconds.toString().padStart(2, '0')}`;
            if (remaining <= 0) {
                setStatus('Hết hạn', 'Expired');
                setMessage('Mã QR đã hết hạn. Vui lòng tạo mã mới.');
                clearTimers();
            }
        }

        function clearTimers() {
            if (pollTimer) window.clearInterval(pollTimer);
            if (countdownTimer) window.clearInterval(countdownTimer);
            pollTimer = null;
            countdownTimer = null;
        }

        function setStatus(text, state) {
            if (!status) return;
            status.textContent = text || '';
            status.dataset.state = state || '';
        }

        function setMessage(text) {
            if (!message) return;
            message.textContent = text || '';
            message.classList.toggle('d-none', !text);
        }

        function renderWarnings(items) {
            if (!warnings) return;
            warnings.innerHTML = '';
            warnings.classList.toggle('d-none', !items.length);
            items.forEach(item => {
                const li = document.createElement('li');
                li.textContent = item;
                warnings.appendChild(li);
            });
        }

        function setBusy(isBusy) {
            createButton.disabled = isBusy;
            createButton.dataset.originalText = createButton.dataset.originalText || createButton.textContent;
            createButton.textContent = isBusy ? 'Đang tạo mã QR...' : createButton.dataset.originalText;
        }
    }

    function findAntiForgeryToken(root) {
        return root.querySelector('input[name="__RequestVerificationToken"]')?.value ||
            document.querySelector('input[name="__RequestVerificationToken"]')?.value ||
            '';
    }
})();
