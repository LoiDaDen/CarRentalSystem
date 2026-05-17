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
            var completedNames = new[] { "Completed", "Confirmed" };
            var bookingsQuery = _context.Bookings
                .AsNoTracking()
                .Where(b => b.BookingStatus != null && b.BookingStatus.Name != "Draft");
            var completedBookingsQuery = bookingsQuery
                .Where(b => b.BookingStatus != null && completedNames.Contains(b.BookingStatus.Name));

            var monthRevenue = await completedBookingsQuery
                .Where(b => b.CreatedAt >= startOfMonth)
                .SumAsync(b => b.TotalPrice);

            var newBookingsThisMonth = await bookingsQuery.CountAsync(b => b.CreatedAt >= startOfMonth);

            var cars = await _context.Cars.AsNoTracking()
                .Include(c => c.CarStatus)
                .Include(c => c.CarBrand)
                .OrderBy(c => c.CarID)
                .Take(1000)
                .ToListAsync();
            var maintenanceLogs = await _context.MaintenanceLogs.AsNoTracking()
                .Include(m => m.Car)
                .OrderByDescending(m => m.ServiceDate)
                .Take(500)
                .ToListAsync();

            // Revenue last 12 months
            var last12 = Enumerable.Range(0, 12)
                .Select(i => now.AddMonths(-11 + i))
                .ToList();

            var revenueLabels = last12.Select(d => d.ToString("MM/yyyy")).ToList();
            var revenueStart = new DateTime(last12.First().Year, last12.First().Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthlyRevenueRows = await completedBookingsQuery
                .Where(b => b.CreatedAt >= revenueStart)
                .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Revenue = g.Sum(b => b.TotalPrice) })
                .ToListAsync();
            var revenueData = last12
                .Select(d => (monthlyRevenueRows.FirstOrDefault(r => r.Year == d.Year && r.Month == d.Month)?.Revenue ?? 0m) / 1000000m)
                .ToList();

            // Booking status breakdown
            var statusGroups = new Dictionary<string, int>
            {
                { "Pending", await bookingsQuery.CountAsync(b => b.BookingStatus != null && b.BookingStatus.Name == "Pending") },
                { "Confirmed", await bookingsQuery.CountAsync(b => b.BookingStatus != null && b.BookingStatus.Name == "Confirmed") },
                { "Completed", await bookingsQuery.CountAsync(b => b.BookingStatus != null && b.BookingStatus.Name == "Completed") },
                { "Cancelled", await bookingsQuery.CountAsync(b => b.BookingStatus != null && b.BookingStatus.Name == "Cancelled") }
            };

            var topCarRows = await bookingsQuery
                .GroupBy(b => b.CarID)
                .Select(g => new
                {
                    CarID = g.Key,
                    RentalCount = g.Count(),
                    TotalRevenue = g.Where(b => b.BookingStatus != null && completedNames.Contains(b.BookingStatus.Name)).Sum(b => b.TotalPrice)
                })
                .OrderByDescending(x => x.RentalCount)
                .Take(5)
                .ToListAsync();

            var bottomCarRows = await bookingsQuery
                .GroupBy(b => b.CarID)
                .Select(g => new
                {
                    CarID = g.Key,
                    RentalCount = g.Count(),
                    TotalRevenue = g.Where(b => b.BookingStatus != null && completedNames.Contains(b.BookingStatus.Name)).Sum(b => b.TotalPrice)
                })
                .OrderBy(x => x.RentalCount)
                .Take(5)
                .ToListAsync();
            var carIds = topCarRows.Select(c => c.CarID).Concat(bottomCarRows.Select(c => c.CarID)).Distinct().ToList();
            var carLookup = await _context.Cars.AsNoTracking()
                .Include(c => c.CarBrand)
                .Where(c => carIds.Contains(c.CarID))
                .ToDictionaryAsync(c => c.CarID);
            var topCars = topCarRows
                .Where(x => carLookup.ContainsKey(x.CarID))
                .Select(x => new TopCarViewModel { Car = carLookup[x.CarID], RentalCount = x.RentalCount, TotalRevenue = x.TotalRevenue })
                .ToList();
            var bottomCars = bottomCarRows
                .Where(x => carLookup.ContainsKey(x.CarID))
                .Select(x => new TopCarViewModel { Car = carLookup[x.CarID], RentalCount = x.RentalCount, TotalRevenue = x.TotalRevenue })
                .ToList();

            var topCustomerRows = await bookingsQuery
                .GroupBy(b => b.UserID)
                .Select(g => new
                {
                    UserID = g.Key,
                    BookingCount = g.Count(),
                    TotalSpent = g.Where(b => b.BookingStatus != null && completedNames.Contains(b.BookingStatus.Name)).Sum(b => b.TotalPrice)
                })
                .OrderByDescending(x => x.TotalSpent)
                .Take(5)
                .ToListAsync();
            var userIds = topCustomerRows.Select(c => c.UserID).Distinct().ToList();
            var userLookup = await _context.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.UserID))
                .ToDictionaryAsync(u => u.UserID);
            var topCustomers = topCustomerRows
                .Where(x => userLookup.ContainsKey(x.UserID))
                .Select(x => new TopCustomerViewModel { User = userLookup[x.UserID], BookingCount = x.BookingCount, TotalSpent = x.TotalSpent })
                .ToList();

            // Maintenance cost this year
            var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var maintenanceCost = await _context.MaintenanceLogs.AsNoTracking()
                .Where(m => m.ServiceDate >= yearStart)
                .SumAsync(m => m.Cost);

            // ═══════════════════════════════════════════════════
            // 📊 FLEET UTILIZATION RATE
            // Tỷ lệ sử dụng = (Tổng ngày thuê / Tổng ngày × số xe) × 100%
            // ═══════════════════════════════════════════════════
            int totalCarsCount = cars.Count;
            int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            int totalAvailableDays = totalCarsCount * daysInMonth;

            var completedThisMonth = await completedBookingsQuery
                .Where(b => b.StartDate >= startOfMonth)
                .Select(b => new { b.CarID, b.StartDate, b.EndDate, b.TotalPrice })
                .ToListAsync();
            var thisMonthRentalDays = completedThisMonth
                .Sum(b => (int)Math.Ceiling((b.EndDate - b.StartDate).TotalDays));

            decimal utilizationRate = totalAvailableDays > 0
                ? Math.Round((decimal)thisMonthRentalDays / totalAvailableDays * 100, 1)
                : 0;

            // Per-vehicle utilization (for fleet table)
            var maintenanceByCar = await _context.MaintenanceLogs.AsNoTracking()
                .Where(m => m.ServiceDate >= yearStart)
                .GroupBy(m => m.CarID)
                .Select(g => new { CarID = g.Key, Cost = g.Sum(m => m.Cost) })
                .ToDictionaryAsync(x => x.CarID, x => x.Cost);
            var fleetUtilization = cars.Select(c =>
            {
                var carBookings = completedThisMonth.Where(b => b.CarID == c.CarID).ToList();
                int rentalDays = carBookings.Sum(b => (int)Math.Ceiling((b.EndDate - b.StartDate).TotalDays));
                decimal carRevenue = carBookings.Sum(b => b.TotalPrice);
                decimal carMaintenanceCost = maintenanceByCar.TryGetValue(c.CarID, out var cost) ? cost : 0m;

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
            ViewBag.TotalRevenue = await completedBookingsQuery.SumAsync(b => b.TotalPrice);
            ViewBag.TotalBookings = await bookingsQuery.CountAsync();
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
            ViewBag.AvgRevenuePerCar = totalCarsCount > 0 ? await completedBookingsQuery.SumAsync(b => b.TotalPrice) / totalCarsCount : 0;

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
