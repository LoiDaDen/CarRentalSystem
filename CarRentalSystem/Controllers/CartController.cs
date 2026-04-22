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
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemID == cartItemId && ci.Cart.UserID == userId);

            if (item != null)
            {
                var package = await _context.ContractTypes.FindAsync(packageId);
                if (package != null)
                {
                    item.ContractTypeID = packageId;
                    _context.CartItems.Update(item);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, packageName = package.Name, discount = package.DiscountPercent });
                }
            }
            return Json(new { success = false });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ToggleAddon(int cartItemId, int addonId, bool selected)
        {
            var userId = GetUserId();
            var item = await _context.CartItems
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
            _context.CartItems.Update(item);
            await _context.SaveChangesAsync();
            
            return Json(new { success = true });
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

            // 1. Transaction
            var user = await _context.Users.Include(u => u.MembershipTier).FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null) return RedirectToAction("Login", "Auth");

            decimal baseTotalAmount = cart.CartItems.Sum(ci => ci.EstimatedPrice);
            decimal memDiscountPct = user?.MembershipTier?.DiscountPercent ?? 0;
            decimal totalDiscountAmount = baseTotalAmount * (memDiscountPct / 100m);
            decimal totalAmount = baseTotalAmount - totalDiscountAmount;

            var transaction = new BookingTransaction
            {
                UserID = userId,
                TotalAmount = totalAmount,
                Status = BookingStatusConstants.Draft,
                CreatedAt = DateTime.UtcNow
            };
            _context.BookingTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            var pendingStatus = await _context.BookingStatuses.FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Pending) ??
                              new BookingStatus { Name = BookingStatusConstants.Pending };
            if (pendingStatus.BookingStatusID == 0)
            {
                _context.BookingStatuses.Add(pendingStatus);
                await _context.SaveChangesAsync();
            }

            // 2. Bookings
            var skippedCars = new List<string>();
            foreach (var item in cart.CartItems)
            {
                // 🔒 Overlap check cho từng xe trong giỏ hàng
                bool isOverlapped = await _context.Bookings
                    .Include(b => b.BookingStatus)
                    .AnyAsync(b => b.CarID == item.CarID &&
                        b.BookingStatus != null &&
                        b.BookingStatus.Name != "Cancelled" && b.BookingStatus.Name != "Completed" &&
                        ((item.StartDate >= b.StartDate && item.StartDate < b.EndDate) ||
                         (item.EndDate > b.StartDate && item.EndDate <= b.EndDate) ||
                         (item.StartDate <= b.StartDate && item.EndDate >= b.EndDate)));

                if (isOverlapped)
                {
                    skippedCars.Add(item.Car?.CarName ?? $"Xe #{item.CarID}");
                    continue; // Skip xe bị trùng, không tạo booking
                }

                decimal itemDiscount = item.EstimatedPrice * (memDiscountPct / 100m);
                var booking = new Booking
                {
                    UserID = userId,
                    CarID = item.CarID,
                    StartDate = item.StartDate,
                    EndDate = item.EndDate,
                    TotalPrice = item.EstimatedPrice - itemDiscount,
                    DiscountAmount = itemDiscount,
                    TotalHours = (int)Math.Ceiling((item.EndDate - item.StartDate).TotalHours),
                    BookingStatusID = pendingStatus.BookingStatusID,
                    DepositAmount = item.Car?.DefaultDeposit ?? 0,
                    DepositStatus = DepositStatusConstants.Holding,
                    TransactionID = transaction.TransactionID,
                    PickupLocationID = item.Car?.LocationID ?? 1,
                    ReturnLocationID = item.Car?.LocationID ?? 1
                };
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();
                
                // Thêm BookingAddOns nếu có
                if (!string.IsNullOrEmpty(item.SelectedAddOns))
                {
                    var addonIds = item.AddOnIDs;
                    foreach (var addonId in addonIds)
                    {
                        var addon = await _context.AddOns.FindAsync(addonId);
                        if (addon != null)
                        {
                            _context.BookingAddOns.Add(new BookingAddOn
                            {
                                BookingID = booking.BookingID,
                                AddOnID = addonId,
                                Quantity = 1,
                                TotalPrice = addon.PricePerDay
                            });
                        }
                    }
                    await _context.SaveChangesAsync();
                }
            }
            await _context.SaveChangesAsync();

            // 3. Master Digital Contract
            const int MaxFieldLength = 1000;
            var carPlates = string.Join(", ", cart.CartItems.Select(ci => ci.Car?.LicensePlate ?? "Chưa rõ"));
            if (carPlates.Length > MaxFieldLength) carPlates = carPlates[..MaxFieldLength] + "...";
            var carModels = string.Join(" | ", cart.CartItems.Select(ci => $"{ci.Car?.CarBrand?.Name} {ci.Car?.CarName}"));
            if (carModels.Length > MaxFieldLength) carModels = carModels[..MaxFieldLength] + "...";
            var minStartDate = cart.CartItems.Min(ci => ci.StartDate);
            var maxEndDate = cart.CartItems.Max(ci => ci.EndDate);

            string termsCarsStr = string.Join("\n", cart.CartItems.Select((ci, index) => 
                $"- Xe {index+1}: {ci.Car?.CarBrand?.Name} {ci.Car?.CarName}, BKS: {ci.Car?.LicensePlate}, Giá thuê: {ci.EstimatedPrice.ToString("N0")} VNĐ"));

            var newContract = new DigitalContract
            {
                TransactionID = transaction.TransactionID,
                ContractNo = $"HD-Cart-{transaction.TransactionID}-{DateTime.UtcNow:MMddyy}",
                CustomerName = user?.Name ?? "",
                CustomerIdentityCard = _piiEncryption.DecryptIfNotEmpty(user?.CitizenId) ?? "Chưa cập nhật",
                CustomerPhone = user?.Phone ?? "",
                CustomerAddress = user?.Address ?? "Chưa cập nhật",
                CarLicensePlate = carPlates,
                CarModel = carModels,
                RentalStartDate = minStartDate,
                RentalEndDate = maxEndDate,
                RentalPricePerDay = 0,
                TotalContractValue = totalAmount,
                DepositAmount = cart.CartItems.Sum(ci => ci.Car?.DefaultDeposit) ?? 0,
                TermsAndConditions = $@"Sau khi bàn bạc, thỏa thuận, hai bên thống nhất ký kết Hợp đồng thuê XE tự lái với các điều khoản như sau:

ĐIỀU 1: NỘI DUNG HỢP ĐỒNG
- Bên A đồng ý cho bên B thuê nhiều xe ô tô tự lái trong thời gian cho thuê.
- Danh sách xe:
{termsCarsStr}

ĐIỀU 2: GIÁ TRỊ HỢP ĐỒNG, PHƯƠNG THỨC THANH TOÁN
- Tổng tiền thuê xe nền tảng: {baseTotalAmount.ToString("N0")} VNĐ.
{(memDiscountPct > 0 ? $"- Ưu đãi hạng thẻ thành viên {user?.MembershipTier?.Name} (Giảm {memDiscountPct}%): -{totalDiscountAmount.ToString("N0")} VNĐ.\n" : "")}- Tổng cộng tiền thuê phải thanh toán: {totalAmount.ToString("N0")} VNĐ.
- Tiền cọc tổng (Giữ chỗ): {(cart.CartItems.Sum(ci => ci.Car?.DefaultDeposit) ?? 0).ToString("N0")} VNĐ.
- Bên B thanh toán đầy đủ phí thuê và tiền cọc cho Bên A ngay khi hợp đồng có hiệu lực.

ĐIỀU 3: TRÁCH NHIỆM CHUNG (Áp dụng cho tất cả các xe)
- Không sử dụng xe thuê vào mục đích vi phạm pháp luật.
- Chịu toàn bộ chi phí nhiên liệu, cầu đường, đỗ xe trong thời gian thuê từng xe.
- Trả xe đúng thời gian và địa điểm theo như thỏa thuận gốc.

ĐIỀU 4: HIỆU LỰC HỢP ĐỒNG
- Hợp đồng có giá trị từ {minStartDate:HH:mm dd/MM/yyyy} đến hết ngày {maxEndDate:HH:mm dd/MM/yyyy}.

Hợp đồng điện tử này có giá trị pháp lý tương đương bản giấy.",
                IsSignedByCustomer = false,
                IsSignedByOwner = false
            };

            _context.DigitalContracts.Add(newContract);
            
            // 4. Clear cart
            _context.CartItems.RemoveRange(cart.CartItems);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã khởi tạo đơn hàng từ giỏ thành công! Vui lòng thanh toán tiền cọc.";
            if (skippedCars.Any())
            {
                TempData["Warning"] = $"Lưu ý: {skippedCars.Count} xe đã bị bỏ qua do trùng lịch: {string.Join(", ", skippedCars)}";
            }
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
