using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin", "Manager", "Staff")]
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
        public async Task<IActionResult> Index(string? status = null)
        {
            var query = _context.SupportTickets.AsNoTracking().Include(t => t.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status);

            return Json(await query.OrderByDescending(t => t.CreatedAt).Take(200).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status, string? resolution)
        {
            var result = await _supportTickets.UpdateStatusAsync(id, status, GetCurrentUserId(), User.Identity?.Name ?? "Staff", resolution);
            return Json(new { success = result.IsSuccess, message = result.Message });
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
