using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class BookingController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly ICarService _carService;
        private readonly IAuthService _authService;
        private readonly IReservationService _reservationService;
        private readonly IBookingQuoteService _quoteService;
        private readonly IPayOSPaymentService _payOSPaymentService;
        private readonly IDeliveryScopeService _deliveryScopeService;
        private readonly Data.ApplicationDbContext _context;

        public BookingController(
            IBookingService bookingService,
            ICarService carService,
            IAuthService authService,
            IReservationService reservationService,
            IBookingQuoteService quoteService,
            IPayOSPaymentService payOSPaymentService,
            IDeliveryScopeService deliveryScopeService,
            Data.ApplicationDbContext context)
        {
            _bookingService = bookingService;
            _carService = carService;
            _authService = authService;
            _reservationService = reservationService;
            _quoteService = quoteService;
            _payOSPaymentService = payOSPaymentService;
            _deliveryScopeService = deliveryScopeService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Checkout(int CarID, DateTime StartDate, DateTime EndDate, string PickupLocation, string pickupLocationType = "customer", [FromQuery] List<string>? SelectedAddons = null)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId))
            {
                var user = await _context.Users.Include(u => u.MembershipTier).FirstOrDefaultAsync(u => u.UserID == userId);
                if (user != null)
                {
                    ViewBag.UserPhone = user.Phone;
                    if (user.IsBlacklisted)
                    {
                        TempData["BookingError"] = "Tài khoản của bạn đã bị khóa: " + user.BlacklistReason;
                        return RedirectToAction("Details", "Car", new { id = CarID });
                    }
                    if (!user.IsVerified)
                    {
                        TempData["BookingError"] = "Vui lòng Xác thực danh tính (eKYC) bằng cách cập nhật CCCD trước khi đặt xe.";
                        TempData["ShowEkycModal"] = "true";
                        TempData["EkycReturnUrl"] = $"{Request.Path}{Request.QueryString}";
                        return RedirectToAction("Details", "Car", new { id = CarID });
                    }
                }
            }

            var car = await _carService.GetCarByIdAsync(CarID);
            if (car == null) return NotFound();

            if (pickupLocationType == "owner")
            {
                var carLat = car.Latitude ?? car.Location?.Latitude ?? 10.7769;
                var carLng = car.Longitude ?? car.Location?.Longitude ?? 106.7009;
                var scope = _deliveryScopeService.CheckScope(car.CarAddress ?? car.Location?.Name, carLat, carLng, PickupLocation);
                if (!scope.IsWithinScope)
                {
                    TempData["BookingError"] = scope.MessageVi;
                    return RedirectToAction("Details", "Car", new { id = CarID });
                }
            }

            // Phase 3: Maintenance blocking
            if (car.NextInspectionDate.HasValue && car.NextInspectionDate.Value < DateTime.UtcNow)
            {
                TempData["BookingError"] = "Xe này đang tạm ngưng cho thuê do quá hạn Đăng Kiểm. Vui lòng chọn xe khác.";
                return RedirectToAction("Details", "Car", new { id = CarID });
            }

            if (EndDate <= StartDate)
            {
                TempData["BookingError"] = "Ngày kết thúc phải sau ngày bắt đầu.";
                return RedirectToAction("Details", "Car", new { id = CarID });
            }

            // 🔒 DB-level overlap check (không load toàn bộ bookings vào RAM)
            var availability = await _reservationService.CheckCarAvailabilityAsync(CarID, StartDate, EndDate);
            bool isOverlapped = !availability.IsAvailable;
            /*
            bool legacyIsOverlapped = await _context.Bookings
                .Include(b => b.BookingStatus)
                .AnyAsync(b => b.CarID == CarID &&
                    b.BookingStatus != null &&
                    b.BookingStatus.Name != "Cancelled" && b.BookingStatus.Name != "Completed" &&
                    ((StartDate >= b.StartDate && StartDate < b.EndDate) ||
                     (EndDate > b.StartDate && EndDate <= b.EndDate) ||
                     (StartDate <= b.StartDate && EndDate >= b.EndDate)));
            */

            if (isOverlapped)
            {
                TempData["BookingError"] = "Rất tiếc, xe này đã được đặt trong khoảng thời gian bạn chọn. Vui lòng chọn ngày khác.";
                return RedirectToAction("Details", "Car", new { id = CarID });
            }

            var diffTime = EndDate - StartDate;
            int totalHours = (int)Math.Ceiling(diffTime.TotalHours);
            if (totalHours <= 0) totalHours = 1;

            Func<int, decimal> calcPrice = (int hrs) =>
            {
                decimal p4 = car.PricePerDay * 0.4m > 0 ? car.PricePerDay * 0.4m : 4 * car.PricePerHour;
                decimal p8 = car.PricePerDay * 0.7m > 0 ? car.PricePerDay * 0.7m : 8 * car.PricePerHour;
                decimal p12 = car.PricePerDay * 0.85m > 0 ? car.PricePerDay * 0.85m : 12 * car.PricePerHour;
                decimal p24 = car.PricePerDay > 0 ? car.PricePerDay : 24 * car.PricePerHour;

                if (hrs <= 0) return 0;
                if (hrs <= 4) return Math.Min(hrs * car.PricePerHour, p4);
                if (hrs <= 8) return Math.Min(p4 + (hrs - 4) * car.PricePerHour, p8);
                if (hrs <= 12) return Math.Min(p8 + (hrs - 8) * car.PricePerHour, p12);
                if (hrs <= 24) return Math.Min(p12 + (hrs - 12) * car.PricePerHour, p24);
                
                int fullDays = hrs / 24;
                int rem = hrs % 24;
                decimal remP = 0;
                if (rem > 0)
                {
                    if (rem <= 4) remP = Math.Min(rem * car.PricePerHour, p4);
                    else if (rem <= 8) remP = Math.Min(p4 + (rem - 4) * car.PricePerHour, p8);
                    else if (rem <= 12) remP = Math.Min(p8 + (rem - 8) * car.PricePerHour, p12);
                    else remP = Math.Min(p12 + (rem - 12) * car.PricePerHour, p24);
                }
                return (fullDays * p24) + remP;
            };

            decimal totalPrice = calcPrice(totalHours);
            
            int days = totalHours / 24;
            int remHours = totalHours % 24;

            var booking = new Booking 
            { 
                CarID = CarID, 
                Car = car,
                StartDate = StartDate,
                EndDate = EndDate,
                TotalPrice = totalPrice,
                TotalHours = totalHours,
                DepositAmount = car.DefaultDeposit,
                DepositStatus = "Holding"
            };
            
            ViewBag.PickupLocation = PickupLocation;
            ViewBag.PickupLocationType = pickupLocationType;
            ViewBag.DiffDays = days > 0 ? days : 1;
            ViewBag.DiffHours = remHours;
            
            // Process incoming addons
            var addonList = new List<KeyValuePair<string, decimal>>();
            decimal addonTotal = 0;
            if (SelectedAddons != null && SelectedAddons.Any())
            {
                foreach (var addon in SelectedAddons)
                {
                    var parts = addon.Split('|');
                    if (parts.Length == 2 && decimal.TryParse(parts[1], out decimal price))
                    {
                        addonTotal += price;
                        addonList.Add(new KeyValuePair<string, decimal>(parts[0], price));
                    }
                }
            }
            booking.AddOnFee = addonTotal;
            ViewBag.SelectedAddons = addonList;
            
            // Pass membership tier for JS calculation
            if (int.TryParse(userIdString, out int uId))
            {
                var usr = await _context.Users.Include(u => u.MembershipTier).FirstOrDefaultAsync(u => u.UserID == uId);
                ViewBag.MembershipTier = usr?.MembershipTier;
            }

            if (int.TryParse(userIdString, out int quoteUserId))
            {
                var quote = await _quoteService.BuildQuoteAsync(new BookingQuoteRequest
                {
                    UserID = quoteUserId,
                    CarID = CarID,
                    StartDate = StartDate,
                    EndDate = EndDate,
                    InsuranceTier = "Basic",
                    DeliveryFee = 0m,
                    SelectedAddons = SelectedAddons
                });

                booking.TotalPrice = quote.TotalPrice;
                booking.TotalHours = quote.TotalHours;
                booking.DepositAmount = quote.CollateralAmount;
                booking.ReservationFee = quote.HoldingFee;
                booking.DiscountAmount = quote.DiscountAmount;
                booking.AddOnFee = quote.AddOnFee;
                ViewBag.Quote = quote;
            }

            // Generate a temporary booking code for display
            ViewBag.TempBookingCode = DateTime.UtcNow.Ticks.ToString();

            // Fetch available drivers for selection (filtered by car's location)
            var driversQuery = _context.Drivers
                .Where(d => d.IsApproved && d.IsAvailable);

            if (car != null && car.LocationID > 0)
            {
                driversQuery = driversQuery.Where(d => d.LocationID == car.LocationID);
            }
            else
            {
                driversQuery = driversQuery.Where(d => false); // No drivers if car has no location
            }

            ViewBag.Drivers = await driversQuery.ToListAsync();
            ViewBag.CarLocationID = car?.LocationID ?? 0;
            ViewBag.CarLocationName = car?.Location?.Name ?? "";

            return View(booking);
        }

        [HttpGet]
        public async Task<IActionResult> Deposit(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var bookings = await _bookingService.GetUserBookingsAsync(userId);
            var booking = bookings.FirstOrDefault(b => b.BookingID == id);
            
            if (booking == null) return NotFound();
            
            return View(booking);
        }

        [HttpGet]
        public async Task<IActionResult> DepositTransaction(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var transaction = await _context.BookingTransactions
                .FirstOrDefaultAsync(t => t.TransactionID == id && t.UserID == userId);

            if (transaction == null) return NotFound();

            // Neu chua co bookings (dang o trang thai Unpaid), lay tu snapshot
            if (!transaction.Bookings.Any() && !string.IsNullOrEmpty(transaction.PendingCartSnapshot))
            {
                try
                {
                    var snapshot = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(transaction.PendingCartSnapshot);
                    ViewBag.Snapshot = snapshot;
                }
                catch { }
            }

            return View(transaction);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(Booking booking, int? SelectedDriverID, decimal DriverFee, List<string>? SelectedAddons)
        {
            // Validate input trước khi xử lý
            if (booking.EndDate <= booking.StartDate)
            {
                TempData["BookingError"] = "Ngày kết thúc phải sau ngày bắt đầu.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            if (booking.StartDate.Date < DateTime.UtcNow.Date)
            {
                TempData["BookingError"] = "Không thể đặt xe cho ngày trong quá khứ.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId))
            {
                var user = await _authService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    if (user.IsBlacklisted || !user.IsVerified)
                    {
                        TempData["BookingError"] = "Lỗi xác thực. Vui lòng kiểm tra lại trạng thái tài khoản.";
                        return RedirectToAction("Details", "Car", new { id = booking.CarID });
                    }
                    booking.UserID = userId;
                }
            }

            var car = await _context.Cars.Include(c => c.Location).FirstOrDefaultAsync(c => c.CarID == booking.CarID);
            if (car == null) return NotFound();

            if (booking.DeliveryFee > 0 && string.IsNullOrWhiteSpace(booking.DeliveryAddress))
            {
                TempData["BookingError"] = "Vui lòng nhập địa chỉ giao xe khi chọn dịch vụ giao tận nơi.";
                return RedirectToAction("Checkout", new { CarID = booking.CarID, StartDate = booking.StartDate.ToString("yyyy-MM-ddTHH:mm"), EndDate = booking.EndDate.ToString("yyyy-MM-ddTHH:mm"), PickupLocation = car.Location?.Name, pickupLocationType = "owner" });
            }

            if (booking.DeliveryFee > 0)
            {
                var carLat = car.Latitude ?? car.Location?.Latitude ?? 10.7769;
                var carLng = car.Longitude ?? car.Location?.Longitude ?? 106.7009;
                var scope = _deliveryScopeService.CheckScope(car.CarAddress ?? car.Location?.Name, carLat, carLng, booking.DeliveryAddress);
                if (!scope.IsWithinScope)
                {
                    TempData["BookingError"] = scope.MessageVi;
                    return RedirectToAction("Checkout", new { CarID = booking.CarID, StartDate = booking.StartDate.ToString("yyyy-MM-ddTHH:mm"), EndDate = booking.EndDate.ToString("yyyy-MM-ddTHH:mm"), PickupLocation = car.Location?.Name, pickupLocationType = "owner" });
                }
            }

            try
            {
                var quote = await _quoteService.BuildQuoteAsync(new BookingQuoteRequest
                {
                    UserID = booking.UserID,
                    CarID = booking.CarID,
                    StartDate = booking.StartDate,
                    EndDate = booking.EndDate,
                    InsuranceTier = booking.InsuranceTier,
                    DeliveryFee = booking.DeliveryFee,
                    DeliveryAddress = booking.DeliveryAddress,
                    SelectedAddons = SelectedAddons,
                    SelectedDriverID = SelectedDriverID,
                    PromotionID = booking.PromotionID
                });

                var availability = await _reservationService.CheckCarAvailabilityAsync(booking.CarID, booking.StartDate, booking.EndDate);
                if (!availability.IsAvailable)
                {
                    TempData["BookingError"] = availability.Reason ?? "Xe nay da duoc giu hoac dat trong thoi gian ban chon.";
                    return RedirectToAction("Details", "Car", new { id = booking.CarID });
                }

                var holdResult = await _reservationService.CreateHoldAsync(booking.UserID, booking.CarID, booking.StartDate, booking.EndDate, quote);
                if (!holdResult.IsSuccess || holdResult.Hold == null)
                {
                    TempData["BookingError"] = holdResult.ErrorMessage ?? "Khong the giu xe. Vui long thu lai.";
                    return RedirectToAction("Details", "Car", new { id = booking.CarID });
                }

                var domain = $"{Request.Scheme}://{Request.Host}";
                var payOSResult = await _payOSPaymentService.CreateHoldPaymentAsync(holdResult.Hold, quote, domain);
                if (!payOSResult.IsSuccess || payOSResult.Transaction == null)
                {
                    await _reservationService.ReleaseHoldAsync(holdResult.Hold.BookingHoldID, "PayOS create payment failed.");
                    TempData["BookingError"] = payOSResult.ErrorMessage ?? "Khong the tao thanh toan PayOS.";
                    return RedirectToAction("Details", "Car", new { id = booking.CarID });
                }

                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "Tao QR PayOS",
                    EntityType = "Thanh toan",
                    EntityId = holdResult.Hold.BookingHoldID,
                    UserId = booking.UserID,
                    UserName = User.Identity?.Name,
                    Description = $"Khach tao phieu giu cho #{holdResult.Hold.BookingHoldID}, so tien PayOS {quote.HoldingFee:N0} VND. Chua tao don thue.",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString(),
                    RequestUrl = "POST /Booking/Book",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return RedirectToAction("Hold", "Payment", new { orderCode = payOSResult.Transaction.OrderCode });
            }
            catch (Exception ex)
            {
                TempData["BookingError"] = "Da xay ra loi khi tao thanh toan giu cho: " + ex.Message;
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }
        }

        /*
            // ═══════════════════════════════════════════════════
            // 🔒 DB TRANSACTION — Atomic booking creation
            // Chống race condition (double booking) + data consistency
            // ═══════════════════════════════════════════════════
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                // 🔒 OVERLAP CHECK INSIDE TRANSACTION (DB-level lock)
                bool isOverlapped = await _context.Bookings
                    .Include(b => b.BookingStatus)
                    .AnyAsync(b => b.CarID == booking.CarID &&
                        b.BookingStatus != null &&
                        b.BookingStatus.Name != "Cancelled" && b.BookingStatus.Name != "Completed" &&
                        ((booking.StartDate >= b.StartDate && booking.StartDate < b.EndDate) ||
                         (booking.EndDate > b.StartDate && booking.EndDate <= b.EndDate) ||
                         (booking.StartDate <= b.StartDate && booking.EndDate >= b.EndDate)));

                if (isOverlapped)
                {
                    await transaction.RollbackAsync();
                    TempData["BookingError"] = "Rất tiếc, xe này đã được đặt trong khoảng thời gian bạn chọn. Vui lòng chọn xe khác hoặc đổi ngày.";
                    return RedirectToAction("Details", "Car", new { id = booking.CarID });
                }

                var diffTime = booking.EndDate - booking.StartDate;
                int totalHours = (int)Math.Ceiling(diffTime.TotalHours);
                if (totalHours <= 0) totalHours = 1;

                Func<int, decimal> calcPrice = (int hrs) =>
                {
                    decimal p4 = car.PricePerDay * 0.4m > 0 ? car.PricePerDay * 0.4m : 4 * car.PricePerHour;
                    decimal p8 = car.PricePerDay * 0.7m > 0 ? car.PricePerDay * 0.7m : 8 * car.PricePerHour;
                    decimal p12 = car.PricePerDay * 0.85m > 0 ? car.PricePerDay * 0.85m : 12 * car.PricePerHour;
                    decimal p24 = car.PricePerDay > 0 ? car.PricePerDay : 24 * car.PricePerHour;

                    if (hrs <= 0) return 0;
                    if (hrs <= 4) return Math.Min(hrs * car.PricePerHour, p4);
                    if (hrs <= 8) return Math.Min(p4 + (hrs - 4) * car.PricePerHour, p8);
                    if (hrs <= 12) return Math.Min(p8 + (hrs - 8) * car.PricePerHour, p12);
                    if (hrs <= 24) return Math.Min(p12 + (hrs - 12) * car.PricePerHour, p24);
                    
                    int fullDays = hrs / 24;
                    int rem = hrs % 24;
                    decimal remP = 0;
                    if (rem > 0)
                    {
                        if (rem <= 4) remP = Math.Min(rem * car.PricePerHour, p4);
                        else if (rem <= 8) remP = Math.Min(p4 + (rem - 4) * car.PricePerHour, p8);
                        else if (rem <= 12) remP = Math.Min(p8 + (rem - 8) * car.PricePerHour, p12);
                        else remP = Math.Min(p12 + (rem - 12) * car.PricePerHour, p24);
                    }
                    return (fullDays * p24) + remP;
                };

                decimal basePrice = calcPrice(totalHours);
                
                int days = totalHours / 24;
                
                decimal dailyInsurance = 0;
                switch (booking.InsuranceTier)
                {
                    case "Premium": dailyInsurance = 150000; break;
                    case "Full Cover": dailyInsurance = 300000; break;
                    default: dailyInsurance = 0; break;
                }
                int insuranceDays = days > 0 ? days : 1;
                
                // Phụ phí VAT & Service (Cố định 150k cho demo)
                decimal serviceFee = 150000;
                
                booking.TotalPrice = basePrice + (insuranceDays * dailyInsurance) + serviceFee + booking.DeliveryFee + booking.AddOnFee + DriverFee;
                
                var mUser = await _context.Users.Include(u => u.MembershipTier).FirstOrDefaultAsync(u => u.UserID == booking.UserID);
                if (mUser?.MembershipTier != null && mUser.MembershipTier.DiscountPercent > 0)
                {
                    decimal memDiscount = booking.TotalPrice * (mUser.MembershipTier.DiscountPercent / 100m);
                    booking.DiscountAmount += memDiscount;
                    booking.TotalPrice -= memDiscount;
                }

                // Voucher / Promotion logic
                if (booking.PromotionID.HasValue)
                {
                    var promo = await _context.Promotions.FindAsync(booking.PromotionID.Value);
                    if (promo != null && promo.IsActive && promo.ValidFrom <= DateTime.UtcNow && promo.ValidUntil >= DateTime.UtcNow && promo.UsedCount < promo.UsageLimit)
                    {
                        decimal discount = booking.TotalPrice * promo.DiscountPercent / 100m;
                        if (promo.MaxDiscountAmount.HasValue && discount > promo.MaxDiscountAmount.Value)
                            discount = promo.MaxDiscountAmount.Value;
                        
                        booking.DiscountAmount += discount;
                        booking.TotalPrice -= discount;
                        
                        // Sử dụng atomic update để tránh race condition khi nhiều user cùng dùng promo
                        var promoToUpdate = await _context.Promotions
                            .FirstOrDefaultAsync(p => p.PromotionID == promo.PromotionID && p.UsedCount < p.UsageLimit);
                        if (promoToUpdate != null)
                        {
                            promoToUpdate.UsedCount += 1;
                            _context.Promotions.Update(promoToUpdate);
                        }
                    }
                }

                if (booking.TotalPrice < 0) booking.TotalPrice = 0;
                
                booking.TotalHours = (int)Math.Ceiling(diffTime.TotalHours);
                if (booking.TotalHours == 0) booking.TotalHours = 1;

                booking.DepositAmount = car.DefaultDeposit;
                booking.DepositStatus = DepositStatusConstants.Unpaid;
                
                booking.PickupLocationID = car.LocationID;
                booking.ReturnLocationID = car.LocationID;
                
                await _bookingService.CreateBookingAsync(booking);

                if (SelectedDriverID.HasValue)
                {
                    var driverBooking = new DriverBooking
                    {
                        BookingID = booking.BookingID,
                        DriverID = SelectedDriverID.Value,
                        DriverFee = DriverFee,
                        Status = "Pending"
                    };
                    _context.DriverBookings.Add(driverBooking);
                    await _context.SaveChangesAsync();

                    booking.DriverBookingID = driverBooking.DriverBookingID;
                    _bookingService.UpdateBooking(booking);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToAction("CreatePayment", "Payment", new { bookingId = booking.BookingID });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["BookingError"] = "Đã xảy ra lỗi khi xử lý đặt xe. Vui lòng thử lại.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }
        }
        */

        public async Task<IActionResult> MyBookings()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId))
            {
                var allBookings = await _bookingService.GetUserBookingsAsync(userId);
                // Hide unpaid payment holds; only show real bookings after payment is confirmed.
                var hiddenStatuses = new[] { BookingStatusConstants.Draft, BookingStatusConstants.Unpaid };
                var bookings = allBookings
                    .Where(b => b.BookingStatus == null || !hiddenStatuses.Contains(b.BookingStatus.Name))
                    .ToList();

                // Load HandoverRecords for each booking
                var bookingIds = bookings.Select(b => b.BookingID).ToList();
                var handoverRecords = await _context.HandoverRecords
                    .Where(h => bookingIds.Contains(h.BookingID) && !h.IsDeleted)
                    .ToListAsync();

                ViewBag.HandoverRecords = handoverRecords;

                return View(bookings);
            }
            return Unauthorized();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int id, string? reason)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            // Use atomic update to prevent race conditions
            var booking = await _context.Bookings
                .Include(b => b.Car)
                .Include(b => b.BookingStatus)
                .FirstOrDefaultAsync(b => b.BookingID == id && b.UserID == userId);

            if (booking == null)
            {
                TempData["Error"] = "Không tìm thấy đơn đặt xe.";
                return RedirectToAction(nameof(MyBookings));
            }

            // Only allow cancellation for Pending, Confirmed and Active status
            var cancellableStatuses = new[] { "Pending", "Confirmed", "Active" };
            if (!cancellableStatuses.Contains(booking.BookingStatus?.Name))
            {
                TempData["Error"] = "Không thể hủy đơn ở trạng thái hiện tại.";
                return RedirectToAction(nameof(MyBookings));
            }

            // Get Available status ID
            var availableStatus = await _context.CarStatuses.FirstOrDefaultAsync(s => s.Name == "Available");
            if (availableStatus == null)
            {
                TempData["Error"] = "Lỗi hệ thống. Vui lòng thử lại.";
                return RedirectToAction(nameof(MyBookings));
            }

            // Atomic update based on current status
            if (booking.BookingStatus?.Name == "Active")
            {
                // Update booking status to Cancelled and car status atomically
                    booking.DepositStatus = "PendingRefund";
                    booking.ActualReturnDate = DateTime.UtcNow;
                    _context.Bookings.Update(booking);

                // Reset car status to Available
                if (booking.Car != null)
                {
                    booking.Car.CarStatusID = availableStatus.CarStatusID;
                    _context.Cars.Update(booking.Car);
                }

                // Update booking status
                await _bookingService.UpdateBookingStatusAsync(id, "Cancelled");

                TempData["Success"] = "Chuyến đi đã được hủy. Tiền cọc sẽ được hoàn trả sau 15 ngày.";
                return RedirectToAction(nameof(MyBookings));
            }

            if (booking.BookingStatus?.Name == "Confirmed")
            {
                // Lấy status ID của Cancelled và Refunded trước
                var cancelledStatus = await _context.BookingStatuses.FirstOrDefaultAsync(st => st.Name == "Cancelled");
                
                // Update booking status to Cancelled, cập nhật deposit status và car status atomically
                    booking.BookingStatusID = cancelledStatus.BookingStatusID;
                    booking.DepositStatus = "Refunded";
                    _context.Bookings.Update(booking);

                // Reset car status to Available
                if (booking.Car != null)
                {
                    booking.Car.CarStatusID = availableStatus.CarStatusID;
                    _context.Cars.Update(booking.Car);
                }

                TempData["Success"] = "Chuyến đi đã được hủy. Tiền cọc sẽ được hoàn trả trong 15 ngày.";
                return RedirectToAction(nameof(MyBookings));
            }

            await _bookingService.CancelBookingAsync(id, reason ?? "Khách hàng tự hủy");
            TempData["Success"] = "Đơn đặt xe đã được hủy thành công.";
            return RedirectToAction(nameof(MyBookings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CustomerConfirmPickup(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.DigitalContract)
                .Include(b => b.Car)
                .Include(b => b.RentalContract)
                .FirstOrDefaultAsync(b => b.BookingID == id && b.UserID == userId);

            if (booking == null || booking.BookingStatus?.Name != "Confirmed")
            {
                TempData["Error"] = "Đơn đặt xe không hợp lệ để nhận xe.";
                return RedirectToAction(nameof(MyBookings));
            }

            // 1. Enforce Contract Signing
            if (booking.DigitalContract == null || !booking.DigitalContract.IsSignedByCustomer)
            {
                TempData["Error"] = "Bạn vui lòng xem và ký hợp đồng điện tử trước khi nhận xe.";
                return RedirectToAction(nameof(Contract), new { id = booking.BookingID });
            }

            // 2. BẮT BUỘC: Kiểm tra biên bản nhận xe (CheckIn) đã được tạo chưa
            var checkInRecord = await _context.HandoverRecords
                .Where(h => h.BookingID == id && h.Type == "CheckIn" && !h.IsDeleted)
                .FirstOrDefaultAsync();

            if (checkInRecord == null)
            {
                TempData["Error"] = "Bạn phải tạo biên bản nhận xe trước khi xác nhận nhận xe. Vui lòng chụp ảnh xe và điền thông tin biên bản.";
                return RedirectToAction("Capture", "Handover", new { bookingId = id, type = "CheckIn" });
            }

            // 3. Start Trip
            booking.ActualPickupDate = DateTime.UtcNow;
            
            var activeStatus = await _context.BookingStatuses.FirstOrDefaultAsync(s => s.Name == "Active" || s.Name.Contains("thuê") || s.Name.Contains("đi"));
            if (activeStatus == null)
            {
                activeStatus = new BookingStatus { Name = "Active" };
                _context.BookingStatuses.Add(activeStatus);
                await _context.SaveChangesAsync();
            }
            
            booking.BookingStatusID = activeStatus.BookingStatusID;
            booking.BookingStatus = activeStatus; // Force reference update
            _context.Bookings.Update(booking);
            
            // Mark car as Rented
            if (booking.Car != null)
            {
                var rentedStatus = await _context.CarStatuses.FirstOrDefaultAsync(s => s.Name == "Rented" || s.Name.Contains("đang"));
                if (rentedStatus == null)
                {
                    rentedStatus = new CarStatus { Name = "Rented" };
                    _context.CarStatuses.Add(rentedStatus);
                    await _context.SaveChangesAsync();
                }
                booking.Car.CarStatusID = rentedStatus.CarStatusID;
                _context.Cars.Update(booking.Car);
            }

            // Save status changes
            await _context.SaveChangesAsync();

            // 4. Initialize Rental Contract with current car odometer from CheckIn record
            if (booking.RentalContract == null)
            {
                booking.RentalContract = new RentalContract
                {
                    BookingID = booking.BookingID,
                    BasePrice = booking.TotalPrice,
                    StartKm = checkInRecord.Odo > 0 ? checkInRecord.Odo : (booking.Car?.CurrentOdometer ?? 0),
                    EndKm = checkInRecord.Odo > 0 ? checkInRecord.Odo : (booking.Car?.CurrentOdometer ?? 0)
                };
                _context.RentalContracts.Add(booking.RentalContract);
            }
            else
            {
                booking.RentalContract.StartKm = checkInRecord.Odo > 0 ? checkInRecord.Odo : (booking.Car?.CurrentOdometer ?? 0);
            }

            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Bạn đã nhận xe thành công! Chúc bạn có một chuyến đi an toàn.";
            return RedirectToAction(nameof(MyBookings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CustomerConfirmReturn(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.Car)
                .Include(b => b.RentalContract)
                .FirstOrDefaultAsync(b => b.BookingID == id && b.UserID == userId);

            if (booking == null || booking.BookingStatus?.Name != "Active")
            {
                TempData["Error"] = "Đơn đặt xe không hợp lệ để trả xe.";
                return RedirectToAction(nameof(MyBookings));
            }

            // BẮT BUỘC: Kiểm tra biên bản trả xe (CheckOut) đã được tạo chưa
            var checkOutRecord = await _context.HandoverRecords
                .Where(h => h.BookingID == id && h.Type == "CheckOut" && !h.IsDeleted)
                .OrderByDescending(h => h.RecordDate)
                .FirstOrDefaultAsync();

            if (checkOutRecord == null)
            {
                TempData["Error"] = "Bạn phải tạo biên bản trả xe trước khi xác nhận trả xe. Vui lòng chụp ảnh xe và điền thông tin biên bản.";
                return RedirectToAction("Capture", "Handover", new { bookingId = id, type = "CheckOut" });
            }

            // 1. Record Return Date
            booking.ActualReturnDate = DateTime.UtcNow;
            
            // 2. Update status
            await _bookingService.UpdateBookingStatusAsync(id, "Completed");

            // ═══════════════════════════════════════════════════
            // 💰 TÍNH PHÍ TRỄ HẠN — OverTimeFee * số giờ trễ
            // ═══════════════════════════════════════════════════
            string msg = "Bạn đã trả xe thành công!";
            bool hasViolation = false;

            if (booking.ActualReturnDate > booking.EndDate)
            {
                var lateSpan = booking.ActualReturnDate.Value - booking.EndDate;
                int lateHours = (int)Math.Ceiling(lateSpan.TotalHours);

                // Grace period: miễn phí 30 phút đầu
                if (lateSpan.TotalMinutes > 30 && booking.Car != null)
                {
                    booking.LateFeeAmount = lateHours * booking.Car.OverTimeFee;
                    booking.TotalPrice += booking.LateFeeAmount;
                    msg += $" ⚠️ Trả xe trễ {lateHours} giờ. Phụ phí: {booking.LateFeeAmount:N0}đ ({booking.Car.OverTimeFee:N0}đ/giờ).";
                    hasViolation = true;
                }
                else
                {
                    msg += $" (Trễ {(int)lateSpan.TotalMinutes} phút — trong thời gian miễn phí 30 phút)";
                }
            }

            // ═══════════════════════════════════════════════════
            // 📊 CẬP NHẬT ODOMETER TỪ HANDOVER RECORD
            // ═══════════════════════════════════════════════════
            // Sử dụng Odo từ biên bản CheckOut (đã bắt buộc ở trên)
            if (booking.Car != null)
            {
                booking.Car.CurrentOdometer = checkOutRecord.Odo;
            }

            // 3. Calculate Mileage & Fees in RentalContract
            if (booking.RentalContract != null)
            {
                booking.RentalContract.EndKm = checkOutRecord.Odo;
                
                booking.RentalContract.TotalKm = booking.RentalContract.EndKm - booking.RentalContract.StartKm;
                booking.RentalContract.FinalPrice = booking.TotalPrice;
            }

            // 4. Reset car status → Available
            if (booking.Car != null)
            {
                var availableStatus = await _context.CarStatuses.FirstOrDefaultAsync(s => s.Name == "Available");
                if (availableStatus != null)
                {
                    booking.Car.CarStatusID = availableStatus.CarStatusID;
                }
                _context.Cars.Update(booking.Car);
            }

            // ═══════════════════════════════════════════════════
            // 💰 DEPOSIT REFUND LOGIC — Giữ 15 ngày rồi hoàn trả
            // ═══════════════════════════════════════════════════
            if (!hasViolation && booking.LateFeeAmount == 0)
            {
                // Không vi phạm → lên lịch hoàn cọc sau 15 ngày (chờ kiểm tra phạt nguồi)
                booking.DepositStatus = "PendingRefund";
                msg += " Tiền cọc sẽ được hoàn trả sau 15 ngày kiểm tra phạt nguội.";
            }
            else
            {
                // Có vi phạm → Admin sẽ xử lý cọc thủ công
                booking.DepositStatus = "UnderReview";
                msg += " Tiền cọc đang được xem xét do phát sinh phí.";
            }

            await _context.SaveChangesAsync();

            if (hasViolation)
            {
                TempData["Warning"] = msg;
            }
            else
            {
                TempData["Success"] = msg;
            }

            return RedirectToAction(nameof(MyBookings));
        }

        public async Task<IActionResult> Contract(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.Car)
                .Include(b => b.User)
                .Include(b => b.DigitalContract)
                .Include(b => b.BookingStatus)
                .FirstOrDefaultAsync(b => b.BookingID == id && b.UserID == userId);

            if (booking == null) return NotFound();

            if (booking.DigitalContract == null)
            {
                // Generate a new digital contract based on the booking
                var user = booking.User;
                var car = booking.Car;
                
                var newContract = new DigitalContract
                {
                    BookingID = booking.BookingID,
                    ContractNo = $"HD-{booking.BookingID}-{DateTime.UtcNow:MMddyy}",
                    CustomerName = user?.Name ?? "",
                    CustomerIdentityCard = "Chưa cập nhật",
                    CustomerPhone = user?.Phone ?? "",
                    CustomerAddress = "Chưa cập nhật",
                    CarLicensePlate = car?.LicensePlate ?? "",
                    CarModel = $"{car?.CarBrand?.Name} {car?.CarName}",
                    RentalStartDate = booking.StartDate,
                    RentalEndDate = booking.EndDate,
                    RentalPricePerDay = car?.PricePerDay ?? 0,
                    TotalContractValue = booking.TotalPrice,
                    DepositAmount = booking.DepositAmount,
                    TermsAndConditions = $@"Sau khi bàn bạc, thỏa thuận, hai bên thống nhất ký kết Hợp đồng thuê xe với các điều khoản như sau:

ĐIỀU 1: NỘI DUNG HỢP ĐỒNG
- Bên A đồng ý cho bên B thuê một xe ô tô tự lái trong thời gian cho thuê.
- Kiểu xe: {car?.CarBrand?.Name} {car?.CarName}, biển số kiểm soát: {car?.LicensePlate}

ĐIỀU 2: GIÁ TRỊ HỢP ĐỒNG, PHƯƠNG THỨC THANH TOÁN
- Giá thuê xe là: {car?.PricePerDay.ToString("N0") ?? "0"} VNĐ/ngày.
- Tiền cọc (Giữ chỗ): {booking.DepositAmount.ToString("N0")} VNĐ.
- Tổng cộng tiền thuê xe: {booking.TotalPrice.ToString("N0")} VNĐ.
- Bên B thanh toán đầy đủ phí thuê và tiền cọc cho Bên A ngay khi hợp đồng có hiệu lực.

ĐIỀU 3: TRÁCH NHIỆM CỦA CÁC BÊN
3.1. Trách nhiệm của bên A (Bên cho thuê):
- Giao xe và toàn bộ giấy tờ bản sao lưu hành hợp pháp liên quan đến xe ngay sau khi Hợp đồng có hiệu lực.
- Chịu trách nhiệm pháp lý về nguồn gốc và quyền sở hữu của xe.
- Mua bảo hiểm xe, đăng kiểm xe và bảo dưỡng định kỳ trước khi giao.

3.2. Trách nhiệm, quyền hạn của bên B (Bên thuê):
- Thanh toán tiền thuê xe và cọc đúng hạn.
- Bên B được toàn quyền sử dụng xe do Bên A giao trong lãnh thổ Việt Nam.
- Không sử dụng xe vào mục đích vi phạm pháp luật, cầm cố thế chấp.
- Chịu toàn bộ chi phí nhiên liệu, cầu đường, đỗ xe trong thời gian thuê.
- Phải đền bù thiệt hại 100% nếu gây xước xát, hư hỏng xe hoặc tai nạn.

ĐIỀU 4: HIỆU LỰC HỢP ĐỒNG
- Hợp đồng có giá trị từ {booking.StartDate:HH:mm dd/MM/yyyy} đến hết ngày {booking.EndDate:HH:mm dd/MM/yyyy}.
- Yêu cầu gia hạn hoặc hủy bỏ phải thông báo trước ít nhất 24 giờ.

ĐIỀU 5: ĐIỀU KHOẢN CHUNG
- Hai bên cam kết thi hành đúng các điều khoản, mọi vi phạm phải xử lý theo pháp luật.
- Các vấn đề phát sinh không thỏa thuận được sẽ do Tòa Án Nhân Dân có thẩm quyền giải quyết.
- Hợp đồng điện tử này có giá trị pháp lý tương đương bản giấy.",
                    IsSignedByCustomer = false,
                    IsSignedByOwner = booking.BookingStatus?.Name == "Confirmed" || booking.BookingStatus?.Name == "Active",
                    OwnerSignDate = (booking.BookingStatus?.Name == "Confirmed" || booking.BookingStatus?.Name == "Active") ? DateTime.UtcNow : null
                };

                _context.DigitalContracts.Add(newContract);
                await _context.SaveChangesAsync();
                booking.DigitalContract = newContract;
            }
            else if (!booking.DigitalContract.IsSignedByOwner && (booking.BookingStatus?.Name == "Confirmed" || booking.BookingStatus?.Name == "Active"))
            {
                booking.DigitalContract.IsSignedByOwner = true;
                booking.DigitalContract.OwnerSignDate = DateTime.UtcNow;
                _context.DigitalContracts.Update(booking.DigitalContract);
                await _context.SaveChangesAsync();
            }

            return View(booking.DigitalContract);
        }

        public async Task<IActionResult> TransactionContract(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var contract = await _context.DigitalContracts
                .Include(c => c.BookingTransaction)
                .FirstOrDefaultAsync(c => c.TransactionID == id && c.BookingTransaction.UserID == userId);

            if (contract == null) return NotFound("Không tìm thấy hợp đồng giao dịch.");

            return View("Contract", contract);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignContract(int id, string customerName, string customerIdentityCard, string customerPhone, string customerAddress, string customerCompanyName, string customerDepartmentCode, string paymentMethod, string customerBankAccount)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var contract = await _context.DigitalContracts
                .Include(c => c.Booking)
                .Include(c => c.BookingTransaction)
                .FirstOrDefaultAsync(c => c.DigitalContractID == id && 
                    ((c.Booking != null && c.Booking.UserID == userId) || 
                     (c.BookingTransaction != null && c.BookingTransaction.UserID == userId)));

            if (contract == null) return NotFound();

            if (!contract.IsSignedByCustomer)
            {
                contract.CustomerName = customerName ?? contract.CustomerName;
                contract.CustomerIdentityCard = customerIdentityCard ?? contract.CustomerIdentityCard;
                contract.CustomerPhone = customerPhone ?? contract.CustomerPhone;
                contract.CustomerAddress = customerAddress ?? contract.CustomerAddress;
                // Corporate fields
                contract.CustomerCompanyName = customerCompanyName;
                contract.CustomerDepartmentCode = customerDepartmentCode;
                contract.PaymentMethod = paymentMethod;
                contract.CustomerBankAccount = customerBankAccount;

                contract.IsSignedByCustomer = true;
                contract.CustomerSignDate = DateTime.UtcNow;
                
                // Optional: Admin also auto-signs or wait for admin manual sign.
                // Assuming "Hợp đồng được 2 bên đồng ý" when the customer signs it, Admin's system can mark it.
                contract.IsSignedByOwner = true;
                contract.OwnerSignDate = DateTime.UtcNow;

                _context.DigitalContracts.Update(contract);
                await _context.SaveChangesAsync();
                
                TempData["Success"] = "Ký hợp đồng thành công!";
            }

            if (contract.BookingID != null)
                return RedirectToAction(nameof(Contract), new { id = contract.BookingID });
            else
                return RedirectToAction(nameof(TransactionContract), new { id = contract.TransactionID });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            var bookings = await _bookingService.GetAllBookingsAsync();
            return View(bookings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmManualPayment(int id)
        {
            // =========================================
            // SECURITY: Verify ownership
            // =========================================
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var booking = await _bookingService.GetBookingByIdAsync(id);
            if (booking == null) return NotFound();

            // Check ownership or Admin
            if (booking.UserID != userId && !User.IsInRole("Admin"))
                return Forbid();

            // Change from Draft to Pending (Awaiting Approval)
            await _bookingService.UpdateBookingStatusAsync(id, "Pending");

            // Kiểm tra đã có Payment chưa, nếu chưa thì tạo mới
            var existingPayment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingID == id);
            if (existingPayment == null)
            {
                // Log a pending payment record
                var payment = new Payment
                {
                    BookingID = id,
                    Amount = booking.TotalPrice * 0.2m,
                    PaymentMethod = "Manual Transfer",
                    PaymentStatus = "Pending",
                    PaymentDate = DateTime.UtcNow
                };
                await _bookingService.ProcessPaymentAsync(payment);
            }

            TempData["Success"] = "Hệ thống đã ghi nhận thông tin chuyển khoản của bạn. Vui lòng chờ Admin xác nhận đơn hàng trong giây lát.";
            return RedirectToAction("Success", "Payment", new { bookingId = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmManualPaymentTransaction(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var transaction = await _context.BookingTransactions
                .Include(t => t.Bookings).ThenInclude(b => b.BookingStatus)
                .FirstOrDefaultAsync(t => t.TransactionID == id && t.UserID == userId);

            if (transaction == null) return NotFound();

            // Neu chua co Bookings (tu CheckoutAll moi), tao bookings tu PendingCartSnapshot
            if (!transaction.Bookings.Any() && !string.IsNullOrEmpty(transaction.PendingCartSnapshot))
            {
                var user = await _context.Users.Include(u => u.MembershipTier).FirstOrDefaultAsync(u => u.UserID == userId);

                var pendingStatus = await _context.BookingStatuses.FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Pending);
                if (pendingStatus == null)
                {
                    pendingStatus = new BookingStatus { Name = BookingStatusConstants.Pending };
                    _context.BookingStatuses.Add(pendingStatus);
                    await _context.SaveChangesAsync();
                }

                try
                {
                    // =========================================
                    // SECURITY: Wrap in transaction with recheck
                    // =========================================
                    using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                    var snapshot = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(transaction.PendingCartSnapshot);
                    var items = snapshot.GetProperty("items");
                    var excludedStatuses = new[] { "Cancelled", "Completed", "Rejected" };

                    // Recheck each item for availability
                    foreach (var item in items.EnumerateArray())
                    {
                        var carId = item.GetProperty("carId").GetInt32();
                        var startDate = item.GetProperty("startDate").GetDateTime();
                        var endDate = item.GetProperty("endDate").GetDateTime();

                        var car = await _context.Cars.Include(c => c.CarStatus).FirstOrDefaultAsync(c => c.CarID == carId);
                        if (car == null)
                            throw new Exception($"Xe ID {carId} khong ton tai.");
                        if (car.CarStatus?.Name != "Available")
                            throw new Exception($"Xe '{car.CarName}' khong con san.");

                        bool hasOverlap = await _context.Bookings.Include(b => b.BookingStatus)
                            .AnyAsync(b => b.CarID == carId && b.BookingStatus != null &&
                                !excludedStatuses.Contains(b.BookingStatus.Name) &&
                                startDate < b.EndDate.AddHours(-1) && endDate.AddHours(1) > b.StartDate);
                        if (hasOverlap)
                            throw new Exception($"Xe '{car.CarName}' da co nguoi dat trong khoang thoi gian.");
                    }

                    var contractNo = $"HD-Cart-{transaction.TransactionID}-{DateTime.UtcNow:MMddyy}";
                    var minStart = DateTime.MaxValue;
                    var maxEnd = DateTime.MinValue;
                    var carsList = new List<string>();
                    var carsModels = new List<string>();
                    decimal baseTotal = 0;
                    decimal totalDeposit = 0;

                    foreach (var item in items.EnumerateArray())
                    {
                        minStart = item.GetProperty("startDate").GetDateTime() < minStart ? item.GetProperty("startDate").GetDateTime() : minStart;
                        maxEnd = item.GetProperty("endDate").GetDateTime() > maxEnd ? item.GetProperty("endDate").GetDateTime() : maxEnd;
                        carsList.Add(item.GetProperty("licensePlate").GetString() ?? "N/A");
                        carsModels.Add($"{item.GetProperty("carBrand").GetString()} {item.GetProperty("carName").GetString()}");
                        baseTotal += item.GetProperty("totalPrice").GetDecimal();
                        totalDeposit += item.GetProperty("defaultDeposit").GetDecimal();
                    }

                    var termsCarsStr = "";
                    int idx = 1;
                    foreach (var item in items.EnumerateArray())
                    {
                        termsCarsStr += $"- Xe {idx++}: {item.GetProperty("carBrand").GetString()} {item.GetProperty("carName").GetString()}, BKS: {item.GetProperty("licensePlate").GetString()}, Gia: {item.GetProperty("totalPrice").GetDecimal():N0} VNĐ\n";
                    }

                    var newContract = new DigitalContract
                    {
                        TransactionID = transaction.TransactionID,
                        ContractNo = contractNo,
                        CustomerName = snapshot.GetProperty("userName").GetString() ?? "",
                        CustomerIdentityCard = "",
                        CustomerPhone = snapshot.GetProperty("userPhone").GetString() ?? "",
                        CustomerAddress = snapshot.GetProperty("userAddress").GetString() ?? "",
                        CarLicensePlate = string.Join(", ", carsList),
                        CarModel = string.Join(" | ", carsModels),
                        RentalStartDate = minStart,
                        RentalEndDate = maxEnd,
                        RentalPricePerDay = 0,
                        TotalContractValue = transaction.TotalAmount,
                        DepositAmount = totalDeposit,
                        TermsAndConditions = $@"Sau khi ban bạc, thỏa thuận, hai bên thống nhất ký kết Hợp đồng thuê XE tự lái với các điều khoản như sau:

ĐIỀU 1: NỘI DUNG HỢP ĐỒNG
- Danh sách xe:
{termsCarsStr}

ĐIỀU 2: GIÁ TRỊ HỢP ĐỒNG, PHƯƠNG THỨC THANH TOÁN
- Tong tien thue: {baseTotal:N0} VNĐ.
- Tong cong: {transaction.TotalAmount:N0} VNĐ.
- Tien coc: {totalDeposit:N0} VNĐ.

ĐIỀU 3: TRÁCH NHIỆM CHUNG
- Khong su dung xe thue vao muc dich vi pham phap luat.
- Chịu toàn bộ chi phí nhiên liệu, cầu đường, đỗ xe.
- Trả xe đúng thời gian và địa điểm.

ĐIỀU 4: HIỆU LỰC HỢP ĐỒNG
- Hợp đồng có giá trị từ {minStart:HH:mm dd/MM/yyyy} đến hết {maxEnd:HH:mm dd/MM/yyyy}.

Hợp đồng điện tử này có giá trị pháp lý tương đương bản giấy.",
                        IsSignedByCustomer = false,
                        IsSignedByOwner = false
                    };
                    _context.DigitalContracts.Add(newContract);

                    // Tao Booking cho tung item
                    foreach (var item in items.EnumerateArray())
                    {
                        var carId = item.GetProperty("carId").GetInt32();
                        var days = item.GetProperty("days").GetInt32();

                        var booking = new Booking
                        {
                            UserID = userId,
                            CarID = carId,
                            StartDate = item.GetProperty("startDate").GetDateTime(),
                            EndDate = item.GetProperty("endDate").GetDateTime(),
                            TotalPrice = item.GetProperty("totalPrice").GetDecimal(),
                            DiscountAmount = 0,
                            TotalHours = item.GetProperty("hours").GetInt32(),
                            BookingStatusID = pendingStatus.BookingStatusID,
                            DepositAmount = item.GetProperty("defaultDeposit").GetDecimal(),
                            DepositStatus = DepositStatusConstants.Holding,
                            TransactionID = transaction.TransactionID,
                            PickupLocationID = item.GetProperty("locationId").GetInt32(),
                            ReturnLocationID = item.GetProperty("locationId").GetInt32(),
                            ContractTypeID = item.TryGetProperty("contractTypeId", out var ctid) && ctid.ValueKind != System.Text.Json.JsonValueKind.Null ? ctid.GetInt32() : null
                        };
                        _context.Bookings.Add(booking);
                        await _context.SaveChangesAsync();

                        // ═══ Tạo DriverBooking nếu có chọn tài xế ═══
                        if (item.TryGetProperty("driverId", out var driverIdProp) && 
                            driverIdProp.ValueKind == System.Text.Json.JsonValueKind.Number &&
                            driverIdProp.GetInt32() > 0)
                        {
                            var driverId = driverIdProp.GetInt32();
                            var driver = await _context.Drivers.FindAsync(driverId);
                            if (driver != null)
                            {
                                var driverBooking = new DriverBooking
                                {
                                    BookingID = booking.BookingID,
                                    DriverID = driverId,
                                    DriverFee = driver.DailyRate * item.GetProperty("days").GetInt32(),
                                    Status = "Pending",
                                    CreatedAt = DateTime.UtcNow
                                };
                                _context.DriverBookings.Add(driverBooking);
                                await _context.SaveChangesAsync();
                            }
                        }

                        // BookingAddOns
                        if (item.TryGetProperty("addonDetails", out var addonDetails) && addonDetails.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var ad in addonDetails.EnumerateArray())
                            {
                                _context.BookingAddOns.Add(new BookingAddOn
                                {
                                    BookingID = booking.BookingID,
                                    AddOnID = ad.GetProperty("id").GetInt32(),
                                    Quantity = 1,
                                    TotalPrice = ad.GetProperty("total").GetDecimal()
                                });
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await dbTransaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Loi khi tao don: " + ex.Message;
                    return RedirectToAction("Index", "Cart");
                }
            }

            // Cap nhat transaction
            transaction.Status = BookingStatusConstants.Confirmed;
            _context.BookingTransactions.Update(transaction);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Thanh toán thành công! Đơn thuê đã được xác nhận.";
            return RedirectToAction("SuccessTransaction", "Payment", new { transactionId = id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.Car).ThenInclude(c => c.CarBrand)
                .Include(b => b.Car).ThenInclude(c => c.Location)
                .Include(b => b.Car).ThenInclude(c => c.CarImages)
                .Include(b => b.User)
                .Include(b => b.BookingStatus)
                .Include(b => b.Payment)
                .Include(b => b.RentalContract)
                .Include(b => b.DigitalContract)
                .Include(b => b.HandoverRecords)
                .Include(b => b.BookingAddOns).ThenInclude(ba => ba.AddOn)
                .FirstOrDefaultAsync(b => b.BookingID == id && b.UserID == userId);

            if (booking == null) return NotFound();
            return View(booking);
        }
    }
}
