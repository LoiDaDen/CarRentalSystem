document.addEventListener("DOMContentLoaded", () => {
    if (typeof Chart === "undefined") return;

    const data = window.dashboardData || {};
    const styles = getComputedStyle(document.documentElement);
    const textColor = styles.getPropertyValue("--admin-muted").trim() || "#6b7280";
    const borderColor = styles.getPropertyValue("--admin-border").trim() || "#e5e7eb";
    const primary = styles.getPropertyValue("--admin-primary").trim() || "#2563eb";
    const palette = ["#2563eb", "#16a34a", "#f59e0b", "#dc2626", "#0284c7", "#7c3aed", "#64748b"];

    Chart.defaults.color = textColor;
    Chart.defaults.borderColor = borderColor;

    const revenueCanvas = document.getElementById("revenueChart");
    if (revenueCanvas) {
        new Chart(revenueCanvas, {
            type: "line",
            data: {
                labels: data.depositLabels || [],
                datasets: [{
                    label: "Tiền cọc",
                    data: data.depositData || [],
                    borderColor: primary,
                    backgroundColor: "rgba(37, 99, 235, .10)",
                    fill: true,
                    tension: .35,
                    borderWidth: 2,
                    pointRadius: 3,
                    pointHoverRadius: 5
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { grid: { display: false } },
                    y: { beginAtZero: true, ticks: { callback: (value) => `${value} tr` } }
                }
            }
        });
    }

    const bookingCanvas = document.getElementById("bookingsChart");
    if (bookingCanvas) {
        new Chart(bookingCanvas, {
            type: "doughnut",
            data: {
                labels: data.bookingLabels || [],
                datasets: [{ data: data.bookingData || [], backgroundColor: palette, borderWidth: 0 }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: "68%",
                plugins: { legend: { position: "bottom", labels: { boxWidth: 9, usePointStyle: true } } }
            }
        });
    }

    const carCanvas = document.getElementById("carsChart");
    if (carCanvas) {
        new Chart(carCanvas, {
            type: "bar",
            data: {
                labels: data.carLabels || [],
                datasets: [{ data: data.carData || [], backgroundColor: palette, borderRadius: 6 }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: { x: { grid: { display: false } }, y: { beginAtZero: true, ticks: { precision: 0 } } }
            }
        });
    }
});
