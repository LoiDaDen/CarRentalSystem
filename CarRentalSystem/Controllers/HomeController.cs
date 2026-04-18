using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Added for .Include() and ToListAsync()
using CarRentalSystem.Data; // Assuming your DbContext is in this namespace

namespace CarRentalSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context; // Added DbContext field

        public HomeController(ApplicationDbContext context) // Added constructor for dependency injection
        {
            _context = context;
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
                .Include(c => c.CarImages)
                .Include(c => c.CarType)
                .Include(c => c.CarStatus)
                .Include(c => c.Location)
                .Include(c => c.CarBrand)
                .Where(c => c.IsPremium && c.CarStatus!.Name == "Available");

            var availableCarsQuery = _context.Cars
                .Include(c => c.CarImages)
                .Include(c => c.CarType)
                .Include(c => c.CarStatus)
                .Include(c => c.Location)
                .Include(c => c.CarBrand)
                .Where(c => !c.IsPremium && c.CarStatus!.Name == "Available");

            if (currentRegionId.HasValue)
            {
                featuredCarsQuery = featuredCarsQuery.Where(c => c.LocationID == currentRegionId.Value);
                availableCarsQuery = availableCarsQuery.Where(c => c.LocationID == currentRegionId.Value);
            }

            var featuredCars = await featuredCarsQuery.Take(6).ToListAsync();
            var availableCars = await availableCarsQuery.Take(6).ToListAsync();

            ViewBag.Locations = await _context.Locations.ToListAsync();
            ViewBag.CarTypes = await _context.CarTypes.ToListAsync();
            ViewBag.Brands = await _context.CarBrands.ToListAsync();
            ViewBag.AvailableCars = availableCars;

            return View(featuredCars);
        }

        [HttpPost]
        public IActionResult SetRegion(int? locationId, string returnUrl)
        {
            if (locationId.HasValue)
            {
                Response.Cookies.Append("RegionID", locationId.Value.ToString(), new CookieOptions { Expires = DateTime.UtcNow.AddDays(30) });
            }
            else
            {
                Response.Cookies.Delete("RegionID");
            }
            return LocalRedirect(returnUrl ?? "/");
        }

        [HttpPost]
        public async Task<IActionResult> AutoDetectRegion(double lat, double lng, string returnUrl)
        {
            var locations = await _context.Locations.ToListAsync();
            Models.Location closest = null;
            double minDistance = double.MaxValue;

            foreach (var loc in locations)
            {
                var dLat = (lat - loc.Latitude) * Math.PI / 180.0;
                var dLon = (lng - loc.Longitude) * Math.PI / 180.0;
                var a = Math.Sin(dLat/2)*Math.Sin(dLat/2) + Math.Cos(loc.Latitude*Math.PI/180.0)*Math.Cos(lat*Math.PI/180.0)*Math.Sin(dLon/2)*Math.Sin(dLon/2);
                var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
                var distance = 6371 * c; // in km

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = loc;
                }
            }

            if (closest != null)
            {
                Response.Cookies.Append("RegionID", closest.LocationID.ToString(), new CookieOptions { Expires = DateTime.UtcNow.AddDays(30) });
            }
            return LocalRedirect(returnUrl ?? "/");
        }
    }
}
