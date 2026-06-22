using Microsoft.AspNetCore.Authorization;
using CarRentalSystem.Authorization;
// CarRentalSystem/Areas/Admin/Controllers/AreaLocationController.cs
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels.Areas;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class AreaLocationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AreaLocationController> _logger;
        private readonly IAreaService _areaService;

        public AreaLocationController(ApplicationDbContext context, ILogger<AreaLocationController> logger, IAreaService areaService)
        {
            _context = context;
            _logger = logger;
            _areaService = areaService;
        }

        // ═══════════════════════════════════════════════
        // ADD — gán Location vào Area
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(AreaLocationAddViewModel vm)
        {
            var area = await _context.Areas.FindAsync(vm.AreaID);
            if (area == null || area.IsDeleted)
                return NotFound();

            if (!await CanManageAreaAsync(vm.AreaID))
                return Forbid();

            var location = await _context.Locations.FindAsync(vm.LocationID);
            if (location == null)
            {
                TempData["Error"] = "Location không tồn tại.";
                return RedirectToAction("Show", "Area", new { id = vm.AreaID });
            }

            // Dùng AsNoTracking + IgnoreQueryFilters để query trực tiếp DB,
            // tránh EF Core ChangeTracker trả về entity đã tracked (có thể là bản cũ)
            var existingMapping = await _context.AreaLocations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(al => al.AreaID == vm.AreaID && al.LocationID == vm.LocationID);

            if (existingMapping != null)
            {
                TempData["Error"] = existingMapping.IsDeleted
                    ? "Không thể thêm địa điểm này — bản ghi đã bị xóa trước đó."
                    : "Địa điểm này đã thuộc khu vực rồi.";
                return RedirectToAction("Show", "Area", new { id = vm.AreaID });
            }

            // Kiểm tra lại lần nữa với tracked query (phòng trường hợp race condition)
            var trackedConflict = await _context.AreaLocations
                .AnyAsync(al => al.AreaID == vm.AreaID && al.LocationID == vm.LocationID);
            if (trackedConflict)
            {
                TempData["Error"] = "Bản ghi này đã tồn tại trong hệ thống.";
                return RedirectToAction("Show", "Area", new { id = vm.AreaID });
            }

            _context.AreaLocations.Add(new AreaLocation
            {
                AreaID = vm.AreaID,
                LocationID = vm.LocationID
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key") == true)
            {
                TempData["Error"] = "Bản ghi này đã tồn tại trong cơ sở dữ liệu.";
                return RedirectToAction("Show", "Area", new { id = vm.AreaID });
            }

            await WriteAuditLogAsync("Create", "AreaLocation", null, null,
                JsonSerializer.Serialize(new { vm.AreaID, vm.LocationID }),
                $"Gán location \"{location.Name}\" vào khu vực \"{area.Name}\"");

            TempData["Success"] = $"Đã gán \"{location.Name}\" vào khu vực \"{area.Name}\".";
            return RedirectToAction("Show", "Area", new { id = vm.AreaID });
        }

        // ═══════════════════════════════════════════════
        // REMOVE — gỡ Location khỏi Area
        // Chặn nếu có Booking active trong Area
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int areaId, int locationId)
        {
            var area = await _context.Areas.FindAsync(areaId);
            if (area == null || area.IsDeleted)
                return NotFound();

            if (!await CanManageAreaAsync(areaId))
                return Forbid();

            var mapping = await _context.AreaLocations
                .Include(al => al.Location)
                .FirstOrDefaultAsync(al =>
                    al.AreaID == areaId &&
                    al.LocationID == locationId &&
                    !al.IsDeleted);

            if (mapping == null)
            {
                TempData["Error"] = "Mapping không tồn tại.";
                return RedirectToAction("Show", "Area", new { id = areaId });
            }

            // Kiểm tra booking active trong Area (không chỉ riêng location đó)
            var locationIds = await _context.AreaLocations
                .Where(al => al.AreaID == areaId && !al.IsDeleted)
                .Select(al => al.LocationID)
                .ToListAsync();

            var activeStatuses = await _context.BookingStatuses
                .Where(bs => !new[] { "Completed", "Cancelled", "Rejected" }.Contains(bs.Name))
                .Select(bs => bs.BookingStatusID)
                .ToListAsync();

            var hasActiveBooking = locationIds.Any() && await _context.Bookings
                .IgnoreQueryFilters()
                .AnyAsync(b =>
                    b.Car != null &&
                    locationIds.Contains(b.Car.LocationID) &&
                    activeStatuses.Contains(b.BookingStatusID) &&
                    !b.IsDeleted);

            if (hasActiveBooking)
            {
                TempData["Error"] = "Không thể gỡ location khi khu vực đang có booking đang hoạt động. Vui lòng hoàn tất hoặc hủy các booking trước.";
                return RedirectToAction("Show", "Area", new { id = areaId });
            }

            mapping.IsDeleted = true;
            await _context.SaveChangesAsync();

            await WriteAuditLogAsync("Delete", "AreaLocation", null,
                JsonSerializer.Serialize(new { AreaID = areaId, LocationID = locationId }),
                null,
                $"Gỡ location \"{mapping.Location?.Name}\" khỏi khu vực \"{area.Name}\"");

            TempData["Success"] = $"Đã gỡ \"{mapping.Location?.Name}\" khỏi khu vực \"{area.Name}\".";
            return RedirectToAction("Show", "Area", new { id = areaId });
        }

        // ═══════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════
        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub")
                     ?? User.FindFirst("UserID");
            return int.TryParse(claim?.Value, out var id) ? id : 0;
        }

        private string GetCurrentRole() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
         ?? User.FindFirst("role")?.Value ?? "";

        private async Task<bool> CanManageAreaAsync(int areaId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();
            return await _areaService.CanManageAreaAsync(userId, role, areaId);
        }

        private async Task WriteAuditLogAsync(
            string action, string entityType, int? entityId,
            string? oldValues, string? newValues, string description)
        {
            var userId = GetCurrentUserId();

            _context.AuditLogs.Add(new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                UserId = userId > 0 ? userId : null,
                UserName = User.Identity?.Name ?? "System",
                Description = description,
                OldValues = oldValues,
                NewValues = newValues,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers["User-Agent"].ToString().Length > 500
                    ? Request.Headers["User-Agent"].ToString()[..500]
                    : Request.Headers["User-Agent"].ToString(),
                RequestUrl = Request.Path,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }
    }
}
