using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IPiiEncryptionService _piiEncryption;
        private readonly ILogger<BookingController> _logger;
        private readonly ILegalComplianceService _legalCompliance;
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
            IPiiEncryptionService piiEncryption,
            ILogger<BookingController> logger,
            ILegalComplianceService legalCompliance,
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
            _piiEncryption = piiEncryption;
            _logger = logger;
            _legalCompliance = legalCompliance;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Checkout(int CarID, DateTime StartDate, DateTime EndDate, string PickupLocation, string pickupLocationType = "customer", double? deliveryLatitude = null, double? deliveryLongitude = null, [FromQuery] List<string>? SelectedAddons = null)
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
                    var eligibility = await _eligibilityService.CheckRentalEligibilityAsync(userId);
                    if (!eligibility.IsEligible)
                    {
                        if (eligibility.FailureCode == EligibilityFailureCode.EkycNotVerified ||
                            eligibility.FailureCode == EligibilityFailureCode.MissingIdentityDocuments)
                        {
                            TempData["ShowEkycModal"] = "true";
                            TempData["EkycReturnUrl"] = $"{Request.Path}{Request.QueryString}";
                            TempData["WarningMessage"] = eligibility.Reason ?? "Cần xác thực eKYC trước khi thuê xe.";
                        }
                        else
                        {
                            TempData["BookingError"] = eligibility.Reason ?? "Tài khoản chưa đủ điều kiện đặt xe.";
                        }
                        return RedirectToAction("Details", "Car", new { id = CarID });
                    }
                }
            }

            var car = await _carService.GetCarByIdAsync(CarID);
            if (car == null) return NotFound();

            var deliveryMode = string.Equals(pickupLocationType, "owner", StringComparison.OrdinalIgnoreCase)
                ? DeliveryModeConstants.OwnerDelivery
                : DeliveryModeConstants.CustomerPickup;
            DeliveryQuoteResult? deliveryQuote = null;

            if (deliveryMode == DeliveryModeConstants.OwnerDelivery)
            {
                var deliveryValidation = await TryBuildDeliveryQuoteAsync(car, PickupLocation, deliveryLatitude, deliveryLongitude);
                if (!deliveryValidation.IsSuccess || deliveryValidation.Quote == null)
                {
                    TempData["BookingError"] = deliveryValidation.ErrorMessage ?? "Vui lòng chọn vị trí giao xe hợp lệ trên bản đồ.";
                    return RedirectToAction("Details", "Car", new { id = CarID });
                }

                deliveryQuote = deliveryValidation.Quote;
            }

            // Phase 3: Maintenance blocking
            if (car.NextInspectionDate.HasValue && car.NextInspectionDate.Value.Date < GetVietnamToday())
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
                DepositStatus = "Holding",
                DeliveryMode = deliveryMode,
                DeliveryAddress = deliveryMode == DeliveryModeConstants.OwnerDelivery ? PickupLocation : null,
                DeliveryLatitude = deliveryQuote?.Latitude,
                DeliveryLongitude = deliveryQuote?.Longitude,
                DeliveryDistanceKm = deliveryQuote?.DistanceKm ?? 0m,
                DeliveryEtaMinutes = deliveryQuote?.EtaMinutes,
                DeliveryQuotedAt = deliveryQuote?.QuotedAtUtc,
                DeliveryFee = deliveryQuote?.Fee ?? 0m
            };

            ViewBag.PickupLocation = PickupLocation;
            ViewBag.PickupLocationType = pickupLocationType;
            ViewBag.DeliveryLatitude = deliveryQuote?.Latitude;
            ViewBag.DeliveryLongitude = deliveryQuote?.Longitude;
            ViewBag.DeliveryDistanceKm = deliveryQuote?.DistanceKm;
            ViewBag.DeliveryEtaMinutes = deliveryQuote?.EtaMinutes;
            ViewBag.DeliveryFee = deliveryQuote?.Fee ?? 0m;
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
                    DeliveryFee = deliveryQuote?.Fee ?? 0m,
                    DeliveryAddress = deliveryMode == DeliveryModeConstants.OwnerDelivery ? PickupLocation : null,
                    DeliveryMode = deliveryMode,
                    DeliveryLatitude = deliveryQuote?.Latitude,
                    DeliveryLongitude = deliveryQuote?.Longitude,
                    DeliveryDistanceKm = deliveryQuote?.DistanceKm ?? 0m,
                    DeliveryEtaMinutes = deliveryQuote?.EtaMinutes,
                    DeliveryQuotedAt = deliveryQuote?.QuotedAtUtc,
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

            // Nếu chưa có bookings (đang ở trạng thái Unpaid), lấy từ snapshot
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
        public async Task<IActionResult> Book(Booking booking, bool SelfDriveAgreement, bool LegalAgreement, int? SelectedDriverID, decimal DriverFee, List<string>? SelectedAddons)
        {
            if (!LegalAgreement)
            {
                TempData["BookingError"] = "Bạn phải đồng ý Điều khoản sử dụng và Thông báo xử lý dữ liệu cá nhân trước khi đặt xe.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            if (!SelfDriveAgreement)
            {
                TempData["BookingError"] = "Bạn phải xác nhận là người trực tiếp điều khiển xe và có giấy phép lái xe hợp lệ.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            if (SelectedDriverID.HasValue || DriverFee > 0 || booking.DriverID.HasValue || booking.DriverBookingID.HasValue)
            {
                TempData["BookingError"] = "Đơn thuê xe tự lái không được gán tài xế hoặc phí tài xế.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            // Validate input trước khi xử lý
            if (booking.EndDate <= booking.StartDate)
            {
                TempData["BookingError"] = "Ngày kết thúc phải sau ngày bắt đầu.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            if (booking.StartDate.Date < GetVietnamToday())
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
                    var eligibility = await _eligibilityService.CheckRentalEligibilityAsync(userId);
                    if (!eligibility.IsEligible)
                    {
                        if (eligibility.FailureCode == EligibilityFailureCode.EkycNotVerified ||
                            eligibility.FailureCode == EligibilityFailureCode.MissingIdentityDocuments)
                        {
                            TempData["ShowEkycModal"] = "true";
                            TempData["EkycReturnUrl"] = Url.Action("Details", "Car", new { id = booking.CarID }) ?? "/";
                            TempData["WarningMessage"] = eligibility.Reason ?? "Cần xác thực eKYC trước khi thuê xe.";
                        }
                        else
                        {
                            TempData["BookingError"] = eligibility.Reason ?? "Lỗi xác thực. Vui lòng kiểm tra lại trạng thái tài khoản.";
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
                TempData["BookingError"] = "Xe này không thuộc luồng thuê xe tự lái.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            var validLicense = await _businessRules.GetValidCustomerDrivingLicenseAsync(booking.UserID);
            if (validLicense == null)
            {
                TempData["ShowEkycModal"] = "true";
                TempData["EkycReturnUrl"] = Url.Action("Details", "Car", new { id = booking.CarID }) ?? "/";
                TempData["WarningMessage"] = "Vui lòng xác thực giấy phép lái xe hợp lệ trước khi thuê xe tự lái.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }

            booking.ServiceTypeID = await _businessRules.GetServiceTypeIdAsync(ServiceTypeConstants.SelfDrive);
            booking.CustomerDrivingLicenseID = validLicense.CustomerDrivingLicenseID;
            booking.ContractTypeCode = "SELF_DRIVE_CONTRACT";
            await _businessRules.ValidateSelfDriveAsync(booking, DriverFee);

            var deliveryRequested = string.Equals(booking.DeliveryMode, DeliveryModeConstants.OwnerDelivery, StringComparison.OrdinalIgnoreCase)
                || booking.DeliveryFee > 0
                || booking.DeliveryLatitude.HasValue
                || booking.DeliveryLongitude.HasValue;

            if (deliveryRequested)
            {
                var deliveryValidation = await TryBuildDeliveryQuoteAsync(car, booking.DeliveryAddress, booking.DeliveryLatitude, booking.DeliveryLongitude);
                if (!deliveryValidation.IsSuccess || deliveryValidation.Quote == null)
                {
                    TempData["BookingError"] = deliveryValidation.ErrorMessage ?? "Vui lòng chọn vị trí giao xe hợp lệ trên bản đồ.";
                    return RedirectToAction("Checkout", new
                    {
                        CarID = booking.CarID,
                        StartDate = booking.StartDate.ToString("yyyy-MM-ddTHH:mm"),
                        EndDate = booking.EndDate.ToString("yyyy-MM-ddTHH:mm"),
                        PickupLocation = booking.DeliveryAddress ?? car.Location?.Name,
                        pickupLocationType = "owner",
                        deliveryLatitude = booking.DeliveryLatitude,
                        deliveryLongitude = booking.DeliveryLongitude
                    });
                }

                booking.DeliveryMode = DeliveryModeConstants.OwnerDelivery;
                booking.DeliveryAddress = booking.DeliveryAddress?.Trim();
                booking.DeliveryLatitude = deliveryValidation.Quote.Latitude;
                booking.DeliveryLongitude = deliveryValidation.Quote.Longitude;
                booking.DeliveryDistanceKm = deliveryValidation.Quote.DistanceKm;
                booking.DeliveryEtaMinutes = deliveryValidation.Quote.EtaMinutes;
                booking.DeliveryQuotedAt = deliveryValidation.Quote.QuotedAtUtc;
                booking.DeliveryFee = deliveryValidation.Quote.Fee;
            }
            else
            {
                booking.DeliveryMode = DeliveryModeConstants.CustomerPickup;
                booking.DeliveryAddress = null;
                booking.DeliveryLatitude = null;
                booking.DeliveryLongitude = null;
                booking.DeliveryDistanceKm = 0m;
                booking.DeliveryEtaMinutes = null;
                booking.DeliveryQuotedAt = null;
                booking.DeliveryFee = 0m;
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
                    DeliveryMode = booking.DeliveryMode,
                    DeliveryLatitude = booking.DeliveryLatitude,
                    DeliveryLongitude = booking.DeliveryLongitude,
                    DeliveryDistanceKm = booking.DeliveryDistanceKm,
                    DeliveryEtaMinutes = booking.DeliveryEtaMinutes,
                    DeliveryQuotedAt = booking.DeliveryQuotedAt,
                    SelectedAddons = SelectedAddons,
                    PromotionID = booking.PromotionID
                });
                var termsDocument = await _legalCompliance.GetActiveDocumentAsync(LegalDocumentCodes.TermsOfUse);
                var privacyDocument = await _legalCompliance.GetActiveDocumentAsync(LegalDocumentCodes.PrivacyNotice);
                var selfDriveDocument = await _legalCompliance.GetActiveDocumentAsync(LegalDocumentCodes.SelfDriveAgreement);
                var agreementAt = DateTime.UtcNow;
                var agreementIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                var agreementUserAgent = Request.Headers.UserAgent.ToString();

                quote.TermsDocumentVersionID = termsDocument.LegalDocumentVersionID;
                quote.PrivacyNoticeVersionID = privacyDocument.LegalDocumentVersionID;
                quote.SelfDriveAgreementAcceptedAt = agreementAt;
                quote.SelfDriveAgreementIpAddress = agreementIp;
                quote.SelfDriveAgreementUserAgent = agreementUserAgent;
                booking.TermsDocumentVersionID = termsDocument.LegalDocumentVersionID;
                booking.PrivacyNoticeVersionID = privacyDocument.LegalDocumentVersionID;
                booking.SelfDriveAgreementAcceptedAt = agreementAt;
                booking.SelfDriveAgreementIpAddress = agreementIp;
                booking.SelfDriveAgreementUserAgent = agreementUserAgent;

                var bookingConsentEvidence = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Form = "Booking/Book",
                    booking.CarID,
                    booking.StartDate,
                    booking.EndDate,
                    SelfDriveAgreement,
                    LegalAgreement
                });
                await _legalCompliance.RecordConsentAsync(booking.UserID, LegalDocumentCodes.TermsOfUse, ConsentPurposeCodes.BookingTerms, LegalAgreement, "Booking", HttpContext, bookingConsentEvidence);
                await _legalCompliance.RecordConsentAsync(booking.UserID, LegalDocumentCodes.PrivacyNotice, ConsentPurposeCodes.PrivacyNoticeAcknowledgement, LegalAgreement, "Booking", HttpContext, bookingConsentEvidence);
                await _legalCompliance.RecordConsentAsync(booking.UserID, LegalDocumentCodes.SelfDriveAgreement, ConsentPurposeCodes.SelfDriveAgreement, SelfDriveAgreement, "Booking", HttpContext, bookingConsentEvidence);

                var availability = await _reservationService.CheckCarAvailabilityAsync(booking.CarID, booking.StartDate, booking.EndDate);
                if (!availability.IsAvailable)
                {
                    TempData["BookingError"] = availability.Reason ?? "Xe này đã được giữ hoặc đặt trong thời gian bạn chọn.";
                    return RedirectToAction("Details", "Car", new { id = booking.CarID });
                }

                holdResult = await _reservationService.CreateHoldAsync(booking.UserID, booking.CarID, booking.StartDate, booking.EndDate, quote);
                if (!holdResult.IsSuccess || holdResult.Hold == null)
                {
                    TempData["BookingError"] = holdResult.ErrorMessage ?? "Không thể giữ xe. Vui lòng thử lại.";
                    return RedirectToAction("Details", "Car", new { id = booking.CarID });
                }

                var domain = PaymentUrlResolver.ResolvePublicBaseUrl(HttpContext);
                var payOSResult = await _payOSPaymentService.CreateHoldPaymentAsync(holdResult.Hold, quote, domain);
                if (!payOSResult.IsSuccess || payOSResult.Transaction == null || string.IsNullOrWhiteSpace(payOSResult.CheckoutUrl))
                {
                    await _reservationService.ReleaseHoldAsync(holdResult.Hold.BookingHoldID, "PayOS create payment failed.");
                    TempData["BookingError"] = payOSResult.ErrorMessage ?? "Không thể tạo thanh toán PayOS.";
                    return RedirectToAction("Details", "Car", new { id = booking.CarID });
                }
                paymentCreated = true;

                try
                {
                    _context.AuditLogs.Add(new AuditLog
                    {
                        Action = "Tạo QR PayOS",
                        EntityType = "Thanh toán",
                        EntityId = holdResult.Hold.BookingHoldID,
                        UserId = booking.UserID,
                        UserName = User.Identity?.Name,
                        Description = $"Khách tạo phiếu giữ chỗ #{holdResult.Hold.BookingHoldID}, số tiền PayOS {quote.HoldingFee:N0} VND. Chưa tạo đơn thuê.",
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
                TempData["BookingError"] = "Đã xảy ra lỗi khi tạo thanh toán giữ chỗ. Hệ thống đã giải phóng giữ chỗ nếu chưa tạo được PayOS. Vui lòng thử lại.";
                return RedirectToAction("Details", "Car", new { id = booking.CarID });
            }
        }

        // NOTE: Đoạn "atomic booking creation" cũ (BeginTransactionAsync Serializable + price calc inline + promo update inline)
        // đã được thay bằng luồng hold + PayOS confirmation. Xem:
        //   - ReservationService.CreateHoldAsync
        //   - PayOSPaymentService.ConvertPaidHoldToBookingAsync
        //   - BookingQuoteService (single-source pricing)
        // Lịch sử đầy đủ ở git history. (TASK-B06: dead-code đã loại bỏ trong Pass 1 audit.)

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
                TempData["Error"] = "Không tìm thấy đơn đặt xe.";
                return RedirectToAction(nameof(MyBookings));
            }

            var cancellableStatuses = new[] { BookingStatusConstants.Pending, BookingStatusConstants.Confirmed, BookingStatusConstants.Active };
            if (!cancellableStatuses.Contains(booking.BookingStatus?.Name))
            {
                TempData["Error"] = "Không thể hủy đơn ở trạng thái hiện tại.";
                return RedirectToAction(nameof(MyBookings));
            }

            var result = await _bookingTransitionService.CancelAsync(
                id,
                userId,
                User.IsInRole("Admin") ? "Admin" : "Customer",
                string.IsNullOrWhiteSpace(reason) ? "Customer cancelled booking." : reason.Trim());

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage ?? "Không thể hủy đơn ở trạng thái hiện tại.";
                return RedirectToAction(nameof(MyBookings));
            }

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
                .Include(b => b.Car)
                .Include(b => b.RentalContract)
                .FirstOrDefaultAsync(b => b.BookingID == id && b.UserID == userId);

            if (booking == null || booking.BookingStatus?.Name != BookingStatusConstants.Confirmed)
            {
                TempData["Error"] = "Đơn đặt xe không hợp lệ để nhận xe.";
                return RedirectToAction(nameof(MyBookings));
            }

            var checkInRecord = await _context.HandoverRecords
                .Where(h => h.BookingID == id && h.Type == "CheckIn" && !h.IsDeleted)
                .OrderByDescending(h => h.RecordDate)
                .FirstOrDefaultAsync();

            if (checkInRecord == null)
            {
                TempData["Error"] = "Bạn phải tạo biên bản nhận xe trước khi xác nhận nhận xe.";
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
                TempData["Error"] = transition.ErrorMessage ?? "Không thể xác nhận nhận xe.";
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

            TempData["Success"] = "Bạn đã xác nhận nhận xe thành công.";
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

            // 2. Update status through the transition service so booking/car/deposit changes are audited.
            var completeResult = await _bookingTransitionService.TransitionAsync(id, BookingStatusConstants.Completed, userId, "Customer", "Customer confirmed vehicle return.");
            if (!completeResult.IsSuccess)
            {
                TempData["Error"] = completeResult.ErrorMessage;
                return RedirectToAction(nameof(MyBookings));
            }

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
                    var rawLateFee = lateHours * booking.Car.OverTimeFee;
                    var oneDayCap = booking.Car.PricePerDay > 0 ? booking.Car.PricePerDay : rawLateFee;
                    booking.LateFeeAmount = Math.Min(rawLateFee, oneDayCap);
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
            // Car status is reset to Available inside IBookingTransitionService.Completed.

            // ═══════════════════════════════════════════════════
            // 💰 DEPOSIT REFUND LOGIC — Giữ 15 ngày rồi hoàn trả
            // ═══════════════════════════════════════════════════
            if (!hasViolation && booking.LateFeeAmount == 0)
            {
                // Không vi phạm → lên lịch hoàn cọc sau 15 ngày (chờ kiểm tra phạt nguồi)
                await _paymentTransitionService.SetBookingDepositStatusAsync(id, DepositStatusConstants.PendingRefund, userId, "Customer", "Clean return pending deposit refund.");
                msg += " Tiền cọc sẽ được hoàn trả sau 15 ngày kiểm tra phạt nguội.";
            }
            else
            {
                // Có vi phạm → Admin sẽ xử lý cọc thủ công
                await _paymentTransitionService.SetBookingDepositStatusAsync(id, DepositStatusConstants.UnderReview, userId, "Customer", "Return requires settlement review.");
                await _settlementReviewService.CreateAsync(
                    id,
                    booking.LateFeeAmount > 0 ? SettlementReviewTypeConstants.Late : SettlementReviewTypeConstants.Other,
                    booking.LateFeeAmount,
                    "Return generated settlement review.",
                    userId,
                    checkOutRecord.HandoverID);
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

        public IActionResult Contract(int id)
        {
            if (!IsInternalUser()) return Forbid();

            TempData["Info"] = "Hệ thống đã bỏ hợp đồng điện tử. Vui lòng upload hợp đồng giấy đã ký vào mục Hợp đồng vật lý.";
            return RedirectToAction("Index", "PhysicalContracts", new { area = "Admin", bookingId = id });
        }

        public IActionResult TransactionContract(int id)
        {
            if (!IsInternalUser()) return Forbid();

            TempData["Info"] = "Hệ thống đã bỏ hợp đồng điện tử. Vui lòng upload hợp đồng giấy đã ký vào mục Hợp đồng vật lý.";
            return RedirectToAction("Index", "PhysicalContracts", new { area = "Admin" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SignContract(int id, string customerName, string customerIdentityCard, string customerPhone, string customerAddress, string customerCompanyName, string customerDepartmentCode, string paymentMethod, string customerBankAccount)
        {
            if (!IsInternalUser()) return Forbid();

            TempData["Info"] = "Hệ thống đã bỏ ký hợp đồng điện tử. Vui lòng upload ảnh/PDF hợp đồng giấy đã ký.";
            return RedirectToAction("Index", "PhysicalContracts", new { area = "Admin" });
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage(int page = 1)
        {
            var bookings = await _bookingService.GetAllBookingsPageAsync(page, 50);
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
                    Amount = booking.ReservationFee > 0
                        ? booking.ReservationFee
                        : Math.Round(booking.TotalPrice * 0.2m, 0, MidpointRounding.AwayFromZero),
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

            // Nếu chưa có Bookings (từ CheckoutAll mới), tạo bookings từ PendingCartSnapshot
            if (!transaction.Bookings.Any() && !string.IsNullOrEmpty(transaction.PendingCartSnapshot))
            {
                var user = await _context.Users.Include(u => u.MembershipTier).FirstOrDefaultAsync(u => u.UserID == userId);

                var pendingStatus = await _context.BookingStatuses.FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Pending);
                if (pendingStatus == null)
                {
                    TempData["Error"] = "Thiếu lookup BookingStatus Pending. Vui lòng chạy seed dữ liệu tham chiếu.";
                    return RedirectToAction("Index", "Cart");
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
                            throw new Exception($"Xe ID {carId} không tồn tại.");
                        if (car.CarStatus?.Name != "Available")
                            throw new Exception($"Xe '{car.CarName}' không còn sẵn.");

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
                        throw new Exception("Khách thuê xe tự lái phải cung cấp giấy phép lái xe hợp lệ.");

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
                        CustomerIdentityCard = _piiEncryption.DecryptIfNotEmpty(user?.CitizenId) ?? "",
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
- Tổng tiền thuê: {baseTotal:N0} VNĐ.
- Tổng cộng: {transaction.TotalAmount:N0} VNĐ.
- Tiền cọc: {totalDeposit:N0} VNĐ.

ĐIỀU 3: TRÁCH NHIỆM CHUNG
- Không sử dụng xe thuê vào mục đích vi phạm pháp luật.
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
                            ContractTypeID = item.TryGetProperty("contractTypeId", out var ctid) && ctid.ValueKind != System.Text.Json.JsonValueKind.Null ? ctid.GetInt32() : null,
                            TermsDocumentVersionID = item.TryGetProperty("termsDocumentVersionId", out var tdv) && tdv.ValueKind != System.Text.Json.JsonValueKind.Null ? tdv.GetInt32() : null,
                            PrivacyNoticeVersionID = item.TryGetProperty("privacyNoticeVersionId", out var pdv) && pdv.ValueKind != System.Text.Json.JsonValueKind.Null ? pdv.GetInt32() : null,
                            SelfDriveAgreementAcceptedAt = item.TryGetProperty("selfDriveAgreementAcceptedAt", out var sda) && sda.ValueKind != System.Text.Json.JsonValueKind.Null ? sda.GetDateTime() : null,
                            SelfDriveAgreementIpAddress = item.TryGetProperty("selfDriveAgreementIpAddress", out var sdip) && sdip.ValueKind != System.Text.Json.JsonValueKind.Null ? sdip.GetString() : null,
                            SelfDriveAgreementUserAgent = item.TryGetProperty("selfDriveAgreementUserAgent", out var sdua) && sdua.ValueKind != System.Text.Json.JsonValueKind.Null ? sdua.GetString() : null
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
                    TempData["Error"] = "Lỗi khi tạo đơn: " + ex.Message;
                    return RedirectToAction("Index", "Cart");
                }
            }

            // Cập nhật transaction
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
                .FirstOrDefaultAsync(b => b.BookingID == id);

            if (booking == null) return NotFound();
            if (booking.UserID != userId && !IsInternalUser()) return Forbid();
            return View(booking);
        }

        private bool IsInternalUser()
        {
            return User.IsInRole(UserRoleConstants.Admin) ||
                   User.IsInRole(UserRoleConstants.Manager) ||
                   User.IsInRole(UserRoleConstants.Staff);
        }

        private async Task<DeliveryQuoteBuildResult> TryBuildDeliveryQuoteAsync(Car car, string? deliveryAddress, double? deliveryLatitude, double? deliveryLongitude)
        {
            if (!car.DeliveryEnabled)
                return DeliveryQuoteBuildResult.Fail("Xe này chưa bật dịch vụ giao tận nơi. Vui lòng chọn nhận xe tại vị trí xe.");

            if (string.IsNullOrWhiteSpace(deliveryAddress))
                return DeliveryQuoteBuildResult.Fail("Vui lòng nhập địa chỉ giao xe khi chọn dịch vụ giao tận nơi.");

            if (!deliveryLatitude.HasValue || !deliveryLongitude.HasValue ||
                deliveryLatitude < -90 || deliveryLatitude > 90 ||
                deliveryLongitude < -180 || deliveryLongitude > 180)
            {
                return DeliveryQuoteBuildResult.Fail("Vui lòng chọn vị trí giao xe trên bản đồ.");
            }

            var carLat = car.Latitude ?? car.Location?.Latitude ?? 10.7769;
            var carLng = car.Longitude ?? car.Location?.Longitude ?? 106.7009;
            var scope = _deliveryScopeService.CheckScope(car.CarAddress ?? car.Location?.Name, carLat, carLng, deliveryLatitude.Value, deliveryLongitude.Value);
            if (!scope.IsWithinScope)
                return DeliveryQuoteBuildResult.Fail(scope.MessageVi);

            var deliveryService = HttpContext.RequestServices.GetService<IDeliveryService>();
            if (deliveryService == null)
                return DeliveryQuoteBuildResult.Fail("Chưa cấu hình dịch vụ tính phí giao xe.");

            var info = await deliveryService.CalculateDeliveryAsync(carLat, carLng, deliveryLatitude.Value, deliveryLongitude.Value);
            if (car.MaxDeliveryDistanceKm > 0 && (decimal)info.DistanceKm > car.MaxDeliveryDistanceKm)
            {
                return DeliveryQuoteBuildResult.Fail($"Vị trí giao xe cách {info.DistanceKm:F1} km, vượt quá phạm vi {car.MaxDeliveryDistanceKm:N0} km của xe này.");
            }

            var roundedDistance = Math.Round((decimal)info.DistanceKm, 2, MidpointRounding.AwayFromZero);
            var billableKm = Math.Max(0m, Math.Ceiling(roundedDistance - car.FreeDeliveryRadiusKm));
            var fee = billableKm * car.DeliveryFeePerKm;
            var etaMinutes = (int)Math.Ceiling(info.DurationMinutes + 60);

            return DeliveryQuoteBuildResult.Ok(new DeliveryQuoteResult(
                deliveryLatitude.Value,
                deliveryLongitude.Value,
                roundedDistance,
                etaMinutes,
                fee,
                DateTime.UtcNow));
        }

        private sealed record DeliveryQuoteResult(
            double Latitude,
            double Longitude,
            decimal DistanceKm,
            int EtaMinutes,
            decimal Fee,
            DateTime QuotedAtUtc);

        private sealed record DeliveryQuoteBuildResult(bool IsSuccess, DeliveryQuoteResult? Quote, string? ErrorMessage)
        {
            public static DeliveryQuoteBuildResult Ok(DeliveryQuoteResult quote) => new(true, quote, null);
            public static DeliveryQuoteBuildResult Fail(string message) => new(false, null, message);
        }

        private static DateTime GetVietnamToday()
        {
            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
        }
    }
}
