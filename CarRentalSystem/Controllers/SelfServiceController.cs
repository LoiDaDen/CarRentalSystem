using System.Security.Claims;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class SelfServiceController : Controller
    {
        private readonly ICustomerSelfServiceService _selfService;

        public SelfServiceController(ICustomerSelfServiceService selfService)
        {
            _selfService = selfService;
        }

        [HttpGet]
        public async Task<IActionResult> Summary()
        {
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
            if (userId == 0) return Unauthorized();
            var summary = await _selfService.GetSummaryAsync(userId);
            return Json(new { success = true, summary });
        }
    }
}
