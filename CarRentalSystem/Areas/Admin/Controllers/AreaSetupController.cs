using Microsoft.AspNetCore.Authorization;
using CarRentalSystem.Authorization;
// CarRentalSystem/Areas/Admin/Controllers/AreaSetupController.cs
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class AreaSetupController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AreaSetupController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
