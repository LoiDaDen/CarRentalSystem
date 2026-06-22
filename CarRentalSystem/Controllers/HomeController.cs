using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Added for .Include() and ToListAsync()
using CarRentalSystem.Data; // Assuming your DbContext is in this namespace
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;

namespace CarRentalSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context; // Added DbContext field
        private readonly ICatalogCacheService? _catalogCache;

        public HomeController(ApplicationDbContext context, ICatalogCacheService? catalogCache = null) // Added constructor for dependency injection
        {
            _context = context;
            _catalogCache = catalogCache;
        }

        public async Task<IActionResult> Index() // Changed method signature to async Task<IActionResult>
        {
            var regionCookie = HttpContext.Request.Cookies["RegionID"];
            int? currentRegionId = null;
            if (!string.IsNullOrEmpty(regionCookie) && int.TryParse(regionCookie, out int parsedRegionId))
            {
                currentRegionId = parsedRegionId;
            }

            var featuredCarsQuery = _context.Cars
                .AsNoTracking()
                .WhereSelfDriveRentable()
                .Where(c => c.IsPremium);

            var availableCarsQuery = _context.Cars
                .AsNoTracking()
                .WhereSelfDriveRentable()
                .Where(c => !c.IsPremium);

            if (currentRegionId.HasValue)
            {
                featuredCarsQuery = featuredCarsQuery.Where(c => c.LocationID == currentRegionId.Value);
                availableCarsQuery = availableCarsQuery.Where(c => c.LocationID == currentRegionId.Value);
            }

            var featuredCars = await ProjectCarCards(featuredCarsQuery)
                .OrderByDescending(c => c.CarID)
                .Take(6)
                .ToListAsync();
            var availableCars = await ProjectCarCards(availableCarsQuery)
                .OrderByDescending(c => c.CarID)
                .Take(8)
                .ToListAsync();

            ViewBag.Locations = _catalogCache != null
                ? (object)await _catalogCache.GetLocationsAsync()
                : await _context.Locations.AsNoTracking().OrderBy(l => l.Name).ToListAsync();
            ViewBag.CarTypes = _catalogCache != null
                ? (object)await _catalogCache.GetCarTypesAsync()
                : await _context.CarTypes.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
            ViewBag.Brands = _catalogCache != null
                ? (object)await _catalogCache.GetBrandsAsync()
                : await _context.CarBrands.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
            return View(new HomeIndexViewModel
            {
                PremiumCars = featuredCars,
                AvailableCars = availableCars
            });
        }

        private static IQueryable<HomeCarCardViewModel> ProjectCarCards(IQueryable<Models.Car> cars)
        {
            return cars.Select(car => new HomeCarCardViewModel
            {
                CarID = car.CarID,
                CarName = car.CarName,
                ImageUrl = car.CarImages!
                    .OrderBy(image => image.ImageID)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault() ?? car.ImageURL,
                BrandName = car.CarBrand!.Name,
                Transmission = car.Transmission,
                Seats = car.Seats,
                LocationName = car.Location!.Name,
                PricePerDay = car.PricePerDay,
                AverageRating = car.Reviews!
                    .Where(review => review.IsApproved)
                    .Select(review => (double?)review.Rating)
                    .Average() ?? 5.0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRegion(int? locationId, string? returnUrl)
        {
            if (locationId.HasValue)
            {
                var locationExists = await _context.Locations
                    .AsNoTracking()
                    .AnyAsync(location => location.LocationID == locationId.Value);
                if (!locationExists)
                {
                    Response.Cookies.Delete("RegionID");
                    if (WantsJsonResponse())
                        return BadRequest(new { success = false, message = "Khu vực đã chọn không tồn tại." });

                    TempData["ErrorMessage"] = "Khu vực đã chọn không tồn tại.";
                    return LocalRedirect(IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/");
                }

                Response.Cookies.Append("RegionID", locationId.Value.ToString(), new CookieOptions { Expires = DateTime.UtcNow.AddDays(30) });
            }
            else
            {
                Response.Cookies.Delete("RegionID");
            }

            if (WantsJsonResponse())
                return Json(new { success = true, locationId });

            return LocalRedirect(IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/");
        }

        private bool WantsJsonResponse()
        {
            return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
                Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
        }

        private bool IsSafeLocalReturnUrl(string? returnUrl)
        {
            return !string.IsNullOrWhiteSpace(returnUrl) &&
                (Url?.IsLocalUrl(returnUrl) == true || (returnUrl.StartsWith("/") && !returnUrl.StartsWith("//")));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
