using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AlertController : Controller
    {
        private readonly ITrackingService _trackingService;

        public AlertController(ITrackingService trackingService)
        {
            _trackingService = trackingService;
        }

        public async Task<IActionResult> Index()
        {
            var alerts = await _trackingService.GetActiveAlertsAsync();
            return View(alerts);
        }

        [HttpPost]
        public async Task<IActionResult> Resolve(int id)
        {
            await _trackingService.ResolveAlertAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
