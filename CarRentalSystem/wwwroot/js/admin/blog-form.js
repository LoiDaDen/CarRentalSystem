document.querySelectorAll('.js-image-input').forEach((input) => {
    input.addEventListener('change', () => {
        const previewId = input.getAttribute('data-preview');
        const preview = previewId ? document.getElementById(previewId) : null;
        const file = input.files && input.files[0];
        if (!preview || !file) return;

        preview.src = URL.createObjectURL(file);
        preview.style.display = 'block';
    });
});
