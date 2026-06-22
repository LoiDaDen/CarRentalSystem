using CarRentalSystem.Authorization;
using System.Security.Claims;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class DisputeController : Controller
    {
        private readonly IDisputeExportService _disputeExport;
        private readonly IConfiguration _configuration;

        public DisputeController(IDisputeExportService disputeExport, IConfiguration configuration)
        {
            _disputeExport = disputeExport;
            _configuration = configuration;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Export(int bookingId, bool? includeSensitive = null)
        {
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
            var include = includeSensitive ?? _configuration.GetValue("DisputeExport:IncludeSensitiveDefault", false);
            var result = await _disputeExport.ExportBookingAsync(bookingId, userId, include);
            if (result == null)
                return NotFound();

            Response.Headers["X-Dispute-Package-Hash"] = result.ContentHash;
            return File(result.Content, "application/zip", result.FileName);
        }
    }
}
