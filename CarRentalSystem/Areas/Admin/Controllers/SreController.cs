using CarRentalSystem.Authorization;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class SreController : Controller
    {
        private readonly ISloMetricsService _sloMetrics;

        public SreController(ISloMetricsService sloMetrics)
        {
            _sloMetrics = sloMetrics;
        }

        [HttpGet]
        public async Task<IActionResult> Slo()
        {
            var dashboard = await _sloMetrics.GetDashboardAsync();
            if (Request.Headers.Accept.Any(h => h?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
                return Json(dashboard);

            return View(dashboard);
        }
    }
}
