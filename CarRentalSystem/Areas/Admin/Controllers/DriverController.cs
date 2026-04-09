using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DriverController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DriverController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Driver
        public async Task<IActionResult> Index(string? status)
        {
            var query = _context.Drivers
                .Include(d => d.Location)
                .Include(d => d.User)
                .OrderByDescending(d => d.CreatedAt)
                .AsQueryable();

            ViewBag.Status = status;

            if (status == "pending")
                query = query.Where(d => !d.IsApproved);
            else if (status == "approved")
                query = query.Where(d => d.IsApproved);

            var drivers = await query.ToListAsync();
            
            ViewBag.TotalCount = await _context.Drivers.CountAsync();
            ViewBag.PendingCount = await _context.Drivers.CountAsync(d => !d.IsApproved);
            ViewBag.ApprovedCount = await _context.Drivers.CountAsync(d => d.IsApproved);
            
            return View(drivers);
        }

        // POST: Admin/Driver/Approve/5
        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound();

            driver.IsApproved = true;
            driver.IsAvailable = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã duyệt tài xế {driver.FullName} thành công!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Driver/Reject/5
        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound();

            driver.IsApproved = false;
            driver.IsAvailable = false;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã từ chối tài xế {driver.FullName}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Driver/ToggleAvailability/5
        [HttpPost]
        public async Task<IActionResult> ToggleAvailability(int id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound();

            driver.IsAvailable = !driver.IsAvailable;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Tài xế {driver.FullName} đã {(driver.IsAvailable ? "bật" : "tắt")} trạng thái sẵn sàng.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Driver/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound();

            _context.Drivers.Remove(driver);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã xóa tài xế {driver.FullName}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Driver/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var driver = await _context.Drivers
                .Include(d => d.Location)
                .Include(d => d.User)
                .Include(d => d.DriverBookings)
                .FirstOrDefaultAsync(d => d.DriverID == id);

            if (driver == null) return NotFound();
            return View(driver);
        }
    }
}
