// Admin Dashboard Charts
document.addEventListener('DOMContentLoaded', function () {
    // Revenue Chart
    const ctxRev = document.getElementById('revenueChart');
    if (ctxRev) {
        new Chart(ctxRev, {
            type: 'line',
            data: {
                labels: window.dashboardData?.revenueLabels || [],
                datasets: [{
                    label: 'Doanh thu (Triệu VNĐ)',
                    data: window.dashboardData?.revenueData || [],
                    borderColor: '#3b82f6',
                    backgroundColor: 'rgba(59, 130, 246, 0.1)',
                    borderWidth: 3,
                    fill: true,
                    tension: 0.4,
                    pointBackgroundColor: '#fff',
                    pointBorderColor: '#3b82f6',
                    pointBorderWidth: 2,
                    pointRadius: 4
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { grid: { display: false } },
                    y: {
                        beginAtZero: true,
                        grid: { color: 'rgba(0,0,0,0.05)' }
                    }
                }
            }
        });
    }

    // Bookings Chart
    const ctxBook = document.getElementById('bookingsChart');
    if (ctxBook) {
        new Chart(ctxBook, {
            type: 'doughnut',
            data: {
                labels: window.dashboardData?.bookingLabels || [],
                datasets: [{
                    data: window.dashboardData?.bookingData || [],
                    backgroundColor: ['#3b82f6', '#ef4444', '#10b981', '#f59e0b'],
                    borderWidth: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '70%',
                plugins: {
                    legend: { position: 'bottom' }
                }
            }
        });
    }

    // Cars Chart
    const ctxCars = document.getElementById('carsChart');
    if (ctxCars) {
        new Chart(ctxCars, {
            type: 'bar',
            data: {
                labels: window.dashboardData?.carLabels || [],
                datasets: [{
                    label: 'Số lượng',
                    data: window.dashboardData?.carData || [],
                    backgroundColor: '#3b82f6',
                    borderRadius: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    y: { beginAtZero: true, ticks: { stepSize: 1 } }
                }
            }
        });
    }
});
