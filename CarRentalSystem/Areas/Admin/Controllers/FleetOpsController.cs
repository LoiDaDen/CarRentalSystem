using Microsoft.AspNetCore.Authorization;
using CarRentalSystem.Authorization;
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class FleetOpsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FleetOpsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Readiness()
        {
            var now = DateTime.UtcNow;
            var trackingCutoff = now.AddHours(-24);
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var readinessByStatus = await _context.Cars.AsNoTracking()
                .Where(c => !c.IsDeleted)
                .GroupBy(c => c.CarStatus != null ? c.CarStatus.Name : "Unknown")
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var maintenanceRisk = await _context.Cars.AsNoTracking()
                .Where(c => !c.IsDeleted && c.NextMaintenanceDate != null && c.NextMaintenanceDate <= now.AddDays(14))
                .OrderBy(c => c.NextMaintenanceDate)
                .Take(50)
                .Select(c => new
                {
                    c.CarID,
                    c.CarName,
                    c.LicensePlate,
                    c.NextMaintenanceDate
                })
                .ToListAsync();

            var utilization = await _context.Bookings.AsNoTracking()
                .Where(b => b.StartDate >= monthStart && b.BookingStatus != null && (b.BookingStatus.Name == "Completed" || b.BookingStatus.Name == "Confirmed" || b.BookingStatus.Name == "Active"))
                .GroupBy(b => b.CarID)
                .Select(g => new
                {
                    CarID = g.Key,
                    BookingCount = g.Count(),
                    Revenue = g.Sum(b => b.TotalPrice)
                })
                .OrderByDescending(x => x.BookingCount)
                .Take(50)
                .ToListAsync();

            var recentAlerts = await _context.Alerts.AsNoTracking()
                .Where(a => a.AlertTime >= now.AddDays(-7))
                .OrderByDescending(a => a.AlertTime)
                .Take(50)
                .ToListAsync();

            var recentlyTrackedCarIds = _context.VehicleTrackings.AsNoTracking()
                .Where(t => t.Timestamp >= trackingCutoff)
                .Select(t => t.CarID)
                .Distinct()
                .AsQueryable();

            var missingTracking = await _context.Cars.AsNoTracking()
                .Where(c => !c.IsDeleted && !recentlyTrackedCarIds.Contains(c.CarID))
                .OrderBy(c => c.CarName)
                .Take(50)
                .Select(c => new { c.CarID, c.CarName, c.LicensePlate })
                .ToListAsync();

            return Json(new
            {
                readinessByStatus,
                maintenanceRisk,
                utilization,
                recentAlerts,
                missingTracking
            });
        }
    }
}
