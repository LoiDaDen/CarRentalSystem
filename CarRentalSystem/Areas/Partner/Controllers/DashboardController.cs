using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CarRentalSystem.Areas.Partner.Controllers
{
    [Area("Partner")]
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public DashboardController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) return null;
            if (int.TryParse(claim.Value, out int id)) return id;
            return null;
        }

        // ─── Saves an uploaded image file to wwwroot/uploads/cars/ ────────────────
        private async Task<string?> SaveCarImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return null;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext)) return null;

            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "cars");
            Directory.CreateDirectory(uploadDir); // ensure folder exists

            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return $"/uploads/cars/{fileName}";
        }

        // GET /Partner/Dashboard
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login", "Auth", new { area = "" });
            }

            var user = await _db.Users.FindAsync(userId);
            if (user == null || (user.Role != "Partner" && user.Role != "Admin"))
                return RedirectToAction("Register");

            var myCars = await _db.Cars
                .Include(c => c.CarStatus)
                .Include(c => c.Bookings)
                .Where(c => c.OwnerID == userId)
                .ToListAsync();

            var myCarIds = myCars.Select(c => c.CarID).ToList();
            var completedBookings = await _db.Bookings
                .Include(b => b.Car)
                .Where(b => myCarIds.Contains(b.CarID) && b.BookingStatus.Name == "Completed")
                .ToListAsync();

            decimal totalRevenue = completedBookings.Sum(b => b.TotalPrice);
            decimal myShare = totalRevenue * (1 - myCars.FirstOrDefault()?.CommissionRate / 100 ?? 0.80m);

            ViewBag.TotalCars = myCars.Count;
            ViewBag.ActiveBookings = await _db.Bookings
                .Where(b => myCarIds.Contains(b.CarID) && (b.BookingStatus.Name == "Confirmed" || b.BookingStatus.Name == "Active"))
                .CountAsync();
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.MyEarnings = myShare;
            ViewBag.Cars = myCars;
            ViewBag.PartnerUser = user;

            return View();
        }

        // GET /Partner/Dashboard/Register
        public IActionResult Register()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Auth", new { area = "" });
            return View();
        }

        // POST /Partner/Dashboard/Register
        [HttpPost]
        public async Task<IActionResult> Register(string agreeTerms)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Auth", new { area = "" });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.Role = "Partner";
            user.IsVerified = false;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Yêu cầu đã được gửi! Bạn đã được cấp quyền Truy cập Đối tác, hãy hoàn tất hồ sơ eKYC để được Duyệt bán xe.";
            return RedirectToAction("Index");
        }

        // GET /Partner/Dashboard/Profile
        public async Task<IActionResult> Profile()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Auth", new { area = "" });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            return View(user);
        }

        // POST /Partner/Dashboard/Profile
        [HttpPost]
        public async Task<IActionResult> Profile(string? phone, string? partnerAddress, double? partnerLatitude, double? partnerLongitude)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Auth", new { area = "" });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(phone))
                user.Phone = phone;

            user.PartnerAddress = partnerAddress;
            if (partnerLatitude.HasValue) user.PartnerLatitude = partnerLatitude;
            if (partnerLongitude.HasValue) user.PartnerLongitude = partnerLongitude;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật thông tin hồ sơ thành công!";
            return RedirectToAction("Profile");
        }

        // GET /Partner/Dashboard/AddCar
        public IActionResult AddCar()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Auth", new { area = "" });
            ViewBag.Locations = _db.Locations.ToList();
            ViewBag.CarTypes = _db.CarTypes.ToList();
            ViewBag.Brands = _db.CarBrands.ToList();
            ViewBag.Statuses = _db.CarStatuses.ToList();
            return View();
        }

        // POST /Partner/Dashboard/AddCar
        [HttpPost]
        public async Task<IActionResult> AddCar(Car car, string? CarAddress, string? Latitude, string? Longitude, IFormFile? imageFile)
        {
            if (!string.IsNullOrEmpty(CarAddress)) car.CarAddress = CarAddress;
            if (double.TryParse(Latitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat)) car.Latitude = lat;
            if (double.TryParse(Longitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng)) car.Longitude = lng;

            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Auth", new { area = "" });

            car.OwnerID = userId;
            car.CommissionRate = 20m;
            car.CreatedAt = DateTime.Now;

            if (car.PricePerHour == 0)
            {
                car.PricePerHour = Math.Round(car.PricePerDay / 8m);
            }

            // Set status to 'Pending' for Admin approval
            var pendingStatus = await _db.CarStatuses.FirstOrDefaultAsync(s => s.Name == "Pending");
            if (pendingStatus != null)
                car.CarStatusID = pendingStatus.CarStatusID;

            // Handle image upload
            var savedPath = await SaveCarImageAsync(imageFile);
            if (savedPath != null)
                car.ImageURL = savedPath;

            _db.Cars.Add(car);
            await _db.SaveChangesAsync();

            // Also save to CarImages table for multi-image support
            if (savedPath != null)
            {
                _db.CarImages.Add(new CarImage { CarID = car.CarID, ImageUrl = savedPath });
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Xe đã được đăng thành công lên sàn!";
            return RedirectToAction("MyCars");
        }

        // GET /Partner/Dashboard/MyCars
        public async Task<IActionResult> MyCars()
        {
            var userId = GetCurrentUserId();
            var myCars = await _db.Cars
                .Include(c => c.CarStatus)
                .Include(c => c.CarType)
                .Include(c => c.CarBrand)
                .Where(c => c.OwnerID == userId)
                .ToListAsync();
            return View(myCars);
        }

        // GET /Partner/Dashboard/EditCar/{id}
        public async Task<IActionResult> EditCar(int id)
        {
            var userId = GetCurrentUserId();
            var car = await _db.Cars.FirstOrDefaultAsync(c => c.CarID == id && c.OwnerID == userId);
            if (car == null) return NotFound();
            ViewBag.Locations = _db.Locations.ToList();
            ViewBag.CarTypes = _db.CarTypes.ToList();
            ViewBag.Brands = _db.CarBrands.ToList();
            ViewBag.Statuses = _db.CarStatuses.ToList();
            return View(car);
        }

        // POST /Partner/Dashboard/EditCar
        [HttpPost]
        public async Task<IActionResult> EditCar(Car car, string? CarAddress, string? Latitude, string? Longitude, IFormFile? imageFile)
        {
            if (!string.IsNullOrEmpty(CarAddress)) car.CarAddress = CarAddress;
            if (double.TryParse(Latitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat)) car.Latitude = lat;
            if (double.TryParse(Longitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng)) car.Longitude = lng;

            var userId = GetCurrentUserId();
            var existingCar = await _db.Cars.AsNoTracking().FirstOrDefaultAsync(c => c.CarID == car.CarID && c.OwnerID == userId);
            if (existingCar == null) return NotFound();

            car.OwnerID = userId;
            car.CommissionRate = existingCar.CommissionRate;
            car.CreatedAt = existingCar.CreatedAt;

            // Handle image upload — if no new file, keep old image
            var savedPath = await SaveCarImageAsync(imageFile);
            if (savedPath != null)
            {
                car.ImageURL = savedPath;

                // Add new image to CarImages table
                _db.CarImages.Add(new CarImage { CarID = car.CarID, ImageUrl = savedPath });
            }
            else
            {
                car.ImageURL = existingCar.ImageURL;
            }

            if (car.PricePerHour == 0)
            {
                car.PricePerHour = Math.Round(car.PricePerDay / 8m);
            }

            _db.Cars.Update(car);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Cập nhật thông tin xe thành công!";
            return RedirectToAction("MyCars");
        }

        // GET /Partner/Dashboard/Bookings
        public async Task<IActionResult> Bookings()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Auth", new { area = "" });

            var user = await _db.Users.FindAsync(userId);
            if (user == null || (user.Role != "Partner" && user.Role != "Admin"))
                return RedirectToAction("Register");

            var myCarIds = await _db.Cars.Where(c => c.OwnerID == userId).Select(c => c.CarID).ToListAsync();

            var bookings = await _db.Bookings
                .Include(b => b.Car).ThenInclude(c => c.CarBrand)
                .Include(b => b.User)
                .Include(b => b.BookingStatus)
                .Where(b => myCarIds.Contains(b.CarID) && b.BookingStatus.Name != "Draft")
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }
    }
}
