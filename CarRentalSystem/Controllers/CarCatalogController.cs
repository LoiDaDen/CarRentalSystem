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
            var models = await _catalogCache.GetCarModelsAsync(brandId);
            return Json(models);
        }
    }
}
