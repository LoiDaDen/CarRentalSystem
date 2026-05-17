using System.Security.Claims;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class SupportController : Controller
    {
        private readonly ISupportTicketService _supportTickets;

        public SupportController(ISupportTicketService supportTickets)
        {
            _supportTickets = supportTickets;
        }

        [HttpGet]
        public async Task<IActionResult> Mine()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();
            return Json(await _supportTickets.GetForUserAsync(userId.Value));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string ticketType, string priority, string subject, string description, int? bookingId)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var ticket = await _supportTickets.CreateAsync(new SupportTicketCreateRequest(
                userId.Value,
                ticketType,
                priority,
                subject,
                description,
                BookingId: bookingId));

            return Json(new { success = true, ticketId = ticket.SupportTicketID });
        }

        private int? GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? userId
                : null;
        }
    }
}
