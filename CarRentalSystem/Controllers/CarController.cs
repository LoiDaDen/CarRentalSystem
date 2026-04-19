using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Controllers
{
    public class CarController : Controller
    {
        private readonly ICarService _carService;

        private readonly ApplicationDbContext _context; // Assuming ApplicationDbContext is the DbContext

        public CarController(ICarService carService, ApplicationDbContext context) // Added DbContext to constructor
        {
            _carService = carService;
            _context = context; // Initialize DbContext
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
                .Include(c => c.CarType)
                .Include(c => c.Location)
                .Include(c => c.CarImages)
                .Include(c => c.CarStatus)
                .Include(c => c.CarBrand)
                .Include(c => c.Reviews)
                .Include(c => c.Bookings)
                .Where(c => c.CarStatus!.Name == "Available")
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
                carsQuery = carsQuery.Where(c => !c.Bookings!.Any(b =>
                    b.BookingStatusID != 4 &&
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

            var carTypes = await _context.CarTypes.ToListAsync();
            var brands = await _context.CarBrands.ToListAsync();

            ViewBag.CarTypes = carTypes;
            ViewBag.Brands = brands;
            ViewBag.Locations = await _context.Locations.OrderBy(l => l.Name).ToListAsync();
            ViewBag.CurrentBrandId = brandId;
            ViewBag.CurrentCarTypeId = carTypeId;
            ViewBag.CurrentSeats = seats;
            ViewBag.ContractId = contractId;
            ViewBag.Contract = contractId.HasValue ? await _context.ContractTypes.FindAsync(contractId.Value) : null;
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
            ViewBag.Contract = contractId.HasValue ? await _context.ContractTypes.FindAsync(contractId.Value) : null;

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int userId))
                {
                    var currentUser = await _context.Users.Include(u => u.MembershipTier).FirstOrDefaultAsync(u => u.UserID == userId);
                    ViewBag.MembershipTier = currentUser?.MembershipTier;
                }
            }

            var viewModel = new CarDetailsViewModel
            {
                Car = car,
                Amenities = new List<AmenityItem>
                {
                    new AmenityItem { Name = "Bluetooth", Icon = "bi-bluetooth" },
                    new AmenityItem { Name = "Camera 360", Icon = "bi-camera-video" },
                    new AmenityItem { Name = "Cruise Control", Icon = "bi-speedometer" },
                    new AmenityItem { Name = "Cảm biến lốp", Icon = "bi-radioactive" },
                    new AmenityItem { Name = "Cảnh báo tốc độ", Icon = "bi-exclamation-triangle" },
                    new AmenityItem { Name = "Định vị GPS", Icon = "bi-geo-alt" },
                    new AmenityItem { Name = "Khe cắm USB", Icon = "bi-usb-drive" },
                    new AmenityItem { Name = "Màn hình DVD", Icon = "bi-display" },
                    new AmenityItem { Name = "ETC", Icon = "bi-credit-card" },
                    new AmenityItem { Name = "Bản đồ", Icon = "bi-map" },
                    new AmenityItem { Name = "Camera cập lề", Icon = "bi-camera" },
                    new AmenityItem { Name = "Camera lùi", Icon = "bi-camera-fill" },
                    new AmenityItem { Name = "Cảnh báo va chạm", Icon = "bi-shield-exclamation" },
                    new AmenityItem { Name = "Cửa sổ trời", Icon = "bi-sun" },
                    new AmenityItem { Name = "Lốp dự phòng", Icon = "bi-vinyl" },
                    new AmenityItem { Name = "Số túi khí", Icon = "bi-shield-check" },
                    new AmenityItem { Name = "Vietmap Live", Icon = "bi-map-fill" },
                    new AmenityItem { Name = "Màn hình cảm ứng", Icon = "bi-hand-index-thumb" },
                    new AmenityItem { Name = "Android Box", Icon = "bi-box" },
                    new AmenityItem { Name = "Dây đai đa năng", Icon = "bi-person-check" },
                    new AmenityItem { Name = "Cốp điện", Icon = "bi-lightning-charge" },
                    new AmenityItem { Name = "Phanh tay điện tử", Icon = "bi-toggles" }
                },
                Pricing = new PricingPackages
                {
                    Price4h = Math.Round(car.PricePerDay * 0.25m / 10000m) * 10000m,
                    Price8h = Math.Round(car.PricePerDay * 0.40m / 10000m) * 10000m,
                    Price12h = Math.Round(car.PricePerDay * 0.55m / 10000m) * 10000m,
                    Price24h = car.PricePerDay,
                    DiscountPercent = 17,
                    DepositAmount = 500000,
                    InsuranceFee = 3000000
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
            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create(Car car, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
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
        public async Task<IActionResult> AddReview(int carId, int rating, string comment)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr != null && int.TryParse(userIdStr, out int userId))
            {
                // Check if user has actually rented and completed the rental for this car
                bool hasCompletedBooking = await _context.Bookings
                    .Include(b => b.BookingStatus)
                    .AnyAsync(b => b.UserID == userId && b.CarID == carId && b.BookingStatus != null && b.BookingStatus.Name == BookingStatusConstants.Completed);

                if (!hasCompletedBooking)
                {
                    TempData["BookingError"] = "Bạn chỉ có thể đánh giá chiếc xe này sau khi đã hoàn tất chuyến đi bằng xe này.";
                    return RedirectToAction(nameof(Details), new { id = carId });
                }

                var review = new Review
                {
                    CarID = carId,
                    UserID = userId,
                    Rating = rating,
                    Comment = comment,
                    CreatedAt = DateTime.UtcNow
                };
                await _carService.AddReviewAsync(review);
                TempData["SuccessMessage"] = "Ảm ơn bạn đã để lại đánh giá!";
            }
            return RedirectToAction(nameof(Details), new { id = carId });
        }

        // === EPIC 2: Availability Calendar API ===
        // GET /Car/BusyDates?carId=X
        [HttpGet]
        public IActionResult BusyDates(int carId)
        {
            var bookings = _context.Bookings
                .Include(b => b.BookingStatus)
                .Where(b => b.CarID == carId &&
                    (b.BookingStatus.Name == BookingStatusConstants.Confirmed ||
                     b.BookingStatus.Name == BookingStatusConstants.Pending ||
                     b.BookingStatus.Name == BookingStatusConstants.Active ||
                     b.BookingStatus.Name == BookingStatusConstants.Draft))
                .Select(b => new { b.StartDate, b.EndDate })
                .ToList();

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
        public IActionResult MapSearch(string? feature = null)
        {
            var carsQuery = _context.Cars
                .Include(c => c.CarStatus)
                .Include(c => c.Location)
                .Include(c => c.CarBrand)
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

            var cars = carsQuery
                .ToList() // fetch locally before parsing to avoid EF translation issues with Coalesce
                .Select(c => new {
                    c.CarID,
                    c.CarName,
                    Brand = c.CarBrand?.Name ?? "Chưa rõ",
                    c.PricePerDay,
                    Location = c.CarAddress ?? c.Location?.Name ?? "Chưa cập nhật vị trí",
                    Lat = c.Latitude ?? c.Location?.Latitude ?? 10.7769,
                    Lng = c.Longitude ?? c.Location?.Longitude ?? 106.7009,
                    Image = c.ImageURL ?? c.CarImages?.Select(i => i.ImageUrl).FirstOrDefault(),
                    Type = c.CarType?.Name ?? "Khác",
                    Transmission = c.Transmission,
                    Rating = c.Reviews != null && c.Reviews.Any() ? c.Reviews.Average(r => r.Rating) : 5.0,
                    Features = c.Features
                })
                .ToList();

            ViewBag.CarTypes = _context.CarTypes.Select(t => t.Name).ToList();
            
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
                .Include(c => c.Location)
                .FirstOrDefaultAsync(c => c.CarID == carId);

            if (car == null)
                return Json(new { success = false, message = "Không tìm thấy xe" });

            // Lấy tọa độ xe (ưu tiên Latitude/Longitude của xe, fallback về Location)
            var carLat = car.Latitude ?? car.Location?.Latitude ?? 10.7769;
            var carLng = car.Longitude ?? car.Location?.Longitude ?? 106.7009;

            // Tính delivery
            var deliveryService = HttpContext.RequestServices.GetService<IDeliveryService>();
            var deliveryInfo = await deliveryService!.CalculateDeliveryAsync(carLat, carLng, customerLat, customerLng);

            return Json(new {
                success = true,
                distanceKm = deliveryInfo.DistanceKm,
                durationMinutes = deliveryInfo.DurationMinutes,
                shippingFee = deliveryInfo.ShippingFee,
                earliestPickupTime = deliveryInfo.EarliestPickupTime.ToString("yyyy-MM-ddTHH:mm"),
                formattedDistance = deliveryInfo.FormattedDistance,
                formattedDuration = deliveryInfo.FormattedDuration,
                formattedFee = deliveryInfo.FormattedFee
            });
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
            int page = 2)
        {
            const int pageSize = 12;
            var carsQuery = _context.Cars
                .Include(c => c.CarType)
                .Include(c => c.Location)
                .Include(c => c.CarImages)
                .Include(c => c.CarStatus)
                .Include(c => c.CarBrand)
                .Include(c => c.Reviews)
                .Include(c => c.Bookings)
                .Where(c => c.CarStatus!.Name == "Available")
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
                    b.BookingStatusID != 4 &&
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

            var totalCars = await carsQuery.CountAsync();
            var cars = await carsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = cars.Select(c => new {
                c.CarID,
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
                avgRating = c.Reviews != null && c.Reviews.Any() ? c.Reviews.Where(r => r.IsApproved).Average(r => r.Rating) : 5.0,
                reviewCount = c.Reviews?.Count(r => r.IsApproved) ?? 0,
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
