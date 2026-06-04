// CarRentalSystem/Areas/Admin/Controllers/AreaDashboardController.cs
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
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
    public class AreaDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAreaService _areaService;

        public AreaDashboardController(ApplicationDbContext context, IAreaService areaService)
        {
            _context = context;
            _areaService = areaService;
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

        public async Task<IActionResult> Index()
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role == "Admin")
            {
                return RedirectToAction("Index", "Area");
            }

            var myAreaIds = await _areaService.GetAreaIdsForUserAsync(userId);
            if (myAreaIds.Count == 0)
            {
                TempData["Info"] = "Bạn chưa được gán vào khu vực nào. Vui lòng liên hệ Admin để được phân công.";
                return RedirectToAction("Index", "Dashboard");
            }

            if (myAreaIds.Count == 1)
            {
                return RedirectToAction("Show", "Area", new { id = myAreaIds[0] });
            }

            var areas = await _context.Areas
                .Include(a => a.Manager)
                .Include(a => a.AreaStaffs!)
                    .ThenInclude(asx => asx.User)
                .Include(a => a.AreaLocations!)
                .Where(a => myAreaIds.Contains(a.AreaID) && !a.IsDeleted)
                .OrderBy(a => a.Name)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var completedStatusIds = await _context.BookingStatuses
                .Where(bs => bs.Name == "Completed")
                .Select(bs => bs.BookingStatusID)
                .ToListAsync();

            var areaStats = new List<AreaDashboardStats>();

            foreach (var area in areas)
            {
                var locIds = area.AreaLocations!
                    .Where(al => !al.IsDeleted && al.Location != null)
                    .Select(al => al.LocationID)
                    .ToList();

                var totalCars = locIds.Any()
                    ? await _context.Cars.AsNoTracking().CountAsync(c => locIds.Contains(c.LocationID) && !c.IsDeleted)
                    : 0;
                var availableCars = locIds.Any()
                    ? await _context.Cars.AsNoTracking().CountAsync(c => locIds.Contains(c.LocationID) && !c.IsDeleted && c.CarStatusID == 1)
                    : 0;

                var monthlyBookingsQuery = _context.Bookings
                    .AsNoTracking()
                    .Where(b =>
                        b.Car != null &&
                        locIds.Contains(b.Car.LocationID) &&
                        !b.IsDeleted &&
                        b.CreatedAt >= startOfMonth);

                var monthlyOrders = locIds.Any() ? await monthlyBookingsQuery.CountAsync() : 0;
                var monthlyRevenue = locIds.Any()
                    ? await monthlyBookingsQuery
                        .Where(b => completedStatusIds.Contains(b.BookingStatusID))
                        .SumAsync(b => b.TotalPrice)
                    : 0m;

                var isAreaManager = area.AreaStaffs!
                    .Any(asx => asx.UserID == userId && AreaStaffRoleConstants.IsManager(asx.RoleInArea) && !asx.IsDeleted);

                var today = DateTime.UtcNow.Date;
                var cameraEventQuery = _context.CameraGateEvents.AsNoTracking()
                    .Where(e => e.AreaID == area.AreaID || (e.LocationID.HasValue && locIds.Contains(e.LocationID.Value)));

                areaStats.Add(new AreaDashboardStats
                {
                    Area = area,
                    TotalCars = totalCars,
                    AvailableCars = availableCars,
                    MonthlyOrders = monthlyOrders,
                    MonthlyRevenue = monthlyRevenue,
                    TotalLocations = locIds.Count,
                    TotalStaffs = area.AreaStaffs!.Count(asx => !asx.IsDeleted),
                    IsAreaManager = isAreaManager,
                    VehiclesInside = await _context.VehicleAreaPresences.AsNoTracking()
                        .CountAsync(p => p.IsInside && (p.AreaID == area.AreaID || (p.LocationID.HasValue && locIds.Contains(p.LocationID.Value)))),
                    CameraEventsToday = await cameraEventQuery.CountAsync(e => e.CapturedAt >= today),
                    UnknownPlateToday = await cameraEventQuery.CountAsync(e => e.CapturedAt >= today && e.MatchStatus == CameraGateMatchStatusConstants.UnknownPlate),
                    LowConfidenceToday = await cameraEventQuery.CountAsync(e => e.CapturedAt >= today && e.MatchStatus == CameraGateMatchStatusConstants.LowConfidence),
                    AreaMismatchToday = await cameraEventQuery.CountAsync(e => e.CapturedAt >= today && e.MatchStatus == CameraGateMatchStatusConstants.AreaMismatch)
                });
            }

            ViewBag.MyAreaIds = myAreaIds;
            return View(areaStats);
        }
    }

    public class AreaDashboardStats
    {
        public Area Area { get; set; } = null!;
        public int TotalCars { get; set; }
        public int AvailableCars { get; set; }
        public int MonthlyOrders { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public int TotalLocations { get; set; }
        public int TotalStaffs { get; set; }
        public bool IsAreaManager { get; set; }
        public int VehiclesInside { get; set; }
        public int CameraEventsToday { get; set; }
        public int UnknownPlateToday { get; set; }
        public int LowConfidenceToday { get; set; }
        public int AreaMismatchToday { get; set; }

        public decimal UtilizationRate => TotalCars > 0
            ? Math.Round((decimal)(TotalCars - AvailableCars) / TotalCars * 100, 1)
            : 0;
    }
}
