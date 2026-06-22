document.addEventListener("DOMContentLoaded", () => {
    const root = document.documentElement;
    const body = document.body;
    const shell = document.querySelector("[data-admin-shell]");
    const sidebar = document.getElementById("adminSidebar");
    const backdrop = document.querySelector("[data-admin-sidebar-backdrop]");
    const themeIcon = document.querySelector("[data-admin-theme-icon]");
    const searchInput = document.querySelector("[data-admin-table-search]");
    const searchWrapper = document.querySelector("[data-admin-global-search]");

    const applyTheme = (theme) => {
        root.setAttribute("data-bs-theme", theme);
        localStorage.setItem("admin-theme", theme);
        if (themeIcon) themeIcon.className = theme === "dark" ? "bi bi-sun" : "bi bi-moon-stars";
    };

    const closeMobileSidebar = () => body.classList.remove("admin-sidebar-open");
    document.querySelectorAll("[data-admin-sidebar-toggle]").forEach((button) => {
        button.addEventListener("click", () => {
            if (window.innerWidth < 992) {
                body.classList.toggle("admin-sidebar-open");
                return;
            }
            shell?.classList.toggle("admin-sidebar-collapsed");
            localStorage.setItem("admin-sidebar-collapsed", shell?.classList.contains("admin-sidebar-collapsed") ? "1" : "0");
        });
    });
    document.querySelectorAll("[data-admin-sidebar-close]").forEach((button) => button.addEventListener("click", closeMobileSidebar));
    backdrop?.addEventListener("click", closeMobileSidebar);
    if (localStorage.getItem("admin-sidebar-collapsed") === "1" && window.innerWidth >= 992) shell?.classList.add("admin-sidebar-collapsed");

    document.querySelector("[data-admin-theme-toggle]")?.addEventListener("click", () => {
        applyTheme(root.getAttribute("data-bs-theme") === "dark" ? "light" : "dark");
    });
    applyTheme(root.getAttribute("data-bs-theme") || "light");

    const tableRows = Array.from(document.querySelectorAll(".admin-content table tbody tr"));
    if (!tableRows.length) {
        searchWrapper?.classList.add("d-none");
    } else {
        searchInput?.addEventListener("input", () => {
            const term = searchInput.value.trim().toLocaleLowerCase("vi");
            tableRows.forEach((row) => {
                row.hidden = Boolean(term) && !row.innerText.toLocaleLowerCase("vi").includes(term);
            });
        });
        document.addEventListener("keydown", (event) => {
            if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
                event.preventDefault();
                searchInput?.focus();
            }
        });
    }

    document.querySelectorAll("[data-bs-toggle='tooltip']").forEach((element) => new bootstrap.Tooltip(element));

    const notificationButton = document.querySelector("[data-admin-notification-button]");
    const notificationCount = document.querySelector("[data-admin-notification-count]");
    const notificationList = document.querySelector("[data-admin-notification-list]");
    const escapeHtml = (value) => String(value ?? "").replace(/[&<>"']/g, (char) => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#039;"
    })[char]);

    const loadNotifications = async () => {
        if (!notificationList) return;
        try {
            const response = await fetch("/Admin/Booking/GetRecentNotifications", { headers: { Accept: "application/json" } });
            if (!response.ok) throw new Error("Notification request failed");
            const data = await response.json();
            const items = Array.isArray(data.items) ? data.items : [];
            const count = Number(data.count || 0);
            notificationCount?.classList.toggle("d-none", count === 0);
            if (notificationCount) notificationCount.textContent = count > 99 ? "99+" : String(count);
            notificationList.innerHTML = items.length
                ? items.map((item) => `<a class="admin-notification-item" href="/Admin/Booking"><i class="bi ${escapeHtml(item.icon || "bi-bell")}"></i><span><strong>${escapeHtml(item.message)}</strong><small>${escapeHtml(item.time)}</small></span></a>`).join("")
                : '<div class="admin-notification-empty"><i class="bi bi-bell-slash"></i><span>Chưa có thông báo mới</span></div>';
        } catch {
            notificationList.innerHTML = '<div class="admin-notification-empty text-danger"><i class="bi bi-wifi-off"></i><span>Không tải được thông báo</span></div>';
        }
    };
    notificationButton?.addEventListener("click", loadNotifications);
    loadNotifications();
});
