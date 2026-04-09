using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CarApprovalController : Controller
    {
        private readonly ApplicationDbContext _db;
        public CarApprovalController(ApplicationDbContext db) { _db = db; }

        public async Task<IActionResult> Index()
        {
            var pendingCars = await _db.Cars
                .Include(c => c.CarStatus)
                .Include(c => c.CarType)
                .Include(c => c.CarBrand)
                .Include(c => c.Owner)
                .Where(c => c.CarStatus.Name == "Pending")
                .ToListAsync();
            return View(pendingCars);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var car = await _db.Cars.FindAsync(id);
            if (car == null) return NotFound();

            var availableStatus = await _db.CarStatuses.FirstOrDefaultAsync(s => s.Name == "Available");
            if (availableStatus != null) {
                car.CarStatusID = availableStatus.CarStatusID;
                await _db.SaveChangesAsync();
                TempData["Success"] = "Đã phê duyệt xe thành công!";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var car = await _db.Cars.FindAsync(id);
            if (car == null) return NotFound();

            var maintenanceStatus = await _db.CarStatuses.FirstOrDefaultAsync(s => s.Name == "Maintenance");
            if (maintenanceStatus != null) {
                car.CarStatusID = maintenanceStatus.CarStatusID;
                await _db.SaveChangesAsync();
                TempData["Error"] = "Đã từ chối xe và đưa vào trạng thái Bảo trì.";
            }
            return RedirectToAction("Index");
        }
    }
}
