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
        private readonly IEligibilityService _eligibilityService;
        private readonly IBookingBusinessRuleService _businessRules;
        private readonly IBookingTransitionService _bookingTransitionService;
        private readonly IPaymentTransitionService _paymentTransitionService;
        private readonly ISettlementReviewService _settlementReviewService;
        private readonly ILogger<BookingController> _logger;
        private readonly Data.ApplicationDbContext _context;

        public BookingController(
            IBookingService bookingService,
            ICarService carService,
            IAuthService authService,
            IReservationService reservationService,
            IBookingQuoteService quoteService,
            IPayOSPaymentService payOSPaymentService,
            IDeliveryScopeService deliveryScopeService,
            IEligibilityService eligibilityService,
            IBookingBusinessRuleService businessRules,
            IBookingTransitionService bookingTransitionService,
            IPaymentTransitionService paymentTransitionService,
            ISettlementReviewService settlementReviewService,
            ILogger<BookingController> logger,
            Data.ApplicationDbContext context)
        {
            _bookingService = bookingService;
            _carService = carService;
            _authService = authService;
            _reservationService = reservationService;
            _quoteService = quoteService;
            _payOSPaymentService = payOSPaymentService;
            _deliveryScopeService = deliveryScopeService;
            _eligibilityService = eligibilityService;
            _businessRules = businessRules;
            _bookingTransitionService = bookingTransitionService;
            _paymentTransitionService = paymentTransitionService;
            _settlementReviewService = settlementReviewService;
            _logger = logger;
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
                        TempData["BookingError"] = "TÃ i khoáº£n cá»§a báº¡n Ä‘Ã£ bá»‹ khÃ³a: " + user.BlacklistReason;
                        return RedirectToAction("Details", "Car", new { id = CarID });
                    }
                    var eligibility = await _eligibilityService.CheckRentalEligibilityAsync(userId);
                    if (!eligibility.IsEligible)
                    {
                        if (eligibility.FailureCode == EligibilityFailureCode.EkycNotVerified ||
                            eligibility.FailureCode == EligibilityFailureCode.MissingIdentityDocuments)
                        {
                            TempData["ShowEkycModal"] = "true";
                            TempData["EkycReturnUrl"] = $"{Request.Path}{Request.QueryString}";
                            TempData["WarningMessage"] = eligibility.Reason ?? "Cáº§n xÃ¡c thá»±c eKYC trÆ°á»›c khi thuÃª xe.";
                        }
                        else
                        {
                            TempData["BookingError"] = eligibility.Reason ?? "TÃ i khoáº£n chÆ°a Ä‘á»§ Ä‘iá»u kiá»‡n Ä‘áº·t xe.";
                        }
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
                TempData["BookingError"] = "Xe nÃ y Ä‘ang táº¡m ngÆ°ng cho thuÃª do quÃ¡ háº¡n ÄÄƒng Kiá»ƒm. Vui lÃ²ng chá»n xe khÃ¡c.";
                return RedirectToAction("Details", "Car", new { id = CarID });
            }

            if (EndDate <= StartDate)
            {
                TempData["BookingError"] = "NgÃ y káº¿t thÃºc pháº£i sau ngÃ y báº¯t Ä‘áº§u.";
                return RedirectToAction("Details", "Car", new { id = CarID });
            }

            // ðŸ”’ DB-level overlap check (khÃ´ng load toÃ n bá»™ bookings vÃ o RAM)
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
                TempData["BookingError"] = "Ráº¥t tiáº¿c, xe nÃ y Ä‘Ã£ Ä‘Æ°á»£c Ä‘áº·t trong khoáº£ng thá»i gian báº¡n chá»n. Vui lÃ²ng chá»n ngÃ y khÃ¡c.";
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
        public async Task<IActionResult> Book(Booking booking, bool SelfDriveAgreement, int? SelectedDriverID, decimal DriverFee, List<string>? SelectedAddons)
        {
            if (!SelfDriveAgreement)
            {
                TempData["BookingError"] = "Báº¡n pháº£i xÃ¡c nháº­n lÃ  ngÆ°á»i trá»±c tiáº¿p Ä‘iá»u khiá»ƒn xe vÃ  cÃ³ giáº¥y phÃ©p lÃ¡i xe há»£p lá»‡.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            if (SelectedDriverID.HasValue || DriverFee > 0 || booking.DriverID.HasValue || booking.DriverBookingID.HasValue)
            {
                TempData["BookingError"] = "ÄÆ¡n thuÃª xe tá»± lÃ¡i khÃ´ng Ä‘Æ°á»£c gÃ¡n tÃ i xáº¿ hoáº·c phÃ­ tÃ i xáº¿.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            // Validate input trÆ°á»›c khi xá»­ lÃ½
            if (booking.EndDate <= booking.StartDate)
            {
                TempData["BookingError"] = "NgÃ y káº¿t thÃºc pháº£i sau ngÃ y báº¯t Ä‘áº§u.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            if (booking.StartDate.Date < DateTime.UtcNow.Date)
            {
                TempData["BookingError"] = "KhÃ´ng thá»ƒ Ä‘áº·t xe cho ngÃ y trong quÃ¡ khá»©.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId))
            {
                var user = await _authService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    var eligibility = await _eligibilityService.CheckRentalEligibilityAsync(userId);
                    if (!eligibility.IsEligible)
                    {
                        if (eligibility.FailureCode == EligibilityFailureCode.EkycNotVerified ||
                            eligibility.FailureCode == EligibilityFailureCode.MissingIdentityDocuments)
                        {
                            TempData["ShowEkycModal"] = "true";
                            TempData["EkycReturnUrl"] = Url.Action("Details", "Car", new { id = booking.CarID }) ?? "/";
                            TempData["WarningMessage"] = eligibility.Reason ?? "Cáº§n xÃ¡c thá»±c eKYC trÆ°á»›c khi thuÃª xe.";
                        }
                        else
                        {
                            TempData["BookingError"] = eligibility.Reason ?? "Lá»—i xÃ¡c thá»±c. Vui lÃ²ng kiá»ƒm tra láº¡i tráº¡ng thÃ¡i tÃ i khoáº£n.";
                        }
                        return RedirectToAction("Details", "Car", new { id = booking.CarID });
                    }
                    booking.UserID = userId;
                }
            }

            var car = await _context.Cars.Include(c => c.Location).FirstOrDefaultAsync(c => c.CarID == booking.CarID);
            if (car == null) return NotFound();

            if (!car.IsSelfDriveAllowed || car.IsWithDriverAllowed)
            {
                TempData["BookingError"] = "Xe nÃ y khÃ´ng thuá»™c luá»“ng thuÃª xe tá»± lÃ¡i.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            var validLicense = await _businessRules.GetValidCustomerDrivingLicenseAsync(booking.UserID);
            if (validLicense == null)
            {
                TempData["ShowEkycModal"] = "true";
                TempData["EkycReturnUrl"] = Url.Action("Details", "Car", new { id = booking.CarID }) ?? "/";
                TempData["WarningMessage"] = "Vui lÃ²ng xÃ¡c thá»±c giáº¥y phÃ©p lÃ¡i xe há»£p lá»‡ trÆ°á»›c khi thuÃª xe tá»± lÃ¡i.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            booking.ServiceTypeID = await _businessRules.GetServiceTypeIdAsync(ServiceTypeConstants.SelfDrive);
            booking.CustomerDrivingLicenseID = validLicense.CustomerDrivingLicenseID;
            booking.ContractTypeCode = "SELF_DRIVE_CONTRACT";
            await _businessRules.ValidateSelfDriveAsync(booking, DriverFee);

            if (booking.DeliveryFee > 0 && string.IsNullOrWhiteSpace(booking.DeliveryAddress))
            {
                TempData["BookingError"] = "Vui lÃ²ng nháº­p Ä‘á»‹a chá»‰ giao xe khi chá»n dá»‹ch vá»¥ giao táº­n nÆ¡i.";
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

            BookingHoldResult? holdResult = null;
            var paymentCreated = false;

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
                    PromotionID = booking.PromotionID
                });

                var availability = await _reservationService.CheckCarAvailabilityAsync(booking.CarID, booking.StartDate, booking.EndDate);
                if (!availability.IsAvailable)
                {
                    TempData["BookingError"] = availability.Reason ?? "Xe nÃ y Ä‘Ã£ Ä‘Æ°á»£c giá»¯ hoáº·c Ä‘áº·t trong thá»i gian báº¡n chá»n.";
                    return RedirectToAction("Details", "Car", new { id = booking.CarID });
                }

                holdResult = await _reservationService.CreateHoldAsync(booking.UserID, booking.CarID, booking.StartDate, booking.EndDate, quote);
                if (!holdResult.IsSuccess || holdResult.Hold == null)
                {
                    TempData["BookingError"] = holdResult.ErrorMessage ?? "KhÃ´ng thá»ƒ giá»¯ xe. Vui lÃ²ng thá»­ láº¡i.";
                    return RedirectToAction("Details", "Car", new { id = booking.CarID });
                }

                var domain = PaymentUrlResolver.ResolvePublicBaseUrl(HttpContext);
                var payOSResult = await _payOSPaymentService.CreateHoldPaymentAsync(holdResult.Hold, quote, domain);
                if (!payOSResult.IsSuccess || payOSResult.Transaction == null || string.IsNullOrWhiteSpace(payOSResult.CheckoutUrl))
                {
                    await _reservationService.ReleaseHoldAsync(holdResult.Hold.BookingHoldID, "PayOS create payment failed.");
                    TempData["BookingError"] = payOSResult.ErrorMessage ?? "KhÃ´ng thá»ƒ táº¡o thanh toÃ¡n PayOS.";
                    return RedirectToAction("Details", "Car", new { id = booking.CarID });
                }
                paymentCreated = true;

                try
                {
                    _context.AuditLogs.Add(new AuditLog
                    {
                        Action = "Táº¡o QR PayOS",
                        EntityType = "Thanh toÃ¡n",
                        EntityId = holdResult.Hold.BookingHoldID,
                        UserId = booking.UserID,
                        UserName = User.Identity?.Name,
                        Description = $"KhÃ¡ch táº¡o phiáº¿u giá»¯ chá»— #{holdResult.Hold.BookingHoldID}, sá»‘ tiá»n PayOS {quote.HoldingFee:N0} VND. ChÆ°a táº¡o Ä‘Æ¡n thuÃª.",
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        UserAgent = Request.Headers["User-Agent"].ToString(),
                        RequestUrl = "POST /Booking/Book",
                        Timestamp = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
                catch (Exception auditException)
                {
                    _logger.LogWarning(auditException, "Failed to write PayOS hold audit log for hold {HoldId}.", holdResult.Hold.BookingHoldID);
                }

                return Redirect(payOSResult.CheckoutUrl);
            }
            catch (Exception ex)
            {
                if (!paymentCreated && holdResult?.Hold != null)
                    await _reservationService.ReleaseHoldAsync(holdResult.Hold.BookingHoldID, "Exception while creating hold payment.");

                _logger.LogError(ex, "Error while creating hold payment for car {CarId}.", booking.CarID);
                TempData["BookingError"] = "ÄÃ£ xáº£y ra lá»—i khi táº¡o thanh toÃ¡n giá»¯ chá»—. Há»‡ thá»‘ng Ä‘Ã£ giáº£i phÃ³ng giá»¯ chá»— náº¿u chÆ°a táº¡o Ä‘Æ°á»£c PayOS. Vui lÃ²ng thá»­ láº¡i.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }
        }

        /*
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            // ðŸ”’ DB TRANSACTION â€” Atomic booking creation
            // Chá»‘ng race condition (double booking) + data consistency
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                // ðŸ”’ OVERLAP CHECK INSIDE TRANSACTION (DB-level lock)
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
                    TempData["BookingError"] = "Ráº¥t tiáº¿c, xe nÃ y Ä‘Ã£ Ä‘Æ°á»£c Ä‘áº·t trong khoáº£ng thá»i gian báº¡n chá»n. Vui lÃ²ng chá»n xe khÃ¡c hoáº·c Ä‘á»•i ngÃ y.";
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

                // Phá»¥ phÃ­ VAT & Service (Cá»‘ Ä‘á»‹nh 150k cho demo)
                decimal serviceFee = 150000;

                booking.TotalPrice = basePrice + (insuranceDays * dailyInsurance) + serviceFee + booking.DeliveryFee + booking.AddOnFee;

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

                        // Sá»­ dá»¥ng atomic update Ä‘á»ƒ trÃ¡nh race condition khi nhiá»u user cÃ¹ng dÃ¹ng promo
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

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToAction("CreatePayment", "Payment", new { bookingId = booking.BookingID });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["BookingError"] = "ÄÃ£ xáº£y ra lá»—i khi xá»­ lÃ½ Ä‘áº·t xe. Vui lÃ²ng thá»­ láº¡i.";
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

            var booking = await _context.Bookings
                .Include(b => b.BookingStatus)
                .FirstOrDefaultAsync(b => b.BookingID == id && b.UserID == userId);

            if (booking == null)
            {
                TempData["Error"] = "Khong tim thay don dat xe.";
                return RedirectToAction(nameof(MyBookings));
            }

            var cancellableStatuses = new[] { BookingStatusConstants.Pending, BookingStatusConstants.Confirmed, BookingStatusConstants.Active };
            if (!cancellableStatuses.Contains(booking.BookingStatus?.Name))
            {
                TempData["Error"] = "Khong the huy don o trang thai hien tai.";
                return RedirectToAction(nameof(MyBookings));
            }

            var result = await _bookingTransitionService.CancelAsync(
                id,
                userId,
                User.IsInRole("Admin") ? "Admin" : "Customer",
                string.IsNullOrWhiteSpace(reason) ? "Customer cancelled booking." : reason.Trim());

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage ?? "Khong the huy don o trang thai hien tai.";
                return RedirectToAction(nameof(MyBookings));
            }

            TempData["Success"] = "Don dat xe da duoc huy thanh cong.";
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
                .Include(b => b.Car)
                .Include(b => b.RentalContract)
                .FirstOrDefaultAsync(b => b.BookingID == id && b.UserID == userId);

            if (booking == null || booking.BookingStatus?.Name != BookingStatusConstants.Confirmed)
            {
                TempData["Error"] = "Don dat xe khong hop le de nhan xe.";
                return RedirectToAction(nameof(MyBookings));
            }

            var checkInRecord = await _context.HandoverRecords
                .Where(h => h.BookingID == id && h.Type == "CheckIn" && !h.IsDeleted)
                .OrderByDescending(h => h.RecordDate)
                .FirstOrDefaultAsync();

            if (checkInRecord == null)
            {
                TempData["Error"] = "Ban phai tao bien ban nhan xe truoc khi xac nhan nhan xe.";
                return RedirectToAction("Capture", "Handover", new { bookingId = id, type = "CheckIn" });
            }

            var transition = await _bookingTransitionService.TransitionAsync(
                id,
                BookingStatusConstants.Active,
                userId,
                User.IsInRole("Admin") ? "Admin" : "Customer",
                "Customer confirmed pickup.");

            if (!transition.IsSuccess)
            {
                TempData["Error"] = transition.ErrorMessage ?? "Khong the xac nhan nhan xe.";
                return RedirectToAction(nameof(MyBookings));
            }

            booking = await _context.Bookings
                .Include(b => b.RentalContract)
                .FirstAsync(b => b.BookingID == id);

            if (booking.RentalContract == null)
            {
                _context.RentalContracts.Add(new RentalContract
                {
                    BookingID = booking.BookingID,
                    BasePrice = booking.TotalPrice,
                    StartKm = checkInRecord.Odo,
                    EndKm = checkInRecord.Odo,
                    TotalKm = 0,
                    FinalPrice = booking.TotalPrice
                });
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Ban da xac nhan nhan xe thanh cong.";
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
                TempData["Error"] = "ÄÆ¡n Ä‘áº·t xe khÃ´ng há»£p lá»‡ Ä‘á»ƒ tráº£ xe.";
                return RedirectToAction(nameof(MyBookings));
            }

            // Báº®T BUá»˜C: Kiá»ƒm tra biÃªn báº£n tráº£ xe (CheckOut) Ä‘Ã£ Ä‘Æ°á»£c táº¡o chÆ°a
            var checkOutRecord = await _context.HandoverRecords
                .Where(h => h.BookingID == id && h.Type == "CheckOut" && !h.IsDeleted)
                .OrderByDescending(h => h.RecordDate)
                .FirstOrDefaultAsync();

            if (checkOutRecord == null)
            {
                TempData["Error"] = "Báº¡n pháº£i táº¡o biÃªn báº£n tráº£ xe trÆ°á»›c khi xÃ¡c nháº­n tráº£ xe. Vui lÃ²ng chá»¥p áº£nh xe vÃ  Ä‘iá»n thÃ´ng tin biÃªn báº£n.";
                return RedirectToAction("Capture", "Handover", new { bookingId = id, type = "CheckOut" });
            }

            // 1. Record Return Date
            booking.ActualReturnDate = DateTime.UtcNow;

            // 2. Update status through the transition service so booking/car/deposit changes are audited.
            var completeResult = await _bookingTransitionService.TransitionAsync(id, BookingStatusConstants.Completed, userId, "Customer", "Customer confirmed vehicle return.");
            if (!completeResult.IsSuccess)
            {
                TempData["Error"] = completeResult.ErrorMessage;
                return RedirectToAction(nameof(MyBookings));
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            // ðŸ’° TÃNH PHÃ TRá»„ Háº N â€” OverTimeFee * sá»‘ giá» trá»…
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            string msg = "Báº¡n Ä‘Ã£ tráº£ xe thÃ nh cÃ´ng!";
            bool hasViolation = false;

            if (booking.ActualReturnDate > booking.EndDate)
            {
                var lateSpan = booking.ActualReturnDate.Value - booking.EndDate;
                int lateHours = (int)Math.Ceiling(lateSpan.TotalHours);

                // Grace period: miá»…n phÃ­ 30 phÃºt Ä‘áº§u
                if (lateSpan.TotalMinutes > 30 && booking.Car != null)
                {
                    booking.LateFeeAmount = lateHours * booking.Car.OverTimeFee;
                    booking.TotalPrice += booking.LateFeeAmount;
                    msg += $" âš ï¸ Tráº£ xe trá»… {lateHours} giá». Phá»¥ phÃ­: {booking.LateFeeAmount:N0}Ä‘ ({booking.Car.OverTimeFee:N0}Ä‘/giá»).";
                    hasViolation = true;
                }
                else
                {
                    msg += $" (Trá»… {(int)lateSpan.TotalMinutes} phÃºt â€” trong thá»i gian miá»…n phÃ­ 30 phÃºt)";
                }
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            // ðŸ“Š Cáº¬P NHáº¬T ODOMETER Tá»ª HANDOVER RECORD
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            // Sá»­ dá»¥ng Odo tá»« biÃªn báº£n CheckOut (Ä‘Ã£ báº¯t buá»™c á»Ÿ trÃªn)
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

            // 4. Reset car status â†’ Available
            // Car status is reset to Available inside IBookingTransitionService.Completed.

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            // ðŸ’° DEPOSIT REFUND LOGIC â€” Giá»¯ 15 ngÃ y rá»“i hoÃ n tráº£
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            if (!hasViolation && booking.LateFeeAmount == 0)
            {
                // KhÃ´ng vi pháº¡m â†’ lÃªn lá»‹ch hoÃ n cá»c sau 15 ngÃ y (chá» kiá»ƒm tra pháº¡t nguá»“i)
                await _paymentTransitionService.SetBookingDepositStatusAsync(id, DepositStatusConstants.PendingRefund, userId, "Customer", "Clean return pending deposit refund.");
                msg += " Tiá»n cá»c sáº½ Ä‘Æ°á»£c hoÃ n tráº£ sau 15 ngÃ y kiá»ƒm tra pháº¡t nguá»™i.";
            }
            else
            {
                // CÃ³ vi pháº¡m â†’ Admin sáº½ xá»­ lÃ½ cá»c thá»§ cÃ´ng
                await _paymentTransitionService.SetBookingDepositStatusAsync(id, DepositStatusConstants.UnderReview, userId, "Customer", "Return requires settlement review.");
                await _settlementReviewService.CreateAsync(
                    id,
                    booking.LateFeeAmount > 0 ? SettlementReviewTypeConstants.Late : SettlementReviewTypeConstants.Other,
                    booking.LateFeeAmount,
                    "Return generated settlement review.",
                    userId,
                    checkOutRecord.HandoverID);
                msg += " Tiá»n cá»c Ä‘ang Ä‘Æ°á»£c xem xÃ©t do phÃ¡t sinh phÃ­.";
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

        public IActionResult Contract(int id)
        {
            if (!IsInternalUser()) return Forbid();

            TempData["Info"] = "He thong da bo hop dong dien tu. Vui long upload hop dong giay da ky vao muc Hop dong vat ly.";
            return RedirectToAction("Index", "PhysicalContracts", new { area = "Admin", bookingId = id });
        }

        public IActionResult TransactionContract(int id)
        {
            if (!IsInternalUser()) return Forbid();

            TempData["Info"] = "He thong da bo hop dong dien tu. Vui long upload hop dong giay da ky vao muc Hop dong vat ly.";
            return RedirectToAction("Index", "PhysicalContracts", new { area = "Admin" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SignContract(int id, string customerName, string customerIdentityCard, string customerPhone, string customerAddress, string customerCompanyName, string customerDepartmentCode, string paymentMethod, string customerBankAccount)
        {
            if (!IsInternalUser()) return Forbid();

            TempData["Info"] = "He thong da bo ky hop dong dien tu. Vui long upload anh/PDF hop dong giay da ky.";
            return RedirectToAction("Index", "PhysicalContracts", new { area = "Admin" });
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

            // Kiá»ƒm tra Ä‘Ã£ cÃ³ Payment chÆ°a, náº¿u chÆ°a thÃ¬ táº¡o má»›i
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

            TempData["Success"] = "Há»‡ thá»‘ng Ä‘Ã£ ghi nháº­n thÃ´ng tin chuyá»ƒn khoáº£n cá»§a báº¡n. Vui lÃ²ng chá» Admin xÃ¡c nháº­n Ä‘Æ¡n hÃ ng trong giÃ¢y lÃ¡t.";
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
                    var selfDriveServiceTypeId = await _businessRules.GetServiceTypeIdAsync(ServiceTypeConstants.SelfDrive);
                    var validLicense = await _businessRules.GetValidCustomerDrivingLicenseAsync(userId);
                    if (validLicense == null)
                        throw new Exception("KhÃ¡ch thuÃª xe tá»± lÃ¡i pháº£i cung cáº¥p giáº¥y phÃ©p lÃ¡i xe há»£p lá»‡.");

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
                        termsCarsStr += $"- Xe {idx++}: {item.GetProperty("carBrand").GetString()} {item.GetProperty("carName").GetString()}, BKS: {item.GetProperty("licensePlate").GetString()}, Gia: {item.GetProperty("totalPrice").GetDecimal():N0} VNÄ\n";
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
                        TermsAndConditions = $@"Sau khi ban báº¡c, thá»a thuáº­n, hai bÃªn thá»‘ng nháº¥t kÃ½ káº¿t Há»£p Ä‘á»“ng thuÃª XE tá»± lÃ¡i vá»›i cÃ¡c Ä‘iá»u khoáº£n nhÆ° sau:

ÄIá»€U 1: Ná»˜I DUNG Há»¢P Äá»’NG
- Danh sÃ¡ch xe:
{termsCarsStr}

ÄIá»€U 2: GIÃ TRá»Š Há»¢P Äá»’NG, PHÆ¯Æ NG THá»¨C THANH TOÃN
- Tá»•ng tiá»n thuÃª: {baseTotal:N0} VNÄ.
- Tá»•ng cá»™ng: {transaction.TotalAmount:N0} VNÄ.
- Tiá»n cá»c: {totalDeposit:N0} VNÄ.

ÄIá»€U 3: TRÃCH NHIá»†M CHUNG
- KhÃ´ng sá»­ dá»¥ng xe thuÃª vÃ o má»¥c Ä‘Ã­ch vi pháº¡m phÃ¡p luáº­t.
- Chá»‹u toÃ n bá»™ chi phÃ­ nhiÃªn liá»‡u, cáº§u Ä‘Æ°á»ng, Ä‘á»— xe.
- Tráº£ xe Ä‘Ãºng thá»i gian vÃ  Ä‘á»‹a Ä‘iá»ƒm.

ÄIá»€U 4: HIá»†U Lá»°C Há»¢P Äá»’NG
- Há»£p Ä‘á»“ng cÃ³ giÃ¡ trá»‹ tá»« {minStart:HH:mm dd/MM/yyyy} Ä‘áº¿n háº¿t {maxEnd:HH:mm dd/MM/yyyy}.

Há»£p Ä‘á»“ng Ä‘iá»‡n tá»­ nÃ y cÃ³ giÃ¡ trá»‹ phÃ¡p lÃ½ tÆ°Æ¡ng Ä‘Æ°Æ¡ng báº£n giáº¥y.",
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
                            ServiceTypeID = selfDriveServiceTypeId,
                            CustomerDrivingLicenseID = validLicense.CustomerDrivingLicenseID,
                            ContractTypeCode = "SELF_DRIVE_CONTRACT",
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
                    TempData["Error"] = "Lá»—i khi táº¡o Ä‘Æ¡n: " + ex.Message;
                    return RedirectToAction("Index", "Cart");
                }
            }

            // Cap nhat transaction
            transaction.Status = BookingStatusConstants.Confirmed;
            _context.BookingTransactions.Update(transaction);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Thanh toÃ¡n thÃ nh cÃ´ng! ÄÆ¡n thuÃª Ä‘Ã£ Ä‘Æ°á»£c xÃ¡c nháº­n.";
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

        private bool IsInternalUser()
        {
            return User.IsInRole(UserRoleConstants.Admin) ||
                   User.IsInRole(UserRoleConstants.Manager) ||
                   User.IsInRole(UserRoleConstants.Staff);
        }
    }
}
