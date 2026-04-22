// CarRentalSystem/Areas/Admin/Controllers/MaintenanceController.cs
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin", "Manager", "Staff")]
    public class MaintenanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAreaService _areaService;

        public MaintenanceController(ApplicationDbContext context, IAreaService areaService)
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

        public async Task<IActionResult> Index(int? areaId, string? search)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            ViewBag.Search = search;
            ViewBag.CurrentAreaId = areaId;

            // Lấy danh sách cars để dropdown filter
            var carsQuery = _context.Cars
                .Include(c => c.Location)
                .AsQueryable();

            // Lọc cars theo area cho Staff/Manager
            if (role == "Staff" || role == "Manager")
            {
                var allowedLocationIds = await _areaService.GetLocationIdsForUserAsync(userId, role);
                if (!allowedLocationIds.Any())
                {
                    ViewBag.Cars = new List<Car>();
                    return View(new List<MaintenanceLog>());
                }
                carsQuery = carsQuery.Where(c => allowedLocationIds.Contains(c.LocationID));
            }
            else if (areaId.HasValue && areaId.Value > 0)
            {
                var areaLocationIds = await _areaService.GetLocationIdsForAreaAsync(areaId.Value);
                if (areaLocationIds.Any())
                    carsQuery = carsQuery.Where(c => areaLocationIds.Contains(c.LocationID));
            }

            var filteredCars = await carsQuery.ToListAsync();
            var allowedCarIds = filteredCars.Select(c => c.CarID).ToList();

            ViewBag.Cars = filteredCars;

            // Lấy logs của những xe trong area được phép
            var logsQuery = _context.MaintenanceLogs
                .Include(m => m.Car!)
                    .ThenInclude(c => c.Location)
                .Include(m => m.Car!)
                    .ThenInclude(c => c.CarBrand)
                .Where(m => allowedCarIds.Contains(m.CarID) && !m.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                logsQuery = logsQuery.Where(m =>
                    (m.Car != null && m.Car.LicensePlate != null &&
                     m.Car.LicensePlate.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (m.Description != null && m.Description.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (m.ServiceCenter != null && m.ServiceCenter.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            var logs = await logsQuery
                .OrderByDescending(m => m.ServiceDate)
                .ToListAsync();

            return View(logs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MaintenanceLog log)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            // Kiểm tra quyền tạo maintenance cho xe này
            var car = await _context.Cars
                .Include(c => c.Location)
                .FirstOrDefaultAsync(c => c.CarID == log.CarID);

            if (car == null)
            {
                TempData["Error"] = "Xe không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            if (role == "Staff" || role == "Manager")
            {
                var allowedLocationIds = await _areaService.GetLocationIdsForUserAsync(userId, role);
                if (!allowedLocationIds.Contains(car.LocationID))
                {
                    TempData["Error"] = "Bạn không có quyền tạo bảo trì cho xe ngoài khu vực của mình.";
                    return RedirectToAction(nameof(Index));
                }
            }

            if (ModelState.IsValid)
            {
                log.CreatedAt = DateTime.UtcNow;
                _context.MaintenanceLogs.Add(log);

                car.CurrentOdometer = log.OdometerAtService;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã thêm bảo trì thành công.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "Dữ liệu không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            var log = await _context.MaintenanceLogs
                .Include(m => m.Car)
                .FirstOrDefaultAsync(m => m.LogID == id && !m.IsDeleted);

            if (log == null)
            {
                TempData["Error"] = "Bản ghi bảo trì không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            if (role == "Staff" || role == "Manager")
            {
                var allowedLocationIds = await _areaService.GetLocationIdsForUserAsync(userId, role);
                if (!allowedLocationIds.Contains(log.Car?.LocationID ?? 0))
                {
                    TempData["Error"] = "Bạn không có quyền xóa bản ghi bảo trì ngoài khu vực của mình.";
                    return RedirectToAction(nameof(Index));
                }
            }

            log.IsDeleted = true;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xóa bản ghi bảo trì.";

            return RedirectToAction(nameof(Index));
        }
    }
}
