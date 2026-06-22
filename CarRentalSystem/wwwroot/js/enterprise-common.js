document.addEventListener("DOMContentLoaded", () => {
    const sidebar = document.getElementById("portalSidebar");
    document.querySelectorAll("[data-portal-menu]").forEach(button => {
        button.addEventListener("click", () => sidebar?.classList.toggle("open"));
    });
});
