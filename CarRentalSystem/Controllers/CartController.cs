using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPiiEncryptionService _piiEncryption;
        private readonly IReservationService _reservationService;
        private readonly IBookingQuoteService _quoteService;
        private readonly IPayOSPaymentService _payOSPaymentService;
        private readonly IEligibilityService _eligibilityService;

        public CartController(
            ApplicationDbContext context,
            IPiiEncryptionService piiEncryption,
            IReservationService reservationService,
            IBookingQuoteService quoteService,
            IPayOSPaymentService payOSPaymentService,
            IEligibilityService eligibilityService)
        {
            _context = context;
            _piiEncryption = piiEncryption;
            _reservationService = reservationService;
            _quoteService = quoteService;
            _payOSPaymentService = payOSPaymentService;
            _eligibilityService = eligibilityService;
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

            if (HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {

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

            ViewBag.AddOns = await _context.AddOns
                .Where(a => a.IsActive)
                .OrderBy(a => a.PricePerDay)
                .ToListAsync();

            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int carId, DateTime startDate, DateTime endDate, bool withDriver, decimal? estimatedPrice)
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

            // Kiem tra car co ton tai va available khong
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

            if (!car.IsSelfDriveAllowed || car.IsWithDriverAllowed)
            {
                return Json(new { success = false, message = "Xe này không thuộc luồng thuê xe tự lái." });
            }

            var availability = await _reservationService.CheckCarAvailabilityAsync(carId, startDate, endDate);
            if (!availability.IsAvailable)
            {
                return Json(new { success = false, message = availability.Reason ?? "Xe này đã có người đặt trong khoảng thời gian bạn chọn." });
            }

            // =========================================
            // RECALCULATE PRICE PHIA SERVER
            // BO qua client estimatedPrice
            // =========================================
            var calculatedPrice = CalculateCartItemPrice(car, startDate, endDate, false);

            var newItem = new CartItem
            {
                CartID = cart.CartID,
                CarID = carId,
                StartDate = startDate,
                EndDate = endDate,
                WithDriver = false,
                DriverID = null,
                EstimatedPrice = calculatedPrice // Server-side calculated price
            };

            _context.CartItems.Add(newItem);
            await _context.SaveChangesAsync();

            var count = await _context.CartItems.CountAsync(ci => ci.CartID == cart.CartID);
            return Json(new { success = true, count = count });
        }

        [HttpPost]
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
            if (User.Identity?.IsAuthenticated != true) return Json(new { count = 0 });

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Json(new { count = 0 });

            var count = await _context.CartItems
                .Where(ci => ci.Cart.UserID == userId)
                .CountAsync();

            return Json(new { count = count });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCartItemDates(int cartItemId, DateTime startDate, DateTime endDate, decimal? estimatedPrice)
        {
            var userId = GetUserId();
            var item = await _context.CartItems
                .Include(ci => ci.Car)
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemID == cartItemId && ci.Cart.UserID == userId);

            if (item != null)
            {
                // Recalculate price phia server
                var calculatedPrice = CalculateCartItemPrice(item.Car, startDate, endDate, false);

                item.StartDate = startDate;
                item.EndDate = endDate;
                item.WithDriver = false;
                item.DriverID = null;
                item.EstimatedPrice = calculatedPrice; // Server-side price
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
        public async Task<IActionResult> UpdateCartItemDriver(int cartItemId, int? driverId)
        {
            var userId = GetUserId();
            var item = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemID == cartItemId && ci.Cart.UserID == userId);

            if (item != null)
            {
                item.WithDriver = false;
                item.DriverID = null;
                _context.CartItems.Update(item);
                await _context.SaveChangesAsync();

                return Json(new { success = true, withDriver = false, driverName = (string?)null, dailyRate = 0 });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCartItemPackage(int cartItemId, int packageId)
        {
            var userId = GetUserId();
            var item = await _context.CartItems
                .Include(ci => ci.Car)
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemID == cartItemId && ci.Cart.UserID == userId);

            if (item == null)
                return Json(new { success = false, message = "Không tìm thấy sản phẩm" });

            var package = await _context.ContractTypes.FindAsync(packageId);
            if (package == null)
                return Json(new { success = false, message = "Không tìm thấy gói thuê" });

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

            item.WithDriver = false;
            item.DriverID = null;
            decimal carAndDriverPrice = basePrice;

            // Lay addon prices
            decimal addonTotal = 0;
            foreach (var adId in item.AddOnIDs)
            {
                var ad = await _context.AddOns.FindAsync(adId);
                if (ad != null) addonTotal += ad.PricePerDay * days;
            }

            decimal originalPrice = carAndDriverPrice + addonTotal;

            // Ap dung discount goi cho gia xe/tai xe; addon duoc tinh rieng khi checkout.
            decimal discountedCarPrice = carAndDriverPrice;
            if (package.DiscountPercent > 0)
            {
                discountedCarPrice = carAndDriverPrice * (1 - package.DiscountPercent / 100m);
            }

            decimal originalPrice_rounded = Math.Round(originalPrice);
            decimal estimatedPrice = Math.Round(discountedCarPrice);
            decimal displayPrice = Math.Round(estimatedPrice + addonTotal);

            item.EstimatedPrice = estimatedPrice;
            _context.CartItems.Update(item);
            await _context.SaveChangesAsync();

            return Json(new { success = true, packageName = package.Name, discount = package.DiscountPercent, estimatedPrice = displayPrice, basePrice = estimatedPrice, addonTotal, originalPrice = originalPrice_rounded });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAddon(int cartItemId, int addonId, bool selected)
        {
            var userId = GetUserId();
            var item = await _context.CartItems
                .Include(ci => ci.Car)
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemID == cartItemId && ci.Cart.UserID == userId);

            if (item == null)
                return Json(new { success = false, message = "Không tìm thấy sản phẩm" });

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

            item.WithDriver = false;
            item.DriverID = null;
            decimal estimatedPrice = basePrice;

            item.EstimatedPrice = estimatedPrice;
            _context.CartItems.Update(item);
            await _context.SaveChangesAsync();

            return Json(new { success = true, estimatedPrice = item.EstimatedPrice + addonTotal, basePrice = item.EstimatedPrice, addonTotal });
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

            // =========================================
            // SECURITY: Kiểm tra blacklist và eKYC trước khi checkout
            // =========================================
            var eligibility = await _eligibilityService.CheckRentalEligibilityAsync(userId);
            if (!eligibility.IsEligible)
            {
                if (eligibility.FailureCode == EligibilityFailureCode.EkycNotVerified ||
                    eligibility.FailureCode == EligibilityFailureCode.MissingIdentityDocuments)
                {
                    TempData["ShowEkycModal"] = "true";
                    TempData["EkycReturnUrl"] = Url.Action("Index", "Cart") ?? "/Cart";
                    TempData["WarningMessage"] = eligibility.Reason ?? "Cần xác thực eKYC trước khi thuê xe.";
                }
                else
                {
                    TempData["Error"] = eligibility.Reason ?? "Tài khoản chưa đủ điều kiện đặt xe.";
                }

                return RedirectToAction("Index");
            }

            // ===== 1. Validate: Tat ca cac xe trong cart phai khong overlap voi nhau =====
            var items = cart.CartItems.ToList();
            foreach (var item in items.Where(i => i.WithDriver || i.DriverID != null))
            {
                item.WithDriver = false;
                item.DriverID = null;
            }
            await _context.SaveChangesAsync();

            foreach (var sameCarItems in items.GroupBy(i => i.CarID))
            {
                var ordered = sameCarItems.OrderBy(i => i.StartDate).ToList();
                for (var i = 1; i < ordered.Count; i++)
                {
                    var previous = ordered[i - 1];
                    var current = ordered[i];
                    var carName = current.Car?.CarName ?? previous.Car?.CarName ?? "xe";

                    if (previous.EndDate > current.StartDate)
                    {
                        TempData["Error"] = $"Xe '{carName}' co 2 don bi trung lich. Vui long chon khoang thoi gian khac.";
                        return RedirectToAction("Index");
                    }

                    if ((current.StartDate - previous.EndDate).TotalHours < 1)
                    {
                        TempData["Error"] = $"Xe '{carName}' cac don phai cach nhau it nhat 1 tieng.";
                        return RedirectToAction("Index");
                    }
                }
            }
            // ===== 2. Kiem tra availability/hold bang ReservationService =====
            var overlapErrors = new List<string>();

            foreach (var item in items)
            {
                var availability = await _reservationService.CheckCarAvailabilityAsync(item.CarID, item.StartDate, item.EndDate);
                if (!availability.IsAvailable)
                {
                    overlapErrors.Add($"'{item.Car?.CarName}': {availability.Reason ?? "Không còn khả dụng."}");
                }
            }

            if (overlapErrors.Any())
            {
                TempData["Error"] = string.Join(" ", overlapErrors);
                return RedirectToAction("Index");
            }

            // ===== 3. Build server quotes, then hold every car atomically =====
            var addons = await _context.AddOns.ToListAsync();
            var quotes = new List<CheckoutQuoteViewModel>();
            var itemDetails = new List<object>();

            foreach (var item in items)
            {
                var selectedAddons = item.AddOnIDs
                    .Select(addonId => addons.FirstOrDefault(a => a.AddOnID == addonId))
                    .Where(a => a != null)
                    .Select(a => $"{a!.Name}|{a.PricePerDay}")
                    .ToList();

                var quote = await _quoteService.BuildQuoteAsync(new BookingQuoteRequest
                {
                    UserID = userId,
                    CarID = item.CarID,
                    StartDate = item.StartDate,
                    EndDate = item.EndDate,
                    InsuranceTier = "Basic",
                    SelectedAddons = selectedAddons,
                    ContractTypeID = item.ContractTypeID
                });
                quotes.Add(quote);

                itemDetails.Add(new
                {
                    carId = item.CarID,
                    carName = quote.CarName,
                    carBrand = quote.CarBrand,
                    licensePlate = quote.LicensePlate,
                    locationId = quote.PickupLocationID,
                    startDate = quote.StartDate,
                    endDate = quote.EndDate,
                    hours = quote.TotalHours,
                    days = quote.RentalDays,
                    withDriver = false,
                    driverId = (int?)null,
                    contractTypeId = item.ContractTypeID,
                    basePrice = quote.BaseRentalAmount,
                    addonTotal = quote.AddOnFee,
                    addonDetails = quote.AddOns,
                    totalPrice = quote.TotalPrice,
                    defaultDeposit = quote.CollateralAmount
                });
            }

            decimal baseTotal = quotes.Sum(q => q.TotalBeforeDiscount);
            decimal discountAmount = quotes.Sum(q => q.DiscountAmount);
            decimal totalAmount = quotes.Sum(q => q.TotalPrice);
            decimal holdingFee = quotes.Sum(q => q.HoldingFee);
            decimal totalDeposit = quotes.Sum(q => q.CollateralAmount);
            decimal memDiscount = user?.MembershipTier?.DiscountPercent ?? 0;

            // ===== 4. Luu transaction va giu xe, nhung chua xoa cart cho den khi PayOS webhook thanh cong =====
            BookingTransaction? transaction = null;
            BookingHoldsResult? holdResult = null;

            try
            {
                transaction = new BookingTransaction
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
                        quotes,
                        holdingFee,
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

                var holdRequests = quotes.Select(q => new BookingHoldRequest(
                    userId,
                    q.CarID,
                    q.StartDate,
                    q.EndDate,
                    q)).ToList();
                holdResult = await _reservationService.CreateHoldsAsync(holdRequests, transaction.TransactionID);
                if (!holdResult.IsSuccess)
                {
                    transaction.Status = BookingStatusConstants.Cancelled;
                    await _context.SaveChangesAsync();
                    TempData["Error"] = holdResult.ErrorMessage ?? "Không thể giữ tất cả xe trong giỏ hàng.";
                    return RedirectToAction("Index");
                }

                var domain = PaymentUrlResolver.ResolvePublicBaseUrl(HttpContext);
                var payOSResult = await _payOSPaymentService.CreateCartHoldPaymentAsync(transaction, holdingFee, domain);
                if (!payOSResult.IsSuccess || string.IsNullOrWhiteSpace(payOSResult.CheckoutUrl))
                {
                    foreach (var hold in holdResult.Holds)
                        await _reservationService.ReleaseHoldAsync(hold.BookingHoldID, "PayOS cart payment create failed.");

                    transaction.Status = PaymentStatusConstants.Failed;
                    await _context.SaveChangesAsync();
                    TempData["Error"] = payOSResult.ErrorMessage ?? "Không thể tạo thanh toán PayOS cho giỏ hàng.";
                    return RedirectToAction("Index");
                }

                TempData["Success"] = $"Đơn hàng #{transaction.TransactionID} đã được giữ. Vui lòng thanh toán {holdingFee:N0}đ để xác nhận.";
                return Redirect(payOSResult.CheckoutUrl);
            }
            catch
            {
                if (holdResult?.Holds != null)
                {
                    foreach (var hold in holdResult.Holds)
                        await _reservationService.ReleaseHoldAsync(hold.BookingHoldID, "Exception while creating cart payment.");
                }

                if (transaction != null)
                {
                    transaction.Status = PaymentStatusConstants.Failed;
                    await _context.SaveChangesAsync();
                }

                TempData["Error"] = "Đã xảy ra lỗi khi tạo thanh toán giữ chỗ giỏ hàng. Hệ thống đã giải phóng giữ chỗ, vui lòng thử lại.";
                return RedirectToAction("Index");
            }
        }

        // =========================================
        // Helper: Tinh gia cart item phia server
        // =========================================
        private static decimal CalculateCartItemPrice(Car? car, DateTime startDate, DateTime endDate, bool withDriver)
        {
            if (car == null) return 0;

            var hours = (int)Math.Ceiling((endDate - startDate).TotalHours);
            var days = Math.Max(1, (int)Math.Ceiling(hours / 24.0));

            decimal pricePerDay = car.PricePerDay;
            decimal pricePerHour = car.PricePerHour > 0 ? car.PricePerHour : 50000m;
            decimal p4 = pricePerDay * 0.4m;
            decimal p8 = pricePerDay * 0.7m;
            decimal p12 = pricePerDay * 0.85m;
            decimal p24 = pricePerDay;
            decimal basePrice = 0;

            if (hours <= 0) return 0;
            if (hours <= 4) basePrice = Math.Min(hours * pricePerHour, p4);
            else if (hours <= 8) basePrice = Math.Min(p4 + (hours - 4) * pricePerHour, p8);
            else if (hours <= 12) basePrice = Math.Min(p8 + (hours - 8) * pricePerHour, p12);
            else if (hours <= 24) basePrice = Math.Min(p12 + (hours - 12) * pricePerHour, p24);
            else
            {
                int fullDays = hours / 24;
                int rem = hours % 24;
                decimal remP = rem <= 4 ? Math.Min(rem * pricePerHour, p4)
                    : rem <= 8 ? Math.Min(p4 + (rem - 4) * pricePerHour, p8)
                    : rem <= 12 ? Math.Min(p8 + (rem - 8) * pricePerHour, p12)
                    : Math.Min(p12 + (rem - 12) * pricePerHour, p24);
                basePrice = (fullDays * p24) + remP;
            }

            return Math.Round(basePrice);
        }

        private int GetUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdString, out int userId);
            return userId;
        }
    }
}
