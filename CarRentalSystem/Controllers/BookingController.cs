using CarRentalSystem.Models;
using CarRentalSystem.Services;
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
        private readonly Data.ApplicationDbContext _context;

        public BookingController(IBookingService bookingService, ICarService carService, IAuthService authService, Data.ApplicationDbContext context)
        {
            _bookingService = bookingService;
            _carService = carService;
            _authService = authService;
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
                        return RedirectToAction("Profile", "Auth");
                    }
                }
            }

            var car = await _carService.GetCarByIdAsync(CarID);
            if (car == null) return NotFound();

            // Phase 3: Maintenance blocking
            if (car.NextInspectionDate.HasValue && car.NextInspectionDate.Value < DateTime.Now)
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
            bool isOverlapped = await _context.Bookings
                .Include(b => b.BookingStatus)
                .AnyAsync(b => b.CarID == CarID &&
                    b.BookingStatus != null &&
                    b.BookingStatus.Name != "Cancelled" && b.BookingStatus.Name != "Completed" &&
                    ((StartDate >= b.StartDate && StartDate < b.EndDate) ||
                     (EndDate > b.StartDate && EndDate <= b.EndDate) ||
                     (StartDate <= b.StartDate && EndDate >= b.EndDate)));

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

            // Generate a temporary booking code for display
            ViewBag.TempBookingCode = DateTime.Now.Ticks.ToString();

            // Fetch available drivers for selection
            ViewBag.Drivers = await _context.Drivers
                .Where(d => d.IsApproved && d.IsAvailable)
                .ToListAsync();

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

        [HttpPost]
        public async Task<IActionResult> Book(Booking booking, int? SelectedDriverID, decimal DriverFee)
        {
            // Validate input trước khi xử lý
            if (booking.EndDate <= booking.StartDate)
            {
                TempData["BookingError"] = "Ngày kết thúc phải sau ngày bắt đầu.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            if (booking.StartDate.Date < DateTime.Today)
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

            if (booking.DeliveryFee > 0 && !string.IsNullOrEmpty(booking.DeliveryAddress) && car.Location != null)
            {
                var regionName = car.Location.Name ?? "";
                bool isValid = booking.DeliveryAddress.Contains(regionName, StringComparison.OrdinalIgnoreCase) ||
                               (regionName.Contains("Hồ Chí Minh") && booking.DeliveryAddress.Contains("HCM", StringComparison.OrdinalIgnoreCase)) ||
                               (regionName.Contains("Sài Gòn") && booking.DeliveryAddress.Contains("HCM", StringComparison.OrdinalIgnoreCase));

                if (!isValid)
                {
                    TempData["BookingError"] = $"Lỗi: Tùy chọn giao xe tận nơi chỉ hỗ trợ trong phạm vi khu vực {regionName} (nơi xe đang bảo quản). Địa chỉ bạn nhập không thuộc khu vực này.";
                    return RedirectToAction("Checkout", new { CarID = booking.CarID, StartDate = booking.StartDate.ToString("yyyy-MM-ddTHH:mm"), EndDate = booking.EndDate.ToString("yyyy-MM-ddTHH:mm"), PickupLocation = car.Location.Name, pickupLocationType = "owner" });
                }
            }

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
                    if (promo != null && promo.IsActive && promo.ValidFrom <= DateTime.Now && promo.ValidUntil >= DateTime.Now && promo.UsedCount < promo.UsageLimit)
                    {
                        decimal discount = booking.TotalPrice * promo.DiscountPercent / 100m;
                        if (promo.MaxDiscountAmount.HasValue && discount > promo.MaxDiscountAmount.Value)
                            discount = promo.MaxDiscountAmount.Value;
                        
                        booking.DiscountAmount += discount;
                        booking.TotalPrice -= discount;
                        
                        promo.UsedCount++;
                        _context.Promotions.Update(promo);
                    }
                }

                if (booking.TotalPrice < 0) booking.TotalPrice = 0;
                
                booking.TotalHours = (int)Math.Ceiling(diffTime.TotalHours);
                if (booking.TotalHours == 0) booking.TotalHours = 1;

                booking.DepositAmount = car.DefaultDeposit;
                booking.DepositStatus = "Holding";
                
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

                return RedirectToAction(nameof(Deposit), new { id = booking.BookingID });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["BookingError"] = "Đã xảy ra lỗi khi xử lý đặt xe. Vui lòng thử lại.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }
        }

        public async Task<IActionResult> MyBookings()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId))
            {
                var allBookings = await _bookingService.GetUserBookingsAsync(userId);
                // Filter out draft bookings (unpaid) to enforce "payment-first" visibility
                var bookings = allBookings.Where(b => b.BookingStatus?.Name != "Draft").ToList();
                return View(bookings);
            }
            return Unauthorized();
        }

        [HttpPost]
        public async Task<IActionResult> CancelBooking(int id, string? reason)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var bookings = await _bookingService.GetUserBookingsAsync(userId);
            var booking = bookings.FirstOrDefault(b => b.BookingID == id);

            if (booking == null)
            {
                TempData["Error"] = "Không tìm thấy đơn đặt xe.";
                return RedirectToAction(nameof(MyBookings));
            }

            // Only allow cancellation for Pending status (Task 1)
            var cancellableStatuses = new[] { "Pending" };
            if (!cancellableStatuses.Contains(booking.BookingStatus?.Name))
            {
                TempData["Error"] = "Không thể hủy đơn khi đã được xác nhận hoặc đang ở trạng thái hiện tại.";
                return RedirectToAction(nameof(MyBookings));
            }

            await _bookingService.CancelBookingAsync(id, reason ?? "Khách hàng tự hủy");
            TempData["Success"] = "Đơn đặt xe đã được hủy thành công.";
            return RedirectToAction(nameof(MyBookings));
        }

        [HttpPost]
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

            // 2. Start Trip
            booking.ActualPickupDate = DateTime.Now;
            
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

            // 3. Initialize Rental Contract with current car odometer
            if (booking.RentalContract == null)
            {
                booking.RentalContract = new RentalContract
                {
                    BookingID = booking.BookingID,
                    BasePrice = booking.TotalPrice,
                    StartKm = booking.Car?.CurrentOdometer ?? 0,
                    EndKm = booking.Car?.CurrentOdometer ?? 0
                };
                _context.RentalContracts.Add(booking.RentalContract);
            }
            else
            {
                booking.RentalContract.StartKm = booking.Car?.CurrentOdometer ?? 0;
            }

            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Bạn đã nhận xe thành công! Chúc bạn có một chuyến đi an toàn.";
            return RedirectToAction(nameof(MyBookings));
        }

        [HttpPost]
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



            // 1. Record Return Date
            booking.ActualReturnDate = DateTime.Now;
            
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
            var checkoutRecord = await _context.HandoverRecords
                .Where(h => h.BookingID == id && h.Type == "CheckOut")
                .OrderByDescending(h => h.RecordDate)
                .FirstOrDefaultAsync();
            
            if (checkoutRecord != null && booking.Car != null)
            {
                booking.Car.CurrentOdometer = checkoutRecord.Odo;
            }

            // 3. Calculate Mileage & Fees in RentalContract
            if (booking.RentalContract != null)
            {
                if (checkoutRecord != null)
                    booking.RentalContract.EndKm = checkoutRecord.Odo;
                
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
                    ContractNo = $"HD-{booking.BookingID}-{DateTime.Now:MMddyy}",
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
                    OwnerSignDate = (booking.BookingStatus?.Name == "Confirmed" || booking.BookingStatus?.Name == "Active") ? DateTime.Now : null
                };

                _context.DigitalContracts.Add(newContract);
                await _context.SaveChangesAsync();
                booking.DigitalContract = newContract;
            }
            else if (!booking.DigitalContract.IsSignedByOwner && (booking.BookingStatus?.Name == "Confirmed" || booking.BookingStatus?.Name == "Active"))
            {
                booking.DigitalContract.IsSignedByOwner = true;
                booking.DigitalContract.OwnerSignDate = DateTime.Now;
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
                contract.CustomerSignDate = DateTime.Now;
                
                // Optional: Admin also auto-signs or wait for admin manual sign.
                // Assuming "Hợp đồng được 2 bên đồng ý" when the customer signs it, Admin's system can mark it.
                contract.IsSignedByOwner = true;
                contract.OwnerSignDate = DateTime.Now;

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
        public async Task<IActionResult> ConfirmManualPayment(int id)
        {
            var booking = await _bookingService.GetBookingByIdAsync(id);
            if (booking == null) return NotFound();

            // Change from Draft to Pending (Awaiting Approval)
            await _bookingService.UpdateBookingStatusAsync(id, "Pending");
            
            // Log a pending payment record
            var payment = new Payment
            {
                BookingID = id,
                Amount = booking.TotalPrice * 0.2m,
                PaymentMethod = "Manual Transfer",
                PaymentStatus = "Pending",
                PaymentDate = DateTime.Now
            };
            await _bookingService.ProcessPaymentAsync(payment);

            TempData["Success"] = "Hệ thống đã ghi nhận thông tin chuyển khoản của bạn. Vui lòng chờ Admin xác nhận đơn hàng trong giây lát.";
            return RedirectToAction("Success", "Payment", new { bookingId = id });
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
