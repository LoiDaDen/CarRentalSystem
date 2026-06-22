using CarRentalSystem.Authorization;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class AlertController : Controller
    {
        private readonly ITrackingService _trackingService;

        public AlertController(ITrackingService trackingService)
        {
            _trackingService = trackingService;
        }

        public async Task<IActionResult> Index()
        {
            await Task.CompletedTask;
            return RedirectToAction("Index", "Alert", new { area = "" });
        }

        [HttpPost]
        public async Task<IActionResult> Resolve(int id)
        {
            await _trackingService.ResolveAlertAsync(id);
            return RedirectToAction("Index", "Alert", new { area = "" });
        }
    }
}
