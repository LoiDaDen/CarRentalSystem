using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CarRentalSystem.Areas.Admin.Models;
using ClosedXML.Excel;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            var bookings = await _context.Bookings
                .Include(b => b.Car).ThenInclude(c => c.CarBrand)
                .Include(b => b.User)
                .Include(b => b.BookingStatus)
                .Where(b => b.BookingStatus != null && b.BookingStatus.Name != "Draft")
                .OrderByDescending(b => b.CreatedAt)
                .Take(5000)
                .ToListAsync();

            var completedBookings = bookings.Where(b => b.BookingStatus?.Name == "Completed" || b.BookingStatus?.Name == "Confirmed").ToList();

            // KPI stats
            var monthRevenue = completedBookings
                .Where(b => b.CreatedAt >= startOfMonth)
                .Sum(b => b.TotalPrice);

            var newBookingsThisMonth = bookings.Count(b => b.CreatedAt >= startOfMonth);

            var cars = await _context.Cars.Include(c => c.CarStatus).Include(c => c.CarBrand).Take(1000).ToListAsync();
            var maintenanceLogs = await _context.MaintenanceLogs.Include(m => m.Car).OrderByDescending(m => m.ServiceDate).Take(5000).ToListAsync();

            // Revenue last 12 months
            var last12 = Enumerable.Range(0, 12)
                .Select(i => now.AddMonths(-11 + i))
                .ToList();

            var revenueLabels = last12.Select(d => d.ToString("MM/yyyy")).ToList();
            var revenueData = last12.Select(d =>
                completedBookings
                    .Where(b => b.CreatedAt.Month == d.Month && b.CreatedAt.Year == d.Year)
                    .Sum(b => b.TotalPrice) / 1000000m
            ).ToList();

            // Booking status breakdown
            var statusGroups = new Dictionary<string, int>
            {
                { "Pending", bookings.Count(b => b.BookingStatus?.Name == "Pending") },
                { "Confirmed", bookings.Count(b => b.BookingStatus?.Name == "Confirmed") },
                { "Completed", bookings.Count(b => b.BookingStatus?.Name == "Completed") },
                { "Cancelled", bookings.Count(b => b.BookingStatus?.Name == "Cancelled") }
            };

            // Top 5 cars (most rented)
            var topCars = bookings
                .GroupBy(b => b.CarID)
                .Select(g => new TopCarViewModel
                {
                    Car = g.First().Car,
                    RentalCount = g.Count(),
                    TotalRevenue = g.Where(b => b.BookingStatus?.Name == "Completed" || b.BookingStatus?.Name == "Confirmed").Sum(b => b.TotalPrice)
                })
                .OrderByDescending(x => x.RentalCount)
                .Take(5)
                .ToList();

            // Bottom 5 cars (slow-moving fleet)
            var bottomCars = bookings
                .GroupBy(b => b.CarID)
                .Select(g => new TopCarViewModel
                {
                    Car = g.First().Car,
                    RentalCount = g.Count(),
                    TotalRevenue = g.Where(b => b.BookingStatus?.Name == "Completed" || b.BookingStatus?.Name == "Confirmed").Sum(b => b.TotalPrice)
                })
                .OrderBy(x => x.RentalCount)
                .Take(5)
                .ToList();

            // Top 5 customers
            var topCustomers = bookings
                .GroupBy(b => b.UserID)
                .Select(g => new TopCustomerViewModel
                {
                    User = g.First().User,
                    BookingCount = g.Count(),
                    TotalSpent = g.Where(b => b.BookingStatus?.Name == "Completed" || b.BookingStatus?.Name == "Confirmed").Sum(b => b.TotalPrice)
                })
                .OrderByDescending(x => x.TotalSpent)
                .Take(5)
                .ToList();

            // Maintenance cost this year
            var maintenanceCost = maintenanceLogs
                .Where(m => m.ServiceDate.Year == now.Year)
                .Sum(m => m.Cost);

            // ═══════════════════════════════════════════════════
            // 📊 FLEET UTILIZATION RATE
            // Tỷ lệ sử dụng = (Tổng ngày thuê / Tổng ngày × số xe) × 100%
            // ═══════════════════════════════════════════════════
            int totalCarsCount = cars.Count;
            int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            int totalAvailableDays = totalCarsCount * daysInMonth;

            var thisMonthRentalDays = completedBookings
                .Where(b => b.StartDate.Month == now.Month && b.StartDate.Year == now.Year)
                .Sum(b => (int)Math.Ceiling((b.EndDate - b.StartDate).TotalDays));

            decimal utilizationRate = totalAvailableDays > 0
                ? Math.Round((decimal)thisMonthRentalDays / totalAvailableDays * 100, 1)
                : 0;

            // Per-vehicle utilization (for fleet table)
            var fleetUtilization = cars.Select(c =>
            {
                var carBookings = completedBookings.Where(b => b.CarID == c.CarID).ToList();
                int rentalDays = carBookings.Sum(b => (int)Math.Ceiling((b.EndDate - b.StartDate).TotalDays));
                decimal carRevenue = carBookings.Sum(b => b.TotalPrice);
                decimal carMaintenanceCost = maintenanceLogs.Where(m => m.CarID == c.CarID).Sum(m => m.Cost);

                return new FleetUtilizationViewModel
                {
                    CarID = c.CarID,
                    CarName = $"{c.CarBrand?.Name} {c.CarName}",
                    LicensePlate = c.LicensePlate,
                    Status = c.CarStatus?.Name ?? "Unknown",
                    TotalBookings = carBookings.Count,
                    TotalRentalDays = rentalDays,
                    UtilizationPercent = daysInMonth > 0 ? Math.Round((decimal)rentalDays / daysInMonth * 100, 1) : 0,
                    Revenue = carRevenue,
                    MaintenanceCost = carMaintenanceCost,
                    Profit = carRevenue - carMaintenanceCost
                };
            })
            .OrderByDescending(f => f.UtilizationPercent)
            .ToList();

            ViewBag.MonthRevenue = monthRevenue;
            ViewBag.NewBookingsThisMonth = newBookingsThisMonth;
            ViewBag.RentedCars = cars.Count(c => c.CarStatus?.Name == "Rented");
            ViewBag.MaintenanceCars = cars.Count(c => c.CarStatus?.Name == "Maintenance");
            ViewBag.TotalRevenue = completedBookings.Sum(b => b.TotalPrice);
            ViewBag.TotalBookings = bookings.Count;
            ViewBag.MaintenanceCostYear = maintenanceCost;

            ViewBag.RevenueLabels = revenueLabels;
            ViewBag.RevenueData = revenueData;
            ViewBag.StatusGroups = statusGroups;
            ViewBag.TopCars = topCars;
            ViewBag.BottomCars = bottomCars;
            ViewBag.TopCustomers = topCustomers;
            ViewBag.MaintenanceLogs = maintenanceLogs.OrderByDescending(m => m.ServiceDate).Take(5).ToList();

            // Fleet KPI
            ViewBag.UtilizationRate = utilizationRate;
            ViewBag.FleetUtilization = fleetUtilization;
            ViewBag.AvgRevenuePerCar = totalCarsCount > 0 ? completedBookings.Sum(b => b.TotalPrice) / totalCarsCount : 0;

            return View();
        }

        // ═══════════════════════════════════════════════════
        // 📥 EXPORT EXCEL — Báo cáo tổng hợp
        // ═══════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> ExportExcel(string period = "month")
        {
            var now = DateTime.UtcNow;
            DateTime startDate = period switch
            {
                "week" => now.AddDays(-7),
                "month" => new DateTime(now.Year, now.Month, 1),
                "quarter" => new DateTime(now.Year, (now.Month - 1) / 3 * 3 + 1, 1),
                "year" => new DateTime(now.Year, 1, 1),
                _ => new DateTime(now.Year, now.Month, 1)
            };

            var bookings = await _context.Bookings
                .Include(b => b.Car).ThenInclude(c => c.CarBrand)
                .Include(b => b.User)
                .Include(b => b.BookingStatus)
                .Where(b => b.CreatedAt >= startDate &&
                            b.BookingStatus != null && b.BookingStatus.Name != "Draft")
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var cars = await _context.Cars.Include(c => c.CarBrand).Include(c => c.CarStatus).ToListAsync();
            var maintenanceLogs = await _context.MaintenanceLogs
                .Include(m => m.Car)
                .Where(m => m.ServiceDate >= startDate)
                .OrderByDescending(m => m.ServiceDate)
                .ToListAsync();

            using var workbook = new XLWorkbook();

            // ═══ SHEET 1: Doanh thu & Booking ═══
            var ws1 = workbook.Worksheets.Add("Doanh Thu");
            ws1.Cell(1, 1).Value = $"BÁO CÁO DOANH THU — {startDate:dd/MM/yyyy} đến {now:dd/MM/yyyy}";
            ws1.Range(1, 1, 1, 10).Merge().Style.Font.Bold = true;
            ws1.Cell(1, 1).Style.Font.FontSize = 14;

            // Headers
            var headers1 = new[] { "Mã ĐĐ", "Ngày tạo", "Khách hàng", "SĐT", "Xe", "Biển số", "Ngày thuê", "Ngày trả", "Tổng tiền", "Trạng thái" };
            for (int i = 0; i < headers1.Length; i++)
            {
                ws1.Cell(3, i + 1).Value = headers1[i];
                ws1.Cell(3, i + 1).Style.Font.Bold = true;
                ws1.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            }

            int row = 4;
            foreach (var b in bookings)
            {
                ws1.Cell(row, 1).Value = b.BookingID;
                ws1.Cell(row, 2).Value = b.CreatedAt.ToString("dd/MM/yyyy HH:mm");
                ws1.Cell(row, 3).Value = b.User?.Name ?? "";
                ws1.Cell(row, 4).Value = b.User?.Phone ?? "";
                ws1.Cell(row, 5).Value = $"{b.Car?.CarBrand?.Name} {b.Car?.CarName}";
                ws1.Cell(row, 6).Value = b.Car?.LicensePlate ?? "";
                ws1.Cell(row, 7).Value = b.StartDate.ToString("dd/MM/yyyy HH:mm");
                ws1.Cell(row, 8).Value = b.EndDate.ToString("dd/MM/yyyy HH:mm");
                ws1.Cell(row, 9).Value = b.TotalPrice;
                ws1.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
                ws1.Cell(row, 10).Value = b.BookingStatus?.Name ?? "";
                row++;
            }

            // Summary row
            row += 1;
            ws1.Cell(row, 8).Value = "TỔNG DOANH THU:";
            ws1.Cell(row, 8).Style.Font.Bold = true;
            ws1.Cell(row, 9).Value = bookings.Where(b => b.BookingStatus?.Name == "Completed" || b.BookingStatus?.Name == "Confirmed").Sum(b => b.TotalPrice);
            ws1.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
            ws1.Cell(row, 9).Style.Font.Bold = true;
            ws1.Columns().AdjustToContents();

            // ═══ SHEET 2: Công suất đội xe ═══
            var ws2 = workbook.Worksheets.Add("Công Suất Đội Xe");
            ws2.Cell(1, 1).Value = $"BÁO CÁO CÔNG SUẤT ĐỘI XE — Tháng {now:MM/yyyy}";
            ws2.Range(1, 1, 1, 9).Merge().Style.Font.Bold = true;
            ws2.Cell(1, 1).Style.Font.FontSize = 14;

            var headers2 = new[] { "Xe", "Biển số", "Trạng thái", "Số booking", "Ngày thuê", "Tỷ lệ sử dụng (%)", "Doanh thu", "Chi phí BD", "Lợi nhuận" };
            for (int i = 0; i < headers2.Length; i++)
            {
                ws2.Cell(3, i + 1).Value = headers2[i];
                ws2.Cell(3, i + 1).Style.Font.Bold = true;
                ws2.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
            }

            int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            var completedBookings = bookings.Where(b => b.BookingStatus?.Name == "Completed" || b.BookingStatus?.Name == "Confirmed").ToList();

            row = 4;
            foreach (var car in cars.OrderBy(c => c.CarName))
            {
                var carBookings = completedBookings.Where(b => b.CarID == car.CarID).ToList();
                int rentalDays = carBookings.Sum(b => (int)Math.Ceiling((b.EndDate - b.StartDate).TotalDays));
                decimal revenue = carBookings.Sum(b => b.TotalPrice);
                decimal mCost = maintenanceLogs.Where(m => m.CarID == car.CarID).Sum(m => m.Cost);
                decimal utilRate = daysInMonth > 0 ? Math.Round((decimal)rentalDays / daysInMonth * 100, 1) : 0;

                ws2.Cell(row, 1).Value = $"{car.CarBrand?.Name} {car.CarName}";
                ws2.Cell(row, 2).Value = car.LicensePlate;
                ws2.Cell(row, 3).Value = car.CarStatus?.Name ?? "";
                ws2.Cell(row, 4).Value = carBookings.Count;
                ws2.Cell(row, 5).Value = rentalDays;
                ws2.Cell(row, 6).Value = utilRate;
                ws2.Cell(row, 7).Value = revenue;
                ws2.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
                ws2.Cell(row, 8).Value = mCost;
                ws2.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
                ws2.Cell(row, 9).Value = revenue - mCost;
                ws2.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
                row++;
            }
            ws2.Columns().AdjustToContents();

            // ═══ SHEET 3: Bảo dưỡng ═══
            var ws3 = workbook.Worksheets.Add("Bảo Dưỡng");
            ws3.Cell(1, 1).Value = $"LỊCH SỬ BẢO DƯỠNG — {startDate:dd/MM/yyyy} đến {now:dd/MM/yyyy}";
            ws3.Range(1, 1, 1, 6).Merge().Style.Font.Bold = true;

            var headers3 = new[] { "Xe", "Biển số", "Mô tả", "Ngày BD", "Chi phí", "Nơi bảo dưỡng" };
            for (int i = 0; i < headers3.Length; i++)
            {
                ws3.Cell(3, i + 1).Value = headers3[i];
                ws3.Cell(3, i + 1).Style.Font.Bold = true;
                ws3.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
            }

            row = 4;
            foreach (var log in maintenanceLogs)
            {
                ws3.Cell(row, 1).Value = log.Car?.CarName ?? "";
                ws3.Cell(row, 2).Value = log.Car?.LicensePlate ?? "";
                ws3.Cell(row, 3).Value = log.Description ?? "";
                ws3.Cell(row, 4).Value = log.ServiceDate.ToString("dd/MM/yyyy");
                ws3.Cell(row, 5).Value = log.Cost;
                ws3.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                ws3.Cell(row, 6).Value = log.ServiceCenter ?? "";
                row++;
            }
            ws3.Columns().AdjustToContents();

            // Export
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            string fileName = $"BaoCao_ThuêXe_{now:yyyyMMdd_HHmm}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}
