using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace CarRentalSystem.Controllers
{
    /// <summary>
    /// Controller xử lý thuê dài hạn - Chuẩn Enterprise
    /// </summary>
    public class LongTermController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPiiEncryptionService _piiEncryption;
        private readonly ILongTermRentalService _rentalService;
        private readonly IReservationService _reservationService;
        private readonly IBookingQuoteService _quoteService;
        private readonly IPayOSPaymentService _payOSPaymentService;
        private readonly IEligibilityService _eligibilityService;
        private readonly ILogger<LongTermController> _logger;

        public LongTermController(
            ApplicationDbContext context,
            IPiiEncryptionService piiEncryption,
            ILongTermRentalService rentalService,
            IReservationService reservationService,
            IBookingQuoteService quoteService,
            IPayOSPaymentService payOSPaymentService,
            IEligibilityService eligibilityService,
            ILogger<LongTermController> logger)
        {
            _context = context;
            _piiEncryption = piiEncryption;
            _rentalService = rentalService;
            _reservationService = reservationService;
            _quoteService = quoteService;
            _payOSPaymentService = payOSPaymentService;
            _eligibilityService = eligibilityService;
            _logger = logger;
        }

        /// <summary>
        /// Trang chính - Hiển thị danh sách gói thuê dài hạn
        /// </summary>
        public async Task<IActionResult> Index()
        {
            // Đảm bảo các gói thuê dài hạn tồn tại trong database
            await EnsureLongTermContractsExistAsync();
            await RepairMojibakeContractTypesAsync();

            // Lấy danh sách gói thuê dài hạn (tối thiểu 7 ngày)
            var contracts = await _context.ContractTypes
                .Where(c => c.IsActive && c.MinDurationDays >= 1)
                .OrderBy(c => c.MinDurationDays)
                .ToListAsync();

            ViewBag.ContractTypes = contracts;
            return View();
        }

        /// <summary>
        /// Form nhập nhu cầu thuê dài hạn
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RequestForm(int? contractId)
        {
            var contracts = await _context.ContractTypes
                .Where(c => c.IsActive && c.MinDurationDays >= 1)
                .OrderBy(c => c.MinDurationDays)
                .ToListAsync();

            ViewBag.ContractTypes = contracts;
            ViewBag.Locations = await _context.Locations.ToListAsync();
            
            // Don't load drivers here - they will be loaded dynamically based on selected car's location

            // Lấy region từ cookie
            var regionCookie = Request.Cookies["RegionID"];
            if (!string.IsNullOrEmpty(regionCookie) && int.TryParse(regionCookie, out int regionId))
            {
                ViewBag.CurrentRegionID = regionId;
            }

            var selectedContract = contracts.FirstOrDefault(c => c.ContractTypeID == contractId);
            ViewBag.SelectedContract = selectedContract;

            return View();
        }

        /// <summary>
        /// Xử lý submit yêu cầu thuê - Auto-matching xe
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRequest(
            string FullName,
            string Phone,
            string? Note,
            int ContractTypeID,
            string Location,
            string? Brand,
            string? ModelStr,
            string? RentDuration,
            DateTime StartDate,
            string? SelectedDates,
            int? CarID,
            int? SelectedDriverID)
        {
            if (SelectedDriverID.HasValue)
            {
                TempData["Error"] = "Thuê xe tự lái không được gán tài xế.";
                return RedirectToAction("RequestForm", new { contractId = ContractTypeID });
            }

            _logger.LogInformation("SubmitRequest started for ContractTypeID: {ContractTypeID}, CarID: {CarID}", ContractTypeID, CarID);

            // Validate cơ bản
            if (StartDate < DateTime.UtcNow.Date)
            {
                TempData["Error"] = "Ngày bắt đầu thuê phải từ hôm nay trở đi.";
                return RedirectToAction("RequestForm", new { contractId = ContractTypeID });
            }

            // Lấy thông tin gói thuê
            var contractType = await _context.ContractTypes.FindAsync(ContractTypeID);
            if (contractType == null || !contractType.IsActive)
            {
                TempData["Error"] = "Gói thuê không hợp lệ hoặc đã bị vô hiệu hóa.";
                return RedirectToAction("Index");
            }

            // Tính ngày kết thúc dựa trên duration
            var (endDate, totalDays) = CalculateEndDate(StartDate, RentDuration, contractType, SelectedDates);

            // Validate tổng số ngày
            if (totalDays < contractType.MinDurationDays)
            {
                TempData["Error"] = $"Thời gian thuê tối thiểu cho gói {contractType.Name} là {contractType.MinDurationDays} ngày.";
                return RedirectToAction("RequestForm", new { contractId = ContractTypeID });
            }

            // Tìm location ID
            var location = await _context.Locations
                .FirstOrDefaultAsync(l => l.Name == Location);
            var locationId = location?.LocationID ?? 1;

            // Tìm xe - Ưu tiên xe đã chọn từ form, nếu không thì tìm xe tự động
            Car? selectedCar = null;

            if (CarID.HasValue && CarID.Value > 0)
            {
                // Xe đã được chọn từ danh sách
                selectedCar = await _context.Cars
                    .Include(c => c.CarBrand)
                    .Include(c => c.CarStatus)
                    .FirstOrDefaultAsync(c => c.CarID == CarID.Value && c.CarStatus != null && c.CarStatus.Name == "Available");

                if (selectedCar == null)
                {
                    TempData["Error"] = "Xe bạn chọn không còn có sẵn. Vui lòng chọn xe khác.";
                    return RedirectToAction("RequestForm", new { contractId = ContractTypeID });
                }

                // Kiểm tra booking trùng lịch
                var confirmedStatuses = await _context.BookingStatuses
                    .Where(s => s.Name == "Confirmed" || s.Name == "Active" || s.Name == "CheckedOut")
                    .Select(s => s.BookingStatusID)
                    .ToListAsync();

                var hasConflict = await _context.Bookings
                    .AnyAsync(b => b.CarID == selectedCar.CarID
                        && confirmedStatuses.Contains(b.BookingStatusID)
                        && b.StartDate < endDate
                        && b.EndDate > StartDate);

                if (hasConflict)
                {
                    TempData["Error"] = $"Xe {selectedCar.LicensePlate} đã được đặt trong khoảng thời gian bạn chọn. Vui lòng chọn xe khác hoặc đổi ngày.";
                    return RedirectToAction("RequestForm", new { contractId = ContractTypeID });
                }
            }
            else
            {
                // Không có xe chọn sẵn - Tìm xe tự động
                var availableResult = await _rentalService.FindAvailableCarAsync(
                    preferredCarId: null,
                    brand: Brand,
                    model: ModelStr,
                    locationId: locationId,
                    startDate: StartDate,
                    endDate: endDate);

                if (availableResult.SelectedCar == null)
                {
                    var message = availableResult.TotalAvailable == 0
                        ? "Hiện không có xe nào trong hệ thống phù hợp với yêu cầu của bạn."
                        : $"Có {availableResult.TotalAvailable} xe phù hợp nhưng không có xe trống trong khoảng thời gian bạn chọn. Vui lòng thử chọn ngày khác.";

                    TempData["Error"] = message;
                    _logger.LogWarning("No available car found. TotalAvailable: {TotalAvailable}", availableResult.TotalAvailable);
                    return RedirectToAction("RequestForm", new { contractId = ContractTypeID });
                }

                selectedCar = availableResult.SelectedCar;
            }

            _logger.LogInformation(
                "Found car: {CarName} (ID: {CarID}, Plate: {Plate}) for rental from {StartDate} to {EndDate}",
                selectedCar.CarName,
                selectedCar.CarID,
                selectedCar.LicensePlate,
                StartDate,
                endDate);

            // Chuyển đến trang xem trước hợp đồng
            return RedirectToAction("ContractPreview", new
            {
                carId = selectedCar.CarID,
                contractId = ContractTypeID,
                duration = RentDuration,
                customerName = FullName,
                customerPhone = Phone,
                startDateStr = StartDate.ToString("yyyy-MM-ddTHH:mm"),
                selectedDates = SelectedDates ?? ""
            });
        }

        /// <summary>
        /// Trang xem trước hợp đồng trước khi xác nhận
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ContractPreview(
            int carId,
            int contractId,
            string? duration,
            string? customerName,
            string? customerPhone,
            string startDateStr,
            string? selectedDates,
            int? selectedDriverId)
        {
            // Lấy thông tin user
            selectedDriverId = null;

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int currentUserId))
            {
                return Unauthorized();
            }

            var user = await _context.Users
                .Include(u => u.MembershipTier)
                .FirstOrDefaultAsync(u => u.UserID == currentUserId);

            if (user == null)
            {
                return NotFound("Không tìm thấy thông tin người dùng.");
            }
            
            if (selectedDriverId.HasValue)
            {
                ViewBag.SelectedDriver = await _context.Drivers.FirstOrDefaultAsync(d => d.DriverID == selectedDriverId.Value);
            }

            // Lấy thông tin xe
            var car = await _context.Cars
                .Include(c => c.CarBrand)
                .Include(c => c.Location)
                .FirstOrDefaultAsync(c => c.CarID == carId);

            if (car == null)
            {
                return NotFound("Không tìm thấy thông tin xe.");
            }

            // Lấy thông tin gói thuê
            var contract = await _context.ContractTypes.FindAsync(contractId);
            if (contract == null)
            {
                return NotFound("Không tìm thấy gói thuê.");
            }

            // Parse ngày bắt đầu
            if (!DateTime.TryParse(startDateStr, out var startDate))
            {
                startDate = DateTime.UtcNow;
            }

            // Tính ngày kết thúc và số ngày
            var (endDate, totalDays) = CalculateEndDate(startDate, duration, contract, selectedDates);

            // Tính giá
            var pricing = await _rentalService.CalculateRentalPriceAsync(
                car, contract, user, startDate, endDate,
                CalculateTermMonths(startDate, endDate));

            // Set ViewBag
            ViewBag.Car = car;
            ViewBag.Contract = contract;
            ViewBag.User = user;
            ViewBag.Duration = duration;
            ViewBag.CustomerName = customerName ?? user.Name;
            ViewBag.CustomerPhone = customerPhone ?? user.Phone;
            ViewBag.MembershipTier = user.MembershipTier;
            ViewBag.StartDateStr = startDateStr;
            ViewBag.SelectedDates = selectedDates;
            ViewBag.Pricing = pricing;
            ViewBag.EndDate = endDate;
            ViewBag.TotalDays = totalDays;

            return View();
        }

        /// <summary>
        /// Trang kiểm tra xe trước khi nhận
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> VehicleInspection(
            int carId,
            int contractId,
            string? duration,
            string? customerName,
            string? customerPhone,
            string startDateStr,
            string? selectedDates,
            int? selectedDriverId)
        {
            var car = await _context.Cars
                .Include(c => c.CarBrand)
                .Include(c => c.Location)
                .FirstOrDefaultAsync(c => c.CarID == carId);

            var contract = await _context.ContractTypes.FindAsync(contractId);

            if (car == null || contract == null)
            {
                return NotFound();
            }

            ViewBag.Car = car;
            ViewBag.Contract = contract;
            ViewBag.Duration = duration;
            ViewBag.CustomerName = customerName;
            ViewBag.CustomerPhone = customerPhone;
            ViewBag.StartDateStr = startDateStr;
            ViewBag.SelectedDates = selectedDates;
            ViewBag.SelectedDriverId = null;

            return View();
        }

        /// <summary>
        /// Xác nhận đặt cọc và tạo hợp đồng
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmAndDeposit(
            int carId,
            int contractId,
            string? duration,
            string? customerName,
            string? customerPhone,
            string startDateStr,
            string? selectedDates,
            int? selectedDriverId)
        {
            // Lấy user ID
            if (selectedDriverId.HasValue)
            {
                TempData["Error"] = "Thuê xe tự lái không được gán tài xế.";
                return RedirectToAction("Index");
            }

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                return Unauthorized();
            }

            // Parse ngày bắt đầu
            if (!DateTime.TryParse(startDateStr, out var startDate))
            {
                TempData["Error"] = "Ngày bắt đầu không hợp lệ.";
                return RedirectToAction("Index");
            }

            // Lấy thông tin cần thiết
            var contractType = await _context.ContractTypes.FindAsync(contractId);
            if (contractType == null)
            {
                TempData["Error"] = "Gói thuê không hợp lệ.";
                return RedirectToAction("Index");
            }

            // Tính ngày kết thúc
            var (endDate, totalDays) = CalculateEndDate(startDate, duration, contractType, selectedDates);

            // Validate lại
            var validation = await _rentalService.ValidateRentalRequestAsync(carId, contractId, startDate, CalculateTermMonths(startDate, endDate));
            if (!validation.IsValid)
            {
                TempData["Error"] = string.Join("; ", validation.Errors);
                return RedirectToAction("Index");
            }

            // Tạo long-term rental với service mới
            var eligibility = await _eligibilityService.CheckRentalEligibilityAsync(userId);
            if (!eligibility.IsEligible)
            {
                if (eligibility.FailureCode == EligibilityFailureCode.EkycNotVerified ||
                    eligibility.FailureCode == EligibilityFailureCode.MissingIdentityDocuments)
                {
                    var returnRoute = new
                    {
                        carId,
                        contractId,
                        duration,
                        customerName,
                        customerPhone,
                        startDateStr,
                        selectedDates
                    };

                    TempData["ShowEkycModal"] = "true";
                    TempData["EkycReturnUrl"] = Url.Action("VehicleInspection", "LongTerm", returnRoute) ?? "/LongTerm";
                    TempData["WarningMessage"] = eligibility.Reason ?? "Cần xác thực eKYC trước khi thuê xe dài hạn.";
                    return RedirectToAction("VehicleInspection", returnRoute);
                }

                TempData["Error"] = eligibility.Reason ?? "Tài khoản chưa đủ điều kiện thuê xe dài hạn.";
                return RedirectToAction("Index");
            }

            var availability = await _reservationService.CheckCarAvailabilityAsync(carId, startDate, endDate);
            if (!availability.IsAvailable)
            {
                TempData["Error"] = availability.Reason ?? "Xe không còn khả dụng trong thời gian thuê dài hạn.";
                return RedirectToAction("Index");
            }

            BookingHoldResult? holdResult = null;
            try
            {
                var quote = await _quoteService.BuildQuoteAsync(new BookingQuoteRequest
                {
                    UserID = userId,
                    CarID = carId,
                    StartDate = startDate,
                    EndDate = endDate,
                    InsuranceTier = "Basic",
                    SelectedDriverID = null,
                    ContractTypeID = contractId,
                    IsLongTerm = true,
                    LongTermMonths = CalculateTermMonths(startDate, endDate)
                });
                quote.CustomerName = customerName ?? quote.CustomerName;
                quote.CustomerPhone = customerPhone ?? quote.CustomerPhone;

                holdResult = await _reservationService.CreateHoldAsync(userId, carId, startDate, endDate, quote);
                if (!holdResult.IsSuccess || holdResult.Hold == null)
                {
                    TempData["Error"] = holdResult.ErrorMessage ?? "Không thể giữ xe dài hạn. Vui lòng thử lại.";
                    return RedirectToAction("Index");
                }

                var domain = PaymentUrlResolver.ResolvePublicBaseUrl(HttpContext);
                var payOSResult = await _payOSPaymentService.CreateHoldPaymentAsync(holdResult.Hold, quote, domain);
                if (!payOSResult.IsSuccess || payOSResult.Transaction == null || string.IsNullOrWhiteSpace(payOSResult.CheckoutUrl))
                {
                    await _reservationService.ReleaseHoldAsync(holdResult.Hold.BookingHoldID, "Long-term PayOS create payment failed.");
                    TempData["Error"] = payOSResult.ErrorMessage ?? "Không thể tạo thanh toán giữ chỗ thuê dài hạn.";
                    return RedirectToAction("Index");
                }

                _logger.LogInformation(
                    "Long-term hold created successfully. HoldID: {HoldId}, OrderCode: {OrderCode}",
                    holdResult.Hold.BookingHoldID,
                    payOSResult.Transaction.OrderCode);

                TempData["Success"] = "Xe đã được giữ cho thuê dài hạn. Vui lòng thanh toán giữ chỗ/cọc để hoàn tất.";
                return RedirectToAction("Hold", "Payment", new { orderCode = payOSResult.Transaction.OrderCode });
            }
            catch (Exception ex)
            {
                if (holdResult?.Hold != null)
                    await _reservationService.ReleaseHoldAsync(holdResult.Hold.BookingHoldID, "Exception while creating long-term payment.");

                _logger.LogError(ex, "Failed to create long-term hold payment for car {CarId}.", carId);
                TempData["Error"] = "Đã xảy ra lỗi khi tạo thanh toán giữ chỗ thuê dài hạn. Hệ thống đã giải phóng giữ chỗ, vui lòng thử lại.";
                return RedirectToAction("Index");
            }
            /*
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage ?? "Đã xảy ra lỗi khi tạo đơn thuê. Vui lòng thử lại.";
                _logger.LogError("Failed to create long-term rental: {Error}", result.ErrorMessage);
                return RedirectToAction("Index");
            }

            if (selectedDriverId.HasValue && selectedDriverId.Value > 0)
            {
                var driver = await _context.Drivers.FindAsync(selectedDriverId.Value);
                if (driver != null)
                {
                    var driverFee = driver.DailyRate * totalDays;
                    var driverBooking = new DriverBooking
                    {
                        BookingID = result.BookingID,
                        DriverID = driver.DriverID,
                        DriverFee = driverFee,
                        Status = "Pending"
                    };
                    _context.DriverBookings.Add(driverBooking);
                    
                    var bUpdate = await _context.Bookings.FindAsync(result.BookingID);
                    if (bUpdate != null)
                    {
                        bUpdate.TotalPrice += driverFee;
                        // For long-term, we don't necessarily set DriverBookingID natively on booking, but adding the row is enough.
                    }
                    await _context.SaveChangesAsync();
                }
            }

            _logger.LogInformation(
                "Long-term rental created successfully. BookingID: {BookingID}, ContractNo: {ContractNo}",
                result.BookingID,
                result.ContractNo);

            TempData["Success"] = "Đơn đặt của bạn đã được ghi nhận. Vui lòng thanh toán tiền cọc để hoàn tất!";
            TempData["ContractNo"] = result.ContractNo;

            return RedirectToAction("Deposit", "Booking", new { id = result.BookingID, area = "" });
            */
        }

        /// <summary>
        /// API: Lấy danh sách xe có sẵn cho một khoảng thời gian
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAvailableCars(DateTime startDate, DateTime endDate, int? locationId, string? brand, int? contractTypeId)
        {
            if (startDate >= endDate)
            {
                return Json(new { success = false, message = "Ngày kết thúc phải sau ngày bắt đầu" });
            }

            // Lấy thông tin gói thuê nếu có
            ContractType? contractType = null;
            if (contractTypeId.HasValue)
            {
                contractType = await _context.ContractTypes.FindAsync(contractTypeId.Value);
            }

            // Query xe từ database
            var query = _context.Cars
                .Include(c => c.CarBrand)
                .Include(c => c.Location)
                .Include(c => c.CarStatus)
                .Where(c => c.CarStatus != null && c.CarStatus.Name == "Available")
                .AsQueryable();

            // Lọc theo location nếu có
            if (locationId.HasValue && locationId.Value > 0)
            {
                query = query.Where(c => c.LocationID == locationId.Value);
            }

            // Lọc theo brand nếu có
            if (!string.IsNullOrEmpty(brand) && brand != "Linh hoạt")
            {
                query = query.Where(c => c.CarBrand != null &&
                    c.CarBrand.Name.ToLower().Contains(brand.ToLower()));
            }

            // Kiểm tra availability theo thời gian
            var confirmedStatuses = await _context.BookingStatuses
                .Where(s => s.Name == "Confirmed" || s.Name == "Active" || s.Name == "CheckedOut")
                .Select(s => s.BookingStatusID)
                .ToListAsync();

            var cars = await query.ToListAsync();

            // Lọc xe không bị booking trùng lịch
            var availableCars = new List<Car>();
            foreach (var car in cars)
            {
                var hasConflict = await _context.Bookings
                    .AnyAsync(b => b.CarID == car.CarID
                        && confirmedStatuses.Contains(b.BookingStatusID)
                        && b.StartDate < endDate
                        && b.EndDate > startDate);

                if (!hasConflict)
                {
                    availableCars.Add(car);
                }
            }

            var result = availableCars.Select(c => new
            {
                carId = c.CarID,
                carName = c.CarName,
                brandName = c.CarBrand?.Name,
                brandId = c.BrandID,
                licensePlate = c.LicensePlate,
                pricePerDay = c.PricePerDay,
                pricePerMonth = c.PricePerDay * 30,
                imageUrl = c.ImageURL,
                location = c.Location?.Name,
                seats = c.Seats,
                transmission = c.Transmission,
                fuelType = c.FuelType,
                features = c.Features
            });

            return Json(new
            {
                success = true,
                total = availableCars.Count,
                contractType = contractType != null ? new
                {
                    name = contractType.Name,
                    discountPercent = contractType.DiscountPercent,
                    minDays = contractType.MinDurationDays,
                    maxDays = contractType.MaxDurationDays
                } : null,
                cars = result
            });
        }

        /// <summary>
        /// API: Lấy danh sách xe cho dropdown/browse (không cần ngày)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCarsForSelection(int? locationId, string? brand, int? minSeats, string? carType, int? maxMonthlyPrice)
        {
            var query = _context.Cars
                .Include(c => c.CarBrand)
                .Include(c => c.Location)
                .Include(c => c.CarStatus)
                .Include(c => c.CarType)
                .Where(c => c.CarStatus != null && c.CarStatus.Name == "Available")
                .AsQueryable();

            // Lọc theo location
            if (locationId.HasValue && locationId.Value > 0)
            {
                query = query.Where(c => c.LocationID == locationId.Value);
            }

            // Lọc theo brand
            if (!string.IsNullOrEmpty(brand) && brand != "Linh hoạt" && brand != "Tất cả")
            {
                query = query.Where(c => c.CarBrand != null &&
                    c.CarBrand.Name.ToLower().Contains(brand.ToLower()));
            }

            // Lọc theo số ghế
            if (minSeats.HasValue && minSeats.Value > 0)
            {
                query = query.Where(c => c.Seats >= minSeats.Value);
            }

            // Lọc theo car type
            if (!string.IsNullOrEmpty(carType) && carType != "Tất cả")
            {
                query = query.Where(c => c.CarType != null &&
                    c.CarType.Name.ToLower().Contains(carType.ToLower()));
            }

            // Lọc theo ngân sách tối đa
            if (maxMonthlyPrice.HasValue && maxMonthlyPrice.Value > 0)
            {
                query = query.Where(c => c.PricePerDay * 30 <= maxMonthlyPrice.Value);
            }

            var cars = await query
                .OrderBy(c => c.PricePerDay)
                .ToListAsync();

            var result = cars.Select(c => new
            {
                carId = c.CarID,
                carName = c.CarName,
                brandName = c.CarBrand?.Name,
                brandId = c.BrandID,
                licensePlate = c.LicensePlate,
                pricePerDay = c.PricePerDay,
                pricePerMonth = c.PricePerDay * 30,
                imageUrl = c.ImageURL,
                location = c.Location?.Name,
                locationId = c.LocationID,
                seats = c.Seats,
                transmission = c.Transmission,
                fuelType = c.FuelType,
                features = c.Features,
                carType = c.CarType?.Name
            });

            return Json(new
            {
                success = true,
                total = cars.Count,
                cars = result
            });
        }

        /// <summary>
        /// API: Tính giá thuê dài hạn
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CalculatePrice(int carId, int contractTypeId, DateTime startDate, int termMonths)
        {
            var car = await _context.Cars
                .Include(c => c.CarBrand)
                .FirstOrDefaultAsync(c => c.CarID == carId);

            var contractType = await _context.ContractTypes.FindAsync(contractTypeId);

            if (car == null || contractType == null)
            {
                return Json(new { success = false, message = "Thông tin xe hoặc gói thuê không hợp lệ" });
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            User? user = null;
            if (int.TryParse(userIdStr, out int userId))
            {
                user = await _context.Users
                    .Include(u => u.MembershipTier)
                    .FirstOrDefaultAsync(u => u.UserID == userId);
            }

            var endDate = startDate.AddMonths(termMonths);
            var pricing = await _rentalService.CalculateRentalPriceAsync(
                car, contractType, user, startDate, endDate, termMonths);

            return Json(new
            {
                success = true,
                pricing = new
                {
                    totalDays = pricing.TotalDays,
                    basePrice = pricing.BasePrice,
                    contractDiscount = pricing.ContractDiscount,
                    membershipDiscount = pricing.MembershipDiscount,
                    finalPrice = pricing.FinalPrice,
                    depositAmount = pricing.DepositAmount,
                    monthlyRate = pricing.MonthlyRate
                }
            });
        }

        #region Private Helper Methods

        /// <summary>
        /// Đảm bảo các gói thuê dài hạn tồn tại trong database
        /// </summary>
        private async Task EnsureLongTermContractsExistAsync()
        {
            var hasLongTerm = await _context.ContractTypes
                .AnyAsync(c => c.IsActive && c.MinDurationDays >= 1);

            if (!hasLongTerm)
            {
                _logger.LogInformation("Creating default long-term contract packages");

                var contracts = new List<ContractType>
                {
                    new()
                    {
                        Name = "Theo ngày",
                        MinDurationDays = 1,
                        MaxDurationDays = 6,
                        DiscountPercent = 0,
                        FreeMaintenanceCount = 0,
                        IsActive = true,
                        Description = "Thuê xe theo ngày, không giảm giá"
                    },
                    new()
                    {
                        Name = "1 Tuần",
                        MinDurationDays = 7,
                        MaxDurationDays = 29,
                        DiscountPercent = 5,
                        FreeMaintenanceCount = 0,
                        IsActive = true,
                        Description = "Thuê từ 7 ngày, giảm 5% so với giá lẻ"
                    },
                    new()
                    {
                        Name = "1 Tháng",
                        MinDurationDays = 30,
                        MaxDurationDays = 30,
                        DiscountPercent = 10,
                        FreeMaintenanceCount = 1,
                        IsActive = true,
                        Description = "Thuê 30 ngày, giảm 10%, miễn phí 1 lần bảo dưỡng"
                    },
                    new()
                    {
                        Name = "3 Tháng",
                        MinDurationDays = 90,
                        MaxDurationDays = 90,
                        DiscountPercent = 15,
                        FreeMaintenanceCount = 2,
                        IsActive = true,
                        Description = "Thuê 3 tháng, giảm 15%, miễn phí 2 lần bảo dưỡng"
                    },
                    new()
                    {
                        Name = "6 Tháng",
                        MinDurationDays = 180,
                        MaxDurationDays = 180,
                        DiscountPercent = 20,
                        FreeMaintenanceCount = 4,
                        IsActive = true,
                        Description = "Thuê 6 tháng, giảm 20%, miễn phí 4 lần bảo dưỡng"
                    },
                    new()
                    {
                        Name = "1 Năm",
                        MinDurationDays = 360,
                        MaxDurationDays = 365,
                        DiscountPercent = 25,
                        FreeMaintenanceCount = 8,
                        IsActive = true,
                        Description = "Thuê 1 năm, giảm 25%, miễn phí 8 lần bảo dưỡng, ưu tiên xe mới"
                    }
                };

                _context.ContractTypes.AddRange(contracts);
                await _context.SaveChangesAsync();
            }
        }

        private async Task RepairMojibakeContractTypesAsync()
        {
            var contracts = await _context.ContractTypes
                .Where(c => c.IsActive)
                .OrderBy(c => c.ContractTypeID)
                .ToListAsync();
            var changed = false;

            foreach (var contract in contracts)
            {
                var fixedName = FixMojibake(contract.Name);
                var fixedDescription = FixMojibake(contract.Description);
                var fixedAfterSales = FixMojibake(contract.AfterSalesDescription);

                if (!string.Equals(contract.Name, fixedName, StringComparison.Ordinal))
                {
                    contract.Name = fixedName!;
                    changed = true;
                }

                if (!string.Equals(contract.Description, fixedDescription, StringComparison.Ordinal))
                {
                    contract.Description = fixedDescription;
                    changed = true;
                }

                if (!string.Equals(contract.AfterSalesDescription, fixedAfterSales, StringComparison.Ordinal))
                {
                    contract.AfterSalesDescription = fixedAfterSales;
                    changed = true;
                }
            }

            var duplicateGroups = contracts
                .GroupBy(c => new
                {
                    Name = c.Name.Trim().ToUpperInvariant(),
                    c.MinDurationDays,
                    c.MaxDurationDays,
                    Discount = decimal.Round(c.DiscountPercent, 2)
                })
                .Where(g => g.Count() > 1);

            foreach (var group in duplicateGroups)
            {
                foreach (var duplicate in group.Skip(1))
                {
                    duplicate.IsActive = false;
                    changed = true;
                    _logger.LogInformation("Deactivated duplicate contract type {ContractTypeID} after mojibake repair.", duplicate.ContractTypeID);
                }
            }

            if (changed)
                await _context.SaveChangesAsync();
        }

        private static string? FixMojibake(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || !LooksLikeMojibake(value))
                return value;

            try
            {
                return Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(value));
            }
            catch
            {
                return value;
            }
        }

        private static bool LooksLikeMojibake(string value) =>
            value.Contains('Ã') ||
            value.Contains('Ä') ||
            value.Contains('Â') ||
            value.Contains("áº", StringComparison.Ordinal) ||
            value.Contains("á»", StringComparison.Ordinal);

        /// <summary>
        /// Tính ngày kết thúc dựa trên duration và contract type
        /// </summary>
        private (DateTime endDate, int totalDays) CalculateEndDate(
            DateTime startDate,
            string? duration,
            ContractType contractType,
            string? selectedDates)
        {
            DateTime endDate;
            int totalDays;

            // Xử lý ngày tùy chỉnh
            if (!string.IsNullOrEmpty(selectedDates))
            {
                var dateList = selectedDates
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(d => DateTime.TryParse(d.Trim(), out var date) ? date : (DateTime?)null)
                    .Where(d => d.HasValue)
                    .OrderBy(d => d)
                    .Select(d => d!.Value)
                    .ToList();

                if (dateList.Count > 0)
                {
                    totalDays = dateList.Count;
                    endDate = dateList.Last().Date.AddDays(1);
                    return (endDate, totalDays);
                }
            }

            // Xử lý theo duration string
            if (!string.IsNullOrEmpty(duration))
            {
                var parts = duration.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && int.TryParse(parts[0], out int value))
                {
                    var unit = parts[1].ToLower();

                    switch (unit)
                    {
                        case var u when u.Contains("năm") || u.Contains("year"):
                            endDate = startDate.AddYears(value);
                            break;
                        case var u when u.Contains("tháng") || u.Contains("month"):
                            endDate = startDate.AddMonths(value);
                            break;
                        case var u when u.Contains("tuần") || u.Contains("week"):
                            endDate = startDate.AddDays(value * 7);
                            break;
                        case var u when u.Contains("ngày") || u.Contains("day"):
                            endDate = startDate.AddDays(value);
                            break;
                        default:
                            endDate = startDate.AddMonths(1);
                            break;
                    }

                    totalDays = (int)Math.Ceiling((endDate - startDate).TotalDays);
                    return (endDate, Math.Max(totalDays, 1));
                }
            }

            // Default: 1 tháng
            endDate = startDate.AddMonths(1);
            totalDays = 30;

            return (endDate, totalDays);
        }

        /// <summary>
        /// Tính số tháng thuê
        /// </summary>
        private int CalculateTermMonths(DateTime startDate, DateTime endDate)
        {
            var months = ((endDate.Year - startDate.Year) * 12) + endDate.Month - startDate.Month;
            if (endDate.Day < startDate.Day)
            {
                months--;
            }
            return Math.Max(1, months);
        }

        #endregion
    }
}
