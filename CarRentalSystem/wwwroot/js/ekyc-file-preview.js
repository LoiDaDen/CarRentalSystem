(function () {
    if (window.DriveGoEkycFilePreview) return;

    const previewApi = {
        initAll,
        init,
        update
    };

    window.DriveGoEkycFilePreview = previewApi;
    document.addEventListener('DOMContentLoaded', initAll);

    function initAll(root = document) {
        const scope = root && typeof root.querySelectorAll === 'function' ? root : document;
        scope.querySelectorAll('[data-ekyc-file-input]').forEach(init);
    }

    function init(input) {
        if (!input || input.dataset.ekycFilePreviewReady === 'true') return;
        input.dataset.ekycFilePreviewReady = 'true';
        input.addEventListener('change', () => update(input));
        update(input);
    }

    function update(input) {
        if (!input) return;

        const preview = findPreview(input);
        if (!preview) return;

        const image = preview.querySelector('[data-ekyc-file-preview-image]');
        const name = preview.querySelector('[data-ekyc-file-preview-name]');
        const size = preview.querySelector('[data-ekyc-file-preview-size]');
        const file = input.files && input.files.length ? input.files[0] : null;

        if (preview.dataset.previewUrl) {
            URL.revokeObjectURL(preview.dataset.previewUrl);
            delete preview.dataset.previewUrl;
        }

        if (!file || !file.type || !file.type.startsWith('image/')) {
            if (image) {
                image.removeAttribute('src');
                image.classList.add('d-none');
            }
            if (name) name.textContent = '';
            if (size) size.textContent = '';
            preview.classList.add('d-none');
            return;
        }

        const previewUrl = URL.createObjectURL(file);
        preview.dataset.previewUrl = previewUrl;

        if (image) {
            image.src = previewUrl;
            image.classList.remove('d-none');
        }
        if (name) name.textContent = file.name;
        if (size) size.textContent = formatSize(file.size);
        preview.classList.remove('d-none');
    }

    function findPreview(input) {
        if (input.dataset.ekycPreviewTarget) {
            const target = document.querySelector(input.dataset.ekycPreviewTarget);
            if (target) return target;
        }

        const card = input.closest('[data-ekyc-file-card]') || input.parentElement;
        return card ? card.querySelector('[data-ekyc-file-preview]') : null;
    }

    function formatSize(bytes) {
        if (!Number.isFinite(bytes)) return '';
        if (bytes < 1024) return `${bytes} B`;
        if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
        return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
    }
})();
