using CarRentalSystem.Filters;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin", "Manager", "Staff")]
    public class EnterpriseController : Controller
    {
        private readonly IEnterpriseOperationsService _enterpriseOperations;

        public EnterpriseController(IEnterpriseOperationsService enterpriseOperations)
        {
            _enterpriseOperations = enterpriseOperations;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var model = await _enterpriseOperations.GetCommandCenterAsync(cancellationToken);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Snapshot(CancellationToken cancellationToken)
        {
            var model = await _enterpriseOperations.GetCommandCenterAsync(cancellationToken);
            return Json(model);
        }
    }
}
