// CarRentalSystem/Areas/Admin/Controllers/AreaStaffController.cs
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
    [RoleAuthorize("Admin", "Manager", "Staff")]
    public class AreaStaffController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AreaStaffController> _logger;
        private readonly IAreaService _areaService;

        public AreaStaffController(ApplicationDbContext context, ILogger<AreaStaffController> logger, IAreaService areaService)
        {
            _context = context;
            _logger = logger;
            _areaService = areaService;
        }

        // ═══════════════════════════════════════════════
        // INDEX — danh sách staff của một area
        // ═══════════════════════════════════════════════
        public async Task<IActionResult> Index(int areaId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            // Neu chua chon area → lay area chinh cua user hien tai lam mac dinh
            if (areaId <= 0)
            {
                var areaIds = await _areaService.GetAreaIdsForUserAsync(userId);
                if (areaIds.Count > 0)
                    areaId = areaIds[0];
            }

            if (areaId <= 0)
            {
                TempData["Info"] = "Bạn chưa được gán vào khu vực nào.";
                ViewBag.Area = null;
                ViewBag.ManagerRole = "manager";
                ViewBag.AllRoles = new[] { "manager", "staff", "viewer" };
                return View(new List<AreaStaff>());
            }

            var area = await _context.Areas
                .Include(a => a.AreaStaffs!)
                    .ThenInclude(asx => asx.User)
                .Include(a => a.Manager)
                .FirstOrDefaultAsync(a => a.AreaID == areaId && !a.IsDeleted);

            if (area == null)
                return NotFound();

            if (!await _areaService.CanViewAreaAsync(userId, role, areaId))
                return Forbid();

            var activeStaffs = area.AreaStaffs!
                .Where(asx => !asx.IsDeleted)
                .OrderBy(asx => asx.RoleInArea)
                .ThenBy(asx => asx.User!.Name)
                .ToList();

            ViewBag.Area = area;
            ViewBag.ManagerRole = "manager";
            ViewBag.AllRoles = new[] { "manager", "staff", "viewer" };

            return View(activeStaffs);
        }

        // ═══════════════════════════════════════════════
        // ADD — gán user vào area
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(AreaStaffAddViewModel vm)
        {
            var area = await _context.Areas.FindAsync(vm.AreaID);
            if (area == null || area.IsDeleted)
                return NotFound();

            if (!await CanManageAreaAsync(vm.AreaID))
                return Forbid();

            // Validate không trùng
            if (await _context.AreaStaffs.AnyAsync(asx =>
                asx.AreaID == vm.AreaID && asx.UserID == vm.UserID && !asx.IsDeleted))
            {
                TempData["Error"] = "User này đã thuộc khu vực rồi.";
                return RedirectToAction(nameof(Index), new { areaId = vm.AreaID });
            }

            // Validate user tồn tại và có role phù hợp
            var user = await _context.Users.FindAsync(vm.UserID);
            if (user == null)
            {
                TempData["Error"] = "User không tồn tại.";
                return RedirectToAction(nameof(Index), new { areaId = vm.AreaID });
            }

            if (!new[] { "Manager", "Staff", "Viewer" }.Contains(user.Role))
            {
                TempData["Error"] = "Chỉ có thể gán user có role Manager/Staff/Viewer vào khu vực.";
                return RedirectToAction(nameof(Index), new { areaId = vm.AreaID });
            }

            var currentUserId = GetCurrentUserId();

            var staff = new AreaStaff
            {
                AreaID = vm.AreaID,
                UserID = vm.UserID,
                RoleInArea = vm.RoleInArea.ToLowerInvariant(),
                AssignedAt = DateTime.UtcNow,
                AssignedBy = currentUserId > 0 ? currentUserId : null
            };

            _context.AreaStaffs.Add(staff);
            await _context.SaveChangesAsync();

            await WriteAuditLogAsync("Create", "AreaStaff", staff.ID, null,
                JsonSerializer.Serialize(new { staff.AreaID, staff.UserID, staff.RoleInArea }),
                $"Gán user \"{user.Name}\" ({user.Email}) vào khu vực \"{area.Name}\" với vai trò \"{staff.RoleInArea}\"");

            TempData["Success"] = $"Đã gán \"{user.Name}\" vào khu vực \"{area.Name}\".";
            return RedirectToAction(nameof(Index), new { areaId = vm.AreaID });
        }

        // ═══════════════════════════════════════════════
        // UPDATE ROLE — đổi RoleInArea của staff
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRole(AreaStaffUpdateRoleViewModel vm)
        {
            var staff = await _context.AreaStaffs
                .Include(asx => asx.Area)
                .Include(asx => asx.User)
                .FirstOrDefaultAsync(asx => asx.ID == vm.StaffID && !asx.IsDeleted);

            if (staff == null || staff.Area == null)
                return NotFound();

            if (!await CanManageAreaAsync(staff.AreaID))
                return Forbid();

            var oldRole = staff.RoleInArea;
            var newRole = vm.NewRole.ToLowerInvariant();

            if (oldRole == newRole)
                return RedirectToAction(nameof(Index), new { areaId = staff.AreaID });

            // Chặn đổi role nếu user đó là ManagerID của area
            if (staff.Area.ManagerID == staff.UserID)
            {
                TempData["Error"] = "Không thể thay đổi vai trò của Manager chính khu vực. Hãy đổi Manager của khu vực trước.";
                return RedirectToAction(nameof(Index), new { areaId = staff.AreaID });
            }

            staff.RoleInArea = newRole;
            await _context.SaveChangesAsync();

            await WriteAuditLogAsync("Update", "AreaStaff", staff.ID,
                JsonSerializer.Serialize(new { RoleInArea = oldRole }),
                JsonSerializer.Serialize(new { RoleInArea = newRole }),
                $"Đổi vai trò của \"{staff.User?.Name}\" trong khu vực \"{staff.Area.Name}\" từ \"{oldRole}\" → \"{newRole}\"");

            TempData["Success"] = $"Đã cập nhật vai trò của \"{staff.User?.Name}\" thành \"{newRole}\".";
            return RedirectToAction(nameof(Index), new { areaId = staff.AreaID });
        }

        // ═══════════════════════════════════════════════
        // REMOVE — gỡ staff khỏi area
        // Chặn nếu là ManagerID của area
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id)
        {
            var staff = await _context.AreaStaffs
                .Include(asx => asx.Area)
                .Include(asx => asx.User)
                .FirstOrDefaultAsync(asx => asx.ID == id && !asx.IsDeleted);

            if (staff == null || staff.Area == null)
                return NotFound();

            if (!await CanManageAreaAsync(staff.AreaID))
                return Forbid();

            // Chặn xóa Manager chính
            if (staff.Area.ManagerID == staff.UserID)
            {
                TempData["Error"] = "Không thể gỡ Manager chính của khu vực. Hãy đổi Manager của khu vực trước.";
                return RedirectToAction(nameof(Index), new { areaId = staff.AreaID });
            }

            staff.IsDeleted = true;
            await _context.SaveChangesAsync();

            await WriteAuditLogAsync("Delete", "AreaStaff", staff.ID,
                JsonSerializer.Serialize(new { staff.AreaID, staff.UserID, staff.RoleInArea }),
                null,
                $"Gỡ user \"{staff.User?.Name}\" khỏi khu vực \"{staff.Area.Name}\"");

            TempData["Success"] = $"Đã gỡ \"{staff.User?.Name}\" khỏi khu vực.";
            return RedirectToAction(nameof(Index), new { areaId = staff.AreaID });
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
