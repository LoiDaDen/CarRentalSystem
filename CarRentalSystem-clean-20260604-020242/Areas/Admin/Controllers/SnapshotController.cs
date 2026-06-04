// CarRentalSystem/Areas/Admin/Controllers/SnapshotController.cs
using CarRentalSystem.Filters;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin")]
    public class SnapshotController : Controller
    {
        private readonly ISnapshotService _snapshotService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly IAuditTrailService _auditTrail;

        public SnapshotController(
            ISnapshotService snapshotService,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            IAuditTrailService auditTrail)
        {
            _snapshotService = snapshotService;
            _configuration = configuration;
            _environment = environment;
            _auditTrail = auditTrail;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub")
                     ?? User.FindFirst("UserID");
            return int.TryParse(claim?.Value, out var id) ? id : 0;
        }

        // ═══════════════════════════════════════════════
        // INDEX — danh sách snapshot
        // ═══════════════════════════════════════════════
        public async Task<IActionResult> Index()
        {
            var snapshots = await _snapshotService.GetAllSnapshotsAsync();
            return View(snapshots);
        }

        // ═══════════════════════════════════════════════
        // CREATE — tạo snapshot mới
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name, string? description, string type = "Full")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Tên snapshot là bắt buộc.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _snapshotService.CreateSnapshotAsync(name.Trim(), description, type);
            if (result.Success)
                TempData["Success"] = result.Message;
            else
                TempData["Error"] = result.Message;

            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════
        // RESTORE — khôi phục snapshot
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var userId = GetCurrentUserId();
            await WriteSnapshotAuditAsync("SnapshotRestoreAttempt", id, userId);
            var result = await _snapshotService.RestoreSnapshotAsync(id, userId);

            if (result.Success)
                TempData["Success"] = result.Message;
            else
                TempData["Error"] = result.Message;

            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════
        // DELETE — xóa snapshot
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _snapshotService.DeleteSnapshotAsync(id);

            if (result.Success)
                TempData["Success"] = result.Message;
            else
                TempData["Error"] = result.Message;

            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════
        // DOWNLOAD — tải JSON của snapshot
        // ═══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var userId = GetCurrentUserId();
            await WriteSnapshotAuditAsync("SnapshotDownloadAttempt", id, userId);

            if (_environment.IsProduction() && !_configuration.GetValue("Snapshot:AllowUnsafeDownload", false))
            {
                await WriteSnapshotAuditAsync("SnapshotDownloadBlocked", id, userId);
                return Forbid();
            }

            var snapshot = await _snapshotService.GetSnapshotByIdAsync(id);
            if (snapshot == null)
                return NotFound();

            await WriteSnapshotAuditAsync("SnapshotDownload", id, userId);
            var bytes = System.Text.Encoding.UTF8.GetBytes(snapshot.DataJson);
            var fileName = $"snapshot_{snapshot.Name.Replace(" ", "_")}_{snapshot.CreatedAt:yyyyMMdd_HHmmss}.json";

            return File(bytes, "application/json", fileName);
        }

        private async Task WriteSnapshotAuditAsync(string action, int snapshotId, int userId)
        {
            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = action,
                EntityType = "DbSnapshot",
                EntityId = snapshotId,
                UserId = userId == 0 ? null : userId,
                UserName = User.Identity?.Name,
                Description = $"{action} for snapshot #{snapshotId}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                RequestUrl = $"{Request.Method} {Request.Path}{Request.QueryString}",
                Timestamp = DateTime.UtcNow
            });
        }

        // ═══════════════════════════════════════════════
        // DETAILS — xem chi tiết snapshot
        // ═══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            await WriteSnapshotAuditAsync("SnapshotDetailsAttempt", id, userId);

            var snapshot = await _snapshotService.GetSnapshotByIdAsync(id);
            if (snapshot == null)
                return NotFound();

            if (_environment.IsProduction() && !_configuration.GetValue("Snapshot:AllowUnsafeDetails", false))
            {
                await WriteSnapshotAuditAsync("SnapshotDetailsMetadataOnly", id, userId);
                snapshot.DataJson = "{}";
                ViewBag.SnapshotDetailsRedacted = true;
                return View(snapshot);
            }

            await WriteSnapshotAuditAsync("SnapshotDetails", id, userId);
            return View(snapshot);
        }
    }
}
