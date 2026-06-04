using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Controllers
{
    public class CarController : Controller
    {
        private readonly ICarService _carService;
        private readonly ISelfDrivePricingService _selfDrivePricingService;
        private readonly ICatalogCacheService? _catalogCache;

        private readonly ApplicationDbContext _context; // Assuming ApplicationDbContext is the DbContext

        public CarController(ICarService carService, ApplicationDbContext context, ISelfDrivePricingService selfDrivePricingService, ICatalogCacheService? catalogCache = null) // Added DbContext to constructor
        {
            _carService = carService;
            _context = context; // Initialize DbContext
            _selfDrivePricingService = selfDrivePricingService;
            _catalogCache = catalogCache;
        }

        // Customer view: List all available cars with advanced filtering
        public async Task<IActionResult> Index(
            int? brandId,
            int? carTypeId,
            int? seats,
            decimal? minPrice,
            decimal? maxPrice,
            string location,
            string fuelType,
            string transmission,
            string category,
            bool? isPremium,
            bool? isSale,
            DateTime? startDate,
            DateTime? endDate,
            string sort,
            int? contractId,
            int page = 1)
        {
            const int pageSize = 12;
            var carsQuery = _context.Cars
                .AsNoTracking()
                .Include(c => c.CarType)
                .Include(c => c.Location)
                .Include(c => c.CarImages)
                .Include(c => c.CarStatus)
                .Include(c => c.CarBrand)
                .Include(c => c.Reviews)
                .Where(c => c.CarStatus!.Name == "Available")
                .AsSplitQuery()
                .AsQueryable();

            var regionCookie = HttpContext.Request.Cookies["RegionID"];
            if (!string.IsNullOrEmpty(regionCookie) && int.TryParse(regionCookie, out int parsedRegionId))
            {
                carsQuery = carsQuery.Where(c => c.LocationID == parsedRegionId);
            }

            if (brandId.HasValue)
            {
                carsQuery = carsQuery.Where(c => c.BrandID == brandId.Value);
            }

            if (carTypeId.HasValue)
            {
                carsQuery = carsQuery.Where(c => c.CarTypeID == carTypeId.Value);
            }

            if (seats.HasValue)
                carsQuery = carsQuery.Where(c => c.Seats == seats.Value);

            if (minPrice.HasValue)
                carsQuery = carsQuery.Where(c => c.PricePerDay >= minPrice.Value);

            if (maxPrice.HasValue)
                carsQuery = carsQuery.Where(c => c.PricePerDay <= maxPrice.Value);

            if (!string.IsNullOrEmpty(location))
                carsQuery = carsQuery.Where(c => c.Location != null && c.Location.Name != null && c.Location.Name.Contains(location));

            if (!string.IsNullOrEmpty(fuelType))
                carsQuery = carsQuery.Where(c => c.FuelType == fuelType);

            if (!string.IsNullOrEmpty(transmission))
                carsQuery = carsQuery.Where(c => c.Transmission == transmission);

            if (!string.IsNullOrEmpty(category))
                carsQuery = carsQuery.Where(c => c.CarType != null && c.CarType.Name == category);

            if (isPremium.HasValue && isPremium.Value)
                carsQuery = carsQuery.Where(c => c.IsPremium);

            if (isSale.HasValue && isSale.Value)
                carsQuery = carsQuery.Where(c => c.DiscountPrice.HasValue);

            // Filter out cars that are booked in this period
            if (startDate.HasValue && endDate.HasValue && startDate.Value < endDate.Value)
            {
                // Lọc các booking chưa hoàn thành hoặc đã hủy
                carsQuery = carsQuery.Where(c => !c.Bookings!.Any(b =>
                    b.BookingStatus != null && BookingStatusConstants.AvailabilityBlocking.Contains(b.BookingStatus.Name) &&
                    (
                      (startDate.Value >= b.StartDate && startDate.Value < b.EndDate) ||
                      (endDate.Value > b.StartDate && endDate.Value <= b.EndDate) ||
                      (startDate.Value <= b.StartDate && endDate.Value >= b.EndDate)
                    )
                ));
            }

            // Sorting
            carsQuery = sort switch
            {
                "price_asc" => carsQuery.OrderBy(c => c.PricePerDay),
                "price_desc" => carsQuery.OrderByDescending(c => c.PricePerDay),
                "newest" => carsQuery.OrderByDescending(c => c.CreatedAt),
                _ => carsQuery.OrderByDescending(c => c.IsPremium).ThenByDescending(c => c.CreatedAt)
            };

            var totalCars = await carsQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCars / (double)pageSize);

            var cars = await carsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            object carTypes = _catalogCache != null
                ? await _catalogCache.GetCarTypesAsync()
                : await _context.CarTypes.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
            object brands = _catalogCache != null
                ? await _catalogCache.GetBrandsAsync()
                : await _context.CarBrands.AsNoTracking().OrderBy(b => b.Name).ToListAsync();

            ViewBag.CarTypes = carTypes;
            ViewBag.Brands = brands;
            ViewBag.Locations = _catalogCache != null
                ? (object)await _catalogCache.GetLocationsAsync()
                : await _context.Locations.AsNoTracking().OrderBy(l => l.Name).ToListAsync();
            ViewBag.CurrentBrandId = brandId;
            ViewBag.CurrentCarTypeId = carTypeId;
            ViewBag.CurrentSeats = seats;
            ViewBag.ContractId = contractId;
            ViewBag.Contract = contractId.HasValue ? await _context.ContractTypes.AsNoTracking().FirstOrDefaultAsync(c => c.ContractTypeID == contractId.Value) : null;
            ViewBag.TotalCars = totalCars;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;

            return View(cars);
        }

        public async Task<IActionResult> Details(int id, string startDate = null, string endDate = null, int? duration = null, int? contractId = null)
        {
            var car = await _carService.GetCarByIdAsync(id);
            if (car == null) return NotFound();

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.Duration = duration;
            ViewBag.ContractId = contractId;
            ViewBag.Contract = contractId.HasValue ? await _context.ContractTypes.AsNoTracking().FirstOrDefaultAsync(c => c.ContractTypeID == contractId.Value) : null;

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int userId))
                {
                    var currentUser = await _context.Users
                        .AsNoTracking()
                        .Include(u => u.MembershipTier)
                        .FirstOrDefaultAsync(u => u.UserID == userId);
                    ViewBag.MembershipTier = currentUser?.MembershipTier;

                    // Kiểm tra user đã thuê xe này chưa (booking đã hoàn thành)
                    var hasRentedThisCar = await _context.Bookings
                        .AsNoTracking()
                        .AnyAsync(b => b.UserID == userId && b.CarID == id && b.BookingStatus != null && b.BookingStatus.Name == "Completed");
                    ViewBag.HasRentedThisCar = hasRentedThisCar;

                    // Kiểm tra user đã đánh giá xe này chưa
                    var hasReviewedThisCar = await _context.Reviews.AsNoTracking().AnyAsync(r => r.UserID == userId && r.CarID == id);
                    ViewBag.HasReviewedThisCar = hasReviewedThisCar;
                }
            }

            var quoteStart = DateTime.UtcNow;
            var viewModel = new CarDetailsViewModel
            {
                Car = car,
                Amenities = car.CarAmenityMappings?
                    .Where(mapping => mapping.CarAmenity != null)
                    .OrderBy(mapping => mapping.CarAmenity!.DisplayOrder)
                    .ThenBy(mapping => mapping.CarAmenity!.Name)
                    .Select(mapping => new AmenityItem
                    {
                        Name = mapping.CarAmenity!.Name,
                        Icon = string.IsNullOrWhiteSpace(mapping.CarAmenity.Icon)
                            ? "bi-check2"
                            : mapping.CarAmenity.Icon
                    })
                    .ToList() ?? new List<AmenityItem>(),
                Pricing = new PricingPackages
                {
                    Price4h = _selfDrivePricingService.Calculate(car, quoteStart, quoteStart.AddHours(4)).BaseRentalAmount,
                    Price8h = _selfDrivePricingService.Calculate(car, quoteStart, quoteStart.AddHours(8)).BaseRentalAmount,
                    Price12h = _selfDrivePricingService.Calculate(car, quoteStart, quoteStart.AddHours(12)).BaseRentalAmount,
                    Price24h = _selfDrivePricingService.Calculate(car, quoteStart, quoteStart.AddHours(24)).BaseRentalAmount,
                    DiscountPercent = 0,
                    DepositAmount = car.DefaultDeposit,
                    InsuranceFee = 0
                }
            };

            return View(viewModel);
        }

        // Admin views
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage(int? brandId, int? locationId)
        {
            var query = _context.Cars
                .Include(c => c.CarBrand)
                .Include(c => c.Location)
                .Include(c => c.CarType)
                .Include(c => c.CarStatus)
                .Include(c => c.CarImages)
                .AsQueryable();

            if (brandId.HasValue)
                query = query.Where(c => c.BrandID == brandId.Value);

            if (locationId.HasValue)
                query = query.Where(c => c.LocationID == locationId.Value);

            var cars = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();

            ViewBag.Brands = await _context.CarBrands.OrderBy(b => b.Name).ToListAsync();
            ViewBag.Locations = await _context.Locations.OrderBy(l => l.Name).ToListAsync();
            
            ViewBag.CurrentBrandId = brandId;
            ViewBag.CurrentLocationId = locationId;

            return View(cars);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Locations = _context.Locations.ToList();
            ViewBag.Brands = _context.CarBrands.ToList();
            ViewBag.CarTypes = _context.CarTypes.ToList();
            ViewBag.ServiceTypes = _context.ServiceTypes.Where(s => s.IsActive).OrderBy(s => s.ServiceTypeID).ToList();
            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Car car, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
                var selfDriveServiceType = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.Code == ServiceTypeConstants.SelfDrive);
                var withDriverServiceType = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.Code == ServiceTypeConstants.WithDriver);
                car.ServiceTypeID ??= selfDriveServiceType?.ServiceTypeID;
                if (car.ServiceTypeID == withDriverServiceType?.ServiceTypeID)
                {
                    car.IsSelfDriveAllowed = false;
                    car.IsWithDriverAllowed = true;
                    car.TransportPermitStatus = string.IsNullOrWhiteSpace(car.TransportPermitStatus) ? "Pending" : car.TransportPermitStatus;
                    car.IsApproved = false;
                }
                else
                {
                    car.ServiceTypeID = selfDriveServiceType?.ServiceTypeID;
                    car.IsSelfDriveAllowed = true;
                    car.IsWithDriverAllowed = false;
                    car.TransportPermitStatus = "NotRequired";
                    car.BadgeType = null;
                    car.BadgeExpiryDate = null;
                    car.IsApproved = true;
                }

                await _carService.AddCarAsync(car);
                
                // Handle image upload if provided
                if (imageFile != null && imageFile.Length > 0)
                {
                    // 🔒 Validate file size (max 5MB)
                    if (imageFile.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("imageFile", "Ảnh xe vượt quá giới hạn 5MB.");
                        return View(car);
                    }

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                    
                    if (!string.IsNullOrEmpty(ext) && allowedExtensions.Contains(ext))
                    {
                        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "cars");
                        Directory.CreateDirectory(uploadsFolder);
                        string uniqueFileName = Guid.NewGuid().ToString() + ext;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        
                        // Further impl: Save CarImage entity
                    }
                }
                
                return RedirectToAction(nameof(Manage));
            }
            return View(car);
        }
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int carId, int rating, string comment)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized();
            }

            // Validate rating range (1-5)
            if (rating < 1 || rating > 5)
            {
                TempData["BookingError"] = "Điểm đánh giá phải từ 1 đến 5 sao.";
                return RedirectToAction(nameof(Details), new { id = carId });
            }

            // Check if user has actually rented and completed the rental for this car
            bool hasCompletedBooking = await _context.Bookings
                .Include(b => b.BookingStatus)
                .AnyAsync(b => b.UserID == userId && b.CarID == carId && b.BookingStatus != null && b.BookingStatus.Name == BookingStatusConstants.Completed);

            if (!hasCompletedBooking)
            {
                TempData["BookingError"] = "Bạn chỉ có thể đánh giá chiếc xe này sau khi đã hoàn tất chuyến đi bằng xe này.";
                return RedirectToAction(nameof(Details), new { id = carId });
            }

            // Check if user already reviewed this car
            bool alreadyReviewed = await _context.Reviews
                .AnyAsync(r => r.CarID == carId && r.UserID == userId);

            if (alreadyReviewed)
            {
                TempData["BookingError"] = "Bạn đã đánh giá chiếc xe này rồi.";
                return RedirectToAction(nameof(Details), new { id = carId });
            }

            var review = new Review
            {
                CarID = carId,
                UserID = userId,
                Rating = rating,
                Comment = comment?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };
            await _carService.AddReviewAsync(review);
            TempData["SuccessMessage"] = "Cảm ơn bạn đã để lại đánh giá!";
            return RedirectToAction(nameof(Details), new { id = carId });
        }

        // === EPIC 2: Availability Calendar API ===
        // GET /Car/BusyDates?carId=X
        [HttpGet]
        public async Task<IActionResult> BusyDates(int carId)
        {
            var bookings = await _context.Bookings
                .AsNoTracking()
                .Where(b => b.CarID == carId &&
                    b.BookingStatus != null &&
                    (b.BookingStatus.Name == BookingStatusConstants.Confirmed ||
                     b.BookingStatus.Name == BookingStatusConstants.Pending ||
                     b.BookingStatus.Name == BookingStatusConstants.Active ||
                     b.BookingStatus.Name == BookingStatusConstants.Unpaid ||
                     b.BookingStatus.Name == BookingStatusConstants.Draft))
                .Select(b => new { b.StartDate, b.EndDate })
                .ToListAsync();

            // Expand each booking into individual disabled dates
            var busyDates = new System.Collections.Generic.List<string>();
            foreach (var booking in bookings)
            {
                for (var d = booking.StartDate.Date; d <= booking.EndDate.Date; d = d.AddDays(1))
                    busyDates.Add(d.ToString("yyyy-MM-dd"));
            }
            return Json(busyDates.Distinct());
        }

        // === EPIC 3: Map-based Discovery ===
        // GET /Car/MapSearch
        public async Task<IActionResult> MapSearch(string? feature = null)
        {
            var carsQuery = _context.Cars
                .AsNoTracking()
                .Include(c => c.CarStatus)
                .Include(c => c.Location)
                .Include(c => c.CarBrand)
                .Include(c => c.CarType)
                .Include(c => c.CarImages)
                .Include(c => c.Reviews)
                .Where(c => c.CarStatus!.Name == "Available");

            var regionCookie = HttpContext.Request.Cookies["RegionID"];
            if (!string.IsNullOrEmpty(regionCookie) && int.TryParse(regionCookie, out int parsedRegionId))
            {
                carsQuery = carsQuery.Where(c => c.LocationID == parsedRegionId);
            }

            if (!string.IsNullOrEmpty(feature))
            {
                carsQuery = carsQuery.Where(c => c.Features != null && c.Features.Contains(feature));
            }

            var cars = (await carsQuery
                .OrderByDescending(c => c.IsPremium)
                .ThenByDescending(c => c.CreatedAt)
                .Take(200)
                .ToListAsync())
                .Select(c => new {
                    c.CarID,
                    c.CarName,
                    Brand = c.CarBrand?.Name ?? "Chưa rõ",
                    c.PricePerDay,
                    Location = c.CarAddress ?? c.Location?.Name ?? "Chưa cập nhật vị trí",
                    Lat = c.Latitude ?? (c.Location != null ? c.Location.Latitude : 10.7769),
                    Lng = c.Longitude ?? (c.Location != null ? c.Location.Longitude : 106.7009),
                    Image = c.ImageURL ?? c.CarImages.OrderBy(i => i.ImageID).Select(i => i.ImageUrl).FirstOrDefault(),
                    Type = c.CarType?.Name ?? "Khác",
                    c.Transmission,
                    c.Seats,
                    c.FuelType,
                    Rating = c.Reviews.Where(r => r.IsApproved).Select(r => (double?)r.Rating).Average() ?? 5.0,
                    c.Features
                })
                .ToList();

            ViewBag.CarTypes = _catalogCache != null
                ? (await _catalogCache.GetCarTypesAsync()).Select(t => t.Name).ToList()
                : await _context.CarTypes.AsNoTracking().OrderBy(t => t.Name).Select(t => t.Name).ToListAsync();
            
            // predefined common features for filtering
            ViewBag.Amenities = new List<string> { "Bluetooth", "GPS", "Camera lùi", "Cửa sổ trời", "Số tự động", "Bản đồ" };
            ViewBag.CarsJson = System.Text.Json.JsonSerializer.Serialize(cars);
            return View();
        }

        // === TÍNH PHÍ SHIP XE - API Endpoint ===
        // GET /Car/CalculateDelivery?carId=X&customerLat=Y&customerLng=Z
        [HttpGet]
        public async Task<IActionResult> CalculateDelivery(int carId, double customerLat, double customerLng)
        {
            var car = await _context.Cars
                .AsNoTracking()
                .Include(c => c.Location)
                .FirstOrDefaultAsync(c => c.CarID == carId);

            if (car == null)
                return Json(new { success = false, message = "Không tìm thấy xe" });

            // Lấy tọa độ xe (ưu tiên Latitude/Longitude của xe, fallback về Location)
            if (!car.DeliveryEnabled)
                return Json(new { success = false, isWithinDeliveryScope = false, message = "Xe này chưa bật dịch vụ giao tận nơi." });

            var carLat = car.Latitude ?? car.Location?.Latitude ?? 10.7769;
            var carLng = car.Longitude ?? car.Location?.Longitude ?? 106.7009;

            var deliveryScope = HttpContext.RequestServices.GetRequiredService<IDeliveryScopeService>();
            var scope = deliveryScope.CheckScope(car.CarAddress ?? car.Location?.Name, carLat, carLng, customerLat, customerLng);
            if (!scope.IsWithinScope)
            {
                return Json(new {
                    success = false,
                    isWithinDeliveryScope = false,
                    message = scope.MessageVi,
                    messageEn = scope.MessageEn,
                    originProvince = scope.OriginProvince,
                    destinationProvince = scope.DestinationProvince
                });
            }

            // Tính delivery
            var deliveryService = HttpContext.RequestServices.GetService<IDeliveryService>();
            var deliveryInfo = await deliveryService!.CalculateDeliveryAsync(carLat, carLng, customerLat, customerLng);
            if (car.MaxDeliveryDistanceKm > 0 && (decimal)deliveryInfo.DistanceKm > car.MaxDeliveryDistanceKm)
            {
                return Json(new
                {
                    success = false,
                    isWithinDeliveryScope = false,
                    message = $"Vị trí giao xe cách {deliveryInfo.DistanceKm:F1} km, vượt quá phạm vi {car.MaxDeliveryDistanceKm:N0} km của xe này.",
                    originProvince = scope.OriginProvince,
                    destinationProvince = scope.DestinationProvince
                });
            }

            var roundedDistance = Math.Round((decimal)deliveryInfo.DistanceKm, 2, MidpointRounding.AwayFromZero);
            var billableKm = Math.Max(0m, Math.Ceiling(roundedDistance - car.FreeDeliveryRadiusKm));
            var deliveryFee = billableKm * car.DeliveryFeePerKm;

            return Json(new {
                success = true,
                isWithinDeliveryScope = true,
                scopeMessage = "",
                originProvince = scope.OriginProvince,
                destinationProvince = scope.DestinationProvince,
                distanceKm = deliveryInfo.DistanceKm,
                durationMinutes = deliveryInfo.DurationMinutes,
                shippingFee = deliveryFee,
                earliestPickupTime = deliveryInfo.EarliestPickupTime.ToString("yyyy-MM-ddTHH:mm"),
                formattedDistance = deliveryInfo.FormattedDistance,
                formattedDuration = deliveryInfo.FormattedDuration,
                formattedFee = $"{deliveryFee:N0} đ"
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCarsWithDistance(double customerLat, double customerLng)
        {
            var cars = await _context.Cars
                .AsNoTracking()
                .Include(c => c.Location)
                .Include(c => c.CarBrand)
                .Include(c => c.CarType)
                .Include(c => c.CarImages)
                .Include(c => c.Reviews)
                .Include(c => c.CarStatus)
                .Where(c => c.CarStatus != null && c.CarStatus.Name == "Available" && c.DeliveryEnabled)
                .OrderByDescending(c => c.IsPremium)
                .ThenByDescending(c => c.CreatedAt)
                .Take(50)
                .ToListAsync();

            var deliveryService = HttpContext.RequestServices.GetRequiredService<IDeliveryService>();
            var deliveryScope = HttpContext.RequestServices.GetRequiredService<IDeliveryScopeService>();
            var result = new List<object>();

            foreach (var car in cars)
            {
                var carLat = car.Latitude ?? car.Location?.Latitude ?? 10.7769;
                var carLng = car.Longitude ?? car.Location?.Longitude ?? 106.7009;
                var scope = deliveryScope.CheckScope(car.CarAddress ?? car.Location?.Name, carLat, carLng, customerLat, customerLng);
                if (!scope.IsWithinScope)
                    continue;

                var distanceKm = deliveryService.CalculateHaversineDistance(carLat, carLng, customerLat, customerLng);
                var durationMinutes = distanceKm / 30.0 * 60.0;
                if (car.MaxDeliveryDistanceKm > 0 && (decimal)distanceKm > car.MaxDeliveryDistanceKm)
                    continue;

                var roundedDistance = Math.Round((decimal)distanceKm, 2, MidpointRounding.AwayFromZero);
                var billableKm = Math.Max(0m, Math.Ceiling(roundedDistance - car.FreeDeliveryRadiusKm));
                var deliveryFee = billableKm * car.DeliveryFeePerKm;
                var totalEstimate = car.PricePerDay + deliveryFee;
                var approvedReviews = car.Reviews?.Where(r => r.IsApproved).ToList() ?? new List<Review>();
                var rating = approvedReviews.Any() ? approvedReviews.Average(r => r.Rating) : 5.0;
                result.Add(new
                {
                    carId = car.CarID,
                    carName = car.CarName,
                    brand = car.CarBrand?.Name ?? "",
                    imageUrl = car.ImageURL ?? car.CarImages?.Select(i => i.ImageUrl).FirstOrDefault(),
                    image = car.ImageURL ?? car.CarImages?.Select(i => i.ImageUrl).FirstOrDefault(),
                    type = car.CarType?.Name ?? "",
                    transmission = car.Transmission,
                    seats = car.Seats,
                    isPremium = car.IsPremium,
                    rating,
                    reviewCount = approvedReviews.Count,
                    pricePerDay = car.PricePerDay,
                    location = car.CarAddress ?? car.Location?.Name ?? "",
                    distanceKm,
                    durationMinutes,
                    shippingFee = deliveryFee,
                    totalEstimate,
                    formattedDistance = $"{distanceKm:F1} km",
                    formattedDuration = $"~{Math.Ceiling(1 + durationMinutes / 60.0)} tiếng",
                    formattedShippingFee = $"{deliveryFee:N0} đ",
                    earliestPickupTime = DateTime.UtcNow.AddHours(1 + durationMinutes / 60.0),
                    originProvince = scope.OriginProvince,
                    destinationProvince = scope.DestinationProvince
                });
            }

            return Json(new { success = true, cars = result });
        }

        // API: Load more cars (JSON)
        [HttpGet]
        public async Task<IActionResult> LoadMoreCars(
            int? brandId,
            string category,
            string location,
            string startDate,
            string endDate,
            string sort,
            bool? isPremium,
            int? contractId,
            decimal? minPrice,
            decimal? maxPrice,
            int page = 2)
        {
            const int pageSize = 12;
            var carsQuery = _context.Cars
                .AsNoTracking()
                .Include(c => c.CarType)
                .Include(c => c.Location)
                .Include(c => c.CarImages)
                .Include(c => c.CarStatus)
                .Include(c => c.CarBrand)
                .Include(c => c.Reviews)
                .Where(c => c.CarStatus!.Name == "Available")
                .AsSplitQuery()
                .AsQueryable();

            var regionCookie = HttpContext.Request.Cookies["RegionID"];
            if (!string.IsNullOrEmpty(regionCookie) && int.TryParse(regionCookie, out int parsedRegionId))
            {
                carsQuery = carsQuery.Where(c => c.LocationID == parsedRegionId);
            }

            if (brandId.HasValue)
                carsQuery = carsQuery.Where(c => c.BrandID == brandId.Value);

            if (!string.IsNullOrEmpty(category))
                carsQuery = carsQuery.Where(c => c.CarType != null && c.CarType.Name == category);

            if (!string.IsNullOrEmpty(location))
                carsQuery = carsQuery.Where(c => c.Location != null && c.Location.Name != null && c.Location.Name.Contains(location));

            if (startDate != null && endDate != null && DateTime.TryParse(startDate, out var sDate) && DateTime.TryParse(endDate, out var eDate) && sDate < eDate)
            {
                carsQuery = carsQuery.Where(c => !c.Bookings!.Any(b =>
                    b.BookingStatus != null && BookingStatusConstants.AvailabilityBlocking.Contains(b.BookingStatus.Name) &&
                    ((sDate >= b.StartDate && sDate < b.EndDate) ||
                     (eDate > b.StartDate && eDate <= b.EndDate) ||
                     (sDate <= b.StartDate && eDate >= b.EndDate))
                ));
            }

            carsQuery = sort switch
            {
                "price_asc" => carsQuery.OrderBy(c => c.PricePerDay),
                "price_desc" => carsQuery.OrderByDescending(c => c.PricePerDay),
                "newest" => carsQuery.OrderByDescending(c => c.CreatedAt),
                _ => carsQuery.OrderByDescending(c => c.IsPremium).ThenByDescending(c => c.CreatedAt)
            };

            if (isPremium == true)
                carsQuery = carsQuery.Where(c => c.IsPremium);

            if (minPrice.HasValue)
                carsQuery = carsQuery.Where(c => c.PricePerDay >= minPrice.Value);

            if (maxPrice.HasValue)
                carsQuery = carsQuery.Where(c => c.PricePerDay <= maxPrice.Value);

            var totalCars = await carsQuery.CountAsync();
            var cars = await carsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = cars.Select(c => new {
                carId = c.CarID,
                c.CarName,
                brandName = c.CarBrand?.Name ?? "",
                c.Transmission,
                c.Seats,
                c.FuelType,
                c.PricePerDay,
                c.PricePerHour,
                c.IsPremium,
                isSystem = c.OwnerID == null,
                isElectric = c.FuelType != null && (c.FuelType.Contains("Điện") || c.FuelType.Contains("Electric")),
                imageUrl = c.CarImages?.FirstOrDefault()?.ImageUrl ?? "https://images.unsplash.com/photo-1541899481282-d53bffe3c35d?auto=format&fit=crop&q=80&w=800",
                avgRating = c.Reviews != null && c.Reviews.Any(r => r.IsApproved) ? c.Reviews.Where(r => r.IsApproved).Average(r => r.Rating) : 5.0,
                reviewCount = c.Reviews?.Count(r => r.IsApproved) ?? 0,
                locationId = c.LocationID,
                locationName = c.Location?.Name ?? "",
                p4h = Math.Round(c.PricePerDay * 0.25m / 10000m) * 10000m,
                p8h = Math.Round(c.PricePerDay * 0.40m / 10000m) * 10000m,
                p12h = Math.Round(c.PricePerDay * 0.55m / 10000m) * 10000m,
                p24h = c.PricePerDay
            }).ToList();

            return Json(new {
                cars = result,
                currentPage = page,
                totalCars = totalCars,
                hasMore = page * pageSize < totalCars,
                remaining = Math.Max(0, totalCars - page * pageSize)
            });
        }
    }
}

