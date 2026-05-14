using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace CarRentalSystem.Services
{
    /// <summary>
    /// Service xử lý nghiệp vụ thuê dài hạn - Chuẩn Enterprise
    /// </summary>
    public interface ILongTermRentalService
    {
        /// <summary>
        /// Tìm xe phù hợp cho thuê dài hạn với kiểm tra availability đầy đủ
        /// </summary>
        Task<CarAvailabilityResult> FindAvailableCarAsync(
            int? preferredCarId,
            string? brand,
            string? model,
            int locationId,
            DateTime startDate,
            DateTime endDate);

        /// <summary>
        /// Tính toán chi phí thuê dài hạn
        /// </summary>
        Task<RentalPricingResult> CalculateRentalPriceAsync(
            Car car,
            ContractType contractType,
            User? user,
            DateTime startDate,
            DateTime endDate,
            int termMonths);

        /// <summary>
        /// Tạo booking và các record liên quan cho thuê dài hạn
        /// </summary>
        Task<LongTermRentalResult> CreateLongTermRentalAsync(
            int userId,
            int carId,
            int contractTypeId,
            DateTime startDate,
            DateTime endDate,
            string customerName,
            string customerPhone,
            string? notes);

        Task<LongTermRentalResult> CreatePostPaymentLongTermRecordsAsync(
            int bookingId,
            string? customerName,
            string? customerPhone);

        /// <summary>
        /// Validate yêu cầu thuê dài hạn
        /// </summary>
        Task<ValidationResult> ValidateRentalRequestAsync(
            int? carId,
            int contractTypeId,
            DateTime startDate,
            int termMonths);
    }

    public class LongTermRentalService : ILongTermRentalService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPiiEncryptionService _piiEncryption;
        private readonly IReservationService _reservationService;
        private readonly ILogger<LongTermRentalService> _logger;

        // Car Status Constants
        private const int STATUS_AVAILABLE = 1;
        private const int STATUS_MAINTENANCE = 3;
        private const int STATUS_RENTED = 4;

        public LongTermRentalService(
            ApplicationDbContext context,
            IPiiEncryptionService piiEncryption,
            IReservationService reservationService,
            ILogger<LongTermRentalService> logger)
        {
            _context = context;
            _piiEncryption = piiEncryption;
            _reservationService = reservationService;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<CarAvailabilityResult> FindAvailableCarAsync(
            int? preferredCarId,
            string? brand,
            string? model,
            int locationId,
            DateTime startDate,
            DateTime endDate)
        {
            var result = new CarAvailabilityResult();

            // Bước 1: Query cơ bản - chỉ lấy xe ở trạng thái Available
            var query = _context.Cars
                .Include(c => c.CarBrand)
                .Include(c => c.CarStatus)
                .Include(c => c.Location)
                .Where(c => c.CarStatusID == STATUS_AVAILABLE)
                .AsQueryable();

            // Bước 2: Lọc theo brand/model nếu có
            if (!string.IsNullOrEmpty(brand) && brand != "Linh hoạt")
            {
                query = query.Where(c => c.CarBrand != null &&
                    c.CarBrand.Name.ToLower().Contains(brand.ToLower()));
            }

            if (!string.IsNullOrEmpty(model) && model != "Bất kỳ")
            {
                query = query.Where(c => c.CarName.ToLower().Contains(model.ToLower()));
            }

            // Bước 3: Lọc theo location nếu có
            var availableCars = await query.ToListAsync();

            // Bước 4: Kiểm tra availability theo thời gian
            foreach (var car in availableCars)
            {
                // Kiểm tra booking trùng lịch
                var availability = await _reservationService.CheckCarAvailabilityAsync(car.CarID, startDate, endDate);

                if (availability.IsAvailable)
                {
                    result.AvailableCars.Add(car);
                }
            }

            // Bước 5: Ưu tiên xe được chỉ định trước
            if (preferredCarId.HasValue)
            {
                var preferredCar = result.AvailableCars.FirstOrDefault(c => c.CarID == preferredCarId.Value);
                if (preferredCar != null)
                {
                    result.SelectedCar = preferredCar;
                    result.IsFromPreferred = true;
                }
            }

            // Bước 6: Nếu không có xe được chỉ định, lấy xe đầu tiên phù hợp
            if (result.SelectedCar == null)
            {
                result.SelectedCar = result.AvailableCars.FirstOrDefault();
            }

            result.TotalAvailable = result.AvailableCars.Count;

            return result;
        }

        /// <inheritdoc />
        public async Task<RentalPricingResult> CalculateRentalPriceAsync(
            Car car,
            ContractType contractType,
            User? user,
            DateTime startDate,
            DateTime endDate,
            int termMonths)
        {
            var result = new RentalPricingResult
            {
                StartDate = startDate,
                EndDate = endDate
            };

            // Tính số ngày chính xác
            var totalDays = (int)Math.Ceiling((endDate - startDate).TotalDays);
            if (totalDays < contractType.MinDurationDays)
            {
                totalDays = contractType.MinDurationDays;
            }

            result.TotalDays = totalDays;
            result.BasePrice = car.PricePerDay * totalDays;

            // Áp dụng giảm giá theo gói hợp đồng
            if (contractType.DiscountPercent > 0)
            {
                result.ContractDiscount = result.BasePrice * (contractType.DiscountPercent / 100m);
                result.PriceAfterContractDiscount = result.BasePrice - result.ContractDiscount;
            }
            else
            {
                result.PriceAfterContractDiscount = result.BasePrice;
            }

            // Áp dụng giảm giá thành viên (nếu có)
            if (user?.MembershipTier != null && user.MembershipTier.DiscountPercent > 0)
            {
                result.MembershipDiscount = result.PriceAfterContractDiscount * (user.MembershipTier.DiscountPercent / 100m);
                result.PriceAfterMembershipDiscount = result.PriceAfterContractDiscount - result.MembershipDiscount;
            }
            else
            {
                result.PriceAfterMembershipDiscount = result.PriceAfterContractDiscount;
            }

            // Tính tiền cọc (dựa trên giá trị xe)
            result.DepositAmount = car.DefaultDeposit;

            // Tính giá hàng tháng (cho thuê dài hạn)
            if (termMonths > 0)
            {
                result.MonthlyRate = result.PriceAfterMembershipDiscount / termMonths;
            }

            result.FinalPrice = result.PriceAfterMembershipDiscount;

            // Kiểm tra discount cap
            var totalDiscount = result.ContractDiscount + result.MembershipDiscount;
            if (totalDiscount > result.BasePrice * 0.5m) // Không cho phép giảm quá 50%
            {
                result.ValidationWarnings.Add("Tổng chiết khấu vượt quá 50%, vui lòng kiểm tra lại");
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<LongTermRentalResult> CreateLongTermRentalAsync(
            int userId,
            int carId,
            int contractTypeId,
            DateTime startDate,
            DateTime endDate,
            string customerName,
            string customerPhone,
            string? notes)
        {
            var result = new LongTermRentalResult();

            try
            {
                // Lấy thông tin cần thiết
                var user = await _context.Users
                    .Include(u => u.MembershipTier)
                    .FirstOrDefaultAsync(u => u.UserID == userId);

                var car = await _context.Cars
                    .Include(c => c.CarBrand)
                    .FirstOrDefaultAsync(c => c.CarID == carId);

                var contractType = await _context.ContractTypes
                    .FindAsync(contractTypeId);

                if (user == null || car == null || contractType == null)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Thông tin người dùng, xe hoặc gói hợp đồng không hợp lệ";
                    return result;
                }

                // Tính số tháng thuê
                var availability = await _reservationService.CheckCarAvailabilityAsync(carId, startDate, endDate);
                if (!availability.IsAvailable)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = availability.Reason ?? "Xe không còn khả dụng trong thời gian thuê dài hạn.";
                    return result;
                }

                var termMonths = CalculateTermMonths(startDate, endDate);

                // Tính giá
                var pricing = await CalculateRentalPriceAsync(car, contractType, user, startDate, endDate, termMonths);

                // Lấy booking status Draft
                var draftStatus = await _context.BookingStatuses
                    .FirstOrDefaultAsync(s => s.Name == "Draft");

                if (draftStatus == null)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Không tìm thấy trạng thái Draft cho booking";
                    return result;
                }

                // Tạo Booking
                var booking = new Booking
                {
                    UserID = userId,
                    CarID = carId,
                    PickupLocationID = car.LocationID,
                    ReturnLocationID = car.LocationID,
                    StartDate = startDate,
                    EndDate = endDate,
                    ContractTypeID = contractTypeId,
                    BookingStatusID = draftStatus.BookingStatusID,
                    TotalPrice = pricing.FinalPrice,
                    DepositAmount = pricing.DepositAmount,
                    DepositStatus = "Holding",
                    TotalHours = pricing.TotalDays * 24,
                    InsuranceTier = car.InsuranceTier,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                result.Booking = booking;
                result.BookingID = booking.BookingID;

                // Tạo LongTermRental record
                var longTermRental = new LongTermRental
                {
                    BookingID = booking.BookingID,
                    DepositAmount = pricing.DepositAmount,
                    MonthlyRate = pricing.MonthlyRate,
                    TermMonths = termMonths
                };

                _context.LongTermRentals.Add(longTermRental);
                await _context.SaveChangesAsync();

                result.LongTermRental = longTermRental;
                result.RentalID = longTermRental.RentalID;

                // Tạo PaymentSchedule cho từng tháng
                if (termMonths > 0)
                {
                    for (int month = 1; month <= termMonths; month++)
                    {
                        var dueDate = startDate.AddMonths(month);
                        var paymentSchedule = new PaymentSchedule
                        {
                            RentalID = longTermRental.RentalID,
                            DueDate = dueDate,
                            Amount = pricing.MonthlyRate,
                            Status = month == 1 ? "Due" : "Unpaid" // Tháng đầu tiên là Due (đã đặt cọc)
                        };

                        _context.PaymentSchedules.Add(paymentSchedule);
                    }

                    await _context.SaveChangesAsync();
                }

                // Tạo DigitalContract
                var contractNo = GenerateContractNo(booking.BookingID);
                var decryptedCccd = _piiEncryption.DecryptIfNotEmpty(user.CitizenId) ?? "000000000000";
                var digitalContract = new DigitalContract
                {
                    ContractNo = contractNo,
                    BookingID = booking.BookingID,
                    CustomerName = customerName ?? user.Name,
                    CustomerIdentityCard = decryptedCccd.Length > 20 ? decryptedCccd.Substring(0, 20) : decryptedCccd,
                    CustomerPhone = customerPhone ?? user.Phone,
                    CustomerAddress = user.Address ?? "Văn phòng công ty",
                    CarLicensePlate = car.LicensePlate,
                    CarModel = $"{car.CarBrand?.Name} {car.CarName}",
                    RentalStartDate = startDate,
                    RentalEndDate = endDate,
                    RentalPricePerDay = car.PricePerDay,
                    TotalContractValue = pricing.FinalPrice,
                    DepositAmount = pricing.DepositAmount,
                    TermsAndConditions = GenerateTermsAndConditions(car, contractType, pricing, termMonths),
                    IsSignedByCustomer = false,
                    CustomerSignDate = null,
                    IsSignedByOwner = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.DigitalContracts.Add(digitalContract);
                await _context.SaveChangesAsync();

                result.DigitalContract = digitalContract;
                result.ContractNo = contractNo;

                // Cập nhật trạng thái xe (soft lock - chờ thanh toán cọc)
                // Xe vẫn ở trạng thái Available cho đến khi thanh toán thành công

                result.IsSuccess = true;
                result.Pricing = pricing;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Lỗi khi tạo thuê dài hạn: {ex.Message}";
                _logger.LogError(ex, "Error creating long-term rental for user {UserId}", userId);
            }

            return result;
        }

        public async Task<LongTermRentalResult> CreatePostPaymentLongTermRecordsAsync(
            int bookingId,
            string? customerName,
            string? customerPhone)
        {
            var result = new LongTermRentalResult();

            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.User)
                        .ThenInclude(u => u!.MembershipTier)
                    .Include(b => b.Car)
                        .ThenInclude(c => c!.CarBrand)
                    .FirstOrDefaultAsync(b => b.BookingID == bookingId);

                if (booking?.Car == null || booking.User == null || !booking.ContractTypeID.HasValue)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Thong tin booking dai han khong hop le.";
                    return result;
                }

                var contractType = await _context.ContractTypes.FindAsync(booking.ContractTypeID.Value);
                if (contractType == null)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Không tìm thấy gói thuê dài hạn.";
                    return result;
                }

                var existingRental = await _context.LongTermRentals
                    .FirstOrDefaultAsync(r => r.BookingID == booking.BookingID);
                if (existingRental != null)
                {
                    result.IsSuccess = true;
                    result.Booking = booking;
                    result.BookingID = booking.BookingID;
                    result.LongTermRental = existingRental;
                    result.RentalID = existingRental.RentalID;
                    result.DigitalContract = await _context.DigitalContracts
                        .FirstOrDefaultAsync(c => c.BookingID == booking.BookingID);
                    result.ContractNo = result.DigitalContract?.ContractNo;
                    return result;
                }

                var termMonths = booking.TotalHours > 0
                    ? Math.Max(1, (int)Math.Ceiling(booking.TotalHours / 24m / 30m))
                    : CalculateTermMonths(booking.StartDate, booking.EndDate);
                var pricing = await CalculateRentalPriceAsync(
                    booking.Car,
                    contractType,
                    booking.User,
                    booking.StartDate,
                    booking.EndDate,
                    termMonths);

                var longTermRental = new LongTermRental
                {
                    BookingID = booking.BookingID,
                    DepositAmount = booking.DepositAmount > 0 ? booking.DepositAmount : pricing.DepositAmount,
                    MonthlyRate = pricing.MonthlyRate,
                    TermMonths = termMonths
                };

                _context.LongTermRentals.Add(longTermRental);
                await _context.SaveChangesAsync();

                for (var month = 1; month <= termMonths; month++)
                {
                    _context.PaymentSchedules.Add(new PaymentSchedule
                    {
                        RentalID = longTermRental.RentalID,
                        DueDate = booking.StartDate.AddMonths(month),
                        Amount = pricing.MonthlyRate,
                        Status = month == 1 ? "Due" : "Unpaid"
                    });
                }

                var contractNo = GenerateContractNo(booking.BookingID);
                var decryptedCccd = _piiEncryption.DecryptIfNotEmpty(booking.User.CitizenId) ?? "000000000000";
                _context.DigitalContracts.Add(new DigitalContract
                {
                    ContractNo = contractNo,
                    BookingID = booking.BookingID,
                    CustomerName = customerName ?? booking.User.Name,
                    CustomerIdentityCard = decryptedCccd.Length > 20 ? decryptedCccd.Substring(0, 20) : decryptedCccd,
                    CustomerPhone = customerPhone ?? booking.User.Phone ?? "",
                    CustomerAddress = booking.User.Address ?? "Van phong cong ty",
                    CarLicensePlate = booking.Car.LicensePlate,
                    CarModel = $"{booking.Car.CarBrand?.Name} {booking.Car.CarName}",
                    RentalStartDate = booking.StartDate,
                    RentalEndDate = booking.EndDate,
                    RentalPricePerDay = booking.Car.PricePerDay,
                    TotalContractValue = booking.TotalPrice,
                    DepositAmount = booking.DepositAmount,
                    TermsAndConditions = GenerateTermsAndConditions(booking.Car, contractType, pricing, termMonths),
                    IsSignedByCustomer = false,
                    CustomerSignDate = null,
                    IsSignedByOwner = false,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                result.IsSuccess = true;
                result.Booking = booking;
                result.BookingID = booking.BookingID;
                result.LongTermRental = longTermRental;
                result.RentalID = longTermRental.RentalID;
                result.ContractNo = contractNo;
                result.Pricing = pricing;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Lỗi khi tạo hồ sơ thuê dài hạn sau thanh toán: {ex.Message}";
                _logger.LogError(ex, "Error creating post-payment long-term records for booking {BookingId}", bookingId);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<ValidationResult> ValidateRentalRequestAsync(
            int? carId,
            int contractTypeId,
            DateTime startDate,
            int termMonths)
        {
            var result = new ValidationResult { IsValid = true };

            // Validate contract type
            var contractType = await _context.ContractTypes.FindAsync(contractTypeId);
            if (contractType == null)
            {
                result.IsValid = false;
                result.Errors.Add("Gói hợp đồng không tồn tại");
                return result;
            }

            if (!contractType.IsActive)
            {
                result.IsValid = false;
                result.Errors.Add("Gói hợp đồng đã bị vô hiệu hóa");
            }

            // Validate ngày bắt đầu
            var today = DateTime.UtcNow.Date;
            if (startDate.Date < today)
            {
                result.IsValid = false;
                result.Errors.Add("Ngày bắt đầu thuê phải từ hôm nay trở đi");
            }

            // Validate số tháng tối thiểu
            if (termMonths < 1)
            {
                result.IsValid = false;
                result.Errors.Add("Thời gian thuê tối thiểu là 1 tháng");
            }

            // Validate số ngày tối thiểu theo contract type
            var minDays = contractType.MinDurationDays;
            var calculatedDays = termMonths * 30; // Approximate

            // Cho phép linh hoạt với 1 tuần = 7 ngày
            if (calculatedDays < minDays && contractType.Name.Contains("Tuần"))
            {
                // Week-based contracts are flexible
            }
            else if (calculatedDays < minDays)
            {
                result.IsValid = false;
                result.Errors.Add($"Thời gian thuê tối thiểu cho gói {contractType.Name} là {minDays} ngày");
            }

            // Validate xe nếu có chỉ định
            if (carId.HasValue)
            {
                var car = await _context.Cars
                    .Include(c => c.CarStatus)
                    .FirstOrDefaultAsync(c => c.CarID == carId.Value);

                if (car == null)
                {
                    result.IsValid = false;
                    result.Errors.Add("Xe không tồn tại");
                }
                else if (car.CarStatusID != STATUS_AVAILABLE)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Xe {car.LicensePlate} hiện không có sẵn (Trạng thái: {car.CarStatus?.Name})");
                }
            }

            return result;
        }

        #region Private Helper Methods

        private int CalculateTermMonths(DateTime startDate, DateTime endDate)
        {
            var months = ((endDate.Year - startDate.Year) * 12) + endDate.Month - startDate.Month;
            if (endDate.Day < startDate.Day)
            {
                months--;
            }
            return Math.Max(1, months);
        }

        private string GenerateContractNo(int bookingId)
        {
            var timestamp = DateTime.UtcNow.ToString("yyMMddHHmmss");
            return $"HD-BIZ-{timestamp}-{bookingId:D4}";
        }

        private string GenerateTermsAndConditions(
            Car car,
            ContractType contractType,
            RentalPricingResult pricing,
            int termMonths)
        {
            const int MAX_LENGTH = 3500;

            var terms = $@"SAU KHI BAN BAC, THOA THUAN, HAI BEN THONG NHAT KY KET HOP DONG THUE XE DAI HAN

DIEU 1: THONG TIN XE
- Hang xe: {car.CarBrand?.Name}
- Mau xe: {car.CarName}
- Bien so: {car.LicensePlate}
- So ghe: {car.Seats}, Nhien lieu: {car.FuelType}, Hop so: {car.Transmission}

DIEU 2: THOI HAN
- Tu: {pricing.StartDate:dd/MM/yyyy} den: {pricing.EndDate:dd/MM/yyyy}
- Tong: {termMonths} thang ({pricing.TotalDays} ngay)

DIEU 3: GIA TRI
- Don gia: {car.PricePerDay:N0}VND/ngay, Tong: {pricing.TotalDays} ngay
- Gia goc: {pricing.BasePrice:N0}VND
- Chiet khau ({contractType.Name}): -{pricing.ContractDiscount:N0}VND ({contractType.DiscountPercent}%)
{(pricing.MembershipDiscount > 0 ? $"- Thanh vien: -{pricing.MembershipDiscount:N0}VND" : "")}
- TONG: {pricing.FinalPrice:N0}VND
- Tiền cọc: {pricing.DepositAmount:N0}VND

DIEU 4: LICH THANH TOAN
- Hang thang: {pricing.MonthlyRate:N0}VND, Lan 2+ truoc ngay 05/thang

DIEU 5: QUYEN LOI
{(contractType.FreeMaintenanceCount > 0 ? $"- Miễn phí {contractType.FreeMaintenanceCount} lần bảo dưỡng" : "- Không có bảo dưỡng miễn phí")}
{(contractType.FreeInsurance ? "- Miễn phí bảo hiểm" : "")}

DIEU 6: NGHIA VU
- Bao quan xe dung cach, Thanh toan dung han
- Báo cáo sự cố trong 24h, không cho thuê lại

DIEU 7: HIEU LUC
- Hieu luc khi dat coc thanh cong
- Luu tru dien tu, co gia tri phap ly";

            if (terms.Length > MAX_LENGTH)
            {
                terms = terms.Substring(0, MAX_LENGTH);
            }

            return terms;
        }

        #endregion
    }

    #region Result DTOs

    public class CarAvailabilityResult
    {
        public Car? SelectedCar { get; set; }
        public List<Car> AvailableCars { get; set; } = new();
        public int TotalAvailable { get; set; }
        public bool IsFromPreferred { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class RentalPricingResult
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }
        public decimal BasePrice { get; set; }
        public decimal ContractDiscount { get; set; }
        public decimal PriceAfterContractDiscount { get; set; }
        public decimal MembershipDiscount { get; set; }
        public decimal PriceAfterMembershipDiscount { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal MonthlyRate { get; set; }
        public decimal FinalPrice { get; set; }
        public List<string> ValidationWarnings { get; set; } = new();
    }

    public class LongTermRentalResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public Booking? Booking { get; set; }
        public LongTermRental? LongTermRental { get; set; }
        public DigitalContract? DigitalContract { get; set; }
        public int BookingID { get; set; }
        public int RentalID { get; set; }
        public string? ContractNo { get; set; }
        public RentalPricingResult? Pricing { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    #endregion
}
