using CarRentalSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers
{
    public class CarCatalogController : Controller
    {
        private readonly ICatalogCacheService _catalogCache;

        public CarCatalogController(ICatalogCacheService catalogCache)
        {
            _catalogCache = catalogCache;
        }

        [HttpGet]
        public async Task<IActionResult> Models(int brandId)
        {
            if (!ModelState.IsValid || brandId <= 0)
                return BadRequest(new { message = "brandId must be a positive integer." });

            var models = await _catalogCache.GetCarModelsAsync(brandId);
            return Json(models.Select(model => new
            {
                id = model.Id,
                name = model.Name,
                seatCount = model.SeatCount,
                fuelType = model.FuelType,
                transmission = model.Transmission,
                fromYear = model.FromYear,
                toYear = model.ToYear
            }));
        }
    }
}
