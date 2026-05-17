using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class SecureFileController : Controller
    {
        private static readonly HashSet<string> AllowedFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "eKyc",
            "Handovers",
            "Contracts",
            "PhysicalContracts"
        };

        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;
        private readonly IAuditTrailService _auditTrail;

        public SecureFileController(IWebHostEnvironment env, ApplicationDbContext context, IAuditTrailService auditTrail)
        {
            _env = env;
            _context = context;
            _auditTrail = auditTrail;
        }

        [HttpGet]
        public async Task<IActionResult> GetSecureImage(string folder, string fileName)
        {
            var validation = await ValidateSecureFileAccessAsync(folder, fileName);
            if (!validation.IsValid)
                return validation.Result!;

            var resolvedPath = validation.ResolvedPath!;
            if (!System.IO.File.Exists(resolvedPath))
                return NotFound("File not found");

            await WriteAuditLogAsync("SecureFileDownload", folder, validation.FileOwnerId, fileName);

            var extension = Path.GetExtension(resolvedPath).ToLowerInvariant();
            var contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".pdf" => "application/pdf",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
            return PhysicalFile(resolvedPath, contentType, enableRangeProcessing: false);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSecureFile(string folder, string fileName)
        {
            var validation = await ValidateSecureFileAccessAsync(folder, fileName);
            if (!validation.IsValid)
                return validation.Result is ForbidResult ? Forbid() : Json(new { success = false, message = "Invalid file path" });

            if (System.IO.File.Exists(validation.ResolvedPath!))
            {
                System.IO.File.Delete(validation.ResolvedPath!);
                await WriteAuditLogAsync("SecureFileDelete", folder, validation.FileOwnerId, fileName);
                return Json(new { success = true, message = "File deleted" });
            }

            return Json(new { success = false, message = "File not found" });
        }

        private async Task<SecureFileValidation> ValidateSecureFileAccessAsync(string? folder, string? fileName)
        {
            if (!IsAllowedFolder(folder) || !IsSafeRelativeFileName(fileName))
                return SecureFileValidation.Invalid(NotFound("Invalid path"));

            var normalizedFileName = fileName!.Replace('\\', '/');
            var parts = normalizedFileName.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[0], out var fileOwnerId))
                return SecureFileValidation.Invalid(NotFound("Invalid file path"));

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var currentUserId))
                return SecureFileValidation.Invalid(Unauthorized());

            if (fileOwnerId != currentUserId && !User.IsInRole("Admin"))
                return SecureFileValidation.Invalid(Forbid());

            var fileOwner = await _context.Users.FindAsync(fileOwnerId);
            if (fileOwner == null)
                return SecureFileValidation.Invalid(NotFound("User not found"));

            var basePath = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", folder!);
            var fullPath = Path.Combine(basePath, normalizedFileName);
            var resolvedPath = Path.GetFullPath(fullPath);
            var resolvedBasePath = Path.GetFullPath(basePath);
            if (!resolvedPath.StartsWith(resolvedBasePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return SecureFileValidation.Invalid(NotFound("Invalid path"));

            return SecureFileValidation.Valid(resolvedPath, fileOwnerId);
        }

        private static bool IsAllowedFolder(string? folder)
        {
            return !string.IsNullOrWhiteSpace(folder) &&
                   folder.IndexOfAny(new[] { '/', '\\' }) < 0 &&
                   !folder.Contains("..", StringComparison.Ordinal) &&
                   AllowedFolders.Contains(folder);
        }

        private static bool IsSafeRelativeFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            if (Path.IsPathRooted(fileName)) return false;
            if (fileName.Contains('\\')) return false;
            if (fileName.Split('/').Any(part => part is "" or "." or "..")) return false;
            return true;
        }

        private async Task WriteAuditLogAsync(string action, string? folder, int fileOwnerId, string fileName)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = action,
                EntityType = "SecureFile",
                EntityId = fileOwnerId,
                UserId = int.TryParse(userIdClaim, out var currentUserId) ? currentUserId : null,
                UserName = User.Identity?.Name,
                Description = $"{action} folder={folder}, file={fileName}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                RequestUrl = $"{Request.Method} {Request.Path}{Request.QueryString}",
                Timestamp = DateTime.UtcNow
            });
        }

        private sealed record SecureFileValidation(bool IsValid, IActionResult? Result, string? ResolvedPath, int FileOwnerId)
        {
            public static SecureFileValidation Invalid(IActionResult result) => new(false, result, null, 0);
            public static SecureFileValidation Valid(string resolvedPath, int fileOwnerId) => new(true, null, resolvedPath, fileOwnerId);
        }
    }
}
