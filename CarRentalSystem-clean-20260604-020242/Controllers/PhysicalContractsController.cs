using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class PhysicalContractsController : Controller
    {
        [HttpGet]
        public IActionResult Index(
            int? areaId,
            string? q,
            string? typeCode,
            string? status,
            string? fileKind,
            int? carId,
            int? bookingId,
            DateTime? uploadedFrom,
            DateTime? uploadedTo)
        {
            if (User.IsInRole(UserRoleConstants.Admin) ||
                User.IsInRole(UserRoleConstants.Manager) ||
                User.IsInRole(UserRoleConstants.Staff))
            {
                return RedirectToAction("Index", "PhysicalContracts", new
                {
                    area = "Admin",
                    areaId,
                    q,
                    typeCode,
                    status,
                    fileKind,
                    carId,
                    bookingId,
                    uploadedFrom,
                    uploadedTo
                });
            }

            return Forbid();
        }
    }
}
