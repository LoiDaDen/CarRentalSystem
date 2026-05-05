using CarRentalSystem.Data;
using CarRentalSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuditLogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditLogController> _logger;

        public AuditLogController(ApplicationDbContext context, ILogger<AuditLogController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Danh sách audit log với phân trang và filter.
        /// </summary>
        public async Task<IActionResult> Index(
            string? action, string? entityType, string? userName,
            DateTime? fromDate, DateTime? toDate,
            int page = 1, int pageSize = 25)
        {
            // Kiểm tra quyền Admin
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != "Admin") return Forbid();

            try
            {
                // DEBUG: Log timezone and date filter values
                _logger.LogInformation("[AuditLog-DEBUG] fromDate={FromDate} ({Kind}) toDate={ToDate} ({Kind}) page={Page}",
                    fromDate, fromDate?.Kind, toDate, toDate?.Kind, page);

                var query = _context.AuditLogs
                    .OrderByDescending(a => a.Timestamp)
                    .AsQueryable();

                // Filters
                if (!string.IsNullOrEmpty(action))
                    query = query.Where(a => a.Action.Contains(action));

                if (!string.IsNullOrEmpty(entityType))
                    query = query.Where(a => a.EntityType.Contains(entityType));

                if (!string.IsNullOrEmpty(userName))
                    query = query.Where(a => a.UserName != null && a.UserName.Contains(userName));

                if (fromDate.HasValue)
                {
                    _logger.LogInformation("[AuditLog-DEBUG] fromDate filter applied: {FromDate}", fromDate.Value);
                    query = query.Where(a => a.Timestamp >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    _logger.LogInformation("[AuditLog-DEBUG] toDate filter applied: {ToDate}", toDate.Value);
                    query = query.Where(a => a.Timestamp <= toDate.Value.AddDays(1));
                }

                // DEBUG: Count raw SQL
                var rawCount = await _context.AuditLogs.IgnoreQueryFilters().CountAsync();
                _logger.LogInformation("[AuditLog-DEBUG] Raw COUNT(*) from DB = {RawCount}", rawCount);

                // Log the query filter before Count
                var testCount = await _context.AuditLogs.CountAsync();
                _logger.LogInformation("[AuditLog-DEBUG] With filters Count = {Count}", testCount);

                // Log tổng số bản ghi trước phân trang
                var totalCount = await query.CountAsync();
                _logger.LogInformation("[AuditLog] Total records matching query: {TotalCount}", totalCount);

                var paginatedList = await PaginatedList<CarRentalSystem.Models.AuditLog>.CreateAsync(query, page, pageSize);

                // Pass filter values back to view
                ViewBag.CurrentAction = action;
                ViewBag.CurrentEntityType = entityType;
                ViewBag.CurrentUserName = userName;
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

                // Distinct values for dropdowns
                ViewBag.Actions = await _context.AuditLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();
                ViewBag.EntityTypes = await _context.AuditLogs.Select(a => a.EntityType).Distinct().OrderBy(a => a).ToListAsync();

                return View(paginatedList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuditLog] Error loading audit log page. Action={Action}, EntityType={EntityType}, UserName={UserName}", action, entityType, userName);
                TempData["ErrorMessage"] = "Lỗi khi tải audit log: " + ex.Message;
                return View(new PaginatedList<CarRentalSystem.Models.AuditLog>(new List<CarRentalSystem.Models.AuditLog>(), 0, page, pageSize));
            }
        }

        /// <summary>
        /// Chi tiết một audit log entry.
        /// </summary>
        public async Task<IActionResult> Details(long id)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != "Admin") return Forbid();

            var log = await _context.AuditLogs
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.AuditLogID == id);

            if (log == null) return NotFound();

            return View(log);
        }
    }
}
