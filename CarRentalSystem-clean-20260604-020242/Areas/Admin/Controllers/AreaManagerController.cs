// CarRentalSystem/Areas/Admin/Controllers/AreaManagerController.cs
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
    [RoleAuthorize("Admin", "Manager")]
    public class AreaManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AreaManagerController> _logger;
        private readonly IAreaService _areaService;

        public AreaManagerController(
            ApplicationDbContext context,
            ILogger<AreaManagerController> logger,
            IAreaService areaService)
        {
            _context = context;
            _logger = logger;
            _areaService = areaService;
        }

        // ═══════════════════════════════════════════════
        // ALL AREAS — danh sách khu vực + thao tác nhanh
        // ═══════════════════════════════════════════════
        public async Task<IActionResult> AllAreas()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            var areas = await _context.Areas
                .Where(a => !a.IsDeleted)
                .OrderBy(a => a.Code)
                .Select(a => new AreaManagerViewModel
                {
                    AreaID = a.AreaID,
                    Name = a.Name,
                    Code = a.Code,
                    Province = a.Province,
                    Status = a.Status,
                    ManagerName = a.Manager != null ? a.Manager.Name : null,
                    StaffCount = a.AreaStaffs.Count(asx => !asx.IsDeleted),
                    LocationCount = a.AreaLocations.Count(al => !al.IsDeleted)
                })
                .ToListAsync();

            ViewData["Title"] = "Quản lý Nhân sự Khu vực";
            return View(areas);
        }

        // ═══════════════════════════════════════════════
        // AREA SUMMARY — JSON nhẹ cho AJAX
        // ═══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> AreaSummary(int id)
        {
            var area = await _context.Areas
                .Include(a => a.AreaStaffs!)
                .Include(a => a.AreaLocations!)
                .FirstOrDefaultAsync(a => a.AreaID == id && !a.IsDeleted);

            if (area == null)
                return NotFound();

            var locationIds = area.AreaLocations!
                .Where(al => !al.IsDeleted)
                .Select(al => al.LocationID)
                .ToList();

            var totalCars = locationIds.Any()
                ? await _context.Cars.CountAsync(c => locationIds.Contains(c.LocationID) && !c.IsDeleted)
                : 0;

            var availableCars = locationIds.Any()
                ? await _context.Cars.CountAsync(c => locationIds.Contains(c.LocationID) && !c.IsDeleted && c.CarStatusID == 1)
                : 0;

            return Json(new
            {
                status = area.Status,
                managerId = area.ManagerID,
                staffCount = area.AreaStaffs!.Count(asx => !asx.IsDeleted),
                locationCount = area.AreaLocations!.Count(al => !al.IsDeleted),
                totalCars,
                availableCars
            });
        }

        // ═══════════════════════════════════════════════
        // AREA DETAIL — chi tiết khu vực + danh sách nhân sự
        // ═══════════════════════════════════════════════
        public async Task<IActionResult> AreaDetail(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (!await _areaService.CanViewAreaAsync(userId, role, id))
                return Forbid();

            var area = await _context.Areas
                .Include(a => a.AreaStaffs!)
                    .ThenInclude(asx => asx.User)
                .Include(a => a.AreaLocations!)
                    .ThenInclude(al => al.Location)
                .FirstOrDefaultAsync(a => a.AreaID == id && !a.IsDeleted);

            if (area == null)
                return NotFound();

            var vm = new AreaDetailViewModel
            {
                AreaID = area.AreaID,
                Name = area.Name,
                Code = area.Code,
                Province = area.Province,
                Status = area.Status,
                ManagerID = area.ManagerID,
                ManagerName = area.Manager?.Name,
                ActiveStaffs = area.AreaStaffs!
                    .Where(asx => !asx.IsDeleted)
                    .OrderBy(asx => asx.RoleInArea)
                    .ThenBy(asx => asx.User!.Name)
                    .Select(asx => new AreaStaffItemViewModel
                    {
                        ID = asx.ID,
                        UserID = asx.UserID,
                        UserName = asx.User!.Name ?? "?",
                        UserEmail = asx.User!.Email,
                        SystemRole = asx.User!.Role,
                        RoleInArea = asx.RoleInArea,
                        AssignedAt = asx.AssignedAt
                    })
                    .ToList(),
                ActiveLocations = area.AreaLocations!
                    .Where(al => !al.IsDeleted)
                    .Select(al => new AreaLocationItemViewModel
                    {
                        LocationID = al.LocationID,
                        Name = al.Location!.Name,
                        Address = al.Location.Address
                    })
                    .ToList()
            };

            ViewData["Title"] = $"Nhân sự Khu vực — {area.Name}";
            return View(vm);
        }

        // ═══════════════════════════════════════════════
        // ADD STAFF — gán nhân sự vào khu vực (AJAX)
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStaff(AreaStaffAddViewModel vm)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var area = await _context.Areas.FindAsync(vm.AreaID);
            if (area == null || area.IsDeleted)
                return NotFound();

            if (!await CanManageAreaAsync(vm.AreaID))
                return Forbid();

            // Kiểm tra đã tồn tại chưa
            var existing = await _context.AreaStaffs
                .AnyAsync(asx =>
                    asx.AreaID == vm.AreaID &&
                    asx.UserID == vm.UserID &&
                    !asx.IsDeleted);
            if (existing)
                return Conflict(new { message = "User này đã thuộc khu vực rồi." });

            var user = await _context.Users.FindAsync(vm.UserID);
            if (user == null)
                return NotFound(new { message = "User không tồn tại." });

            if (!new[] { "Manager", "Staff", "Viewer" }.Contains(user.Role))
                return BadRequest(new { message = "Chỉ gán được user có role Manager/Staff/Viewer." });

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
                $"Gán \"{user.Name}\" vào KV \"{area.Name}\" với vai trò \"{staff.RoleInArea}\"");

            return Json(new
            {
                success = true,
                message = $"Đã gán \"{user.Name}\" vào \"{area.Name}\".",
                staff = new
                {
                    staff.ID,
                    staff.UserID,
                    userName = user.Name,
                    userEmail = user.Email,
                    systemRole = user.Role,
                    staff.RoleInArea,
                    staff.AssignedAt
                }
            });
        }

        // ═══════════════════════════════════════════════
        // REMOVE STAFF — gỡ nhân sự khỏi khu vực (AJAX)
        // ═══════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> RemoveStaff(int id)
        {
            var staff = await _context.AreaStaffs
                .Include(asx => asx.Area)
                .Include(asx => asx.User)
                .FirstOrDefaultAsync(asx => asx.ID == id && !asx.IsDeleted);

            if (staff == null)
                return NotFound();

            if (!await CanManageAreaAsync(staff.AreaID))
                return Forbid();

            if (staff.Area!.ManagerID == staff.UserID)
                return BadRequest(new { message = "Không thể gỡ Manager chính của khu vực." });

            staff.IsDeleted = true;
            await _context.SaveChangesAsync();

            await WriteAuditLogAsync("Delete", "AreaStaff", staff.ID,
                JsonSerializer.Serialize(new { staff.AreaID, staff.UserID, staff.RoleInArea }),
                null,
                $"Gỡ \"{staff.User?.Name}\" khỏi KV \"{staff.Area.Name}\"");

            return Json(new { success = true, message = $"Đã gỡ \"{staff.User?.Name}\" khỏi khu vực." });
        }

        // ═══════════════════════════════════════════════
        // UPDATE ROLE — đổi vai trò trong khu vực (AJAX)
        // ═══════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> UpdateStaffRole(int id, string newRole)
        {
            var staff = await _context.AreaStaffs
                .Include(asx => asx.Area)
                .Include(asx => asx.User)
                .FirstOrDefaultAsync(asx => asx.ID == id && !asx.IsDeleted);

            if (staff == null)
                return NotFound();

            if (!await CanManageAreaAsync(staff.AreaID))
                return Forbid();

            if (staff.Area!.ManagerID == staff.UserID)
                return BadRequest(new { message = "Không thể đổi vai trò của Manager chính." });

            var validRoles = new[] { "manager", "staff", "viewer" };
            if (!validRoles.Contains(newRole.ToLowerInvariant()))
                return BadRequest(new { message = "Vai trò không hợp lệ." });

            var oldRole = staff.RoleInArea;
            var role = newRole.ToLowerInvariant();

            staff.RoleInArea = role;
            await _context.SaveChangesAsync();

            await WriteAuditLogAsync("Update", "AreaStaff", staff.ID,
                JsonSerializer.Serialize(new { RoleInArea = oldRole }),
                JsonSerializer.Serialize(new { RoleInArea = role }),
                $"Đổi vai trò \"{staff.User?.Name}\" trong KV \"{staff.Area.Name}\": {oldRole} → {role}");

            return Json(new
            {
                success = true,
                message = $"Đã cập nhật vai trò.",
                staff = new { staff.ID, staff.RoleInArea }
            });
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

    // ═══════════════════════════════════════════════════
    // VIEW MODELS
    // ═══════════════════════════════════════════════════
    public class AreaManagerViewModel
    {
        public int AreaID { get; set; }
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string Province { get; set; } = "";
        public string Status { get; set; } = "";
        public string? ManagerName { get; set; }
        public int StaffCount { get; set; }
        public int LocationCount { get; set; }
    }

    public class AreaDetailViewModel
    {
        public int AreaID { get; set; }
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string Province { get; set; } = "";
        public string Status { get; set; } = "";
        public int? ManagerID { get; set; }
        public string? ManagerName { get; set; }

        public List<AreaStaffItemViewModel> ActiveStaffs { get; set; } = new();
        public List<AreaLocationItemViewModel> ActiveLocations { get; set; } = new();
    }

    public class AreaStaffItemViewModel
    {
        public int ID { get; set; }
        public int UserID { get; set; }
        public string UserName { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public string SystemRole { get; set; } = "";
        public string RoleInArea { get; set; } = "";
        public DateTime AssignedAt { get; set; }
    }

    public class AreaLocationItemViewModel
    {
        public int LocationID { get; set; }
        public string Name { get; set; } = "";
        public string? Address { get; set; }
    }
}
