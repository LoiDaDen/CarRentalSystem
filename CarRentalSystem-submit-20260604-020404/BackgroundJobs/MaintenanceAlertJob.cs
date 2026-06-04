using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarRentalSystem.BackgroundJobs
{
    /// <summary>
    /// Hangfire Recurring Job — Chạy mỗi ngày lúc 8:00 AM
    /// Tự động tạo cảnh báo khi xe sắp đến hạn bảo dưỡng/đăng kiểm/bảo hiểm.
    /// </summary>
    public class MaintenanceAlertJob
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<MaintenanceAlertJob> _logger;

        public MaintenanceAlertJob(ApplicationDbContext db, ILogger<MaintenanceAlertJob> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task Execute()
        {
            _logger.LogInformation("🔔 [Hangfire] Bắt đầu kiểm tra bảo dưỡng & đăng kiểm...");
            var now = DateTime.UtcNow;
            var inspectionWarningThreshold = now.AddDays(30); // Cảnh báo đăng kiểm trước 30 ngày
            var maintenanceWarningThreshold = now.AddDays(7); // Cảnh báo bảo dưỡng trước 7 ngày
            int alertsCreated = 0;

            var cars = await _db.Cars
                .Include(c => c.CarBrand)
                .Where(c => c.NextInspectionDate.HasValue || c.NextMaintenanceDate.HasValue || c.NextOilChangeOdo.HasValue)
                .ToListAsync();

            foreach (var car in cars)
            {
                string carName = $"{car.CarBrand?.Name} {car.CarName} ({car.LicensePlate})";

                // 🔴 Đăng kiểm sắp hết hạn
                if (car.NextInspectionDate.HasValue && car.NextInspectionDate.Value <= inspectionWarningThreshold)
                {
                    bool exists = await _db.Alerts.AnyAsync(a => 
                        a.CarID == car.CarID && 
                        a.AlertType == "InspectionExpiring" && 
                        a.Status != "Resolved" &&
                        a.AlertTime >= now.AddDays(-1));

                    if (!exists)
                    {
                        var daysLeft = (car.NextInspectionDate.Value - now).Days;
                        _db.Alerts.Add(new Alert
                        {
                            CarID = car.CarID,
                            AlertType = "InspectionExpiring",
                            Description = daysLeft <= 0
                                ? $"⚠️ {carName} ĐÃ QUÁ HẠN đăng kiểm từ {car.NextInspectionDate.Value:dd/MM/yyyy}! Cần xử lý ngay."
                                : $"🔔 {carName} sắp hết hạn đăng kiểm trong {daysLeft} ngày ({car.NextInspectionDate.Value:dd/MM/yyyy}).",
                            Status = daysLeft <= 0 ? "Critical" : "Active"
                        });
                        alertsCreated++;
                    }
                }

                // 🟡 Bảo dưỡng sắp đến hạn
                if (car.NextMaintenanceDate.HasValue && car.NextMaintenanceDate.Value <= maintenanceWarningThreshold)
                {
                    bool exists = await _db.Alerts.AnyAsync(a => 
                        a.CarID == car.CarID && 
                        a.AlertType == "MaintenanceDue" && 
                        a.Status == "Active" &&
                        a.AlertTime >= now.AddDays(-1));

                    if (!exists)
                    {
                        var daysLeft = (car.NextMaintenanceDate.Value - now).Days;
                        _db.Alerts.Add(new Alert
                        {
                            CarID = car.CarID,
                            AlertType = "MaintenanceDue",
                            Description = $"🔧 {carName} cần bảo dưỡng định kỳ trong {Math.Max(0, daysLeft)} ngày ({car.NextMaintenanceDate.Value:dd/MM/yyyy}).",
                            Status = "Active"
                        });
                        alertsCreated++;
                    }
                }

                // 🔵 Thay dầu theo ODO
                if (car.NextOilChangeOdo.HasValue && car.CurrentOdometer >= car.NextOilChangeOdo.Value - 500)
                {
                    bool exists = await _db.Alerts.AnyAsync(a => 
                        a.CarID == car.CarID && 
                        a.AlertType == "OilChangeDue" && 
                        a.Status == "Active" &&
                        a.AlertTime >= now.AddDays(-3));

                    if (!exists)
                    {
                        var kmLeft = car.NextOilChangeOdo.Value - car.CurrentOdometer;
                        _db.Alerts.Add(new Alert
                        {
                            CarID = car.CarID,
                            AlertType = "OilChangeDue",
                            Description = $"🛢 {carName} cần thay dầu — còn {Math.Max(0, kmLeft)}km (hiện tại: {car.CurrentOdometer}km, tiêu chuẩn: {car.NextOilChangeOdo.Value}km).",
                            Status = "Active"
                        });
                        alertsCreated++;
                    }
                }
            }

            // 🔴 Cảnh báo xe quá hạn trả (đang thuê mà quá EndDate)
            var overdueBookings = await _db.Bookings
                .Include(b => b.Car).ThenInclude(c => c.CarBrand)
                .Include(b => b.BookingStatus)
                .Include(b => b.User)
                .Where(b => b.BookingStatus != null && b.BookingStatus.Name == "Active" && b.EndDate < now)
                .ToListAsync();

            foreach (var booking in overdueBookings)
            {
                bool exists = await _db.Alerts.AnyAsync(a => 
                    a.CarID == booking.CarID && 
                    a.AlertType == "OverdueReturn" && 
                    a.Status == "Active" &&
                    a.AlertTime >= now.AddHours(-6));

                if (!exists)
                {
                    var lateHours = (int)(now - booking.EndDate).TotalHours;
                    _db.Alerts.Add(new Alert
                    {
                        CarID = booking.CarID,
                        AlertType = "OverdueReturn",
                        Description = $"🚨 Xe {booking.Car?.CarBrand?.Name} {booking.Car?.CarName} ({booking.Car?.LicensePlate}) quá hạn trả {lateHours} giờ! Khách: {booking.User?.Name} ({booking.User?.Phone}). Booking #{booking.BookingID}.",
                        Status = "Critical"
                    });
                    alertsCreated++;
                }
            }

            if (alertsCreated > 0)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation("✅ [Hangfire] Đã tạo {Count} cảnh báo mới.", alertsCreated);
            }
            else
            {
                _logger.LogInformation("✅ [Hangfire] Không có cảnh báo mới.");
            }
        }
    }
}
