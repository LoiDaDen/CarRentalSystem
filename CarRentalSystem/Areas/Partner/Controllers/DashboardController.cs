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

        private async Task<IActionResult> RequirePartnerApproved()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Auth", new { area = "" });

            var user = await _db.Users.FindAsync(userId);
            if (user == null || (user.Role != "Partner" && user.Role != "Admin"))
                return RedirectToAction("Register");

            // Nếu là Partner nhưng chưa được duyệt → chuyển sang trang chờ
            if (user.Role == "Partner" && !user.IsPartnerApproved)
            {
                return View("PendingApproval", user);
            }

            return null!; // OK - tiếp tục
        }

        // ─── Saves an uploaded image file to wwwroot/uploads/cars/ ────────────────
        private async Task<string?> SaveCarImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return null;

            // 🔒 Validate file size (max 5MB)
            if (imageFile.Length > 5 * 1024 * 1024) return null;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext)) return null;

            var uploadDir = Path.Combine(_env.WebRootPath, "images", "cars");
            Directory.CreateDirectory(uploadDir); // ensure folder exists

            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return $"/images/cars/{fileName}";
        }

        // GET /Partner/Dashboard
        public async Task<IActionResult> Index()
        {
            var check = await RequirePartnerApproved();
            if (check != null) return check;

            var userId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(userId);

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
        public async Task<IActionResult> Register(string agreeTerms, string signContract)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Auth", new { area = "" });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            // Kiểm tra đã đồng ý điều khoản
            if (string.IsNullOrEmpty(agreeTerms))
            {
                TempData["Error"] = "Bạn phải đồng ý với điều khoản đối tác.";
                return RedirectToAction("Register");
            }

            // Kiểm tra đã ký hợp đồng
            if (string.IsNullOrEmpty(signContract))
            {
                TempData["Error"] = "Bạn phải ký hợp đồng hợp tác để trở thành đối tác.";
                return RedirectToAction("Register");
            }

            // 🔒 SECURITY: Chỉ Customer mới được đăng ký làm Partner
            if (user.Role == "Partner")
            {
                TempData["Info"] = "Bạn đã có quyền Partner.";
                return RedirectToAction("Index");
            }

            // Nếu là Admin/Manager/Staff đăng ký làm partner - lưu role gốc để không mất quyền
            if (user.Role == "Admin" || user.Role == "Manager" || user.Role == "Staff")
            {
                if (string.IsNullOrEmpty(user.OriginalRole))
                {
                    user.OriginalRole = user.Role;
                }
                // Không đổi role, chỉ đánh dấu partner pending
            }
            else
            {
                // Customer bình thường thì đổi role
                user.Role = "Partner";
            }

            user.IsPartnerApproved = false; // Chờ Admin duyệt
            user.IsVerified = false;

            // Tạo hợp đồng đối tác
            var contractCount = await _db.PartnerContracts.CountAsync() + 1;
            var contractNo = $"{contractCount:D4}/{DateTime.UtcNow.Year}/HĐHT-XT";
            var now = DateTime.UtcNow;

            var contract = new PartnerContract
            {
                ContractNo = contractNo,
                PartnerUserID = user.UserID,
                PartyAName = "Công ty TNHH Cho Thuê Xe CarRental",
                PartyAAddress = "TP. Hồ Chí Minh, Việt Nam",
                PartyBFullName = user.Name,
                PartyBCCCD = user.CitizenId ?? "",
                PartyBRevenuePercent = 80.00m,
                PartyARevenuePercent = 20.00m,
                PaymentDay = 5,
                PaymentCycle = "Hàng tháng",
                ContractDurationMonths = 0, // Vĩnh viễn
                NoticePeriodDays = 15,
                Status = "Pending", // Chờ Admin duyệt
                CreatedAt = now,
                ExpiresAt = now.AddDays(7),
                SignedByPartyA = true,
                PartyASignDate = now,
                SignedByPartyB = true, // Đã ký điện tử
                PartyBSignDate = now,
                TermsContent = GeneratePartnerContractTerms(user)
            };

            _db.PartnerContracts.Add(contract);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Bạn đã ký hợp đồng #{contractNo} thành công! Yêu cầu trở thành Đối tác đã được gửi. Vui lòng chờ Admin duyệt.";
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        // GET /Partner/Dashboard/Profile
        public async Task<IActionResult> Profile()
        {
            var check = await RequirePartnerApproved();
            if (check != null) return check;

            var userId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            return View(user);
        }

        // POST /Partner/Dashboard/Profile
        [HttpPost]
        public async Task<IActionResult> Profile(string? phone, string? partnerAddress, double? partnerLatitude, double? partnerLongitude)
        {
            var check = await RequirePartnerApproved();
            if (check != null) return check;

            var userId = GetCurrentUserId();
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
        public async Task<IActionResult> AddCar()
        {
            var check = await RequirePartnerApproved();
            if (check != null) return check;

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
            var check = await RequirePartnerApproved();
            if (check != null) return check;

            var userId = GetCurrentUserId();

            // Kiểm tra VIN đã tồn tại chưa
            var existingVin = await _db.Cars.AnyAsync(c => c.VIN == car.VIN && !c.IsDeleted);
            if (existingVin)
            {
                TempData["Error"] = "Xe với VIN này đã được đăng ký trong hệ thống.";
                return RedirectToAction("AddCar");
            }

            // Kiểm tra LicensePlate đã tồn tại chưa
            var existingPlate = await _db.Cars.AnyAsync(c => c.LicensePlate == car.LicensePlate && !c.IsDeleted);
            if (existingPlate)
            {
                TempData["Error"] = "Xe với biển số này đã được đăng ký trong hệ thống.";
                return RedirectToAction("AddCar");
            }

            if (!string.IsNullOrEmpty(CarAddress)) car.CarAddress = CarAddress;
            if (double.TryParse(Latitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat)) car.Latitude = lat;
            if (double.TryParse(Longitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng)) car.Longitude = lng;

            car.OwnerID = userId;
            car.CommissionRate = 20m;
            car.CreatedAt = DateTime.UtcNow;

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
            var check = await RequirePartnerApproved();
            if (check != null) return check;

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
            var check = await RequirePartnerApproved();
            if (check != null) return check;

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
            var check = await RequirePartnerApproved();
            if (check != null) return check;

            var userId = GetCurrentUserId();

            if (!string.IsNullOrEmpty(CarAddress)) car.CarAddress = CarAddress;
            if (double.TryParse(Latitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat)) car.Latitude = lat;
            if (double.TryParse(Longitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng)) car.Longitude = lng;

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
            var check = await RequirePartnerApproved();
            if (check != null) return check;

            var userId = GetCurrentUserId();
            var myCarIds = await _db.Cars.Where(c => c.OwnerID == userId).Select(c => c.CarID).ToListAsync();

            var bookings = await _db.Bookings
                .Include(b => b.Car).ThenInclude(c => c!.CarBrand)
                .Include(b => b.User)
                .Include(b => b.BookingStatus)
                .Where(b => myCarIds.Contains(b.CarID) && b.BookingStatus.Name != "Draft")
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }

        // ═══ HỢP ĐỒNG ĐỐI TÁC ═══

        // GET /Partner/Dashboard/Contract
        public async Task<IActionResult> Contract(string? status)
        {
            var check = await RequirePartnerApproved();
            if (check != null) return check;

            var userId = GetCurrentUserId();

            var query = _db.PartnerContracts
                .Where(pc => pc.PartnerUserID == userId)
                .OrderByDescending(pc => pc.CreatedAt)
                .AsQueryable();

            // Default: hide cancelled unless explicitly requested
            if (string.IsNullOrEmpty(status))
                query = query.Where(pc => pc.Status != "Cancelled");
            else if (status == "cancelled")
                query = query.Where(pc => pc.Status == "Cancelled");

            ViewBag.Status = status ?? "active";
            var contracts = await query.ToListAsync();
            return View(contracts);
        }

        // GET /Partner/Dashboard/ContractDetails/5
        public async Task<IActionResult> ContractDetails(int id)
        {
            var check = await RequirePartnerApproved();
            if (check != null) return check;

            var userId = GetCurrentUserId();

            var contract = await _db.PartnerContracts
                .FirstOrDefaultAsync(pc => pc.PartnerContractID == id && pc.PartnerUserID == userId);

            if (contract == null) return NotFound();
            return View(contract);
        }

        // POST /Partner/Dashboard/SignContract/5
        [HttpPost]
        public async Task<IActionResult> SignContract(int id)
        {
            var check = await RequirePartnerApproved();
            if (check != null) return check;

            var userId = GetCurrentUserId();

            var contract = await _db.PartnerContracts
                .FirstOrDefaultAsync(pc => pc.PartnerContractID == id && pc.PartnerUserID == userId);

            if (contract == null) return NotFound();

            if (contract.Status != "Pending")
            {
                TempData["Error"] = "Hợp đồng này không ở trạng thái chờ ký.";
                return RedirectToAction("Contract");
            }

            if (DateTime.UtcNow > contract.ExpiresAt)
            {
                TempData["Error"] = "Hợp đồng đã quá hạn ký (7 ngày). Vui lòng liên hệ Admin.";
                return RedirectToAction("Contract");
            }

            contract.SignedByPartyB = true;
            contract.PartyBSignDate = DateTime.UtcNow;
            contract.Status = "Active";
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Đã ký xác nhận hợp đồng #{contract.ContractNo} thành công! Hợp đồng đã có hiệu lực.";
            return RedirectToAction("Contract");
        }

        // POST /Partner/Dashboard/CancelContract/5
        [HttpPost]
        public async Task<IActionResult> CancelContract(int id, string? reason)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Auth", new { area = "" });

            var contract = await _db.PartnerContracts
                .FirstOrDefaultAsync(pc => pc.PartnerContractID == id && pc.PartnerUserID == userId);

            if (contract == null) return NotFound();

            if (contract.Status != "Active")
            {
                TempData["Error"] = "Chỉ có thể hủy hợp đồng đang có hiệu lực.";
                return RedirectToAction("Contract");
            }

            // Lấy user để đổi role
            var user = await _db.Users.FindAsync(userId);

            // Hủy hợp đồng
            contract.Status = "Cancelled";
            contract.CancelledAt = DateTime.UtcNow;
            contract.CancelReason = reason ?? "Bên B yêu cầu hủy hợp đồng";

            // Đổi role về Customer
            if (user != null)
            {
                user.Role = "Customer";
                user.IsPartnerApproved = false;
            }

            await _db.SaveChangesAsync();

            TempData["Info"] = $"Bạn đã hủy hợp đồng #{contract.ContractNo}. Tài khoản đã được chuyển về vai trò Khách hàng.";
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        private string GeneratePartnerContractTerms(User user)
        {
            return @"ĐIỀU 1: NỘI DUNG HỢP TÁC
1.1. Bên B đồng ý ký gửi xe ô tô cho Bên A để thực hiện dịch vụ cho thuê xe tự lái.
1.2. Bên A có trách nhiệm tìm kiếm khách hàng, thực hiện quy trình cho thuê, quản lý tài sản và thu tiền từ khách hàng.

ĐIỀU 2: PHÂN CHIA DOANH THU (80/20)
2.1. Tỷ lệ phân chia: Doanh thu thực tế từ việc cho thuê xe (sau khi trừ các mã giảm giá nếu có) sẽ được chia như sau:
- Bên B (Chủ xe): Nhận 80% doanh thu.
- Bên A (Vận hành): Nhận 20% doanh thu (Phí quản lý, tìm khách và vận hành).
2.2. Kỳ thanh toán: Bên A tổng kết và chuyển thanh toán cho Bên B vào ngày 05 hàng tháng.

ĐIỀU 3: CHI PHÍ VẬN HÀNH
3.1. Bên B chịu trách nhiệm: Phí bảo hiểm vật chất xe, phí đường bộ, đăng kiểm, và các hư hỏng do hao mòn tự nhiên hoặc lỗi kỹ thuật của xe.
3.2. Bên A chịu trách nhiệm: Chi phí marketing, nhân sự giao nhận xe, vệ sinh xe cơ bản sau mỗi lượt thuê.

ĐIỀU 4: TRÁCH NHIỆM BẢO VỆ TÀI SẢN
4.1. Bên A có trách nhiệm kiểm tra kỹ hồ sơ khách thuê (CCCD, GPLX, tài sản thế chấp) để giảm thiểu rủi ro.
4.2. Trong trường hợp khách thuê gây ra va chạm/tai nạn:
- Bên A hỗ trợ làm việc với bảo hiểm và khách thuê để bồi thường cho Bên B.
- Nếu không có bảo hiểm, Bên A có trách nhiệm yêu cầu khách thuê đền bù trực tiếp cho Bên B theo giá trị thiệt hại thực tế.
4.3. Phạt nguội: Bên A có trách nhiệm truy thu tiền phạt từ khách thuê nếu phát sinh vi phạm trong thời gian thuê.

ĐIỀU 5: THỜI HẠN VÀ CHẤM DỨT
5.1. Thời hạn hợp đồng: Vĩnh viễn kể từ ngày ký.
5.2. Nếu một trong hai bên muốn chấm dứt hợp đồng, phải báo trước cho bên kia ít nhất 15 ngày.";
        }
    }
}
