using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

using CarRentalSystem.Services;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPiiEncryptionService _piiEncryption;

        public CartController(ApplicationDbContext context, IPiiEncryptionService piiEncryption)
        {
            _context = context;
            _piiEncryption = piiEncryption;
        }

        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Car)
                        .ThenInclude(car => car.CarImages)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Car)
                        .ThenInclude(car => car.CarBrand)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Car)
                        .ThenInclude(car => car.Location)
                .FirstOrDefaultAsync(c => c.UserID == userId);

            if (cart == null)
            {
                cart = new Cart { UserID = userId };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            // Ensure standard 'Theo tuần' (Weekly) package exists if not already present
            if (!await _context.ContractTypes.AnyAsync(c => c.Name == "Theo tuần"))
            {
                _context.ContractTypes.Add(new ContractType 
                { 
                    Name = "Theo tuần", 
                    DiscountPercent = 5, 
                    MinDurationDays = 7, 
                    MaxDurationDays = 30, 
                    IsActive = true,
                    Description = "Thuê xe theo tuần, giảm 5%"
                });
                await _context.SaveChangesAsync();
            }

            var currentUser = await _context.Users.Include(u => u.MembershipTier).FirstOrDefaultAsync(u => u.UserID == userId);
            ViewBag.MembershipTier = currentUser?.MembershipTier;

            ViewBag.ContractTypes = await _context.ContractTypes
                .Where(c => c.IsActive 
                    && c.Name.ToLower() != "ngày chẵn" 
                    && c.Name.ToLower() != "ngày lẻ" 
                    && c.Name.ToLower() != "cuối tuần"
                    && c.Name.ToLower() != "ngày tùy chỉnh")
                .OrderBy(c => c.MinDurationDays)
                .ToListAsync();

            ViewBag.Drivers = await _context.Drivers
                .Where(d => d.IsAvailable && d.IsApproved)
                .OrderByDescending(d => d.Rating)
                .ToListAsync();

            ViewBag.AddOns = await _context.AddOns
                .Where(a => a.IsActive)
                .OrderBy(a => a.PricePerDay)
                .ToListAsync();

            return View(cart);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddToCart(int carId, DateTime startDate, DateTime endDate, bool withDriver, decimal estimatedPrice)
        {
            // Validate input
            if (carId <= 0)
            {
                return Json(new { success = false, message = "ID xe không hợp lệ." });
            }

            if (startDate >= endDate)
            {
                return Json(new { success = false, message = "Ngày bắt đầu phải trước ngày kết thúc." });
            }

            if (startDate < DateTime.UtcNow.AddHours(-1))
            {
                return Json(new { success = false, message = "Không thể đặt xe trong quá khứ." });
            }

            var userId = GetUserId();
            var cart = await _context.Carts.Include(c => c.CartItems).FirstOrDefaultAsync(c => c.UserID == userId);

            if (cart == null)
            {
                cart = new Cart { UserID = userId };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            // Check if car is already in cart for same dates
            var existing = cart.CartItems.FirstOrDefault(ci => ci.CarID == carId && ci.StartDate == startDate && ci.EndDate == endDate);
            if (existing != null)
            {
                return Json(new { success = false, message = "Xe này đã có trong giỏ hàng với cùng khoảng thời gian." });
            }

            // Kiểm tra car có tồn tại và available không
            var car = await _context.Cars
                .Include(c => c.CarStatus)
                .FirstOrDefaultAsync(c => c.CarID == carId);

            if (car == null)
            {
                return Json(new { success = false, message = "Xe không tồn tại." });
            }

            if (car.CarStatus?.Name != "Available")
            {
                return Json(new { success = false, message = "Xe này hiện không có sẵn để thuê." });
            }

            // Kiểm tra car không bị booking trong khoảng thời gian này
            var hasBooking = await _context.Bookings
                .Include(b => b.BookingStatus)
                .AnyAsync(b => b.CarID == carId &&
                    b.BookingStatus != null &&
                    b.BookingStatus.Name != "Cancelled" &&
                    b.BookingStatus.Name != "Completed" &&
                    b.BookingStatus.Name != "Rejected" &&
                    startDate < b.EndDate &&
                    endDate > b.StartDate);

            if (hasBooking)
            {
                return Json(new { success = false, message = "Xe này đã có người đặt trong khoảng thời gian bạn chọn." });
            }

            var newItem = new CartItem
            {
                CartID = cart.CartID,
                CarID = carId,
                StartDate = startDate,
                EndDate = endDate,
                WithDriver = withDriver,
                EstimatedPrice = estimatedPrice
            };

            _context.CartItems.Add(newItem);
            await _context.SaveChangesAsync();

            var count = await _context.CartItems.CountAsync(ci => ci.CartID == cart.CartID);
            return Json(new { success = true, count = count });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            var userId = GetUserId();
            var item = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemID == id && ci.Cart.UserID == userId);

            if (item != null)
            {
                var cartId = item.CartID;
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
                
                var count = await _context.CartItems.CountAsync(ci => ci.CartID == cartId);
                return Json(new { success = true, count = count });
            }

            return Json(new { success = false, message = "Không tìm thấy sản phẩm hoặc bạn không có quyền xóa." });
        }

        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            if (!User.Identity.IsAuthenticated) return Json(new { count = 0 });

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Json(new { count = 0 });

            var count = await _context.CartItems
                .Where(ci => ci.Cart.UserID == userId)
                .CountAsync();

            return Json(new { count = count });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateCartItemDates(int cartItemId, DateTime startDate, DateTime endDate, decimal estimatedPrice)
        {
            var userId = GetUserId();
            var item = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemID == cartItemId && ci.Cart.UserID == userId);

            if (item != null)
            {
                item.StartDate = startDate;
                item.EndDate = endDate;
                item.EstimatedPrice = estimatedPrice;
                _context.CartItems.Update(item);
                await _context.SaveChangesAsync();

                var cartTotal = await _context.CartItems
                    .Where(ci => ci.CartID == item.CartID)
                    .SumAsync(ci => ci.EstimatedPrice);

                return Json(new { success = true, cartTotal = cartTotal });
            }

            return Json(new { success = false, message = "Không tìm thấy xe trong giỏ hàng." });
        }
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateCartItemDriver(int cartItemId, int? driverId)
        {
            var userId = GetUserId();
            var item = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemID == cartItemId && ci.Cart.UserID == userId);

            if (item != null)
            {
                item.WithDriver = driverId.HasValue && driverId.Value > 0;
                item.DriverID = (driverId.HasValue && driverId.Value > 0) ? driverId : null;
                _context.CartItems.Update(item);
                await _context.SaveChangesAsync();

                Driver? driver = null;
                if (item.DriverID.HasValue)
                    driver = await _context.Drivers.FindAsync(item.DriverID.Value);

                return Json(new { success = true, withDriver = item.WithDriver, driverName = driver?.FullName, dailyRate = driver?.DailyRate ?? 0 });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateCartItemPackage(int cartItemId, int packageId)
        {
            var userId = GetUserId();
            var item = await _context.CartItems
                .Include(ci => ci.Car)
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemID == cartItemId && ci.Cart.UserID == userId);

            if (item == null)
                return Json(new { success = false, message = "Khong tim thay san pham" });

            var package = await _context.ContractTypes.FindAsync(packageId);
            if (package == null)
                return Json(new { success = false, message = "Khong tim thay goi thue" });

            item.ContractTypeID = packageId;

            // ===== Tinh lai EstimatedPrice voi discount =====
            var hours = (int)Math.Ceiling((item.EndDate - item.StartDate).TotalHours);
            var days = Math.Max(1, (int)Math.Ceiling(hours / 24.0));

            // Tinh gia co ban
            decimal p4 = (item.Car?.PricePerDay ?? 600000m) * 0.4m;
            decimal p8 = (item.Car?.PricePerDay ?? 600000m) * 0.7m;
            decimal p12 = (item.Car?.PricePerDay ?? 600000m) * 0.85m;
            decimal p24 = item.Car?.PricePerDay ?? 600000m;
            decimal basePrice = 0;
            int fullDays = hours / 24, rem = hours % 24;
            if (hours <= 4) basePrice = Math.Min(hours * (item.Car?.PricePerHour ?? 50000m), p4);
            else if (hours <= 8) basePrice = Math.Min(p4 + (hours - 4) * (item.Car?.PricePerHour ?? 50000m), p8);
            else if (hours <= 12) basePrice = Math.Min(p8 + (hours - 8) * (item.Car?.PricePerHour ?? 50000m), p12);
            else if (hours <= 24) basePrice = Math.Min(p12 + (hours - 12) * (item.Car?.PricePerHour ?? 50000m), p24);
            else
            {
                decimal remP = rem <= 4 ? Math.Min(rem * (item.Car?.PricePerHour ?? 50000m), p4)
                    : rem <= 8 ? Math.Min(p4 + (rem - 4) * (item.Car?.PricePerHour ?? 50000m), p8)
                    : rem <= 12 ? Math.Min(p8 + (rem - 8) * (item.Car?.PricePerHour ?? 50000m), p12)
                    : Math.Min(p12 + (rem - 12) * (item.Car?.PricePerHour ?? 50000m), p24);
                basePrice = (fullDays * p24) + remP;
            }

            // Lay driver rate
            decimal driverRate = 0;
            if (item.DriverID.HasValue && item.DriverID > 0)
            {
                var driver = await _context.Drivers.FindAsync(item.DriverID.Value);
                if (driver != null) driverRate = driver.DailyRate;
            }

            var driverDays = Math.Max(1, days);
            decimal carAndDriverPrice = basePrice + (driverRate * driverDays);

            // Lay addon prices
            decimal addonTotal = 0;
            foreach (var adId in item.AddOnIDs)
            {
                var ad = await _context.AddOns.FindAsync(adId);
                if (ad != null) addonTotal += ad.PricePerDay * days;
            }

            decimal originalPrice = carAndDriverPrice + addonTotal;

            // Ap dung discount goi
            decimal discountedCarPrice = originalPrice;
            if (package.DiscountPercent > 0)
            {
                discountedCarPrice = carAndDriverPrice * (1 - package.DiscountPercent / 100m) + addonTotal;
            }

            decimal originalPrice_rounded = Math.Round(originalPrice);
            decimal estimatedPrice = Math.Round(discountedCarPrice);

            item.EstimatedPrice = estimatedPrice;
            _context.CartItems.Update(item);
            await _context.SaveChangesAsync();

            return Json(new { success = true, packageName = package.Name, discount = package.DiscountPercent, estimatedPrice, originalPrice = originalPrice_rounded });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ToggleAddon(int cartItemId, int addonId, bool selected)
        {
            var userId = GetUserId();
            var item = await _context.CartItems
                .Include(ci => ci.Car)
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemID == cartItemId && ci.Cart.UserID == userId);

            if (item == null)
                return Json(new { success = false, message = "Khong tim thay san pham" });

            var currentAddOns = item.AddOnIDs;

            if (selected)
            {
                if (!currentAddOns.Contains(addonId))
                    currentAddOns.Add(addonId);
            }
            else
            {
                currentAddOns.Remove(addonId);
            }

            item.AddOnIDs = currentAddOns;

            // ===== Tinh lai EstimatedPrice bao gom addons =====
            var hours = (int)Math.Ceiling((item.EndDate - item.StartDate).TotalHours);
            var days = Math.Max(1, (int)Math.Ceiling(hours / 24.0));

            // Lay addon prices
            decimal addonTotal = 0;
            foreach (var adId in item.AddOnIDs)
            {
                var ad = await _context.AddOns.FindAsync(adId);
                if (ad != null) addonTotal += ad.PricePerDay * days;
            }

            // Lay driver rate
            decimal driverRate = 0;
            if (item.DriverID.HasValue && item.DriverID > 0)
            {
                var driver = await _context.Drivers.FindAsync(item.DriverID.Value);
                if (driver != null) driverRate = driver.DailyRate;
            }

            // Tinh gia co ban (khong co addon)
            decimal p4 = (item.Car?.PricePerDay ?? 600000m) * 0.4m;
            decimal p8 = (item.Car?.PricePerDay ?? 600000m) * 0.7m;
            decimal p12 = (item.Car?.PricePerDay ?? 600000m) * 0.85m;
            decimal p24 = item.Car?.PricePerDay ?? 600000m;
            decimal basePrice = 0;
            int fullDays = hours / 24, rem = hours % 24;
            if (hours <= 4) basePrice = Math.Min(hours * (item.Car?.PricePerHour ?? 50000m), p4);
            else if (hours <= 8) basePrice = Math.Min(p4 + (hours - 4) * (item.Car?.PricePerHour ?? 50000m), p8);
            else if (hours <= 12) basePrice = Math.Min(p8 + (hours - 8) * (item.Car?.PricePerHour ?? 50000m), p12);
            else if (hours <= 24) basePrice = Math.Min(p12 + (hours - 12) * (item.Car?.PricePerHour ?? 50000m), p24);
            else basePrice = (fullDays * p24) + (rem <= 4 ? Math.Min(rem * (item.Car?.PricePerHour ?? 50000m), p4) : rem <= 8 ? Math.Min(p4 + (rem - 4) * (item.Car?.PricePerHour ?? 50000m), p8) : rem <= 12 ? Math.Min(p8 + (rem - 8) * (item.Car?.PricePerHour ?? 50000m), p12) : Math.Min(p12 + (rem - 12) * (item.Car?.PricePerHour ?? 50000m), p24));

            var driverDays = Math.Max(1, days);
            decimal estimatedPrice = basePrice + (driverRate * driverDays);

            // Cong addon vao
            estimatedPrice += addonTotal;

            item.EstimatedPrice = estimatedPrice;
            _context.CartItems.Update(item);
            await _context.SaveChangesAsync();

            return Json(new { success = true, estimatedPrice = item.EstimatedPrice, addonTotal });
        }

        [HttpPost]
        public async Task<IActionResult> CheckoutAll()
        {
            var userId = GetUserId();
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Car)
                        .ThenInclude(car => car.CarBrand)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Car)
                        .ThenInclude(car => car.Location)
                .FirstOrDefaultAsync(c => c.UserID == userId);

            if (cart == null || !cart.CartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction("Index");
            }

            var user = await _context.Users.Include(u => u.MembershipTier).FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null) return RedirectToAction("Login", "Auth");

            // ===== 1. Validate: tat ca cac xe deu phai cach nhau >= 1 tieng =====
            var items = cart.CartItems.ToList();
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                {
                    // Neu cung xe, kiem tra khoang cach thoi gian
                    if (items[i].CarID == items[j].CarID)
                    {
                        var gapHours = Math.Min(
                            (items[i].StartDate - items[j].EndDate).TotalHours,
                            (items[j].StartDate - items[i].EndDate).TotalHours);
                        if (gapHours >= 0 && gapHours < 1)
                        {
                            TempData["Error"] = $"Xe '{items[i].Car?.CarName}' có 2 đơn đặt cách nhau chỉ {gapHours:F1} giờ. Vui lòng đảm bảo khoảng cách tối thiểu 1 tiếng giữa các đơn cùng xe.";
                            return RedirectToAction("Index");
                        }
                    }
                }
            }

            // ===== 2. Kiem tra overlap voi booking da ton tai =====
            var overlapErrors = new List<string>();
            foreach (var item in items)
            {
                bool isOverlapped = await _context.Bookings
                    .Include(b => b.BookingStatus)
                    .AnyAsync(b => b.CarID == item.CarID &&
                        b.BookingStatus != null &&
                        b.BookingStatus.Name != "Cancelled" && b.BookingStatus.Name != "Completed" &&
                        !(((item.StartDate >= b.EndDate.AddHours(1)) || (item.EndDate <= b.StartDate.AddHours(-1)) ||
                          (item.StartDate <= b.StartDate.AddHours(-1) && item.EndDate >= b.EndDate.AddHours(1)))));

                if (isOverlapped)
                {
                    overlapErrors.Add($"'{item.Car?.CarName}' đã có người đặt trong khoảng thời gian bạn chọn (cần cách ít nhất 1 tiếng).");
                }
            }

            if (overlapErrors.Any())
            {
                TempData["Error"] = string.Join(" ", overlapErrors);
                return RedirectToAction("Index");
            }

            // ===== 3. Tinh toan tong tien (bao gom add-ons) =====
            var addons = await _context.AddOns.ToListAsync();
            decimal baseTotal = 0;
            var itemDetails = new List<object>();

            foreach (var item in items)
            {
                var hours = (int)Math.Ceiling((item.EndDate - item.StartDate).TotalHours);
                var days = (int)Math.Ceiling(hours / 24.0);

                // Lay addon da chon
                var selectedAddonIds = item.AddOnIDs;
                decimal addonTotal = 0;
                var addonDetails = new List<object>();
                foreach (var addonId in selectedAddonIds)
                {
                    var addon = addons.FirstOrDefault(a => a.AddOnID == addonId);
                    if (addon != null)
                    {
                        var addonCost = addon.PricePerDay * days;
                        addonTotal += addonCost;
                        addonDetails.Add(new { id = addon.AddOnID, name = addon.Name, pricePerDay = addon.PricePerDay, total = addonCost });
                    }
                }

                decimal itemTotal = item.EstimatedPrice + addonTotal;
                baseTotal += itemTotal;

                itemDetails.Add(new
                {
                    carId = item.CarID,
                    carName = item.Car?.CarName,
                    carBrand = item.Car?.CarBrand?.Name,
                    licensePlate = item.Car?.LicensePlate,
                    locationId = item.Car?.LocationID,
                    startDate = item.StartDate,
                    endDate = item.EndDate,
                    hours,
                    days,
                    withDriver = item.WithDriver,
                    driverId = item.DriverID,
                    contractTypeId = item.ContractTypeID,
                    basePrice = item.EstimatedPrice,
                    addonTotal,
                    addonDetails,
                    totalPrice = itemTotal,
                    defaultDeposit = item.Car?.DefaultDeposit ?? 0
                });
            }

            decimal memDiscount = user?.MembershipTier?.DiscountPercent ?? 0;
            decimal discountAmount = baseTotal * (memDiscount / 100m);
            decimal totalAmount = baseTotal - discountAmount;
            decimal totalDeposit = items.Sum(i => (i.Car?.DefaultDeposit ?? 0));

            // ===== 4. Luu transaction voi PendingCartSnapshot (chua tao booking) =====
            var transaction = new BookingTransaction
            {
                UserID = userId,
                TotalAmount = totalAmount,
                DiscountAmount = discountAmount,
                MembershipDiscountPercent = (int)memDiscount,
                Status = BookingStatusConstants.Unpaid,
                CreatedAt = DateTime.UtcNow,
                PendingCartSnapshot = System.Text.Json.JsonSerializer.Serialize(new
                {
                    items = itemDetails,
                    totalDeposit,
                    baseTotal,
                    discountAmount,
                    totalAmount,
                    membershipName = user?.MembershipTier?.Name,
                    userName = user?.Name,
                    userEmail = user?.Email,
                    userPhone = user?.Phone,
                    userAddress = user?.Address
                })
            };

            _context.BookingTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            // ===== 5. Xoa cart items =====
            _context.CartItems.RemoveRange(cart.CartItems);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đơn hàng #{transaction.TransactionID} đã được tạo. Vui lòng thanh toán tiền cọc {totalDeposit.ToString("N0")}đ để xác nhận.";
            return RedirectToAction("DepositTransaction", "Booking", new { id = transaction.TransactionID });
        }

        private int GetUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdString, out int userId);
            return userId;
        }
    }
}
