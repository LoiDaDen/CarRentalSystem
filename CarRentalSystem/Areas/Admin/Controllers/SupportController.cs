using Microsoft.AspNetCore.Authorization;
using CarRentalSystem.Authorization;
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class SupportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISupportTicketService _supportTickets;

        public SupportController(ApplicationDbContext context, ISupportTicketService supportTickets)
        {
            _context = context;
            _supportTickets = supportTickets;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status = null, string? format = null)
        {
            var tickets = await GetTicketsAsync(status);
            if (WantsJson(format))
                return Json(tickets);

            ViewBag.Status = status;
            return View(tickets);
        }

        [HttpGet]
        public async Task<IActionResult> Data(string? status = null)
        {
            return Json(await GetTicketsAsync(status));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status, string? resolution)
        {
            var result = await _supportTickets.UpdateStatusAsync(id, status, GetCurrentUserId(), User.Identity?.Name ?? "Staff", resolution);
            if (WantsJson(null))
                return Json(new { success = result.IsSuccess, message = result.Message });

            TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<CarRentalSystem.Models.SupportTicket>> GetTicketsAsync(string? status)
        {
            var query = _context.SupportTickets
                .AsNoTracking()
                .Include(ticket => ticket.User)
                .Include(ticket => ticket.AssignedToUser)
                .Include(ticket => ticket.Booking)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(ticket => ticket.Status == status);

            return await query.OrderByDescending(ticket => ticket.CreatedAt).Take(200).ToListAsync();
        }

        private bool WantsJson(string? format)
        {
            if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
                return true;

            if (Request.Headers.TryGetValue("X-Requested-With", out var requestedWith) &&
                string.Equals(requestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
                return true;

            var accept = Request.GetTypedHeaders().Accept;
            return accept != null &&
                accept.Any(media => string.Equals(media.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase)) &&
                !accept.Any(media => string.Equals(media.MediaType.Value, "text/html", StringComparison.OrdinalIgnoreCase));
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub")
                     ?? User.FindFirst("UserID");
            return int.TryParse(claim?.Value, out var id) ? id : 0;
        }
    }
}
