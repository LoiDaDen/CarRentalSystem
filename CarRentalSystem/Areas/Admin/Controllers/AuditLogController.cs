using CarRentalSystem.Data;
using CarRentalSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuditLogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogController(ApplicationDbContext context)
        {
            _context = context;
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
            var role = User.FindFirst("Role")?.Value;
            if (role != "Admin") return Forbid();

            var query = _context.AuditLogs
                .Include(a => a.User)
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
                query = query.Where(a => a.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.Timestamp <= toDate.Value.AddDays(1));

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

        /// <summary>
        /// Chi tiết một audit log entry.
        /// </summary>
        public async Task<IActionResult> Details(long id)
        {
            var role = User.FindFirst("Role")?.Value;
            if (role != "Admin") return Forbid();

            var log = await _context.AuditLogs
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.AuditLogID == id);

            if (log == null) return NotFound();

            return View(log);
        }
    }
}
