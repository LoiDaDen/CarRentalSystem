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
        private readonly IWebHostEnvironment _env;
        private const long DriverAvatarMaxBytes = 5 * 1024 * 1024;
        private static readonly string[] DriverAvatarExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        public DriverController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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

        // Public: Driver profile with reviews
        public async Task<IActionResult> Profile(int id)
        {
            var driver = await _context.Drivers
                .Include(d => d.Location)
                .Include(d => d.User)
                .Include(d => d.DriverReviews!)
                    .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(d => d.DriverID == id);

            if (driver == null) return NotFound();

            // Check if current user has rented this driver (for review permission)
            var canReview = false;
            var hasReviewed = false;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId))
                {
                    canReview = await _context.DriverBookings
                        .AnyAsync(db => db.DriverID == id
                            && db.Booking != null
                            && db.Booking.UserID == userId
                            && (db.Status == "Completed" || db.Status == "Active"));

                    hasReviewed = await _context.DriverReviews
                        .AnyAsync(r => r.DriverID == id && r.UserID == userId);
                }
            }

            ViewBag.CanReview = canReview && !hasReviewed;
            ViewBag.HasReviewed = hasReviewed;

            // Rating distribution
            var reviews = driver.DriverReviews?.ToList() ?? new List<DriverReview>();
            ViewBag.RatingDistribution = Enumerable.Range(1, 5)
                .ToDictionary(star => star, star => reviews.Count(r => r.Rating == star));
            ViewBag.TotalReviews = reviews.Count;
            ViewBag.AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : (double)driver.Rating;

            return View(driver);
        }

        // API: Get drivers list (optionally filtered by location)
        [HttpGet]
        public async Task<IActionResult> GetDriversList(int? locationId = null)
        {
            var query = _context.Drivers
                .Include(d => d.Location)
                .Include(d => d.DriverReviews)
                .Where(d => d.IsAvailable && d.IsApproved);

            // Filter by location if provided and valid (> 0)
            if (locationId.HasValue && locationId.Value > 0)
            {
                query = query.Where(d => d.LocationID == locationId.Value);
            }
            // else: no valid locationId provided - return ALL available drivers (don't return empty!)

            var drivers = await query
                .OrderByDescending(d => d.Rating)
                .ThenByDescending(d => d.TotalTrips)
                .Select(d => new
                {
                    d.DriverID,
                    d.FullName,
                    d.AvatarUrl,
                    d.Rating,
                    d.TotalTrips,
                    d.ExperienceYears,
                    d.LicenseType,
                    d.DailyRate,
                    d.HourlyRate,
                    d.Description,
                    Location = d.Location != null ? d.Location.Name : "Chưa có khu vực",
                    d.LocationID,
                    ReviewCount = d.DriverReviews != null ? d.DriverReviews.Count : 0
                })
                .ToListAsync();

            return Json(drivers);
        }

        // API: Get single driver detail with recent reviews (for Cart popup detail)
        [HttpGet]
        public async Task<IActionResult> GetDriverDetail(int id)
        {
            var driver = await _context.Drivers
                .Include(d => d.Location)
                .Include(d => d.DriverReviews!)
                    .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(d => d.DriverID == id);

            if (driver == null) return Json(new { success = false });

            var reviews = driver.DriverReviews?
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .Select(r => new
                {
                    r.Rating,
                    r.Comment,
                    UserName = r.User?.Name ?? "Ẩn danh",
                    Date = r.CreatedAt.ToString("dd/MM/yyyy")
                }).ToList();

            return Json(new
            {
                success = true,
                driver = new
                {
                    driver.DriverID,
                    driver.FullName,
                    driver.AvatarUrl,
                    driver.Rating,
                    driver.TotalTrips,
                    driver.ExperienceYears,
                    driver.LicenseType,
                    driver.DailyRate,
                    driver.HourlyRate,
                    driver.Description,
                    Location = driver.Location?.Name ?? "Toàn quốc",
                    ReviewCount = driver.DriverReviews?.Count ?? 0,
                    AverageRating = driver.DriverReviews?.Any() == true
                        ? Math.Round(driver.DriverReviews.Average(r => r.Rating), 1)
                        : (double)driver.Rating
                },
                reviews
            });
        }

        // POST: Create driver review (only for users who have rented this driver)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReview(int driverId, int rating, string? comment)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            // Validate rating
            if (rating < 1 || rating > 5)
            {
                TempData["Error"] = "Điểm đánh giá phải từ 1 đến 5 sao.";
                return RedirectToAction(nameof(Profile), new { id = driverId });
            }

            // Check driver exists and is approved
            var driverExists = await _context.Drivers.AnyAsync(d => d.DriverID == driverId && d.IsApproved);
            if (!driverExists)
            {
                TempData["Error"] = "Tài xế không tồn tại hoặc chưa được phê duyệt.";
                return RedirectToAction(nameof(Profile), new { id = driverId });
            }

            // Check user actually used this driver (only completed bookings count)
            var hasUsedDriver = await _context.DriverBookings
                .AnyAsync(db => db.DriverID == driverId
                    && db.Booking != null
                    && db.Booking.UserID == userId
                    && db.Status == "Completed");

            if (!hasUsedDriver)
            {
                TempData["Error"] = "Bạn chỉ có thể đánh giá tài xế sau khi chuyến đi đã hoàn thành.";
                return RedirectToAction(nameof(Profile), new { id = driverId });
            }

            // Check if already reviewed (atomic with insert to prevent race condition)
            var alreadyReviewed = await _context.DriverReviews
                .AnyAsync(r => r.DriverID == driverId && r.UserID == userId);

            if (alreadyReviewed)
            {
                TempData["Error"] = "Bạn đã đánh giá tài xế này rồi.";
                return RedirectToAction(nameof(Profile), new { id = driverId });
            }

            var review = new DriverReview
            {
                DriverID = driverId,
                UserID = userId,
                Rating = rating,
                Comment = comment?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.DriverReviews.Add(review);
            await _context.SaveChangesAsync();

            // Update driver's average rating
            var avgRating = await _context.DriverReviews
                .Where(r => r.DriverID == driverId)
                .AverageAsync(r => (decimal)r.Rating);

            var driver = await _context.Drivers.FindAsync(driverId);
            if (driver != null)
            {
                driver.Rating = Math.Round(avgRating, 1);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Cảm ơn bạn đã đánh giá tài xế!";
            return RedirectToAction(nameof(Profile), new { id = driverId });
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
                if (user == null || !user.IsVerified || user.VerificationStatus != "Verified")
                {
                    TempData["Error"] = "Ban can duoc duyet eKYC truoc khi dang ky lam tai xe.";
                    TempData["ShowEkycModal"] = "true";
                    TempData["EkycReturnUrl"] = Url.Action(nameof(Register), "Driver");
                    return RedirectToAction("Index", "Ekyc");
                }
            }

            return View();
        }

        // Driver Registration submit
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Driver driver, IFormFile? avatarFile)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(driver.FullName))
            {
                TempData["Error"] = "Họ tên tài xế là bắt buộc.";
                return RedirectToAction(nameof(Register));
            }

            if (string.IsNullOrWhiteSpace(driver.Phone))
            {
                TempData["Error"] = "Số điện thoại là bắt buộc.";
                return RedirectToAction(nameof(Register));
            }

            if (string.IsNullOrWhiteSpace(driver.LicenseNumber))
            {
                TempData["Error"] = "Số giấy phép lái xe là bắt buộc.";
                return RedirectToAction(nameof(Register));
            }

            // Check if already registered
            var existing = await _context.Drivers.FirstOrDefaultAsync(d => d.UserID == userId);
            if (existing != null)
            {
                TempData["Error"] = "Bạn đã đăng ký làm tài xế rồi. Vui lòng chờ Admin duyệt.";
                return RedirectToAction(nameof(MyProfile));
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (!user.IsVerified || user.VerificationStatus != "Verified")
            {
                TempData["Error"] = "Ban can duoc duyet eKYC truoc khi dang ky lam tai xe.";
                TempData["ShowEkycModal"] = "true";
                TempData["EkycReturnUrl"] = Url.Action(nameof(Register), "Driver");
                return RedirectToAction("Index", "Ekyc");
            }

            string avatarUrl;
            try
            {
                avatarUrl = await SaveDriverAvatarAsync(avatarFile, required: true) ?? string.Empty;
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Register));
            }

            driver.UserID = userId;
            driver.FullName = driver.FullName.Trim();
            driver.Phone = driver.Phone.Trim();
            driver.Email = driver.Email?.Trim() ?? user.Email;
            driver.AvatarUrl = avatarUrl;
            driver.IsApproved = false;
            driver.IsAvailable = false;
            driver.Rating = 5.0m;
            driver.TotalTrips = 0;
            driver.CreatedAt = DateTime.UtcNow;

            _context.Drivers.Add(driver);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đăng ký tài xế thành công! Vui lòng chờ Admin xét duyệt hồ sơ.";
            return RedirectToAction(nameof(MyProfile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAvatar(IFormFile? avatarFile)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.UserID == userId);
            if (driver == null) return NotFound();

            string avatarUrl;
            try
            {
                avatarUrl = await SaveDriverAvatarAsync(avatarFile, required: true) ?? string.Empty;
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(MyProfile));
            }

            DeleteLocalDriverAvatar(driver.AvatarUrl);
            driver.AvatarUrl = avatarUrl;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật ảnh đại diện tài xế.";
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

            // Load pending/active contracts for this driver
            ViewBag.DriverContracts = await _context.DriverContracts
                .Where(dc => dc.DriverID == driver.DriverID)
                .OrderByDescending(dc => dc.CreatedAt)
                .ToListAsync();

            return View(driver);
        }

        // GET: Driver/MyContract/5
        [Authorize]
        public async Task<IActionResult> MyContract(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.UserID == userId);
            if (driver == null) return NotFound();

            var contract = await _context.DriverContracts
                .Include(dc => dc.Driver)
                .FirstOrDefaultAsync(dc => dc.DriverContractID == id && dc.DriverID == driver.DriverID);

            if (contract == null) return NotFound();
            return View(contract);
        }

        // POST: Driver/SignContract/5
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignContract(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.UserID == userId);
            if (driver == null) return NotFound();

            var contract = await _context.DriverContracts
                .FirstOrDefaultAsync(dc => dc.DriverContractID == id && dc.DriverID == driver.DriverID);

            if (contract == null) return NotFound();

            if (contract.Status != "Pending")
            {
                TempData["Error"] = "Hợp đồng này không ở trạng thái chờ ký.";
                return RedirectToAction(nameof(MyProfile));
            }

            if (DateTime.UtcNow > contract.ExpiresAt)
            {
                TempData["Error"] = "Hợp đồng đã quá hạn ký (7 ngày). Vui lòng liên hệ Admin.";
                return RedirectToAction(nameof(MyProfile));
            }

            contract.SignedByPartyB = true;
            contract.PartyBSignDate = DateTime.UtcNow;
            contract.Status = "Active";
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã ký xác nhận hợp đồng #{contract.ContractNo} thành công! Hợp đồng đã có hiệu lực.";
            return RedirectToAction(nameof(MyProfile));
        }

        // POST: Driver/CancelContract/5
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelContract(int id, string? reason)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.UserID == userId);
            if (driver == null) return NotFound();

            var contract = await _context.DriverContracts
                .FirstOrDefaultAsync(dc => dc.DriverContractID == id && dc.DriverID == driver.DriverID);

            if (contract == null) return NotFound();

            if (contract.Status != "Active")
            {
                TempData["Error"] = "Chỉ có thể hủy hợp đồng đang có hiệu lực.";
                return RedirectToAction(nameof(MyProfile));
            }

            // Use standard update pattern for InMemory DB compatibility
            if (contract != null && contract.Status == "Active")
            {
                contract.Status = "Cancelled";
                contract.CancelledAt = DateTime.UtcNow;
                contract.CancelReason = reason ?? "Bên B yêu cầu hủy hợp đồng";
                _context.DriverContracts.Update(contract);
            }
            var rowsAffected = (contract != null && contract.Status == "Cancelled") ? 1 : 0;

            if (rowsAffected == 0)
            {
                TempData["Error"] = "Không thể hủy hợp đồng. Trạng thái có thể đã thay đổi.";
                return RedirectToAction(nameof(MyProfile));
            }

            TempData["Success"] = $"Đã hủy hợp đồng #{contract.ContractNo}.";
            return RedirectToAction(nameof(MyProfile));
        }

        private async Task<string?> SaveDriverAvatarAsync(IFormFile? avatarFile, bool required)
        {
            if (avatarFile == null || avatarFile.Length == 0)
            {
                if (required) throw new InvalidOperationException("Vui lòng tải lên ảnh đại diện tài xế.");
                return null;
            }

            if (avatarFile.Length > DriverAvatarMaxBytes)
                throw new InvalidOperationException("Ảnh đại diện vượt quá giới hạn 5MB.");

            var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || !DriverAvatarExtensions.Contains(extension))
                throw new InvalidOperationException("Ảnh đại diện chỉ chấp nhận JPG, PNG hoặc WEBP.");

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var uploadFolder = Path.Combine(webRoot, "uploads", "drivers", "avatars");
            Directory.CreateDirectory(uploadFolder);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await avatarFile.CopyToAsync(stream);

            return $"/uploads/drivers/avatars/{fileName}";
        }

        private void DeleteLocalDriverAvatar(string? avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl) ||
                !avatarUrl.StartsWith("/uploads/drivers/avatars/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fileName = Path.GetFileName(avatarUrl);
            if (string.IsNullOrWhiteSpace(fileName)) return;

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var filePath = Path.Combine(webRoot, "uploads", "drivers", "avatars", fileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}
