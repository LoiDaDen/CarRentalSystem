using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            // Redirect to the proper Admin area dashboard with real data
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }
    }
}
