(function () {
    if (window.DriveGoEkycCamera) return;

    const cameraApi = {
        initAll,
        init
    };

    window.DriveGoEkycCamera = cameraApi;

    document.addEventListener('DOMContentLoaded', initAll);

    function initAll(root = document) {
        const scope = root && typeof root.querySelectorAll === 'function' ? root : document;
        scope.querySelectorAll('[data-ekyc-face-capture]').forEach(init);
    }

    function init(root) {
        if (!root || root.dataset.ekycCameraReady === 'true') return;
        root.dataset.ekycCameraReady = 'true';

        const input = root.querySelector('[data-ekyc-face-input]');
        const video = root.querySelector('[data-ekyc-face-video]');
        const canvas = root.querySelector('[data-ekyc-face-canvas]');
        const preview = root.querySelector('[data-ekyc-face-preview]');
        const startBtn = root.querySelector('[data-ekyc-face-start]');
        const captureBtn = root.querySelector('[data-ekyc-face-capture-btn]');
        const retakeBtn = root.querySelector('[data-ekyc-face-retake]');
        const error = root.querySelector('[data-ekyc-face-error]');
        const status = root.querySelector('[data-ekyc-face-status]');

        if (!input || !video || !canvas || !preview || !startBtn || !captureBtn || !retakeBtn) return;

        let stream = null;

        startBtn.addEventListener('click', async () => {
            try {
                await startCamera();
            } catch {
                showError('Không mở được camera. Vui lòng cấp quyền camera và thử lại.');
            }
        });

        captureBtn.addEventListener('click', () => captureFrame());

        retakeBtn.addEventListener('click', async () => {
            input.value = '';
            preview.removeAttribute('src');
            preview.classList.add('d-none');
            retakeBtn.classList.add('d-none');
            root.classList.remove('has-preview');
            await startCamera();
        });

        input.addEventListener('change', () => {
            const hasFile = input.files && input.files.length > 0;
            root.classList.toggle('has-preview', hasFile);
            if (hasFile) clearError();
        });

        async function startCamera() {
            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                showError('Trình duyệt không hỗ trợ camera. Vui lòng dùng Chrome, Edge hoặc Safari mới.');
                return;
            }

            stopCamera();
            stream = await navigator.mediaDevices.getUserMedia({
                video: {
                    facingMode: 'user',
                    width: { ideal: 1280 },
                    height: { ideal: 720 }
                },
                audio: false
            });

            video.srcObject = stream;
            await video.play().catch(() => {});
            video.classList.remove('d-none');
            preview.classList.add('d-none');
            startBtn.classList.add('d-none');
            captureBtn.classList.remove('d-none');
            retakeBtn.classList.add('d-none');
            root.classList.add('is-live');
            setStatus('Đưa mặt vào khung, nhìn thẳng camera và giữ yên.');
            clearError();
        }

        function captureFrame() {
            if (!video.videoWidth || !video.videoHeight) {
                showError('Camera chưa sẵn sàng. Vui lòng chờ thêm một chút.');
                return;
            }

            canvas.width = video.videoWidth;
            canvas.height = video.videoHeight;
            const context = canvas.getContext('2d', { willReadFrequently: true });
            context.drawImage(video, 0, 0, canvas.width, canvas.height);

            const quality = inspectFrameQuality(context, canvas.width, canvas.height);
            if (!quality.ok) {
                showError(quality.message);
                setStatus('Điều chỉnh lại ánh sáng hoặc vị trí khuôn mặt rồi chụp lại.');
                return;
            }

            canvas.toBlob(blob => {
                if (!blob) {
                    showError('Không tạo được ảnh selfie. Vui lòng thử lại.');
                    return;
                }

                const file = new File([blob], 'face-camera.jpg', { type: 'image/jpeg' });
                const dataTransfer = new DataTransfer();
                dataTransfer.items.add(file);
                input.files = dataTransfer.files;
                input.dispatchEvent(new Event('change', { bubbles: true }));

                preview.src = URL.createObjectURL(file);
                preview.classList.remove('d-none');
                stopCamera();
                video.classList.add('d-none');
                startBtn.classList.add('d-none');
                captureBtn.classList.add('d-none');
                retakeBtn.classList.remove('d-none');
                root.classList.remove('is-live');
                root.classList.add('has-preview');
                setStatus('Đã chụp selfie rõ mặt. Bạn có thể chụp lại nếu chưa ưng ý.');
                clearError();
            }, 'image/jpeg', 0.92);
        }

        function inspectFrameQuality(context, width, height) {
            const sampleWidth = Math.min(width, 320);
            const sampleHeight = Math.min(height, 240);
            const imageData = context.getImageData(0, 0, sampleWidth, sampleHeight).data;
            let brightness = 0;
            let samples = 0;
            let edgeScore = 0;
            let previousGray = null;

            for (let i = 0; i < imageData.length; i += 16) {
                const gray = imageData[i] * 0.299 + imageData[i + 1] * 0.587 + imageData[i + 2] * 0.114;
                brightness += gray;
                if (previousGray !== null) edgeScore += Math.abs(gray - previousGray);
                previousGray = gray;
                samples++;
            }

            const averageBrightness = brightness / Math.max(samples, 1);
            const sharpness = edgeScore / Math.max(samples, 1);

            if (averageBrightness < 20)
                return { ok: false, message: 'Ảnh quá tối. Vui lòng bật thêm đèn hoặc quay mặt về nguồn sáng.' };
            if (averageBrightness > 245)
                return { ok: false, message: 'Ảnh quá sáng. Vui lòng tránh ánh sáng chiếu trực tiếp vào camera.' };
            if (sharpness < 1)
                return { ok: false, message: 'Ảnh chưa đủ rõ. Vui lòng giữ yên điện thoại/máy tính khi chụp.' };

            return { ok: true };
        }

        function stopCamera() {
            if (stream) stream.getTracks().forEach(track => track.stop());
            stream = null;
            video.srcObject = null;
        }

        function showError(message) {
            if (!error) return;
            error.textContent = message;
            error.classList.remove('d-none');
        }

        function clearError() {
            if (!error) return;
            error.textContent = '';
            error.classList.add('d-none');
        }

        function setStatus(message) {
            if (status) status.textContent = message;
        }
    }
})();
