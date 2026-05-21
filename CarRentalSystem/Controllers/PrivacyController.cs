using System.Security.Claims;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class PrivacyController : Controller
    {
        private readonly IPrivacyRequestService _privacyRequests;

        public PrivacyController(IPrivacyRequestService privacyRequests)
        {
            _privacyRequests = privacyRequests;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestExport(string? reason)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            await _privacyRequests.CreateAsync(userId.Value, PrivacyRequestTypeConstants.Export, reason);
            TempData["Success"] = "Yêu cầu xuất dữ liệu đã được ghi nhận và sẽ được xem xét.";
            return RedirectToAction("Profile", "Auth");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestDelete(string? reason)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            await _privacyRequests.CreateAsync(userId.Value, PrivacyRequestTypeConstants.Delete, reason);
            TempData["Success"] = "Yêu cầu xóa/anonymize dữ liệu đã được ghi nhận để Admin xem xét.";
            return RedirectToAction("Profile", "Auth");
        }

        private int? GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? userId
                : null;
        }
    }
}
