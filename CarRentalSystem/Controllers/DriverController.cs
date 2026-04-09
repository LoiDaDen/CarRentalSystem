using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CarRentalSystem.Controllers
{
    public class DriverController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DriverController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Public: List available drivers
        public async Task<IActionResult> Index(int? locationId, string? licenseType)
        {
            var query = _context.Drivers
                .Include(d => d.Location)
                .Include(d => d.User)
                .Where(d => d.IsApproved && d.IsAvailable);

            if (locationId.HasValue)
                query = query.Where(d => d.LocationID == locationId);

            if (!string.IsNullOrEmpty(licenseType))
                query = query.Where(d => d.LicenseType == licenseType);

            var drivers = await query.OrderByDescending(d => d.Rating).ThenByDescending(d => d.TotalTrips).ToListAsync();

            ViewBag.Locations = await _context.Locations.OrderBy(l => l.Name).ToListAsync();
            return View(drivers);
        }

        // Public: Driver profile
        public async Task<IActionResult> Profile(int id)
        {
            var driver = await _context.Drivers
                .Include(d => d.Location)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DriverID == id);

            if (driver == null) return NotFound();
            return View(driver);
        }

        // Driver Registration page
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Register()
        {
            ViewBag.Locations = _context.Locations.OrderBy(l => l.Name).ToList();

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                var user = await _context.Users.FindAsync(userId);
                ViewBag.CurrentUser = user;
            }

            return View();
        }

        // Driver Registration submit
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Register(Driver driver)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            // Check if already registered
            var existing = await _context.Drivers.FirstOrDefaultAsync(d => d.UserID == userId);
            if (existing != null)
            {
                TempData["Error"] = "Bạn đã đăng ký làm tài xế rồi. Vui lòng chờ Admin duyệt.";
                return RedirectToAction(nameof(MyProfile));
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            driver.UserID = userId;
            driver.FullName = string.IsNullOrEmpty(driver.FullName) ? user.Name : driver.FullName;
            driver.Phone = string.IsNullOrEmpty(driver.Phone) ? user.Phone : driver.Phone;
            driver.Email = string.IsNullOrEmpty(driver.Email) ? user.Email : driver.Email;
            driver.IsApproved = false;
            driver.IsAvailable = false;
            driver.Rating = 5.0m;
            driver.TotalTrips = 0;
            driver.CreatedAt = DateTime.Now;

            _context.Drivers.Add(driver);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đăng ký tài xế thành công! Vui lòng chờ Admin xét duyệt hồ sơ.";
            return RedirectToAction(nameof(MyProfile));
        }

        // Driver's own profile management
        [Authorize]
        public async Task<IActionResult> MyProfile()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var driver = await _context.Drivers
                .Include(d => d.Location)
                .Include(d => d.DriverBookings)
                .FirstOrDefaultAsync(d => d.UserID == userId);

            if (driver == null)
                return RedirectToAction(nameof(Register));

            return View(driver);
        }
    }
}
