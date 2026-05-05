using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Filters;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin", "Manager", "Staff")]
    public class CarApprovalController : AdminAreaControllerBase
    {
        private readonly ApplicationDbContext _db;

        public CarApprovalController(ApplicationDbContext db, IAreaService areaService)
            : base(areaService)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            var query = _db.Cars
                .Include(c => c.CarStatus)
                .Include(c => c.CarType)
                .Include(c => c.CarBrand)
                .Include(c => c.Owner)
                .Include(c => c.Location)
                .Where(c => c.CarStatus.Name == "Pending")
                .AsQueryable();

            // Manager/Staff: chi thay xe trong khu vuc cua minh
            if (role == "Manager" || role == "Staff")
            {
                var areaLocationIds = await _areaService.GetLocationIdsForUserAsync(userId);
                if (areaLocationIds.Any())
                {
                    query = query.Where(c => areaLocationIds.Contains(c.LocationID));
                }
                else
                {
                    // Khong co khu vuc -> tra ve danh sach rong
                    return View(new List<Car>());
                }
            }

            var pendingCars = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            // Staff chi co quyen xem, khong duoc duyet
            ViewBag.CanApprove = role == "Admin" || role == "Manager";
            ViewBag.CurrentRole = role;

            return View(pendingCars);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            // Chi Admin va Manager moi co quyen duyet
            if (role != "Admin" && role != "Manager")
                return Forbid();

            var car = await _db.Cars
                .Include(c => c.Location)
                .FirstOrDefaultAsync(c => c.CarID == id);

            if (car == null) return NotFound();

            // Manager: kiem tra xe co thuoc khu vuc cua minh khong
            if (role == "Manager")
            {
                var areaLocationIds = await _areaService.GetLocationIdsForUserAsync(userId);
                if (!areaLocationIds.Contains(car.LocationID))
                    return Forbid();
            }

            var availableStatus = await _db.CarStatuses.FirstOrDefaultAsync(s => s.Name == "Available");
            if (availableStatus != null)
            {
                car.CarStatusID = availableStatus.CarStatusID;
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Đã phê duyệt xe \"{car.CarName}\" thành công!";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            // Chi Admin va Manager moi co quyen tu choi
            if (role != "Admin" && role != "Manager")
                return Forbid();

            var car = await _db.Cars
                .Include(c => c.Location)
                .FirstOrDefaultAsync(c => c.CarID == id);

            if (car == null) return NotFound();

            // Manager: kiem tra xe co thuoc khu vuc cua minh khong
            if (role == "Manager")
            {
                var areaLocationIds = await _areaService.GetLocationIdsForUserAsync(userId);
                if (!areaLocationIds.Contains(car.LocationID))
                    return Forbid();
            }

            var maintenanceStatus = await _db.CarStatuses.FirstOrDefaultAsync(s => s.Name == "Maintenance");
            if (maintenanceStatus != null)
            {
                car.CarStatusID = maintenanceStatus.CarStatusID;
                await _db.SaveChangesAsync();
                TempData["Error"] = $"Đã từ chối xe \"{car.CarName}\" và đưa vào trạng thái Bảo trì.";
            }

            return RedirectToAction("Index");
        }
    }
}
