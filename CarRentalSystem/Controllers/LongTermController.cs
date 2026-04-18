using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

using CarRentalSystem.Services;

namespace CarRentalSystem.Controllers
{
    public class LongTermController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPiiEncryptionService _piiEncryption;

        public LongTermController(ApplicationDbContext context, IPiiEncryptionService piiEncryption)
        {
            _context = context;
            _piiEncryption = piiEncryption;
        }

        // GET: LongTerm
        public async Task<IActionResult> Index()
        {
            // Ensure "Ngày tùy chỉnh" contract type exists
            if (!await _context.ContractTypes.AnyAsync(c => c.Name == "Ngày tùy chỉnh"))
            {
                _context.ContractTypes.Add(new ContractType
                {
                    Name = "Ngày tùy chỉnh",
                    MinDurationDays = 1,
                    DiscountPercent = 0,
                    FreeMaintenanceCount = 0,
                    IsActive = true
                });
                await _context.SaveChangesAsync();
            }

            // Fetch long term contract types
            var contracts = await _context.ContractTypes
                .Where(c => c.IsActive && c.Name.ToLower() != "theo ngày" && c.Name.ToLower() != "ngày chẵn" && c.Name.ToLower() != "ngày lẻ" && c.Name.ToLower() != "cuối tuần")
                .OrderBy(c => c.MinDurationDays)
                .ToListAsync();

            ViewBag.ContractTypes = contracts;
            return View();
        }

        public async Task<IActionResult> RequestForm(int? contractId)
        {
            var contracts = await _context.ContractTypes
                .Where(c => c.IsActive && c.Name.ToLower() != "theo ngày" && c.Name.ToLower() != "ngày chẵn" && c.Name.ToLower() != "ngày lẻ" && c.Name.ToLower() != "cuối tuần")
                .OrderBy(c => c.MinDurationDays)
                .ToListAsync();

            ViewBag.ContractTypes = contracts;
            ViewBag.Locations = await _context.Locations.ToListAsync();
            
            var regionCookie = Request.Cookies["RegionID"];
            if (!string.IsNullOrEmpty(regionCookie) && int.TryParse(regionCookie, out int regionId))
            {
                ViewBag.CurrentRegionID = regionId;
            }
            
            var selectedContract = contracts.FirstOrDefault(c => c.ContractTypeID == contractId);
            ViewBag.SelectedContract = selectedContract;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitRequest(string FullName, string Phone, string Note, int ContractTypeID, string Location, string Brand, string ModelStr, string RentDuration, DateTime StartDate, string SelectedDates)
        {
            // Auto-matching logic for Lead -> Contract Signing Auto Flow
            var query = _context.Cars.Include(c => c.CarBrand).AsQueryable();
            
            if (!string.IsNullOrEmpty(Brand) && Brand != "Linh hoạt")
                query = query.Where(c => c.CarBrand.Name.ToLower().Contains(Brand.ToLower()));
            
            if (!string.IsNullOrEmpty(ModelStr) && ModelStr != "Bất kỳ")
                query = query.Where(c => c.CarName.ToLower().Contains(ModelStr.ToLower()));
                
            var matchedCar = await query.FirstOrDefaultAsync();
            
            // Fallback: If strict matching yields nothing, just pick ANY available car in the system to ensure the demo flow completes.
            if (matchedCar == null)
            {
                matchedCar = await _context.Cars.Include(c => c.CarBrand).FirstOrDefaultAsync(c => c.CarStatusID == 1); // 1 = Available
            }

            if (matchedCar == null)
            {
                TempData["Error"] = "Hiện không có sẵn xe nào trong hệ thống phục vụ yêu cầu này.";
                return RedirectToAction("Index");
            }

            return RedirectToAction("ContractPreview", new { 
                carId = matchedCar.CarID, 
                contractId = ContractTypeID,
                duration = RentDuration,
                customerName = FullName,
                customerPhone = Phone,
                startDateStr = StartDate.ToString("yyyy-MM-ddTHH:mm"),
                selectedDates = SelectedDates ?? ""
            });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ContractPreview(int carId, int contractId, string duration, string customerName, string customerPhone, string startDateStr, string selectedDates)
        {
            var car = await _context.Cars.Include(c => c.CarBrand).FirstOrDefaultAsync(c => c.CarID == carId);
            var contract = await _context.ContractTypes.FindAsync(contractId);
            
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int currentUserId)) return Unauthorized();
            var user = await _context.Users.Include(u => u.MembershipTier).FirstOrDefaultAsync(u => u.UserID == currentUserId);

            if (car == null || contract == null) return NotFound();

            ViewBag.Car = car;
            ViewBag.Contract = contract;
            ViewBag.Duration = duration;
            ViewBag.CustomerName = customerName;
            ViewBag.CustomerPhone = customerPhone;
            ViewBag.MembershipTier = user?.MembershipTier;
            ViewBag.StartDateStr = startDateStr;
            ViewBag.SelectedDates = selectedDates;

            return View();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> VehicleInspection(int carId, int contractId, string duration, string customerName, string customerPhone, string startDateStr, string selectedDates)
        {
            var car = await _context.Cars.Include(c => c.CarBrand).FirstOrDefaultAsync(c => c.CarID == carId);
            var contract = await _context.ContractTypes.FindAsync(contractId);

            if (car == null || contract == null) return NotFound();

            ViewBag.Car = car;
            ViewBag.Contract = contract;
            ViewBag.Duration = duration;
            ViewBag.CustomerName = customerName;
            ViewBag.CustomerPhone = customerPhone;
            ViewBag.StartDateStr = startDateStr;
            ViewBag.SelectedDates = selectedDates;

            return View();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ConfirmAndDeposit(int carId, int contractId, string duration, string customerName, string customerPhone, string startDateStr, string selectedDates)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId)) return Unauthorized();
            var user = await _context.Users.Include(u => u.MembershipTier).FirstOrDefaultAsync(u => u.UserID == userId);
            var car = await _context.Cars.FindAsync(carId);
            var cType = await _context.ContractTypes.FindAsync(contractId);

            DateTime startDate = string.IsNullOrEmpty(startDateStr) ? DateTime.UtcNow : DateTime.Parse(startDateStr);
            DateTime endDate = startDate;
            int totalDays = 1;
            string selectedDatesDisplay = "";
            
            // Xử lý "Ngày tùy chỉnh": khách tự chọn từng ngày trên lịch
            if (!string.IsNullOrEmpty(selectedDates))
            {
                var dateList = selectedDates.Split(',').Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => DateTime.Parse(d)).OrderBy(d => d).ToList();
                if (dateList.Count > 0)
                {
                    totalDays = dateList.Count;
                    startDate = dateList.First();
                    endDate = dateList.Last().AddDays(1); // ngày cuối + 1 = ngày trả xe
                    selectedDatesDisplay = string.Join(", ", dateList.Select(d => d.ToString("dd/MM")));
                }
            }
            else
            {
                // Lấy ra value của duration, vd "1 Năm", "3 Tháng"
                var parts = duration?.Split(' ');
                if (parts != null && parts.Length >= 2 && int.TryParse(parts[0], out int periodVal))
                {
                    string unit = parts[1].ToLower();
                    if (unit.Contains("năm") || unit.Contains("year")) endDate = startDate.AddYears(periodVal);
                    else if (unit.Contains("tháng") || unit.Contains("month")) endDate = startDate.AddMonths(periodVal);
                    else if (unit.Contains("tuần") || unit.Contains("week")) endDate = startDate.AddDays(periodVal * 7);
                    else if (unit.Contains("ngày") || unit.Contains("day")) endDate = startDate.AddDays(periodVal);
                }
                else {
                    endDate = startDate.AddMonths(1);
                }
                totalDays = (int)Math.Ceiling((endDate - startDate).TotalDays);
                if (totalDays <= 0) totalDays = 1;
            }

            // Create pseudo-booking mapped to this contract
            var booking = new Booking
            {
                UserID = userId,
                CarID = carId,
                PickupLocationID = car.LocationID,
                ReturnLocationID = car.LocationID,
                StartDate = startDate, 
                EndDate = endDate, 
                ContractTypeID = contractId,
                BookingStatusID = _context.BookingStatuses.FirstOrDefault(s => s.Name == "Draft")?.BookingStatusID ?? 1,
                TotalPrice = car.PricePerDay * totalDays, // Tính theo đúng số ngày thực tế
                DepositAmount = car.DefaultDeposit,
                DepositStatus = "Holding",
                DeliveryAddress = "Phòng Doanh Nghiệp"
            };
            
            // Apply Contract discount
            if (cType != null && cType.DiscountPercent > 0)
            {
                booking.TotalPrice -= booking.TotalPrice * (cType.DiscountPercent / 100m);
            }
            // Apply Membership discount
            if (user?.MembershipTier != null && user.MembershipTier.DiscountPercent > 0)
            {
                decimal memDiscount = booking.TotalPrice * (user.MembershipTier.DiscountPercent / 100m);
                booking.DiscountAmount += memDiscount;
                booking.TotalPrice -= memDiscount;
            }
            
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            // Create standard Digital Contract
            var contract = new DigitalContract
            {
                ContractNo = "HD-BIZ-" + DateTime.UtcNow.ToString("yyMMdd") + booking.BookingID,
                BookingID = booking.BookingID,
                CustomerName = customerName ?? user.Name,
                CustomerIdentityCard = _piiEncryption.DecryptIfNotEmpty(user.CitizenId) ?? "000000000000",
                CustomerPhone = customerPhone ?? user.Phone,
                CustomerAddress = user.Address ?? "Văn phòng công ty",
                CarLicensePlate = car.LicensePlate,
                CarModel = car.CarName,
                RentalStartDate = booking.StartDate,
                RentalEndDate = booking.EndDate,
                RentalPricePerDay = car.PricePerDay,
                TotalContractValue = booking.TotalPrice,
                DepositAmount = car.DefaultDeposit,
                TermsAndConditions = $@"Sau khi bàn bạc, thỏa thuận, hai bên thống nhất ký kết Hợp đồng thuê xe:

ĐIỀU 1: NỘI DUNG HỢP ĐỒNG
- Kiểu xe: {car.CarBrand?.Name} {car.CarName}, biển số: {car.LicensePlate}

ĐIỀU 2: GIÁ TRỊ HỢP ĐỒNG, PHƯƠNG THỨC THANH TOÁN
- Giá thuê xe là: {car.PricePerDay.ToString("N0")} VNĐ/ngày × {totalDays} ngày = {booking.TotalPrice.ToString("N0")} VNĐ.
- Tiền cọc (Giữ chỗ): {booking.DepositAmount.ToString("N0")} VNĐ.
- Tổng cộng: {booking.TotalPrice.ToString("N0")} VNĐ.{(string.IsNullOrEmpty(selectedDatesDisplay) ? "" : $"\n- Các ngày thuê xe: {selectedDatesDisplay}")}

ĐIỀU 3: HIỆU LỰC HỢP ĐỒNG
- Hợp đồng có giá trị từ {booking.StartDate:HH:mm dd/MM/yyyy} đến hết ngày {booking.EndDate:HH:mm dd/MM/yyyy}.
- Hợp đồng B2B điện tử này tuân thủ các quy chuẩn thỏa thuận hệ thống.",
                IsSignedByCustomer = false, // WAIT FOR SIGNING!
                CustomerSignDate = null,
                IsSignedByOwner = false
            };
            _context.DigitalContracts.Add(contract);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đơn đặt của bạn đã được ghi nhận. Vui lòng thanh toán tiền cọc để tiến hành Ký Hợp Đồng Mới!";
            return RedirectToAction("Deposit", "Booking", new { id = booking.BookingID, area = "" });
        }
    }
}
