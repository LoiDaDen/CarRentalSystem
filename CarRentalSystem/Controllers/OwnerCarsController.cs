using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using CarRentalSystem.Helpers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class OwnerCarsController : Controller
    {
        private const decimal DefaultCommissionRate = 0m;
        private readonly ApplicationDbContext _context;
        private readonly IFileUploadValidationService _fileValidation;
        private readonly IAuditTrailService _auditTrail;
        private readonly ICarChangeRequestService _carChanges;
        private readonly IBookingTransitionService _bookingTransitions;
        private readonly IWebHostEnvironment _environment;

        public OwnerCarsController(
            ApplicationDbContext context,
            IFileUploadValidationService fileValidation,
            IAuditTrailService auditTrail,
            ICarChangeRequestService carChanges,
            IBookingTransitionService bookingTransitions,
            IWebHostEnvironment environment)
        {
            _context = context;
            _fileValidation = fileValidation;
            _auditTrail = auditTrail;
            _carChanges = carChanges;
            _bookingTransitions = bookingTransitions;
            _environment = environment;
        }

        public async Task<IActionResult> Index(string? q = null, string service = "all", string status = "all")
        {
            var userId = GetCurrentUserId();
            var allCars = await GetOwnerCarsQuery(userId).ToListAsync();
            var visibleCars = ApplyOwnerCarFilters(allCars, q, service, status).ToList();
            var allCarIds = allCars.Select(c => c.CarID).ToList();
            var visibleCarIds = visibleCars.Select(c => c.CarID).ToList();
            var activeChanges = await LoadActiveChangeRequestsAsync(allCarIds);
            var bookingRows = await LoadOwnerBookingRowsAsync(allCarIds);
            var visibleBookingRows = bookingRows.Where(b => visibleCarIds.Contains(b.CarID)).ToList();
            var latestTracking = await LoadLatestTrackingAsync(allCarIds);
            var signatureProfile = await _context.OwnerDigitalSignatureProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserID == userId);
            var hasReadyDigitalSignature = signatureProfile?.IsReady == true;
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var today = GetVietnamToday();

            var cards = visibleCars.Select(car =>
            {
                var carBookings = visibleBookingRows.Where(b => b.CarID == car.CarID).ToList();
                var inspectionStatus = BuildInspectionStatus(car.NextInspectionDate, today);
                return new OwnerCarCardViewModel
                {
                    CarID = car.CarID,
                    DisplayName = BuildCarDisplayName(car),
                    LicensePlate = car.LicensePlate,
                    ImageUrl = car.CarImages?.FirstOrDefault()?.ImageUrl ?? car.ImageURL ?? "/images/default-car.png",
                    ServiceLabel = car.IsWithDriverAllowed ? "Xe có tài" : "Xe tự lái",
                    StatusName = car.CarStatus?.Name ?? "Unknown",
                    IsApproved = car.IsApproved,
                    ApprovalStatus = car.ApprovalStatus,
                    ApprovalRejectionReason = car.ApprovalRejectionReason,
                    IsPendingDeletion = car.IsPendingDeletion,
                    HasPendingChange = activeChanges.ContainsKey(car.CarID),
                    PendingChangeOperation = activeChanges.TryGetValue(car.CarID, out var cardChange) ? cardChange.Operation : null,
                    PendingChangeStage = cardChange?.CurrentStage,
                    PendingApprovalLabel = BuildPendingApprovalLabel(cardChange),
                    PendingChangeCreatedAt = cardChange?.CreatedAt,
                    PricePerDay = car.PricePerDay,
                    OwnerIncomePerDay = car.PricePerDay,
                    TripCount = carBookings.Count(b => b.StatusName == BookingStatusConstants.Completed),
                    ActiveBookingCount = carBookings.Count(b => b.StatusName is BookingStatusConstants.Confirmed or BookingStatusConstants.Active),
                    MonthRevenue = carBookings
                        .Where(b => b.CreatedAt >= monthStart && IsRevenueStatus(b.StatusName))
                        .Sum(b => b.TotalPrice),
                    HasGps = latestTracking.ContainsKey(car.CarID),
                    HasEnoughImages = (car.CarImages?.Count ?? 0) >= 4,
                    HasRequiredDocuments = CarContractSigningModes.Normalize(car.ContractSigningMode) != CarContractSigningModes.Digital || hasReadyDigitalSignature,
                    ContractSigningMode = CarContractSigningModes.Normalize(car.ContractSigningMode),
                    ContractSigningModeLabel = CarContractSigningModes.DisplayName(car.ContractSigningMode),
                    Seats = car.Seats,
                    FuelType = car.FuelType,
                    Transmission = car.Transmission,
                    NextInspectionDate = car.NextInspectionDate,
                    InspectionStatusLabel = inspectionStatus.Label,
                    InspectionStatusClass = inspectionStatus.CssClass,
                    InspectionStatusIcon = inspectionStatus.Icon
                };
            }).ToList();

            var model = new OwnerCarsDashboardViewModel
            {
                Chrome = await BuildChromeAsync(userId, "Cars"),
                Cars = cards,
                Query = q,
                Service = NormalizeFilter(service),
                Status = NormalizeFilter(status),
                TotalCars = allCars.Count,
                ApprovedCars = allCars.Count(c => c.IsApproved),
                PendingCars = allCars.Count(c => !c.IsApproved),
                ActiveTrips = bookingRows.Count(b => b.StatusName == BookingStatusConstants.Active),
                MonthRevenue = bookingRows
                    .Where(b => b.CreatedAt >= monthStart && IsRevenueStatus(b.StatusName))
                    .Sum(b => b.TotalPrice)
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, bool partial = false)
        {
            var userId = GetCurrentUserId();
            var car = await _context.Cars
                .AsNoTracking()
                .Include(c => c.CarBrand)
                .Include(c => c.CarModel)
                .Include(c => c.CarStatus)
                .Include(c => c.CarImages)
                .Include(c => c.CarAmenityMappings)
                    .ThenInclude(mapping => mapping.CarAmenity)
                .FirstOrDefaultAsync(c => c.CarID == id && c.OwnerID == userId);

            if (car == null)
                return NotFound();

            var bookings = await LoadOwnerBookingRowsAsync(new[] { car.CarID });
            var latestTracking = await LoadLatestTrackingAsync(new[] { car.CarID });
            var activeChanges = await LoadActiveChangeRequestsAsync(new[] { car.CarID });
            activeChanges.TryGetValue(car.CarID, out var activeChange);
            latestTracking.TryGetValue(car.CarID, out var tracking);

            var signatureProfile = await _context.OwnerDigitalSignatureProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.UserID == userId);
            var hasReadyDigitalSignature = signatureProfile?.IsReady == true;
            var hasEnoughImages = (car.CarImages?.Count ?? 0) >= 4;
            var hasRequiredDocuments =
                CarContractSigningModes.Normalize(car.ContractSigningMode) != CarContractSigningModes.Digital ||
                hasReadyDigitalSignature;
            var inspectionStatus = BuildInspectionStatus(car.NextInspectionDate, GetVietnamToday());
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthGmv = bookings
                .Where(booking => booking.CreatedAt >= monthStart && IsRevenueStatus(booking.StatusName))
                .Sum(booking => booking.TotalPrice);

            var model = new OwnerCarDetailsViewModel
            {
                Chrome = await BuildChromeAsync(userId, "Cars"),
                CarID = car.CarID,
                DisplayName = BuildCarDisplayName(car),
                LicensePlate = car.LicensePlate,
                ImageUrls = car.CarImages?
                    .OrderBy(image => image.ImageID)
                    .Select(image => image.ImageUrl)
                    .ToList() ?? new List<string>(),
                Amenities = car.CarAmenityMappings
                    .Where(mapping => !mapping.IsDeleted && mapping.CarAmenity != null)
                    .OrderBy(mapping => mapping.CarAmenity!.DisplayOrder)
                    .ThenBy(mapping => mapping.CarAmenity!.Name)
                    .Select(mapping => mapping.CarAmenity!.Name)
                    .ToList(),
                ServiceLabel = car.IsWithDriverAllowed ? "Xe có tài" : "Xe tự lái",
                StatusName = car.CarStatus?.Name ?? "Unknown",
                IsApproved = car.IsApproved,
                ApprovalStatus = car.ApprovalStatus,
                ApprovalRejectionReason = car.ApprovalRejectionReason,
                IsPendingDeletion = car.IsPendingDeletion,
                HasPendingChange = activeChange != null,
                PendingChangeOperation = activeChange?.Operation,
                PendingChangeStage = activeChange?.CurrentStage,
                PendingChangeStatus = activeChange?.Status,
                PendingApprovalLabel = BuildPendingApprovalLabel(activeChange),
                PendingChangeCreatedAt = activeChange?.CreatedAt,
                PricePerDay = car.PricePerDay,
                OwnerIncomePerDay = car.PricePerDay,
                MonthGmv = monthGmv,
                MonthOwnerIncome = monthGmv,
                CompletedTripCount = bookings.Count(booking => booking.StatusName == BookingStatusConstants.Completed),
                ActiveBookingCount = bookings.Count(booking => booking.StatusName is BookingStatusConstants.Confirmed or BookingStatusConstants.Active),
                HasGps = tracking != null,
                LatestGpsAt = tracking?.Timestamp,
                HasEnoughImages = hasEnoughImages,
                HasRequiredDocuments = hasRequiredDocuments,
                ContractSigningModeLabel = CarContractSigningModes.DisplayName(car.ContractSigningMode),
                Seats = car.Seats,
                FuelType = car.FuelType,
                Transmission = car.Transmission,
                FuelConsumption = car.FuelConsumption,
                Description = car.Description,
                CarAddress = car.CarAddress,
                Latitude = car.Latitude,
                Longitude = car.Longitude,
                NextInspectionDate = car.NextInspectionDate,
                InspectionStatusLabel = inspectionStatus.Label,
                InspectionStatusClass = inspectionStatus.CssClass,
                InspectionStatusIcon = inspectionStatus.Icon,
                ActionItems = BuildOwnerCarActionItems(
                    car,
                    hasEnoughImages,
                    hasRequiredDocuments,
                    tracking != null,
                    inspectionStatus,
                    activeChange)
            };

            if (partial)
                return PartialView("_DetailsModal", model);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Calendar(DateTime? from = null, DateTime? to = null, int? carId = null, string brand = "all", string status = "all", string? q = null)
        {
            var userId = GetCurrentUserId();
            var start = (from ?? DateTime.Today).Date;
            var end = (to ?? start.AddDays(28)).Date;
            if (end < start) end = start.AddDays(28);
            if ((end - start).TotalDays > 120) end = start.AddDays(120);

            var cars = await GetOwnerCarsQuery(userId).ToListAsync();
            var filteredCars = cars.AsEnumerable();
            if (carId.HasValue) filteredCars = filteredCars.Where(c => c.CarID == carId.Value);
            if (!string.IsNullOrWhiteSpace(brand) && brand != "all")
                filteredCars = filteredCars.Where(c => string.Equals(c.CarBrand?.Name, brand, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(q))
                filteredCars = filteredCars.Where(c => MatchesCarSearch(c, q));

            var selectedCarIds = filteredCars.Select(c => c.CarID).ToList();
            var bookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.BookingStatus)
                .Include(b => b.User)
                .Include(b => b.Car).ThenInclude(c => c!.CarBrand)
                .Include(b => b.Car).ThenInclude(c => c!.CarModel)
                .Where(b => selectedCarIds.Contains(b.CarID) && b.StartDate <= end.AddDays(1) && b.EndDate >= start)
                .OrderBy(b => b.StartDate)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(status) && status != "all")
                bookings = bookings.Where(b => string.Equals(b.BookingStatus?.Name, status, StringComparison.OrdinalIgnoreCase)).ToList();

            var model = new OwnerCalendarViewModel
            {
                Chrome = await BuildChromeAsync(userId, "Calendar"),
                From = start,
                To = end,
                CarId = carId,
                Brand = NormalizeFilter(brand),
                Status = NormalizeFilter(status),
                Query = q,
                Cars = cars.Select(ToOwnerCarOption).ToList(),
                Brands = cars.Select(c => c.CarBrand?.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().OrderBy(name => name).Cast<string>().ToList(),
                Bookings = bookings.Select(b => new OwnerCalendarBookingRowViewModel
                {
                    BookingID = b.BookingID,
                    CarID = b.CarID,
                    CarName = BuildCarDisplayName(b.Car),
                    LicensePlate = b.Car?.LicensePlate ?? string.Empty,
                    CustomerName = b.User?.Name ?? "Khách thuê",
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    StatusName = b.BookingStatus?.Name ?? "Unknown",
                    StatusClass = GetStatusClass(b.BookingStatus?.Name),
                    TotalPrice = b.TotalPrice
                }).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Bookings(string tab = "pending")
        {
            var userId = GetCurrentUserId();
            var ownerBookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.BookingStatus)
                .Include(b => b.User)
                .Include(b => b.Car).ThenInclude(c => c!.CarBrand)
                .Include(b => b.Car).ThenInclude(c => c!.CarModel)
                .Where(b => b.Car != null && b.Car.OwnerID == userId)
                .OrderByDescending(b => b.CreatedAt)
                .Take(300)
                .ToListAsync();
            var rows = ownerBookings.Select(ToOwnerBookingRow).ToList();

            var model = new OwnerBookingsViewModel
            {
                Chrome = await BuildChromeAsync(userId, "Bookings"),
                Tab = NormalizeOwnerBookingTab(tab),
                PendingBookings = rows.Where(IsPendingOwnerBooking).ToList(),
                CurrentBookings = rows.Where(IsCurrentOwnerBooking).ToList(),
                HistoryBookings = rows.Where(IsHistoryOwnerBooking).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> BookingDetails(int id)
        {
            var userId = GetCurrentUserId();
            var booking = await LoadOwnerBookingAsync(id, userId, asTracking: false);
            if (booking == null)
                return NotFound();

            var baseRow = ToOwnerBookingRow(booking);
            var model = new OwnerBookingDetailsViewModel
            {
                Chrome = await BuildChromeAsync(userId, "Bookings"),
                BookingID = baseRow.BookingID,
                CarID = baseRow.CarID,
                CarName = baseRow.CarName,
                LicensePlate = baseRow.LicensePlate,
                CustomerName = baseRow.CustomerName,
                CustomerPhone = baseRow.CustomerPhone,
                StartDate = baseRow.StartDate,
                EndDate = baseRow.EndDate,
                StatusName = baseRow.StatusName,
                StatusClass = baseRow.StatusClass,
                TotalPrice = baseRow.TotalPrice,
                DepositAmount = baseRow.DepositAmount,
                DepositStatus = baseRow.DepositStatus,
                CanDecide = baseRow.CanDecide,
                CancelReason = booking.CancelReason,
                HandoverRecords = booking.HandoverRecords?
                    .Where(h => !h.IsDeleted)
                    .OrderBy(h => h.RecordDate)
                    .Select(h => new OwnerHandoverRecordRowViewModel
                    {
                        HandoverID = h.HandoverID,
                        Type = h.Type,
                        TypeLabel = h.Type == "CheckIn" ? "Biên bản giao xe" : "Biên bản trả xe",
                        RecordDate = h.RecordDate,
                        Odo = h.Odo,
                        FuelLevel = h.FuelLevel,
                        CompensationAmount = h.InventoryItems.Sum(i => i.MissingCharge)
                    })
                    .ToList() ?? new List<OwnerHandoverRecordRowViewModel>()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBooking(int id)
        {
            var userId = GetCurrentUserId();
            var booking = await LoadOwnerBookingAsync(id, userId, asTracking: true);
            if (booking == null)
                return NotFound();

            if (booking.BookingStatus?.Name != BookingStatusConstants.Pending)
            {
                TempData["Error"] = "Chỉ đơn đang chờ chủ xe duyệt mới có thể được duyệt.";
                return RedirectToAction(nameof(BookingDetails), new { id });
            }

            var result = await _bookingTransitions.TransitionAsync(
                id,
                BookingStatusConstants.Confirmed,
                userId,
                UserRoleConstants.Partner,
                "Chủ xe duyệt đơn thuê.");

            TempData[result.IsSuccess ? "Success" : "Error"] = result.ErrorMessage ?? "Đã duyệt đơn thuê.";
            return RedirectToAction(nameof(BookingDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectBooking(int id, string? reason)
        {
            var userId = GetCurrentUserId();
            var booking = await LoadOwnerBookingAsync(id, userId, asTracking: true);
            if (booking == null)
                return NotFound();

            if (booking.BookingStatus?.Name != BookingStatusConstants.Pending)
            {
                TempData["Error"] = "Chỉ đơn đang chờ chủ xe duyệt mới có thể bị từ chối.";
                return RedirectToAction(nameof(BookingDetails), new { id });
            }

            var result = await _bookingTransitions.RejectAsync(
                id,
                userId,
                UserRoleConstants.Partner,
                string.IsNullOrWhiteSpace(reason) ? "Chủ xe từ chối đơn thuê." : reason.Trim());

            TempData[result.IsSuccess ? "Success" : "Error"] = result.ErrorMessage ?? "Đã từ chối đơn thuê.";
            return RedirectToAction(nameof(Bookings), new { tab = "history" });
        }

        [HttpGet]
        public async Task<IActionResult> Guide()
        {
            var userId = GetCurrentUserId();
            return View(new OwnerGuideViewModel
            {
                Chrome = await BuildChromeAsync(userId, "Guide"),
                Documents = GetGuideDocuments()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Fines()
        {
            var userId = GetCurrentUserId();

            return View(new OwnerFinesViewModel
            {
                Chrome = await BuildChromeAsync(userId, "Fines")
            });
        }

        [HttpGet]
        public async Task<IActionResult> TaxInfo()
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.AsNoTracking().FirstAsync(u => u.UserID == userId);
            return View(new OwnerTaxInfoViewModel
            {
                Chrome = await BuildChromeAsync(userId, "TaxInfo"),
                FullName = user.Name,
                Phone = user.Phone,
                Email = user.Email,
                Address = user.Address ?? user.PartnerAddress
            });
        }

        [HttpGet]
        public async Task<IActionResult> Tracking(string gps = "all", string? q = null)
        {
            var userId = GetCurrentUserId();
            var cars = await GetOwnerCarsQuery(userId).ToListAsync();
            if (!string.IsNullOrWhiteSpace(q))
                cars = cars.Where(c => MatchesCarSearch(c, q)).ToList();

            var latest = await LoadLatestTrackingAsync(cars.Select(c => c.CarID).ToList());
            var rows = cars.Select(car =>
            {
                latest.TryGetValue(car.CarID, out var tracking);
                var hasGps = tracking != null;
                var isLive = hasGps && (DateTime.UtcNow - tracking!.Timestamp).TotalMinutes <= 10;
                return new OwnerTrackingCarViewModel
                {
                    CarID = car.CarID,
                    Name = BuildCarDisplayName(car),
                    LicensePlate = car.LicensePlate,
                    HasGps = hasGps,
                    IsLive = isLive,
                    LastUpdated = tracking?.Timestamp,
                    Latitude = tracking?.Latitude ?? car.Latitude,
                    Longitude = tracking?.Longitude ?? car.Longitude
                };
            }).ToList();

            if (gps == "gps") rows = rows.Where(r => r.HasGps).ToList();
            if (gps == "nogps") rows = rows.Where(r => !r.HasGps).ToList();

            var points = rows.Select(row => new OwnerTrackingMapPointViewModel
            {
                CarID = row.CarID,
                Name = row.Name,
                LicensePlate = row.LicensePlate,
                Latitude = row.Latitude ?? 10.7769,
                Longitude = row.Longitude ?? 106.7009,
                Status = !row.HasGps ? "fallback" : row.IsLive ? "live" : "stale",
                LastUpdatedText = row.LastUpdated.HasValue ? row.LastUpdated.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : null
            }).ToList();

            return View(new OwnerTrackingViewModel
            {
                Chrome = await BuildChromeAsync(userId, "Tracking"),
                Cars = rows,
                MapPoints = points,
                Query = q,
                GpsFilter = NormalizeFilter(gps)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Contracts()
        {
            var userId = GetCurrentUserId();
            var cars = await GetOwnerCarsQuery(userId).ToListAsync();
            var signatureProfile = await _context.OwnerDigitalSignatureProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserID == userId);

            var contracts = await _context.DigitalContracts
                .AsNoTracking()
                .Include(c => c.Booking).ThenInclude(b => b!.User)
                .Include(c => c.Booking).ThenInclude(b => b!.Car).ThenInclude(c => c!.CarBrand)
                .Include(c => c.BookingTransaction).ThenInclude(t => t!.User)
                .Include(c => c.BookingTransaction).ThenInclude(t => t!.Bookings).ThenInclude(b => b.Car).ThenInclude(c => c!.CarBrand)
                .Where(c =>
                    (c.Booking != null && c.Booking.Car != null && c.Booking.Car.OwnerID == userId)
                    || (c.BookingTransaction != null && c.BookingTransaction.Bookings.Any(b => b.Car != null && b.Car.OwnerID == userId)))
                .OrderByDescending(c => c.CreatedAt)
                .Take(80)
                .ToListAsync();

            return View(new OwnerContractsViewModel
            {
                Chrome = await BuildChromeAsync(userId, "Contracts"),
                SignatureProfile = signatureProfile == null ? null : new OwnerDigitalSignatureProfileViewModel
                {
                    ProviderName = signatureProfile.ProviderName,
                    CertificateSerialNumber = signatureProfile.CertificateSerialNumber,
                    CertificateSubject = signatureProfile.CertificateSubject,
                    CertificateValidUntil = signatureProfile.CertificateValidUntil,
                    Status = signatureProfile.Status,
                    SignatureImageUrl = signatureProfile.SignatureImageUrl,
                    IsReady = signatureProfile.IsReady
                },
                Cars = cars.Select(car => new OwnerContractCarModeViewModel
                {
                    CarID = car.CarID,
                    DisplayName = BuildCarDisplayName(car),
                    LicensePlate = car.LicensePlate,
                    ContractSigningMode = CarContractSigningModes.Normalize(car.ContractSigningMode),
                    ContractSigningModeLabel = CarContractSigningModes.DisplayName(car.ContractSigningMode),
                    CanEnableDigital = signatureProfile?.IsReady == true
                }).ToList(),
                DigitalContracts = contracts.Select(c => new OwnerDigitalContractRowViewModel
                {
                    DigitalContractID = c.DigitalContractID,
                    ContractNo = c.ContractNo,
                    BookingID = c.BookingID,
                    TransactionID = c.TransactionID,
                    CustomerName = c.Booking?.User?.Name ?? c.BookingTransaction?.User?.Name ?? c.CustomerName,
                    CarSummary = BuildContractCarSummary(c),
                    IsSignedByCustomer = c.IsSignedByCustomer,
                    CustomerSignDate = c.CustomerSignDate,
                    CreatedAt = c.CreatedAt
                }).ToList()
            });
        }

        private async Task<IActionResult> ContractsLegacy()
        {
            var userId = GetCurrentUserId();
            var carIds = await _context.Cars.AsNoTracking().Where(c => c.OwnerID == userId).Select(c => c.CarID).ToListAsync();
            var contracts = await _context.PhysicalContracts
                .AsNoTracking()
                .Include(c => c.PhysicalContractType)
                .Include(c => c.Car).ThenInclude(c => c!.CarBrand)
                .Include(c => c.Car).ThenInclude(c => c!.CarModel)
                .Where(c => c.OwnerID == userId || (c.CarID.HasValue && carIds.Contains(c.CarID.Value)))
                .OrderByDescending(c => c.UploadedAt)
                .Take(80)
                .ToListAsync();

            return View(new OwnerContractsViewModel
            {
                Chrome = await BuildChromeAsync(userId, "Contracts"),
                Templates = GetContractTemplates(),
                Contracts = contracts.Select(c => new OwnerContractRowViewModel
                {
                    PhysicalContractID = c.PhysicalContractID,
                    Title = string.IsNullOrWhiteSpace(c.Title) ? $"Tài liệu #{c.PhysicalContractID}" : c.Title,
                    TypeName = c.PhysicalContractType?.Name ?? c.SuggestedContractTypeCode ?? "Chưa phân loại",
                    Status = c.Status,
                    FileUrl = c.FileUrl,
                    CarName = c.Car != null ? BuildCarDisplayName(c.Car) : null,
                    LicensePlate = c.Car?.LicensePlate,
                    BookingID = c.BookingID,
                    UploadedAt = c.UploadedAt
                }).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSignatureProfile(OwnerDigitalSignatureProfileInputModel model, IFormFile? signatureImage)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(model.ProviderName) ||
                string.IsNullOrWhiteSpace(model.CertificateSerialNumber) ||
                string.IsNullOrWhiteSpace(model.CertificateSubject))
            {
                TempData["Error"] = "Vui lòng nhập đủ nhà cung cấp CA, số serial và chủ thể chứng thư.";
                return RedirectToAction(nameof(Contracts));
            }

            var profile = await _context.OwnerDigitalSignatureProfiles.FirstOrDefaultAsync(p => p.UserID == userId)
                ?? new OwnerDigitalSignatureProfile { UserID = userId, CreatedAt = DateTime.UtcNow };

            if (profile.OwnerDigitalSignatureProfileID == 0)
                _context.OwnerDigitalSignatureProfiles.Add(profile);

            profile.ProviderName = model.ProviderName.Trim();
            profile.CertificateSerialNumber = model.CertificateSerialNumber.Trim();
            profile.CertificateSubject = model.CertificateSubject.Trim();
            profile.CertificateValidUntil = model.CertificateValidUntil?.Date;
            profile.UpdatedAt = DateTime.UtcNow;

            if (signatureImage != null && signatureImage.Length > 0)
            {
                if (!await _fileValidation.IsAllowedImageAsync(signatureImage, 2 * 1024 * 1024))
                {
                    TempData["Error"] = "Chữ ký mẫu chỉ chấp nhận JPG, PNG, WEBP tối đa 2MB.";
                    return RedirectToAction(nameof(Contracts));
                }

                var savedSignature = await SaveOwnerSignatureAsync(userId, signatureImage);
                profile.SignatureImageUrl = savedSignature.Url;
                profile.SignatureImageHash = savedSignature.Hash;
            }

            if (string.IsNullOrWhiteSpace(profile.SignatureImageHash))
            {
                profile.Status = OwnerDigitalSignatureProfileStatuses.Pending;
                TempData["Error"] = "Vui lòng tải lên ảnh chữ ký mẫu để hoàn tất hồ sơ chữ ký số.";
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Contracts));
            }

            profile.Status = profile.CertificateValidUntil.HasValue && profile.CertificateValidUntil.Value.Date < DateTime.UtcNow.Date
                ? OwnerDigitalSignatureProfileStatuses.Pending
                : OwnerDigitalSignatureProfileStatuses.Active;
            profile.VerifiedAt = profile.Status == OwnerDigitalSignatureProfileStatuses.Active ? DateTime.UtcNow : null;

            await _context.SaveChangesAsync();
            await RecordOwnerAuditAsync("UpsertOwnerDigitalSignatureProfile", userId, "Owner updated digital signature profile.", new
            {
                profile.ProviderName,
                profile.CertificateSerialNumber,
                profile.CertificateSubject,
                profile.CertificateValidUntil,
                profile.Status,
                profile.SignatureImageHash
            });

            TempData["Success"] = profile.IsReady
                ? "Hồ sơ chữ ký số đã sẵn sàng. Bạn có thể bật hợp đồng số cho xe."
                : "Đã lưu hồ sơ chữ ký số, nhưng hồ sơ chưa sẵn sàng.";
            return RedirectToAction(nameof(Contracts));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateContractMode(int carId, string contractSigningMode)
        {
            var userId = GetCurrentUserId();
            var car = await _context.Cars.FirstOrDefaultAsync(c => c.CarID == carId && c.OwnerID == userId);
            if (car == null) return NotFound();

            var normalizedMode = CarContractSigningModes.Normalize(contractSigningMode);
            if (normalizedMode == CarContractSigningModes.Digital)
            {
                var signatureProfile = await _context.OwnerDigitalSignatureProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserID == userId);
                if (signatureProfile?.IsReady != true)
                {
                    TempData["Error"] = "Bạn cần hoàn tất hồ sơ chữ ký số trước khi bật hợp đồng số cho xe.";
                    return RedirectToAction(nameof(Contracts));
                }
            }

            var oldMode = CarContractSigningModes.Normalize(car.ContractSigningMode);
            car.ContractSigningMode = normalizedMode;
            car.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await RecordOwnerAuditAsync("UpdateCarContractSigningMode", userId, $"Owner changed car #{car.CarID} contract mode.", new
            {
                car.CarID,
                OldMode = oldMode,
                NewMode = normalizedMode
            });

            TempData["Success"] = $"Đã cập nhật {BuildCarDisplayName(car)} sang {CarContractSigningModes.DisplayName(normalizedMode)}.";
            return RedirectToAction(nameof(Contracts));
        }

        [HttpGet]
        public async Task<IActionResult> Privacy()
        {
            var userId = GetCurrentUserId();
            return View(new OwnerPrivacyViewModel
            {
                Chrome = await BuildChromeAsync(userId, "Privacy")
            });
        }

        [HttpGet]
        public async Task<IActionResult> Wallet()
        {
            var userId = GetCurrentUserId();
            var wallet = await _context.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserID == userId);
            var transactions = wallet == null
                ? new List<WalletTransaction>()
                : await _context.WalletTransactions
                    .AsNoTracking()
                    .Where(t => t.WalletID == wallet.WalletID)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(50)
                    .ToListAsync();

            var revenueStatuses = new[] { BookingStatusConstants.Completed, BookingStatusConstants.Confirmed };
            var bookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.BookingStatus)
                .Include(b => b.Car).ThenInclude(c => c!.CarBrand)
                .Include(b => b.Car).ThenInclude(c => c!.CarModel)
                .Where(b => b.Car != null && b.Car.OwnerID == userId && b.BookingStatus != null && revenueStatuses.Contains(b.BookingStatus.Name))
                .OrderByDescending(b => b.StartDate)
                .Take(120)
                .ToListAsync();

            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var bookingRows = bookings.Select(b =>
            {
                return new OwnerWalletBookingRowViewModel
                {
                    BookingID = b.BookingID,
                    CarName = BuildCarDisplayName(b.Car),
                    LicensePlate = b.Car?.LicensePlate ?? string.Empty,
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    RentalAmount = b.TotalPrice,
                    OwnerIncome = b.TotalPrice,
                    DepositAmount = ResolveBookingDepositAmount(b),
                    DepositStatus = b.DepositStatus,
                    StatusName = b.BookingStatus?.Name ?? "Unknown"
                };
            }).ToList();

            var bookingIds = bookings.Select(b => b.BookingID).ToList();
            var depositLedger = bookingIds.Count == 0
                ? new List<PaymentLedgerEntry>()
                : await _context.PaymentLedgerEntries
                    .AsNoTracking()
                    .Where(l => l.BookingID.HasValue && bookingIds.Contains(l.BookingID.Value))
                    .ToListAsync();
            var heldDeposits = depositLedger
                .Where(l => l.EntryType == PaymentLedgerEntryTypeConstants.DepositCharge && l.Status == PaymentStatusConstants.Paid)
                .Sum(l => l.Amount);
            var refundedDeposits = depositLedger
                .Where(l => l.EntryType == PaymentLedgerEntryTypeConstants.Refund && l.Status == PaymentStatusConstants.Refunded)
                .Sum(l => l.Amount);
            var depositDeductions = depositLedger
                .Where(l => l.EntryType == PaymentLedgerEntryTypeConstants.Deduction && l.Status == PaymentStatusConstants.Paid)
                .Sum(l => l.Amount);

            return View(new OwnerWalletViewModel
            {
                Chrome = await BuildChromeAsync(userId, "Wallet"),
                Balance = wallet?.Balance ?? 0m,
                LockedBalance = wallet?.LockedBalance ?? 0m,
                GrossRevenue = bookingRows.Sum(b => b.RentalAmount),
                EstimatedOwnerIncome = bookingRows.Sum(b => b.OwnerIncome),
                MonthIncome = bookingRows.Where(b => b.StartDate >= monthStart).Sum(b => b.OwnerIncome),
                HeldDeposits = heldDeposits,
                RefundedDeposits = refundedDeposits,
                DepositDeductions = depositDeductions,
                CompletedTrips = bookingRows.Count(b => b.StatusName == BookingStatusConstants.Completed),
                Transactions = transactions.Select(t => new OwnerWalletTransactionRowViewModel
                {
                    CreatedAt = t.CreatedAt,
                    Type = t.Type,
                    Description = t.Description,
                    Amount = t.Amount
                }).ToList(),
                BookingRows = bookingRows
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateCreateViewDataAsync();
            return View(new OwnerCarCreateViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> CheckLicensePlate(string? licensePlate, int? excludeCarId = null)
        {
            var normalizedPlate = LicensePlateNormalizer.Normalize(licensePlate);
            if (string.IsNullOrWhiteSpace(normalizedPlate))
                return Json(new { exists = false });

            var rawPlate = licensePlate?.Trim().ToUpperInvariant() ?? string.Empty;
            var query = _context.Cars.AsNoTracking().Where(c => !c.IsDeleted);
            if (excludeCarId.HasValue)
                query = query.Where(c => c.CarID != excludeCarId.Value);

            var exists = await query.AnyAsync(c =>
                c.LicensePlate.ToUpper() == rawPlate ||
                c.LicensePlateNormalized == normalizedPlate ||
                c.LicensePlate.Replace("-", "").Replace(".", "").Replace(" ", "").ToUpper() == normalizedPlate);

            return Json(new
            {
                exists,
                message = exists ? "Biển số xe này đã tồn tại trên hệ thống. Vui lòng kiểm tra lại trước khi gửi duyệt." : "Biển số có thể sử dụng."
            });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();
            var car = await _context.Cars
                .AsNoTracking()
                .Include(c => c.CarAmenityMappings)
                .FirstOrDefaultAsync(c => c.CarID == id && c.OwnerID == userId);

            if (car == null)
                return NotFound();

            await PopulateCreateViewDataAsync();
            ViewBag.CarId = car.CarID;
            ViewBag.HasPendingChange = await _carChanges.HasActiveRequestAsync(car.CarID);
            return PartialView("_EditModal", ToOwnerCarInputModel(car));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OwnerCarCreateViewModel model)
        {
            var userId = GetCurrentUserId();
            var car = await _context.Cars
                .Include(c => c.CarAmenityMappings)
                .FirstOrDefaultAsync(c => c.CarID == id && c.OwnerID == userId);

            if (car == null)
                return NotFound();

            if (await _carChanges.HasActiveRequestAsync(car.CarID))
                return OwnerModalError("Xe đang có yêu cầu thay đổi chờ duyệt. Vui lòng đợi duyệt xong trước khi sửa tiếp.");

            var normalizedContractMode = CarContractSigningModes.Normalize(model.ContractSigningMode);
            if (normalizedContractMode == CarContractSigningModes.Digital)
            {
                var signatureProfile = await _context.OwnerDigitalSignatureProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserID == userId);
                if (signatureProfile?.IsReady != true)
                    ModelState.AddModelError(nameof(model.ContractSigningMode), "Bạn cần hoàn tất hồ sơ chữ ký số trước khi chọn hợp đồng số.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateCreateViewDataAsync();
                ViewBag.CarId = car.CarID;
                ViewBag.HasPendingChange = false;
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return PartialView("_EditModal", model);
            }

            var pendingImageUrls = new List<string>();
            if (model.ImageFiles != null && model.ImageFiles.Count > 0)
            {
                foreach (var file in model.ImageFiles)
                {
                    if (!await _fileValidation.IsAllowedImageAsync(file, 10 * 1024 * 1024))
                        return OwnerModalError("Chỉ chấp nhận ảnh JPG, PNG, WEBP tối đa 10MB.");
                }

                foreach (var file in model.ImageFiles)
                    pendingImageUrls.Add(await SaveFileAsync(file, "images/cars/pending"));
            }

            var selectedModel = await _context.CarModels.FirstOrDefaultAsync(m => m.CarModelID == model.CarModelID);
            var carTypeId = await ResolveCarTypeIdAsync(model.Seats);
            var locationId = await ResolveLocationIdAsync(model.CityOrProvince);
            var withDriverId = await _context.ServiceTypes
                .Where(s => s.Code == ServiceTypeConstants.WithDriver)
                .Select(s => (int?)s.ServiceTypeID)
                .FirstOrDefaultAsync();
            var isWithDriver = model.ServiceTypeID == withDriverId;
            var selectedAmenities = await _context.CarAmenities
                .Where(a => model.AmenityIDs.Distinct().Contains(a.CarAmenityID))
                .OrderBy(a => a.DisplayOrder)
                .ThenBy(a => a.Name)
                .ToListAsync();

            var snapshot = new CarChangeSnapshot
            {
                LicensePlate = model.LicensePlate.Trim().ToUpperInvariant(),
                VIN = string.IsNullOrWhiteSpace(model.VIN) ? car.VIN : model.VIN.Trim().ToUpperInvariant(),
                BrandID = model.BrandID,
                CarModelID = model.CarModelID,
                CarName = selectedModel?.Name ?? car.CarName,
                Seats = model.Seats,
                ManufactureYear = model.ManufactureYear,
                Transmission = model.Transmission,
                FuelType = model.FuelType,
                FuelConsumption = model.FuelConsumption,
                Description = model.Description,
                NextInspectionDate = model.NextInspectionDate?.Date,
                ServiceTypeID = model.ServiceTypeID,
                PricePerDay = model.PricePerDay,
                WeeklyDiscountPercent = model.WeeklyDiscountPercent,
                MonthlyDiscountPercent = model.MonthlyDiscountPercent,
                DeliveryEnabled = model.DeliveryEnabled,
                MaxDeliveryDistanceKm = model.MaxDeliveryDistanceKm,
                DeliveryFeePerKm = model.DeliveryFeePerKm,
                FreeDeliveryRadiusKm = model.FreeDeliveryRadiusKm,
                MileageLimitEnabled = model.MileageLimitEnabled,
                DailyMileageLimitKm = model.DailyMileageLimitKm,
                ExtraMileageFeePerKm = model.ExtraMileageFeePerKm,
                RentalTerms = model.RentalTerms,
                ContractSigningMode = normalizedContractMode,
                LocationID = locationId,
                CarAddress = model.CarAddress,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                CarTypeID = carTypeId,
                IsSelfDriveAllowed = !isWithDriver,
                IsWithDriverAllowed = isWithDriver,
                TransportPermitStatus = isWithDriver ? "Pending" : "NotRequired",
                CommissionRate = NormalizeCommissionRate(car.CommissionRate),
                AmenityIDs = selectedAmenities.Select(a => a.CarAmenityID).ToList(),
                ImageUrls = pendingImageUrls
            };

            await _carChanges.CreateAsync(car, CarChangeOperationConstants.Update, userId, snapshot, "Chỉnh sửa xe");
            TempData["Success"] = "Bản sửa xe đã được gửi cho Staff khu vực, Manager khu vực và Admin duyệt.";
            return Json(new { success = true, message = TempData["Success"] });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            var car = await _context.Cars.FirstOrDefaultAsync(c => c.CarID == id && c.OwnerID == userId);
            if (car == null)
                return NotFound();

            if (await _carChanges.HasActiveRequestAsync(car.CarID))
                return OwnerModalError("Xe đang có yêu cầu thay đổi chờ duyệt. Vui lòng đợi duyệt xong trước khi xóa.");

            car.IsPendingDeletion = true;
            car.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _carChanges.CreateAsync(car, CarChangeOperationConstants.Delete, userId, null, "Yêu cầu xóa xe");
            TempData["Success"] = "Yêu cầu xóa xe đã được gửi duyệt. Xe tạm ngưng nhận booking mới.";
            return Json(new { success = true, message = TempData["Success"] });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OwnerCarCreateViewModel model)
        {
            var initialStep = 1;
            if (string.IsNullOrWhiteSpace(model.CarAddress))
            {
                if (!ModelState.TryGetValue(nameof(model.CarAddress), out var addressState) || !addressState.Errors.Any())
                {
                    ModelState.AddModelError(nameof(model.CarAddress), "Vui lòng nhập địa chỉ giao nhận xe.");
                }
                initialStep = 2;
            }

            if (!model.Latitude.HasValue || !model.Longitude.HasValue)
            {
                if (!ModelState.TryGetValue(nameof(model.Latitude), out var latitudeState) || !latitudeState.Errors.Any())
                {
                    ModelState.AddModelError(nameof(model.Latitude), "Vui lòng chọn vị trí xe trên bản đồ.");
                }
                initialStep = 2;
            }

            if (model.ImageFiles == null || model.ImageFiles.Count < 4)
            {
                ModelState.AddModelError(nameof(model.ImageFiles), "Vui lòng upload tối thiểu 4 ảnh xe.");
                if (initialStep == 1)
                {
                    initialStep = 3;
                }
            }

            if (!model.NextInspectionDate.HasValue)
            {
                initialStep = 1;
            }

            var normalizedContractMode = CarContractSigningModes.Normalize(model.ContractSigningMode);
            if (normalizedContractMode == CarContractSigningModes.Digital)
            {
                var currentUserId = GetCurrentUserId();
                var signatureProfile = await _context.OwnerDigitalSignatureProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserID == currentUserId);
                if (signatureProfile?.IsReady != true)
                {
                    ModelState.AddModelError(nameof(model.ContractSigningMode), "Bạn cần hoàn tất hồ sơ chữ ký số trước khi chọn hợp đồng số.");
                    initialStep = 2;
                }
            }

            var normalizedLicensePlate = model.LicensePlate?.Trim().ToUpperInvariant() ?? string.Empty;
            var normalizedLicensePlateKey = LicensePlateNormalizer.Normalize(normalizedLicensePlate);
            var normalizedVin = string.IsNullOrWhiteSpace(model.VIN) ? null : model.VIN.Trim().ToUpperInvariant();
            model.LicensePlate = normalizedLicensePlate;
            model.VIN = normalizedVin;

            if (!string.IsNullOrWhiteSpace(normalizedLicensePlate) &&
                await _context.Cars.AsNoTracking().AnyAsync(c =>
                    c.LicensePlate == normalizedLicensePlate ||
                    c.LicensePlateNormalized == normalizedLicensePlateKey ||
                    c.LicensePlate.Replace("-", "").Replace(".", "").Replace(" ", "").ToUpper() == normalizedLicensePlateKey))
            {
                ModelState.AddModelError(nameof(model.LicensePlate), "Biển số xe này đã tồn tại trên hệ thống.");
                initialStep = 1;
            }

            if (!string.IsNullOrWhiteSpace(normalizedVin) &&
                await _context.Cars.AsNoTracking().AnyAsync(c => c.VIN == normalizedVin))
            {
                ModelState.AddModelError(nameof(model.VIN), "Số VIN này đã tồn tại trên hệ thống.");
                initialStep = 1;
            }

            if (!ModelState.IsValid)
            {
                await PopulateCreateViewDataAsync();
                ViewBag.InitialStep = initialStep;
                return View(model);
            }

            foreach (var file in model.ImageFiles!)
            {
                if (!await _fileValidation.IsAllowedImageAsync(file, 10 * 1024 * 1024))
                {
                    ModelState.AddModelError(nameof(model.ImageFiles), "Chỉ chấp nhận ảnh JPG, PNG, WEBP tối đa 10MB.");
                    await PopulateCreateViewDataAsync();
                    ViewBag.InitialStep = 3;
                    return View(model);
                }
            }

            var userId = GetCurrentUserId();
            var pendingStatusId = await _context.CarStatuses
                .Where(s => s.Name == "Pending")
                .Select(s => (int?)s.CarStatusID)
                .FirstOrDefaultAsync()
                ?? await _context.CarStatuses.Select(s => s.CarStatusID).FirstAsync();

            var selfDriveId = await _context.ServiceTypes
                .Where(s => s.Code == ServiceTypeConstants.SelfDrive)
                .Select(s => (int?)s.ServiceTypeID)
                .FirstOrDefaultAsync();
            var withDriverId = await _context.ServiceTypes
                .Where(s => s.Code == ServiceTypeConstants.WithDriver)
                .Select(s => (int?)s.ServiceTypeID)
                .FirstOrDefaultAsync();

            var isWithDriver = model.ServiceTypeID == withDriverId;
            var selectedModel = await _context.CarModels.FirstOrDefaultAsync(m => m.CarModelID == model.CarModelID);
            var carTypeId = await ResolveCarTypeIdAsync(model.Seats);
            var selectedAmenities = await _context.CarAmenities
                .Where(a => model.AmenityIDs.Distinct().Contains(a.CarAmenityID))
                .OrderBy(a => a.DisplayOrder)
                .ThenBy(a => a.Name)
                .ToListAsync();

            var car = new Car
            {
                OwnerID = userId,
                CarName = selectedModel?.Name ?? "Mẫu xe",
                BrandID = model.BrandID,
                CarModelID = model.CarModelID,
                Seats = model.Seats,
                LicensePlate = normalizedLicensePlate,
                VIN = normalizedVin ?? GeneratePlaceholderVin(),
                PricePerDay = model.PricePerDay,
                PricePerHour = Math.Max(1, Math.Round(model.PricePerDay / 8m)),
                CarStatusID = pendingStatusId,
                Description = model.Description,
                FuelType = model.FuelType,
                Transmission = model.Transmission,
                FuelConsumption = model.FuelConsumption,
                CarTypeID = carTypeId,
                ServiceTypeID = model.ServiceTypeID ?? selfDriveId,
                IsSelfDriveAllowed = !isWithDriver,
                IsWithDriverAllowed = isWithDriver,
                TransportPermitStatus = isWithDriver ? "Pending" : "NotRequired",
                IsApproved = false,
                ApprovalStatus = CarApprovalStatusConstants.Pending,
                ApprovalRejectionReason = null,
                LocationID = await ResolveLocationIdAsync(model.CityOrProvince),
                CarAddress = model.CarAddress,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                NextInspectionDate = model.NextInspectionDate?.Date,
                Features = string.Join(", ", selectedAmenities.Select(a => a.Name)),
                CurrentOdometer = 0,
                CommissionRate = DefaultCommissionRate,
                WeeklyDiscountPercent = model.WeeklyDiscountPercent,
                MonthlyDiscountPercent = model.MonthlyDiscountPercent,
                DeliveryEnabled = model.DeliveryEnabled,
                MaxDeliveryDistanceKm = model.MaxDeliveryDistanceKm,
                DeliveryFeePerKm = model.DeliveryFeePerKm,
                FreeDeliveryRadiusKm = model.FreeDeliveryRadiusKm,
                MileageLimitEnabled = model.MileageLimitEnabled,
                DailyMileageLimitKm = model.DailyMileageLimitKm,
                ExtraMileageFeePerKm = model.ExtraMileageFeePerKm,
                RentalTerms = model.RentalTerms,
                ContractSigningMode = normalizedContractMode,
                CreatedAt = DateTime.UtcNow
            };

            await using var createTransaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;

            _context.Cars.Add(car);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueCarIdentityConflict(ex))
            {
                if (createTransaction != null)
                    await createTransaction.RollbackAsync();

                _context.Entry(car).State = EntityState.Detached;
                ModelState.AddModelError(nameof(model.LicensePlate), "Biển số hoặc số VIN đã tồn tại trên hệ thống. Vui lòng kiểm tra lại thông tin xe.");
                await PopulateCreateViewDataAsync();
                ViewBag.InitialStep = 1;
                return View(model);
            }

            var savedUrls = new List<string>();
            foreach (var image in model.ImageFiles!)
            {
                var url = await SaveFileAsync(image, "images/cars");
                savedUrls.Add(url);
                _context.CarImages.Add(new CarImage { CarID = car.CarID, ImageUrl = url });
            }
            car.ImageURL = savedUrls.FirstOrDefault();

            foreach (var amenityId in selectedAmenities.Select(a => a.CarAmenityID))
            {
                _context.CarAmenityMappings.Add(new CarAmenityMapping
                {
                    CarID = car.CarID,
                    CarAmenityID = amenityId
                });
            }

            await _context.SaveChangesAsync();
            CarChangeRequest approvalRequest;
            try
            {
                approvalRequest = await _carChanges.CreateAsync(
                    car,
                    CarChangeOperationConstants.Create,
                    userId,
                    BuildSnapshotFromCar(car, selectedAmenities.Select(a => a.CarAmenityID), savedUrls),
                    "Đăng ký xe mới");

                if (createTransaction != null)
                    await createTransaction.CommitAsync();
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
            {
                if (createTransaction != null)
                    await createTransaction.RollbackAsync();

                ModelState.AddModelError(string.Empty, "Xe chưa gửi được vào hàng đợi duyệt. Vui lòng thử lại để hệ thống ghi nhận yêu cầu duyệt.");
                await PopulateCreateViewDataAsync();
                ViewBag.InitialStep = 3;
                return View(model);
            }

            TempData["Success"] = $"{BuildPendingApprovalLabel(approvalRequest)} lúc {approvalRequest.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}.";
            return RedirectToAction(nameof(Index));
        }

        private static string BuildContractCarSummary(DigitalContract contract)
        {
            if (contract.Booking != null)
                return $"{contract.Booking.Car?.CarBrand?.Name} {contract.Booking.Car?.CarName}".Trim();

            if (contract.BookingTransaction?.Bookings != null && contract.BookingTransaction.Bookings.Any())
            {
                return string.Join(", ", contract.BookingTransaction.Bookings
                    .Where(b => b.Car != null)
                    .Select(b => $"{b.Car?.CarBrand?.Name} {b.Car?.CarName}".Trim()));
            }

            return contract.CarModel;
        }

        private async Task<(string Url, string Hash)> SaveOwnerSignatureAsync(int userId, IFormFile file)
        {
            var folder = Path.Combine(_environment.ContentRootPath, "App_Data", "SecureUploads", "OwnerSignatures", userId.ToString());
            Directory.CreateDirectory(folder);

            await using var input = file.OpenReadStream();
            using var memory = new MemoryStream();
            await input.CopyToAsync(memory);
            var bytes = memory.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var path = Path.Combine(folder, fileName);
            await System.IO.File.WriteAllBytesAsync(path, bytes);

            return ($"/SecureFile/GetSecureImage?folder=OwnerSignatures&fileName={userId}/{fileName}", hash);
        }

        private async Task RecordOwnerAuditAsync(string action, int userId, string description, object values)
        {
            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = action,
                EntityType = "OwnerContracts",
                EntityId = userId,
                UserId = userId,
                UserName = User.Identity?.Name,
                Description = description,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                RequestUrl = $"{Request.Method} {Request.Path}{Request.QueryString}",
                NewValues = JsonSerializer.Serialize(values),
                Timestamp = DateTime.UtcNow
            });
        }

        private IQueryable<Car> GetOwnerCarsQuery(int userId)
        {
            return _context.Cars
                .AsNoTracking()
                .Include(c => c.CarBrand)
                .Include(c => c.CarModel)
                .Include(c => c.CarStatus)
                .Include(c => c.CarImages)
                .Where(c => c.OwnerID == userId)
                .OrderByDescending(c => c.CreatedAt);
        }

        private static IEnumerable<Car> ApplyOwnerCarFilters(IEnumerable<Car> cars, string? q, string service, string status)
        {
            var query = cars;
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(c => MatchesCarSearch(c, q));

            query = NormalizeFilter(service) switch
            {
                "selfdrive" => query.Where(c => c.IsSelfDriveAllowed),
                "driver" => query.Where(c => c.IsWithDriverAllowed),
                _ => query
            };

            query = NormalizeFilter(status) switch
            {
                "approved" => query.Where(c => c.IsApproved),
                "pending" => query.Where(c => !c.IsApproved),
                "available" => query.Where(c => string.Equals(c.CarStatus?.Name, CarStatusConstants.Available, StringComparison.OrdinalIgnoreCase)),
                "rented" => query.Where(c => string.Equals(c.CarStatus?.Name, CarStatusConstants.Rented, StringComparison.OrdinalIgnoreCase)),
                "maintenance" => query.Where(c => string.Equals(c.CarStatus?.Name, CarStatusConstants.Maintenance, StringComparison.OrdinalIgnoreCase)),
                _ => query
            };

            return query;
        }

        private static bool MatchesCarSearch(Car car, string value)
        {
            var keyword = value.Trim();
            return Contains(car.CarName, keyword)
                || Contains(car.LicensePlate, keyword)
                || Contains(car.CarBrand?.Name, keyword)
                || Contains(car.CarModel?.Name, keyword);
        }

        private static bool Contains(string? source, string value)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<OwnerPortalChromeViewModel> BuildChromeAsync(int userId, string activeTab)
        {
            var walletBalance = await _context.Wallets
                .AsNoTracking()
                .Where(w => w.UserID == userId)
                .Select(w => (decimal?)w.Balance)
                .FirstOrDefaultAsync() ?? 0m;
            var carCount = await _context.Cars.AsNoTracking().CountAsync(c => c.OwnerID == userId);

            return new OwnerPortalChromeViewModel
            {
                ActiveTab = activeTab,
                WalletBalance = walletBalance,
                CarsCount = carCount,
                Tabs = GetOwnerTabs()
                    .Prepend(new OwnerPortalTabViewModel("Bookings", "Đơn thuê", "Bookings", "bi-receipt"))
                    .ToList()
            };
        }

        private static IReadOnlyList<OwnerPortalTabViewModel> GetOwnerTabs()
        {
            return new[]
            {
                new OwnerPortalTabViewModel("Cars", "Danh sách xe", "Index", "bi-car-front"),
                new OwnerPortalTabViewModel("Calendar", "Lịch xe", "Calendar", "bi-calendar3"),
                new OwnerPortalTabViewModel("Guide", "Cẩm nang cho thuê", "Guide", "bi-journal-check"),
                new OwnerPortalTabViewModel("Fines", "Tra cứu phạt nguội", "Fines", "bi-shield-exclamation"),
                new OwnerPortalTabViewModel("TaxInfo", "Thông tin khai thuế", "TaxInfo", "bi-receipt"),
                new OwnerPortalTabViewModel("Tracking", "Định vị", "Tracking", "bi-geo-alt"),
                new OwnerPortalTabViewModel("Contracts", "Hợp đồng & Chứng từ", "Contracts", "bi-file-earmark-text"),
                new OwnerPortalTabViewModel("Privacy", "Chính sách dữ liệu", "Privacy", "bi-lock"),
                new OwnerPortalTabViewModel("Wallet", "Ví của tôi", "Wallet", "bi-wallet2")
            };
        }

        private async Task<Booking?> LoadOwnerBookingAsync(int bookingId, int ownerUserId, bool asTracking)
        {
            var query = asTracking ? _context.Bookings.AsQueryable() : _context.Bookings.AsNoTracking();
            return await query
                .Include(b => b.BookingStatus)
                .Include(b => b.User)
                .Include(b => b.Payment)
                .Include(b => b.Car).ThenInclude(c => c!.CarBrand)
                .Include(b => b.Car).ThenInclude(c => c!.CarModel)
                .Include(b => b.HandoverRecords).ThenInclude(h => h.InventoryItems)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.Car != null && b.Car.OwnerID == ownerUserId);
        }

        private static OwnerBookingRowViewModel ToOwnerBookingRow(Booking booking)
        {
            var statusName = booking.BookingStatus?.Name ?? "Unknown";
            return new OwnerBookingRowViewModel
            {
                BookingID = booking.BookingID,
                CarID = booking.CarID,
                CarName = BuildCarDisplayName(booking.Car),
                LicensePlate = booking.Car?.LicensePlate ?? string.Empty,
                CustomerName = booking.User?.Name ?? "Khách thuê",
                CustomerPhone = booking.User?.Phone ?? "Đã ẩn",
                StartDate = booking.StartDate,
                EndDate = booking.EndDate,
                StatusName = statusName,
                StatusClass = GetStatusClass(statusName),
                TotalPrice = booking.TotalPrice,
                DepositAmount = ResolveBookingDepositAmount(booking),
                DepositStatus = booking.DepositStatus,
                CanDecide = statusName == BookingStatusConstants.Pending
            };
        }

        private static decimal ResolveBookingDepositAmount(Booking booking)
        {
            if (booking.DepositAmount > 0) return booking.DepositAmount;
            if (booking.ReservationFee > 0) return booking.ReservationFee;
            return DepositPolicy.Calculate(booking.TotalPrice);
        }

        private static string NormalizeOwnerBookingTab(string? tab)
        {
            return NormalizeFilter(tab) switch
            {
                "current" => "current",
                "history" => "history",
                _ => "pending"
            };
        }

        private static bool IsPendingOwnerBooking(OwnerBookingRowViewModel row) =>
            row.StatusName == BookingStatusConstants.Pending;

        private static bool IsCurrentOwnerBooking(OwnerBookingRowViewModel row) =>
            row.StatusName is BookingStatusConstants.Confirmed or BookingStatusConstants.Active;

        private static bool IsHistoryOwnerBooking(OwnerBookingRowViewModel row) =>
            row.StatusName is BookingStatusConstants.Completed
                or BookingStatusConstants.Cancelled
                or BookingStatusConstants.Rejected
                or BookingStatusConstants.Expired;

        private async Task<Dictionary<int, CarChangeRequest>> LoadActiveChangeRequestsAsync(IReadOnlyCollection<int> carIds)
        {
            if (carIds.Count == 0) return new Dictionary<int, CarChangeRequest>();

            var activeStatuses = new[]
            {
                CarChangeRequestStatusConstants.PendingStaff,
                CarChangeRequestStatusConstants.PendingManager,
                CarChangeRequestStatusConstants.PendingAdmin
            };

            var requests = await _context.CarChangeRequests
                .AsNoTracking()
                .Where(request => carIds.Contains(request.CarID) && activeStatuses.Contains(request.Status))
                .OrderByDescending(request => request.CreatedAt)
                .ToListAsync();

            return requests
                .GroupBy(request => request.CarID)
                .ToDictionary(group => group.Key, group => group.First());
        }

        private async Task<List<OwnerBookingProjection>> LoadOwnerBookingRowsAsync(IReadOnlyCollection<int> carIds)
        {
            if (carIds.Count == 0) return new List<OwnerBookingProjection>();

            return await _context.Bookings
                .AsNoTracking()
                .Include(b => b.BookingStatus)
                .Where(b => carIds.Contains(b.CarID))
                .Select(b => new OwnerBookingProjection
                {
                    BookingID = b.BookingID,
                    CarID = b.CarID,
                    StatusName = b.BookingStatus != null ? b.BookingStatus.Name : "Unknown",
                    TotalPrice = b.TotalPrice,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();
        }

        private async Task<Dictionary<int, VehicleTracking>> LoadLatestTrackingAsync(IReadOnlyCollection<int> carIds)
        {
            if (carIds.Count == 0) return new Dictionary<int, VehicleTracking>();

            var recentTrackings = await _context.VehicleTrackings
                .AsNoTracking()
                .Where(t => carIds.Contains(t.CarID))
                .OrderByDescending(t => t.Timestamp)
                .Take(Math.Max(200, carIds.Count * 4))
                .ToListAsync();

            return recentTrackings
                .GroupBy(t => t.CarID)
                .ToDictionary(g => g.Key, g => g.First());
        }

        private async Task<HashSet<int>> LoadOwnerContractCarIdsAsync(int userId, IReadOnlyCollection<int> carIds)
        {
            if (carIds.Count == 0) return new HashSet<int>();

            var ids = await _context.PhysicalContracts
                .AsNoTracking()
                .Where(c => c.OwnerID == userId || (c.CarID.HasValue && carIds.Contains(c.CarID.Value)))
                .Select(c => c.CarID)
                .ToListAsync();

            return ids.Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        }

        private static OwnerCarOptionViewModel ToOwnerCarOption(Car car)
        {
            return new OwnerCarOptionViewModel
            {
                CarID = car.CarID,
                Name = BuildCarDisplayName(car),
                LicensePlate = car.LicensePlate,
                Brand = car.CarBrand?.Name ?? string.Empty
            };
        }

        private static string BuildCarDisplayName(Car? car)
        {
            if (car == null) return "Xe";
            var name = $"{car.CarBrand?.Name} {car.CarModel?.Name}".Trim();
            return string.IsNullOrWhiteSpace(name) ? car.CarName : name;
        }

        private static bool IsRevenueStatus(string? status)
        {
            return status is BookingStatusConstants.Completed or BookingStatusConstants.Confirmed;
        }

        private static string GetStatusClass(string? status)
        {
            return status switch
            {
                BookingStatusConstants.Active => "active",
                BookingStatusConstants.Confirmed => "confirmed",
                BookingStatusConstants.Completed => "completed",
                BookingStatusConstants.Cancelled => "cancelled",
                BookingStatusConstants.Pending => "pending",
                _ => "neutral"
            };
        }

        private static string NormalizeFilter(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "all" : value.Trim().ToLowerInvariant();
        }

        private static string? BuildPendingApprovalLabel(CarChangeRequest? request)
        {
            if (request == null)
                return null;

            return request.AreaID.HasValue
                ? "Đã gửi duyệt - chờ Staff/Manager khu vực duyệt"
                : "Đã gửi duyệt - chờ Admin duyệt do chưa xác định khu vực";
        }

        private static decimal NormalizeCommissionRate(decimal rate)
        {
            return Math.Clamp(rate, 0m, 100m);
        }

        private static decimal CalculateCommission(decimal amount, decimal rate)
        {
            return Math.Round(
                amount * NormalizeCommissionRate(rate) / 100m,
                0,
                MidpointRounding.AwayFromZero);
        }

        private static decimal CalculateOwnerIncome(decimal amount, decimal rate)
        {
            return Math.Max(0m, amount - CalculateCommission(amount, rate));
        }

        private static IReadOnlyList<OwnerCarActionItemViewModel> BuildOwnerCarActionItems(
            Car car,
            bool hasEnoughImages,
            bool hasRequiredDocuments,
            bool hasGps,
            InspectionBadge inspectionStatus,
            CarChangeRequest? activeChange = null)
        {
            var items = new List<OwnerCarActionItemViewModel>();

            if (activeChange != null)
            {
                items.Add(new OwnerCarActionItemViewModel(
                    "Đang chờ duyệt thay đổi",
                    $"Yêu cầu {activeChange.Operation} đang ở bước {activeChange.CurrentStage}. Vui lòng đợi Staff khu vực, Manager khu vực và Admin xử lý xong trước khi gửi thay đổi mới.",
                    "info",
                    "bi-hourglass-split"));
            }

            if (car.IsPendingDeletion)
            {
                items.Add(new OwnerCarActionItemViewModel(
                    "Tạm ngưng nhận booking mới",
                    "Xe đang chờ duyệt yêu cầu xóa nên không còn nhận booking mới trong thời gian chờ duyệt.",
                    "danger",
                    "bi-trash"));
            }

            if (car.ApprovalStatus == CarApprovalStatusConstants.Rejected)
            {
                items.Add(new OwnerCarActionItemViewModel(
                    "Xử lý lý do từ chối",
                    string.IsNullOrWhiteSpace(car.ApprovalRejectionReason)
                        ? "Liên hệ Admin để biết nội dung cần bổ sung."
                        : car.ApprovalRejectionReason,
                    "danger",
                    "bi-x-circle"));
            }
            else if (!car.IsApproved)
            {
                items.Add(new OwnerCarActionItemViewModel(
                    "Theo dõi duyệt xe",
                    "Xe đang chờ Admin kiểm tra và chưa thể nhận booking.",
                    "info",
                    "bi-hourglass-split"));
            }

            if (!hasEnoughImages)
            {
                items.Add(new OwnerCarActionItemViewModel(
                    "Bổ sung ảnh xe",
                    "Cần tối thiểu 4 ảnh rõ nét để hoàn thiện hồ sơ xe.",
                    "warn",
                    "bi-images"));
            }

            if (!hasRequiredDocuments)
            {
                items.Add(new OwnerCarActionItemViewModel(
                    "Hoàn tất hồ sơ chữ ký số",
                    "Xe đang chọn hợp đồng số nhưng hồ sơ chữ ký số chưa sẵn sàng.",
                    "warn",
                    "bi-file-earmark-text"));
            }

            if (inspectionStatus.CssClass is "warn" or "danger")
            {
                items.Add(new OwnerCarActionItemViewModel(
                    "Kiểm tra đăng kiểm",
                    inspectionStatus.Label,
                    inspectionStatus.CssClass,
                    inspectionStatus.Icon));
            }

            if (!car.Latitude.HasValue || !car.Longitude.HasValue || string.IsNullOrWhiteSpace(car.CarAddress))
            {
                items.Add(new OwnerCarActionItemViewModel(
                    "Bổ sung vị trí giao nhận",
                    "Địa chỉ và tọa độ giúp khách tìm đúng điểm nhận xe.",
                    "warn",
                    "bi-geo-alt"));
            }

            if (!hasGps)
            {
                items.Add(new OwnerCarActionItemViewModel(
                    "Kết nối GPS",
                    "Chưa có tín hiệu GPS; hệ thống hiện chỉ dùng vị trí đăng ký của xe.",
                    "info",
                    "bi-broadcast-pin"));
            }

            if (items.Count == 0)
            {
                items.Add(new OwnerCarActionItemViewModel(
                    "Hồ sơ vận hành đã đầy đủ",
                    "Xe đã sẵn sàng để theo dõi và nhận booking theo trạng thái hiện tại.",
                    "ok",
                    "bi-check-circle"));
            }

            return items;
        }

        private static InspectionBadge BuildInspectionStatus(DateTime? nextInspectionDate, DateTime today)
        {
            if (!nextInspectionDate.HasValue)
            {
                return new InspectionBadge("Thiếu ngày đăng kiểm", "warn", "bi-calendar-x");
            }

            var inspectionDate = nextInspectionDate.Value.Date;
            var daysLeft = (inspectionDate - today).Days;
            if (daysLeft < 0)
            {
                return new InspectionBadge($"Quá hạn đăng kiểm {Math.Abs(daysLeft)} ngày", "danger", "bi-exclamation-triangle");
            }

            if (daysLeft <= 30)
            {
                return new InspectionBadge($"Đăng kiểm còn {daysLeft} ngày", "warn", "bi-calendar-event");
            }

            return new InspectionBadge($"Đăng kiểm: {inspectionDate:dd/MM/yyyy}", "ok", "bi-shield-check");
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

        private static IReadOnlyList<OwnerDocumentViewModel> GetGuideDocuments()
        {
            return new[]
            {
                new OwnerDocumentViewModel
                {
                    Title = "Cẩm nang cho thuê xe an toàn",
                    Description = "Tài liệu DriveGo về chuẩn bị hồ sơ, đăng xe, bàn giao, theo dõi chuyến đi và xử lý sự cố.",
                    Url = "/docs/owner/drivego-cam-nang-cho-thue-xe-an-toan.pdf",
                    Icon = "bi-journal-richtext",
                    FileType = "PDF"
                },
                new OwnerDocumentViewModel
                {
                    Title = "Quy trình và biên bản bàn giao xe",
                    Description = "Mẫu ghi nhận tình trạng xe, tài sản thế chấp, km và nhiên liệu khi giao/trả.",
                    Url = "/docs/owner/bien-ban-ban-giao-xe.docx",
                    Icon = "bi-clipboard-check",
                    FileType = "DOCX"
                },
                new OwnerDocumentViewModel
                {
                    Title = "Cẩm nang quyết toán thuế",
                    Description = "Tài liệu tham khảo cho cá nhân kinh doanh qua nền tảng thương mại điện tử.",
                    Url = "/docs/owner/khai-thue-cnkd-tmdt.pdf",
                    Icon = "bi-receipt-cutoff",
                    FileType = "PDF"
                }
            };
        }

        private static IReadOnlyList<OwnerDocumentViewModel> GetContractTemplates()
        {
            return new[]
            {
                new OwnerDocumentViewModel
                {
                    Title = "Hợp đồng cho thuê xe tự lái",
                    Description = "Mẫu hợp đồng để chủ xe và khách thuê rà soát, ký và lưu hồ sơ.",
                    Url = "/docs/owner/hop-dong-thue-xe-tu-lai.docx",
                    Icon = "bi-file-earmark-word",
                    FileType = "DOCX"
                },
                new OwnerDocumentViewModel
                {
                    Title = "Biên bản bàn giao xe",
                    Description = "Mẫu biên bản giao nhận, hoàn trả và ghi nhận tình trạng xe.",
                    Url = "/docs/owner/bien-ban-ban-giao-xe.docx",
                    Icon = "bi-clipboard-check",
                    FileType = "DOCX"
                }
            };
        }

        private async Task PopulateCreateViewDataAsync()
        {
            ViewBag.Brands = await _context.CarBrands.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
            ViewBag.CarModels = await _context.CarModels.AsNoTracking().OrderBy(m => m.Name).ToListAsync();
            ViewBag.ServiceTypes = await _context.ServiceTypes.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.ServiceTypeID).ToListAsync();
            ViewBag.Amenities = await _context.CarAmenities.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.DisplayOrder).ToListAsync();
        }

        private async Task<int> ResolveCarTypeIdAsync(int seats)
        {
            var all = await _context.CarTypes.OrderBy(t => t.CarTypeID).ToListAsync();
            var name = seats >= 7 ? "SUV" : "Sedan";
            return all.FirstOrDefault(t => t.Name.Contains(name, StringComparison.OrdinalIgnoreCase))?.CarTypeID
                   ?? all.First().CarTypeID;
        }

        private async Task<int> ResolveLocationIdAsync(string? province)
        {
            var query = _context.Locations.OrderBy(l => l.LocationID).AsQueryable();
            if (!string.IsNullOrWhiteSpace(province))
            {
                var match = await query.FirstOrDefaultAsync(l => l.Name.Contains(province));
                if (match != null) return match.LocationID;
            }
            return await query.Select(l => l.LocationID).FirstAsync();
        }

        private int GetCurrentUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(id, out var userId)) throw new InvalidOperationException("User id is missing.");
            return userId;
        }

        private static string GeneratePlaceholderVin()
        {
            return $"CRS{Guid.NewGuid():N}"[..17].ToUpperInvariant();
        }

        private static bool IsUniqueCarIdentityConflict(DbUpdateException exception)
        {
            var message = exception.InnerException?.Message ?? exception.Message;
            return message.Contains("IX_Cars_LicensePlate", StringComparison.OrdinalIgnoreCase)
                || message.Contains("IX_Cars_VIN", StringComparison.OrdinalIgnoreCase)
                || message.Contains("LicensePlate", StringComparison.OrdinalIgnoreCase)
                || message.Contains("VIN", StringComparison.OrdinalIgnoreCase);
        }

        private IActionResult OwnerModalError(string message)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message });
        }

        private static OwnerCarCreateViewModel ToOwnerCarInputModel(Car car)
        {
            return new OwnerCarCreateViewModel
            {
                LicensePlate = car.LicensePlate,
                VIN = car.VIN,
                BrandID = car.BrandID,
                CarModelID = car.CarModelID,
                Seats = car.Seats,
                Transmission = car.Transmission,
                FuelType = car.FuelType,
                FuelConsumption = car.FuelConsumption,
                Description = car.Description,
                NextInspectionDate = car.NextInspectionDate,
                ServiceTypeID = car.ServiceTypeID,
                PricePerDay = car.PricePerDay,
                WeeklyDiscountPercent = car.WeeklyDiscountPercent,
                MonthlyDiscountPercent = car.MonthlyDiscountPercent,
                DeliveryEnabled = car.DeliveryEnabled,
                MaxDeliveryDistanceKm = car.MaxDeliveryDistanceKm,
                DeliveryFeePerKm = car.DeliveryFeePerKm,
                FreeDeliveryRadiusKm = car.FreeDeliveryRadiusKm,
                MileageLimitEnabled = car.MileageLimitEnabled,
                DailyMileageLimitKm = car.DailyMileageLimitKm,
                ExtraMileageFeePerKm = car.ExtraMileageFeePerKm,
                RentalTerms = car.RentalTerms,
                ContractSigningMode = CarContractSigningModes.Normalize(car.ContractSigningMode),
                CarAddress = car.CarAddress,
                Latitude = car.Latitude,
                Longitude = car.Longitude,
                AmenityIDs = car.CarAmenityMappings
                    .Where(mapping => !mapping.IsDeleted)
                    .Select(mapping => mapping.CarAmenityID)
                    .ToList()
            };
        }

        private static CarChangeSnapshot BuildSnapshotFromCar(Car car, IEnumerable<int> amenityIds, IEnumerable<string> imageUrls)
        {
            return new CarChangeSnapshot
            {
                LicensePlate = car.LicensePlate,
                VIN = car.VIN,
                BrandID = car.BrandID,
                CarModelID = car.CarModelID,
                CarName = car.CarName,
                Seats = car.Seats,
                Transmission = car.Transmission,
                FuelType = car.FuelType,
                FuelConsumption = car.FuelConsumption,
                Description = car.Description,
                NextInspectionDate = car.NextInspectionDate,
                ServiceTypeID = car.ServiceTypeID,
                PricePerDay = car.PricePerDay,
                WeeklyDiscountPercent = car.WeeklyDiscountPercent,
                MonthlyDiscountPercent = car.MonthlyDiscountPercent,
                DeliveryEnabled = car.DeliveryEnabled,
                MaxDeliveryDistanceKm = car.MaxDeliveryDistanceKm,
                DeliveryFeePerKm = car.DeliveryFeePerKm,
                FreeDeliveryRadiusKm = car.FreeDeliveryRadiusKm,
                MileageLimitEnabled = car.MileageLimitEnabled,
                DailyMileageLimitKm = car.DailyMileageLimitKm,
                ExtraMileageFeePerKm = car.ExtraMileageFeePerKm,
                RentalTerms = car.RentalTerms,
                ContractSigningMode = CarContractSigningModes.Normalize(car.ContractSigningMode),
                LocationID = car.LocationID,
                CarAddress = car.CarAddress,
                Latitude = car.Latitude,
                Longitude = car.Longitude,
                CarTypeID = car.CarTypeID,
                IsSelfDriveAllowed = car.IsSelfDriveAllowed,
                IsWithDriverAllowed = car.IsWithDriverAllowed,
                TransportPermitStatus = car.TransportPermitStatus,
                CommissionRate = NormalizeCommissionRate(car.CommissionRate),
                AmenityIDs = amenityIds.Distinct().ToList(),
                ImageUrls = imageUrls.Where(url => !string.IsNullOrWhiteSpace(url)).ToList()
            };
        }

        private static async Task<string> SaveFileAsync(IFormFile file, string relativeFolder)
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(folder);
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(folder, fileName);
            await using var stream = System.IO.File.Create(path);
            await file.CopyToAsync(stream);
            return $"/{relativeFolder}/{fileName}".Replace("\\", "/");
        }

        private sealed class OwnerBookingProjection
        {
            public int BookingID { get; set; }
            public int CarID { get; set; }
            public string StatusName { get; set; } = string.Empty;
            public decimal TotalPrice { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private sealed record InspectionBadge(string Label, string CssClass, string Icon);
    }

    public class OwnerCarCreateViewModel
    {
        public string LicensePlate { get; set; } = string.Empty;
        public string? VIN { get; set; }
        public int BrandID { get; set; }
        public int? CarModelID { get; set; }
        public int Seats { get; set; } = 4;
        public int ManufactureYear { get; set; } = DateTime.UtcNow.Year;
        public string Transmission { get; set; } = "Tự động";
        public string FuelType { get; set; } = "Xăng";
        public string? FuelConsumption { get; set; }
        public string? Description { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập ngày đăng kiểm tiếp theo.")]
        public DateTime? NextInspectionDate { get; set; }
        public int? ServiceTypeID { get; set; }
        public decimal PricePerDay { get; set; } = 440000m;
        public decimal WeeklyDiscountPercent { get; set; } = 5m;
        public decimal MonthlyDiscountPercent { get; set; }
        public bool DeliveryEnabled { get; set; }
        public decimal MaxDeliveryDistanceKm { get; set; } = 20m;
        public decimal DeliveryFeePerKm { get; set; } = 10000m;
        public decimal FreeDeliveryRadiusKm { get; set; }
        public bool MileageLimitEnabled { get; set; } = true;
        public decimal DailyMileageLimitKm { get; set; } = 400m;
        public decimal ExtraMileageFeePerKm { get; set; } = 3000m;
        public string? RentalTerms { get; set; }
        public string ContractSigningMode { get; set; } = CarContractSigningModes.PhysicalSelfManaged;
        public string? CityOrProvince { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
        public string? Street { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao nhận xe.")]
        public string? CarAddress { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn vị trí xe trên bản đồ.")]
        public double? Latitude { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn vị trí xe trên bản đồ.")]
        public double? Longitude { get; set; }
        public List<int> AmenityIDs { get; set; } = new();
        public List<IFormFile>? ImageFiles { get; set; }
    }
}
