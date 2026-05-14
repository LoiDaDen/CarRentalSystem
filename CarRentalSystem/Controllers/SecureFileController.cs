using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers
{
    /// <summary>
    /// Controller bảo mật để serve file eKYC từ App_Data/SecureUploads
    /// Chỉ user sở hữu hoặc Admin mới có quyền truy cập
    /// </summary>
    [Authorize]
    public class SecureFileController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly Data.ApplicationDbContext _context;

        public SecureFileController(IWebHostEnvironment env, Data.ApplicationDbContext context)
        {
            _env = env;
            _context = context;
        }

        /// <summary>
        /// Lấy hình ảnh eKYC bảo mật
        /// VD: /SecureFile/GetSecureImage?folder=eKyc&fileName=123/id_front.jpg
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSecureImage(string folder, string fileName)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName))
                return NotFound("Invalid path");

            // Parse userId từ fileName (format: {userId}/filename)
            var parts = fileName.Split('/');
            if (parts.Length < 2)
                return NotFound("Invalid file path");

            if (!int.TryParse(parts[0], out int fileOwnerId))
                return NotFound("Invalid user ID");

            // Lấy userId hiện tại
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int currentUserId))
                return Unauthorized();

            // Kiểm tra quyền: chỉ chủ sở hữu hoặc Admin mới được xem
            if (fileOwnerId != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            // Verify user exists and has permission
            var fileOwner = await _context.Users.FindAsync(fileOwnerId);
            if (fileOwner == null)
                return NotFound("User not found");

            // Build secure path
            var basePath = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", folder);
            var fullPath = Path.Combine(basePath, fileName);

            // Prevent directory traversal
            var resolvedPath = Path.GetFullPath(fullPath);
            var resolvedBasePath = Path.GetFullPath(basePath);
            if (!resolvedPath.StartsWith(resolvedBasePath, StringComparison.OrdinalIgnoreCase))
                return NotFound("Invalid path");

            if (!System.IO.File.Exists(resolvedPath))
                return NotFound("File not found");

            // Determine content type
            var extension = Path.GetExtension(resolvedPath).ToLowerInvariant();
            var contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".pdf" => "application/pdf",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            // Serve file với cache headers bảo mật
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
            var fileStream = System.IO.File.OpenRead(resolvedPath);
            return File(fileStream, contentType, enableRangeProcessing: true);
        }

        /// <summary>
        /// Xóa file eKYC (chỉ chủ sở hữu hoặc Admin)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSecureFile(string folder, string fileName)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName))
                return Json(new { success = false, message = "Invalid path" });

            var parts = fileName.Split('/');
            if (parts.Length < 2)
                return Json(new { success = false, message = "Invalid file path" });

            if (!int.TryParse(parts[0], out int fileOwnerId))
                return Json(new { success = false, message = "Invalid user ID" });

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int currentUserId))
                return Unauthorized();

            // Chỉ chủ sở hữu hoặc Admin được xóa
            if (fileOwnerId != currentUserId && !User.IsInRole("Admin"))
                return Forbid();

            var basePath = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", folder);
            var fullPath = Path.Combine(basePath, fileName);

            var resolvedPath = Path.GetFullPath(fullPath);
            var resolvedBasePath = Path.GetFullPath(basePath);
            if (!resolvedPath.StartsWith(resolvedBasePath, StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Invalid path" });

            if (System.IO.File.Exists(resolvedPath))
            {
                System.IO.File.Delete(resolvedPath);
                return Json(new { success = true, message = "File deleted" });
            }

            return Json(new { success = false, message = "File not found" });
        }
    }
}
