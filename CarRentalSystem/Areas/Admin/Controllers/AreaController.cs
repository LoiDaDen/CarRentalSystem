using CarRentalSystem.Authorization;
// CarRentalSystem/Areas/Admin/Controllers/AreaController.cs
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Helpers;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels.Areas;
using Microsoft.AspNetCore.Authorization;
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
    public class AreaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AreaController> _logger;
        private readonly IAreaService _areaService;

        public AreaController(ApplicationDbContext context, ILogger<AreaController> logger, IAreaService areaService)
        {
            _context = context;
            _logger = logger;
            _areaService = areaService;
        }

        // ═══════════════════════════════════════════════
        // LIST — phân trang, filter theo province / status / search
        // Staff chỉ thấy areas mình thuộc về
        // ═══════════════════════════════════════════════
        public async Task<IActionResult> Index(
            string? search, string? province, string? status,
            int page = 1, int pageSize = 20)
        {
            var currentUserId = GetCurrentUserId();
            var currentRole = GetCurrentRole();

            // Lấy danh sách khu vực của user (dùng chung cho nhiều nơi)
            // Manager và Staff chỉ thấy khu vực được gán vào (qua AreaStaff).
            // Admin thay tat ca.
            List<int> myAreaIds = new();
            if (currentRole == "Staff" || currentRole == "Manager")
            {
                myAreaIds = await _areaService.GetAreaIdsForUserAsync(currentUserId);
            }

            var query = _context.Areas
                .Include(a => a.Manager)
                .Include(a => a.AreaStaffs!).ThenInclude(asx => asx.User)
                .Include(a => a.AreaLocations!).ThenInclude(al => al.Location)
                .Where(a => !a.IsDeleted)
                .AsQueryable();

            // Staff/Manager chi thay areas cua minh
            if (currentRole == "Staff" || currentRole == "Manager")
            {
                query = query.Where(a => myAreaIds.Contains(a.AreaID));
            }

            // Filters
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(a => a.Name.Contains(search) || a.Code.Contains(search));

            if (!string.IsNullOrWhiteSpace(province))
                query = query.Where(a => a.Province == province);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(a => a.Status == status);

            query = query.OrderByDescending(a => a.CreatedAt);

            var paginated = await PaginatedList<Area>.CreateAsync(query, page, pageSize);

            // Dropdown values
            ViewBag.Provinces = await _context.Areas
                .Where(a => !a.IsDeleted)
                .Select(a => a.Province).Distinct().OrderBy(p => p).ToListAsync();

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentProvince = province;
            ViewBag.CurrentStatus = status;

            // Quyền tạo khu vực: chỉ Manager/Staff có khu vực được gán mới thấy được
            ViewBag.UserAreaIds = myAreaIds;
            ViewBag.CanCreateArea = currentRole == "Admin" || (currentRole == "Manager" && myAreaIds.Any());

            return View(paginated);
        }

        // ═══════════════════════════════════════════════
        // CREATE — GET
        // ═══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var currentUserId = GetCurrentUserId();
            var currentRole = GetCurrentRole();

            var usersQuery = _context.Users
                .Where(u => new[] { "Manager", "Staff", "Viewer" }.Contains(u.Role))
                .AsQueryable();

            // Staff không thể gán Manager mới — chỉ xem
            if (currentRole == "Staff")
            {
                usersQuery = usersQuery.Where(u => u.UserID == currentUserId);
            }

            ViewBag.Managers = await usersQuery
                .Select(u => new { u.UserID, u.Name, u.Email })
                .OrderBy(u => u.Name).ToListAsync();

            ViewBag.Locations = await _context.Locations
                .OrderBy(l => l.Name).ToListAsync();

            return View(new AreaCreateViewModel
            {
                Name = "",
                Code = "",
                Province = "",
                Status = "active"
            });
        }

        // ═══════════════════════════════════════════════
        // CREATE — POST
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AreaCreateViewModel vm)
        {
            var currentUserId = GetCurrentUserId();
            var currentRole = GetCurrentRole();

            // Kiểm tra trùng Code
            if (await _context.Areas.AnyAsync(a => a.Code == vm.Code && !a.IsDeleted))
                ModelState.AddModelError(nameof(vm.Code), "Mã khu vực đã tồn tại.");

            if (!ModelState.IsValid)
            {
                ViewBag.Managers = await _context.Users
                    .Where(u => new[] { "Manager", "Staff", "Viewer" }.Contains(u.Role))
                    .Select(u => new { u.UserID, u.Name, u.Email })
                    .OrderBy(u => u.Name).ToListAsync();
                ViewBag.Locations = await _context.Locations.OrderBy(l => l.Name).ToListAsync();
                return View(vm);
            }

            var area = new Area
            {
                Name = vm.Name.Trim(),
                Code = vm.Code.Trim().ToUpperInvariant(),
                Province = vm.Province.Trim(),
                Address = vm.Address?.Trim(),
                Latitude = vm.Latitude,
                Longitude = vm.Longitude,
                Status = vm.Status,
                ManagerID = vm.ManagerUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Areas.Add(area);
            await _context.SaveChangesAsync();

            // Thêm manager vào AreaStaff nếu có
            if (vm.ManagerUserId.HasValue)
            {
                var existingAssignment = await _context.AreaStaffs
                    .AnyAsync(asx => asx.AreaID == area.AreaID
                                   && asx.UserID == vm.ManagerUserId.Value
                                   && !asx.IsDeleted);
                if (!existingAssignment)
                {
                    _context.AreaStaffs.Add(new AreaStaff
                    {
                        AreaID = area.AreaID,
                        UserID = vm.ManagerUserId.Value,
                        RoleInArea = "manager",
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = currentUserId
                    });
                }
            }

            // Gán các Location được chọn
            foreach (var locId in vm.LocationIds.Where(id => id > 0).Distinct())
            {
                var alreadyMapped = await _context.AreaLocations
                    .AnyAsync(al => al.AreaID == area.AreaID && al.LocationID == locId && !al.IsDeleted);
                if (!alreadyMapped)
                {
                    _context.AreaLocations.Add(new AreaLocation
                    {
                        AreaID = area.AreaID,
                        LocationID = locId
                    });
                }
            }

            await _context.SaveChangesAsync();

            // AuditLog — chỉ serialize các trường cần thiết, tránh cycle navigation
            await WriteAuditLogAsync("Create", "Area", area.AreaID,
                null, JsonSerializer.Serialize(new { area.Name, area.Code, area.Province, area.Status, area.ManagerID }),
                $"Tạo khu vực: {area.Name} ({area.Code})");

            TempData["Success"] = $"Khu vực \"{area.Name}\" đã được tạo thành công.";
            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════
        // EDIT — GET
        // ═══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var area = await _context.Areas
                .Include(a => a.AreaLocations!)
                    .ThenInclude(al => al.Location)
                .FirstOrDefaultAsync(a => a.AreaID == id && !a.IsDeleted);

            if (area == null)
                return NotFound();

            // Authorization: Staff chỉ sửa area mình thuộc về
            if (!await CanEditAreaAsync(id))
                return Forbid();

            ViewBag.Managers = await _context.Users
                .Where(u => new[] { "Manager", "Staff", "Viewer" }.Contains(u.Role))
                .Select(u => new { u.UserID, u.Name, u.Email })
                .OrderBy(u => u.Name).ToListAsync();

            ViewBag.Locations = await _context.Locations.OrderBy(l => l.Name).ToListAsync();
            ViewBag.AssignedLocationIds = area.AreaLocations!
                .Where(al => !al.IsDeleted)
                .Select(al => al.LocationID)
                .ToList();

            var vm = new AreaEditViewModel
            {
                AreaID = area.AreaID,
                Name = area.Name,
                Code = area.Code,
                Province = area.Province,
                Address = area.Address,
                Latitude = area.Latitude,
                Longitude = area.Longitude,
                Status = area.Status,
                ManagerID = area.ManagerID,
                Status_ReadOnly = area.Status,
                LocationIds = area.AreaLocations!
                    .Where(al => !al.IsDeleted)
                    .Select(al => al.LocationID)
                    .ToList()
            };

            return View(vm);
        }

        // ═══════════════════════════════════════════════
        // EDIT — POST
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AreaEditViewModel vm)
        {
            var currentUserId = GetCurrentUserId();

            if (!await CanEditAreaAsync(vm.AreaID))
                return Forbid();

            var area = await _context.Areas.FindAsync(vm.AreaID);
            if (area == null || area.IsDeleted)
                return NotFound();

            // Kiểm tra trùng Code (không tính chính nó)
            if (await _context.Areas.AnyAsync(a => a.Code == vm.Code && a.AreaID != vm.AreaID && !a.IsDeleted))
                ModelState.AddModelError(nameof(vm.Code), "Mã khu vực đã tồn tại.");

            if (!ModelState.IsValid)
            {
                ViewBag.Managers = await _context.Users
                    .Where(u => new[] { "Manager", "Staff", "Viewer" }.Contains(u.Role))
                    .Select(u => new { u.UserID, u.Name, u.Email })
                    .OrderBy(u => u.Name).ToListAsync();
                ViewBag.Locations = await _context.Locations.OrderBy(l => l.Name).ToListAsync();
                ViewBag.AssignedLocationIds = vm.LocationIds;
                return View(vm);
            }

            var oldValues = JsonSerializer.Serialize(new { area.Name, area.Code, area.Province, area.Address, area.Status, area.ManagerID });

            area.Name = vm.Name.Trim();
            area.Code = vm.Code.Trim().ToUpperInvariant();
            area.Province = vm.Province.Trim();
            area.Address = vm.Address?.Trim();
            area.Latitude = vm.Latitude;
            area.Longitude = vm.Longitude;
            area.Status = vm.Status;
            area.ManagerID = vm.ManagerID;
            area.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Sync AreaStaff — đảm bảo Manager luôn có trong AreaStaff
            if (vm.ManagerID.HasValue)
            {
                var existingAssignment = await _context.AreaStaffs
                    .AnyAsync(asx => asx.AreaID == area.AreaID
                                   && asx.UserID == vm.ManagerID.Value
                                   && !asx.IsDeleted);
                if (!existingAssignment)
                {
                    _context.AreaStaffs.Add(new AreaStaff
                    {
                        AreaID = area.AreaID,
                        UserID = vm.ManagerID.Value,
                        RoleInArea = "manager",
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = currentUserId
                    });
                }
            }

            // Sync AreaLocations
            var currentLocs = await _context.AreaLocations
                .Where(al => al.AreaID == area.AreaID && !al.IsDeleted)
                .Select(al => al.LocationID)
                .ToListAsync();

            // Remove unselected locations
            foreach (var locId in currentLocs.Where(id => !vm.LocationIds.Contains(id)))
            {
                var al = await _context.AreaLocations
                    .FirstOrDefaultAsync(x => x.AreaID == area.AreaID && x.LocationID == locId && !x.IsDeleted);
                if (al != null) al.IsDeleted = true;
            }

            // Add newly selected locations
            foreach (var locId in vm.LocationIds.Where(id => id > 0 && !currentLocs.Contains(id)))
            {
                _context.AreaLocations.Add(new AreaLocation
                {
                    AreaID = area.AreaID,
                    LocationID = locId
                });
            }

            // AuditLog OldValues/NewValues
            var newValues = JsonSerializer.Serialize(new { area.Name, area.Code, area.Province, area.Address, area.Status, area.ManagerID });
            await WriteAuditLogAsync("Update", "Area", area.AreaID, oldValues, newValues,
                $"Cập nhật khu vực: {area.Name} ({area.Code})");

            TempData["Success"] = $"Khu vực \"{area.Name}\" đã được cập nhật.";
            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════
        // DELETE — POST (soft delete)
        // Chặn nếu còn Booking active trong area
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var area = await _context.Areas
                .Include(a => a.AreaLocations!)
                    .ThenInclude(al => al.Location)
                        .ThenInclude(l => l!.Cars)
                .FirstOrDefaultAsync(a => a.AreaID == id && !a.IsDeleted);

            if (area == null)
                return NotFound();

            if (!await CanEditAreaAsync(id))
                return Forbid();

            // Kiểm tra booking active
            var locationIds = area.AreaLocations!
                .Where(al => !al.IsDeleted)
                .Select(al => al.LocationID)
                .ToList();

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
                TempData["Error"] = "Không thể xóa khu vực đang có booking đang hoạt động. Vui lòng hoàn tất hoặc hủy các booking trước.";
                return RedirectToAction(nameof(Index));
            }

            var oldValues = JsonSerializer.Serialize(new { area.Name, area.Code, area.Status });

            area.IsDeleted = true;
            await _context.SaveChangesAsync();

            await WriteAuditLogAsync("Delete", "Area", area.AreaID,
                oldValues, null, $"Xóa khu vực: {area.Name} ({area.Code})");

            TempData["Success"] = $"Khu vực \"{area.Name}\" đã được xóa.";
            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════
        // DETAILS — redirect sang Show (nếu cần)
        // ═══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            return RedirectToAction("Show", new { id });
        }

        // ═══════════════════════════════════════════════
        // SHOW — trang chi tiết area
        // ═══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Show(
            int id,
            int? bookingsPage,
            int? statusFilter,
            string? fromDate,
            string? toDate)
        {
            var area = await _context.Areas
                .Include(a => a.Manager)
                .Include(a => a.AreaStaffs!).ThenInclude(asx => asx.User)
                .Include(a => a.AreaLocations!).ThenInclude(al => al.Location)
                    .ThenInclude(l => l!.Cars)
                .FirstOrDefaultAsync(a => a.AreaID == id && !a.IsDeleted);

            if (area == null)
                return NotFound();

            if (!await CanViewAreaAsync(id))
                return Forbid();

            var locationIds = area.AreaLocations!
                .Where(al => !al.IsDeleted && al.Location != null)
                .Select(al => al.LocationID)
                .ToList();

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var chartStart = now.Date.AddDays(-29);

            // ── Monthly stats ──────────────────────────────────────
            var activeStatusIds = await _context.BookingStatuses
                .Where(bs => !new[] { "Completed", "Cancelled", "Rejected" }.Contains(bs.Name))
                .Select(bs => bs.BookingStatusID)
                .ToListAsync();

            var completedStatusIds = await _context.BookingStatuses
                .Where(bs => bs.Name == "Completed")
                .Select(bs => bs.BookingStatusID)
                .ToListAsync();

            var monthlyBookings = locationIds.Any()
                ? await _context.Bookings
                    .Where(b => b.Car != null
                             && locationIds.Contains(b.Car.LocationID)
                             && !b.IsDeleted
                             && b.CreatedAt >= startOfMonth)
                    .ToListAsync()
                : new List<Booking>();

            ViewBag.MonthlyOrders = monthlyBookings.Count;
            ViewBag.MonthlyRevenue = monthlyBookings
                .Where(b => completedStatusIds.Contains(b.BookingStatusID))
                .Sum(b => b.TotalPrice);

            // ── Chart data: 30 ngày ───────────────────────────────
            var chartData = new int[30];
            for (int i = 0; i < 30; i++)
            {
                var day = chartStart.AddDays(i);
                chartData[i] = monthlyBookings.Count(b =>
                    b.CreatedAt >= day && b.CreatedAt < day.AddDays(1));
            }
            var chartLabels = Enumerable.Range(0, 30)
                .Select(i => chartStart.AddDays(i).ToString("dd/MM"))
                .ToArray();

            ViewBag.ChartData = System.Text.Json.JsonSerializer.Serialize(chartData);
            ViewBag.ChartLabels = System.Text.Json.JsonSerializer.Serialize(chartLabels);

            // ── Booking statuses dropdown ───────────────────────────
            ViewBag.BookingStatuses = await _context.BookingStatuses
                .OrderBy(bs => bs.BookingStatusID)
                .ToListAsync();

            // ── Bookings list ──────────────────────────────────────
            var bookingsQuery = locationIds.Any()
                ? _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Car).ThenInclude(c => c!.CarBrand)
                    .Include(b => b.BookingStatus)
                    .Where(b => b.Car != null
                             && locationIds.Contains(b.Car.LocationID)
                             && !b.IsDeleted)
                : _context.Bookings.Where(b => false).AsQueryable();

            if (statusFilter.HasValue && statusFilter > 0)
                bookingsQuery = bookingsQuery.Where(b => b.BookingStatusID == statusFilter.Value);

            if (DateTime.TryParse(fromDate, out var from))
                bookingsQuery = bookingsQuery.Where(b => b.CreatedAt >= from);

            if (DateTime.TryParse(toDate, out var to))
                bookingsQuery = bookingsQuery.Where(b => b.CreatedAt <= to.AddDays(1));

            bookingsQuery = bookingsQuery.OrderByDescending(b => b.CreatedAt);

            var bookingsPageSize = 20;
            var bookingsPageNum = bookingsPage ?? 1;
            ViewBag.Bookings = await PaginatedList<Booking>.CreateAsync(bookingsQuery, bookingsPageNum, bookingsPageSize);
            ViewBag.BookingsTotal = ViewBag.Bookings.TotalCount;
            ViewBag.BookingsPage = bookingsPageNum;
            ViewBag.BookingsPageSize = bookingsPageSize;

            ViewBag.CurrentStatusFilter = statusFilter;
            ViewBag.CurrentFromDate = fromDate;
            ViewBag.CurrentToDate = toDate;

            return View(area);
        }

        // ═══════════════════════════════════════════════
        // TRASH — danh sách đã xóa (Manager only)
        // ═══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Trash(int page = 1, int pageSize = 20)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();
            if (!await _areaService.CanManageAreaAsync(userId, role, 0))
                return Forbid();

            var query = _context.Areas
                .IgnoreQueryFilters()
                .Include(a => a.Manager)
                .Where(a => a.IsDeleted)
                .OrderByDescending(a => a.UpdatedAt)
                .AsQueryable();

            var paginated = await PaginatedList<Area>.CreateAsync(query, page, pageSize);
            return View("Trash", paginated);
        }

        // ═══════════════════════════════════════════════
        // RESTORE — khôi phục (Manager only)
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            if (!await CanEditAreaAsync(id))
                return Forbid();

            var area = await _context.Areas.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.AreaID == id);
            if (area == null || !area.IsDeleted)
                return NotFound();

            area.IsDeleted = false;
            area.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await WriteAuditLogAsync("Restore", "Area", area.AreaID,
                null, JsonSerializer.Serialize(new { area.Name, area.Code }),
                $"Khôi phục khu vực: {area.Name} ({area.Code})");

            TempData["Success"] = $"Khu vực \"{area.Name}\" đã được khôi phục.";
            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════
        // DELETE PERMANENT — xóa vĩnh viễn (Admin only)
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
        public async Task<IActionResult> DeletePermanent(int id)
        {
            // Lấy area đã bị soft delete
            var area = await _context.Areas
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.AreaID == id && a.IsDeleted);

            if (area == null)
                return NotFound();

            var areaName = area.Name;
            var areaCode = area.Code;

            try
            {
                // Sử dụng ExecuteDelete để xóa trực tiếp trong DB (bypass EF tracking)
                // Xóa AreaStaffs trước (vì có FK đến Areas)
                await _context.AreaStaffs
                    .IgnoreQueryFilters()
                    .Where(asx => asx.AreaID == id)
                    .ExecuteDeleteAsync();

                // Xóa AreaLocations (vì có FK đến Areas)
                await _context.AreaLocations
                    .IgnoreQueryFilters()
                    .Where(al => al.AreaID == id)
                    .ExecuteDeleteAsync();

                // Xóa Area
                await _context.Areas
                    .IgnoreQueryFilters()
                    .Where(a => a.AreaID == id)
                    .ExecuteDeleteAsync();

                // Log audit
                await WriteAuditLogAsync("DeletePermanent", "Area", id,
                    JsonSerializer.Serialize(new { areaName, areaCode }), null,
                    $"Xóa vĩnh viễn khu vực: {areaName} ({areaCode})");

                TempData["Success"] = $"Khu vực \"{areaName}\" đã bị xóa vĩnh viễn.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa vĩnh viễn khu vực ID: {AreaId}", id);
                TempData["Error"] = "Đã xảy ra lỗi khi xóa vĩnh viễn khu vực. Vui lòng thử lại.";
            }

            return RedirectToAction(nameof(Trash));
        }

        // ═══════════════════════════════════════════════
        // SET CURRENT AREA — lưu khu vực đang làm việc vào cookie
        // ═══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> SetCurrentArea(int areaId, string? returnUrl)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            // Admin: cho phep chon bat ky area nao
            // Manager/Staff: chi cho phep chon area thuoc ve minh
            if (role != "Admin" && !await _areaService.IsAreaAllowedForUserAsync(userId, areaId))
                return Forbid();

            if (areaId <= 0)
            {
                // Xoa cookie → reset
                Response.Cookies.Delete("CurrentAreaId");
            }
            else
            {
                Response.Cookies.Append("CurrentAreaId", areaId.ToString(), new CookieOptions
                {
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(30)
                });
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "AreaDashboard");
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

        private async Task<bool> CanEditAreaAsync(int areaId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();
            return await _areaService.CanManageAreaAsync(userId, role, areaId);
        }

        private async Task<bool> CanViewAreaAsync(int areaId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();
            return await _areaService.CanViewAreaAsync(userId, role, areaId);
        }

        private async Task WriteAuditLogAsync(
            string action, string entityType, int? entityId,
            string? oldValues, string? newValues, string description)
        {
            var userId = GetCurrentUserId();
            var userName = User.Identity?.Name ?? "System";

            _context.AuditLogs.Add(new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                UserId = userId > 0 ? userId : null,
                UserName = userName,
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
