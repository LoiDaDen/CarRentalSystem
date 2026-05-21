using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class OwnerCarsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileUploadValidationService _fileValidation;

        public OwnerCarsController(ApplicationDbContext context, IFileUploadValidationService fileValidation)
        {
            _context = context;
            _fileValidation = fileValidation;
        }

        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var cars = await _context.Cars
                .Include(c => c.CarBrand)
                .Include(c => c.CarModel)
                .Include(c => c.CarStatus)
                .Include(c => c.CarImages)
                .Where(c => c.OwnerID == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(cars);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateCreateViewDataAsync();
            return View(new OwnerCarCreateViewModel());
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
                LicensePlate = model.LicensePlate.Trim().ToUpperInvariant(),
                VIN = string.IsNullOrWhiteSpace(model.VIN) ? GeneratePlaceholderVin() : model.VIN.Trim().ToUpperInvariant(),
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
                LocationID = await ResolveLocationIdAsync(model.CityOrProvince),
                CarAddress = model.CarAddress,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                Features = string.Join(", ", selectedAmenities.Select(a => a.Name)),
                CurrentOdometer = 0,
                CommissionRate = 20m,
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
                CreatedAt = DateTime.UtcNow
            };

            _context.Cars.Add(car);
            await _context.SaveChangesAsync();

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
            TempData["Success"] = "Xe đã được gửi lên hệ thống và đang chờ duyệt.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateCreateViewDataAsync()
        {
            ViewBag.Brands = await _context.CarBrands.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
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
        public int? ServiceTypeID { get; set; }
        public decimal PricePerDay { get; set; }
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
