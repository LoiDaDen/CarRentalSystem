// CarRentalSystem/Areas/Admin/Controllers/AreaStatsController.cs
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Models;
using CarRentalSystem.ViewModels.Areas;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin", "Manager", "Staff")]
    public class AreaStatsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AreaStatsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub")
                     ?? User.FindFirst("UserID");
            return int.TryParse(claim?.Value, out var id) ? id : 0;
        }

        private string GetCurrentRole() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
         ?? User.FindFirst("role")?.Value ?? "";

        private async Task<bool> CanViewAreaAsync(int areaId)
        {
            var role = GetCurrentRole();
            if (role == "Manager") return true;

            var userId = GetCurrentUserId();
            return await _context.AreaStaffs
                .IgnoreQueryFilters()
                .AnyAsync(asx => asx.UserID == userId && asx.AreaID == areaId && !asx.IsDeleted);
        }

        // ═══════════════════════════════════════════════
        // STATS — JSON: tổng hợp + chart 30 ngày
        // ═══════════════════════════════════════════════
        public async Task<IActionResult> Stats(int areaId)
        {
            var area = await _context.Areas
                .Include(a => a.AreaLocations!).ThenInclude(al => al.Location)
                    .ThenInclude(l => l!.Cars)
                .Include(a => a.AreaStaffs!)
                .FirstOrDefaultAsync(a => a.AreaID == areaId && !a.IsDeleted);

            if (area == null)
                return NotFound(new { error = "Area not found." });

            if (!await CanViewAreaAsync(areaId))
                return Forbid();

            var locationIds = area.AreaLocations!
                .Where(al => !al.IsDeleted && al.Location != null)
                .Select(al => al.LocationID)
                .ToList();

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            // Active statuses (không phải Completed/Cancelled/Rejected)
            var activeStatusIds = await _context.BookingStatuses
                .Where(bs => !new[] { "Completed", "Cancelled", "Rejected" }.Contains(bs.Name))
                .Select(bs => bs.BookingStatusID)
                .ToListAsync();

            // Completed statuses
            var completedStatusIds = await _context.BookingStatuses
                .Where(bs => bs.Name == "Completed")
                .Select(bs => bs.BookingStatusID)
                .ToListAsync();

            // Booking counts & revenue: tháng hiện tại
            var monthlyBookings = locationIds.Any()
                ? await _context.Bookings
                    .Where(b =>
                        b.Car != null &&
                        locationIds.Contains(b.Car.LocationID) &&
                        !b.IsDeleted &&
                        b.CreatedAt >= startOfMonth)
                    .ToListAsync()
                : new List<Booking>();

            var monthlyOrders = monthlyBookings.Count;
            var monthlyRevenue = monthlyBookings
                .Where(b => completedStatusIds.Contains(b.BookingStatusID))
                .Sum(b => b.TotalPrice);

            // Chart: 30 ngày gần nhất
            var chartStart = now.Date.AddDays(-29);
            var chartData = new List<AreaStatsChartPoint>();

            for (int i = 0; i < 30; i++)
            {
                var day = chartStart.AddDays(i);
                var dayEnd = day.AddDays(1);

                var dayBookings = monthlyBookings
                    .Where(b => b.CreatedAt >= day && b.CreatedAt < dayEnd)
                    .ToList();

                chartData.Add(new AreaStatsChartPoint
                {
                    Date = day.ToString("dd/MM"),
                    Count = dayBookings.Count,
                    Revenue = dayBookings
                        .Where(b => completedStatusIds.Contains(b.BookingStatusID))
                        .Sum(b => b.TotalPrice)
                });
            }

            var allCars = area.AreaLocations!
                .SelectMany(al => al.Location?.Cars ?? Enumerable.Empty<Car>())
                .Where(c => !c.IsDeleted)
                .ToList();

            var result = new
            {
                areaId,
                areaName = area.Name,
                totalCars = allCars.Count,
                availableCars = allCars.Count(c => c.CarStatusID == 1),
                rentedCars = allCars.Count(c => c.CarStatusID != 1),
                totalStaffs = area.AreaStaffs!.Count(asx => !asx.IsDeleted),
                totalLocations = locationIds.Count,
                monthlyOrders,
                monthlyRevenue,
                monthlyRevenueFormatted = monthlyRevenue.ToString("N0") + " ₫",
                utilizationRate = allCars.Count > 0
                    ? Math.Round((decimal)(allCars.Count - allCars.Count(c => c.CarStatusID == 1)) / allCars.Count * 100, 1)
                    : 0,
                chart = chartData
            };

            return Json(result);
        }
    }
}
