using Microsoft.AspNetCore.Authorization;
using CarRentalSystem.Authorization;
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class LostFoundController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAreaService _areaService;
        private readonly IAuditTrailService _auditTrail;

        public LostFoundController(ApplicationDbContext context, IAreaService areaService, IAuditTrailService auditTrail)
        {
            _context = context;
            _areaService = areaService;
            _auditTrail = auditTrail;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status = null)
        {
            var query = _context.LostFoundItems
                .Include(i => i.Booking).ThenInclude(b => b!.User)
                .Include(i => i.Booking).ThenInclude(b => b!.Car).ThenInclude(c => c!.CarBrand)
                .Include(i => i.HandoverRecord)
                .OrderByDescending(i => i.FoundAt)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(i => i.Status == status);

            var items = await query.Take(300).ToListAsync();
            if (User.IsInRole(UserRoleConstants.Manager) || User.IsInRole(UserRoleConstants.Staff))
            {
                var allowedLocationIds = await _areaService.GetLocationIdsForUserAsync(CurrentUserId(), CurrentRole());
                items = items
                    .Where(i => i.Booking?.Car != null && allowedLocationIds.Contains(i.Booking.Car.LocationID))
                    .ToList();
            }

            ViewBag.Status = status;
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status, string? note)
        {
            if (!LostFoundStatusConstants.All.Contains(status))
                return BadRequest("Unsupported lost-found status.");

            var item = await _context.LostFoundItems
                .Include(i => i.Booking).ThenInclude(b => b!.Car)
                .FirstOrDefaultAsync(i => i.LostFoundItemID == id);
            if (item?.Booking == null)
                return NotFound();

            if (!await CanAccessBookingAsync(item.Booking))
                return Forbid();

            var oldStatus = item.Status;
            item.Status = status;
            item.ResolutionNote = note;

            if (status == LostFoundStatusConstants.CustomerNotified && !item.CustomerNotifiedAt.HasValue)
                item.CustomerNotifiedAt = DateTime.UtcNow;

            if (status is LostFoundStatusConstants.Returned or LostFoundStatusConstants.Disposed)
            {
                item.ResolvedAt = DateTime.UtcNow;
                item.ResolvedByUserID = CurrentUserId();
            }

            await _context.SaveChangesAsync();
            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "UpdateLostFoundStatus",
                EntityType = nameof(LostFoundItem),
                EntityId = item.LostFoundItemID,
                UserId = CurrentUserId(),
                Reason = note,
                OldValues = System.Text.Json.JsonSerializer.Serialize(new { Status = oldStatus }),
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { item.Status, item.CustomerNotifiedAt, item.ResolvedAt })
            });

            TempData["Success"] = "Lost-found status updated.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> CanAccessBookingAsync(Booking booking)
        {
            if (User.IsInRole(UserRoleConstants.Admin)) return true;
            if (!User.IsInRole(UserRoleConstants.Manager) && !User.IsInRole(UserRoleConstants.Staff))
                return false;

            var locationId = booking.PickupLocationID > 0 ? booking.PickupLocationID : booking.Car?.LocationID;
            if (!locationId.HasValue || locationId.Value <= 0)
                return false;

            var areaId = await _areaService.GetAreaIdForLocationAsync(locationId.Value);
            return areaId.HasValue && await _areaService.IsAreaAllowedForUserAsync(CurrentUserId(), areaId.Value);
        }

        private int CurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        }

        private string CurrentRole()
        {
            return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        }
    }
}
