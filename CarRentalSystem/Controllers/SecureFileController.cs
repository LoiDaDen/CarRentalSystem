using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CarRentalSystem.Data;
using System.Linq;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class SecureFileController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;

        public SecureFileController(IWebHostEnvironment env, ApplicationDbContext context)
        {
            _env = env;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetSecureImage(string folder, string fileName)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName))
            {
                return BadRequest("Invalid request parameters.");
            }

            // 1. NGĂN CHẶN PATH TRAVERSAL (Tấn công leo thang thư mục)
            if (folder.Contains("..") || fileName.Contains("..") || folder.Contains("/") || folder.Contains("\\") || fileName.Contains("/") || fileName.Contains("\\"))
            {
                return BadRequest("Mã lỗi xác định Path Traversal: Đường dẫn không hợp lệ.");
            }

            string safeFolder = "";
            if (folder.Equals("eKyc", StringComparison.OrdinalIgnoreCase))
                safeFolder = "eKyc";
            else if (folder.Equals("Handovers", StringComparison.OrdinalIgnoreCase))
                safeFolder = "Handovers";
            else
                return BadRequest("Thư mục yêu cầu không được cho phép.");

            // 2. KIỂM TRA PHÂN QUYỀN TRUY CẬP (AUTHORIZATION MÚC TÀI NGUYÊN)
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            bool isAdmin = role == "Admin";
            
            if (!int.TryParse(userIdString, out int userId)) 
                return Unauthorized();

            bool isAuthorized = false;

            if (safeFolder == "eKyc")
            {
                if (isAdmin) 
                {
                    isAuthorized = true;
                }
                else
                {
                    // Chỉ cho phép người dùng xem file eKyc của chính họ
                    isAuthorized = await _context.Users.AnyAsync(u => u.UserID == userId &&
                        (
                            (u.IdCardFrontUrl != null && u.IdCardFrontUrl.Contains(fileName)) || 
                            (u.IdCardBackUrl != null && u.IdCardBackUrl.Contains(fileName)) || 
                            (u.DrivingLicenseUrl != null && u.DrivingLicenseUrl.Contains(fileName))
                        ));
                }
            }
            else if (safeFolder == "Handovers")
            {
                if (isAdmin)
                {
                    isAuthorized = true;
                }
                else
                {
                    // Chỉ cho phép Khách hàng thuê xe (Customer) hoặc Chủ xe (Partner) xem biên bản bàn giao
                    isAuthorized = await _context.HandoverRecords
                        .Include(h => h.Booking)
                        .ThenInclude(b => b.Car)
                        .AnyAsync(h => 
                            (
                                (h.FrontImgUrl != null && h.FrontImgUrl.Contains(fileName)) || 
                                (h.BackImgUrl != null && h.BackImgUrl.Contains(fileName)) || 
                                (h.LeftImgUrl != null && h.LeftImgUrl.Contains(fileName)) || 
                                (h.RightImgUrl != null && h.RightImgUrl.Contains(fileName)) || 
                                (h.InsideImgUrl != null && h.InsideImgUrl.Contains(fileName)) || 
                                (h.SignatureUrl != null && h.SignatureUrl.Contains(fileName))
                            ) &&
                            (h.Booking.UserID == userId || (h.Booking.Car.OwnerID != null && h.Booking.Car.OwnerID == userId))
                        );
                }
            }

            if (!isAuthorized)
            {
                return Forbid("Bạn không có quyền truy cập vào tài liệu này.");
            }

            // 3. ĐỌC FILE SAU KHI ĐÃ XÁC THỰC VÀ PHÂN QUYỀN
            string filePath = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", safeFolder, fileName);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Không tìm thấy file.");
            }

            // 4. NGĂN CHẶN CACHE BROWSER & PROXY
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["X-Content-Type-Options"] = "nosniff";

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            string mimeType = "application/octet-stream";

            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    mimeType = "image/jpeg";
                    break;
                case ".png":
                    mimeType = "image/png";
                    break;
                case ".pdf":
                    mimeType = "application/pdf";
                    break;
                case ".gif":
                    mimeType = "image/gif";
                    break;
                case ".webp":
                    mimeType = "image/webp";
                    break;
            }

            try
            {
                var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return File(stream, mimeType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecureFileController] Error reading file: {ex.Message}");
                // Không throw exception nội bộ ra bên ngoài
                return StatusCode(500, "Lỗi máy chủ khi đọc file.");
            }
        }
    }
}
