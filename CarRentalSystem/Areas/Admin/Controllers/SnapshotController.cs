// CarRentalSystem/Areas/Admin/Controllers/SnapshotController.cs
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
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

        public SnapshotController(ISnapshotService snapshotService)
        {
            _snapshotService = snapshotService;
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
            var snapshot = await _snapshotService.GetSnapshotByIdAsync(id);
            if (snapshot == null)
                return NotFound();

            var bytes = System.Text.Encoding.UTF8.GetBytes(snapshot.DataJson);
            var fileName = $"snapshot_{snapshot.Name.Replace(" ", "_")}_{snapshot.CreatedAt:yyyyMMdd_HHmmss}.json";

            return File(bytes, "application/json", fileName);
        }

        // ═══════════════════════════════════════════════
        // DETAILS — xem chi tiết snapshot
        // ═══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var snapshot = await _snapshotService.GetSnapshotByIdAsync(id);
            if (snapshot == null)
                return NotFound();

            return View(snapshot);
        }
    }
}
