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

            var featuredCars = await featuredCarsQuery.OrderByDescending(c => c.CarID).Take(6).ToListAsync();
            var availableCars = await availableCarsQuery.OrderByDescending(c => c.CarID).Take(6).ToListAsync();

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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
