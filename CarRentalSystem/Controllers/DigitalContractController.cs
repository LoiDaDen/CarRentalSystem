using System.Security.Claims;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class DigitalContractController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDigitalContractService _digitalContractService;
        private readonly IWebHostEnvironment _environment;
        private readonly IAuditTrailService _auditTrail;

        public DigitalContractController(
            ApplicationDbContext context,
            IDigitalContractService digitalContractService,
            IWebHostEnvironment environment,
            IAuditTrailService auditTrail)
        {
            _context = context;
            _digitalContractService = digitalContractService;
            _environment = environment;
            _auditTrail = auditTrail;
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var contract = await LoadContractAsync(id);
            if (contract == null) return NotFound();
            if (!CanView(contract, userId)) return Forbid();

            ViewBag.CanSign = CanSign(contract, userId);
            ViewBag.RenderedContent = _digitalContractService.RenderContractContent(contract);
            await RecordViewAuditAsync(contract, userId);
            return View(contract);
        }

        [HttpGet]
        public async Task<IActionResult> Sign(int id)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var contract = await LoadContractAsync(id);
            if (contract == null) return NotFound();
            if (!CanSign(contract, userId)) return Forbid();

            if (contract.IsSignedByCustomer)
            {
                TempData["Info"] = "Hợp đồng này đã được ký.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.CanSign = true;
            ViewBag.RenderedContent = _digitalContractService.RenderContractContent(contract);
            return View(contract);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sign(int id, string? signatureBase64)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var contract = await LoadContractAsync(id);
            if (contract == null) return NotFound();
            if (!CanSign(contract, userId)) return Forbid();

            var result = await _digitalContractService.SignCustomerAsync(
                id,
                userId,
                signatureBase64,
                _environment.ContentRootPath,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                $"POST {Request.Path}{Request.QueryString}");

            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể ký hợp đồng.");
                ViewBag.CanSign = true;
                ViewBag.RenderedContent = _digitalContractService.RenderContractContent(contract);
                return View(contract);
            }

            TempData["Success"] = "Bạn đã ký hợp đồng số thành công.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<DigitalContract?> LoadContractAsync(int id)
        {
            return await _context.DigitalContracts
                .Include(c => c.Booking).ThenInclude(b => b!.User)
                .Include(c => c.Booking).ThenInclude(b => b!.Car).ThenInclude(c => c!.CarBrand)
                .Include(c => c.Booking).ThenInclude(b => b!.Car).ThenInclude(c => c!.Owner)
                .Include(c => c.BookingTransaction).ThenInclude(t => t!.User)
                .Include(c => c.BookingTransaction).ThenInclude(t => t!.Bookings).ThenInclude(b => b.Car).ThenInclude(c => c!.CarBrand)
                .Include(c => c.BookingTransaction).ThenInclude(t => t!.Bookings).ThenInclude(b => b.Car).ThenInclude(c => c!.Owner)
                .FirstOrDefaultAsync(c => c.DigitalContractID == id);
        }

        private bool CanView(DigitalContract contract, int userId)
        {
            return CanSign(contract, userId) || CanOwnerView(contract, userId) || User.IsInRole(UserRoleConstants.Admin);
        }

        private static bool CanSign(DigitalContract contract, int userId)
        {
            return contract.Booking?.UserID == userId || contract.BookingTransaction?.UserID == userId;
        }

        private static bool CanOwnerView(DigitalContract contract, int userId)
        {
            if (contract.Booking?.Car?.OwnerID == userId)
                return true;

            return contract.BookingTransaction?.Bookings.Any(b => b.Car?.OwnerID == userId) == true;
        }

        private async Task RecordViewAuditAsync(DigitalContract contract, int userId)
        {
            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "ViewDigitalContract",
                EntityType = nameof(DigitalContract),
                EntityId = contract.DigitalContractID,
                UserId = userId,
                UserName = User.Identity?.Name,
                Description = $"User viewed digital contract {contract.ContractNo}.",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                RequestUrl = $"{Request.Method} {Request.Path}{Request.QueryString}",
                Timestamp = DateTime.UtcNow
            });
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdString, out userId);
        }
    }
}
