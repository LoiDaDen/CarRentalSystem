using CarRentalSystem.Data;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class EkycController : Controller
    {
        private readonly IEkycService _ekycService;
        private readonly ApplicationDbContext _context;
        private readonly IPiiEncryptionService _piiSettings;
        private readonly IWebHostEnvironment _env;

        public EkycController(IEkycService ekycService, ApplicationDbContext context, IPiiEncryptionService piiSettings, IWebHostEnvironment env)
        {
            _ekycService = ekycService;
            _context = context;
            _piiSettings = piiSettings;
            _env = env;
        }

        // GET: /Ekyc
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            ViewBag.Status = user.VerificationStatus;
            return View(user);
        }

        // POST: /Ekyc/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(IFormFile idFront, IFormFile idBack, IFormFile license, IFormFile face)
        {
            if (idFront == null || idBack == null || license == null || face == null)
            {
                TempData["ErrorMessage"] = "Vui lòng tải lên đầy đủ 4 hình ảnh (Mặt trước CCCD, Mặt sau CCCD, Bằng lái, và Khuôn mặt).";
                return RedirectToAction("Index");
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            try
            {
                // 1. Perform FPT AI Vision Checks
                var idResultTask = _ekycService.ExtractIdCardAsync(idFront, idBack);
                var licenseResultTask = _ekycService.ExtractDrivingLicenseAsync(license);
                var faceMatchTask = _ekycService.CompareFaceAsync(face, idFront);

                await Task.WhenAll(idResultTask, licenseResultTask, faceMatchTask);

                var idResult = idResultTask.Result;
                var licenseResult = licenseResultTask.Result;
                var faceMatch = faceMatchTask.Result;

                if (!idResult.IsSuccess || !licenseResult.IsSuccess || !faceMatch.IsSuccess)
                {
                    TempData["ErrorMessage"] = "Không thể phân tích một hoặc nhiều hình ảnh do mờ, chói hoặc giả mạo. Vui lòng chụp lại rõ nét hơn.";
                    return RedirectToAction("Index");
                }

                if (!faceMatch.IsMatch || faceMatch.SimilarityPercent < 80)
                {
                    TempData["ErrorMessage"] = $"Xác thực khuôn mặt thất bại. Khuôn mặt thực tế và trên CCCD không khớp (Độ chính xác: {faceMatch.SimilarityPercent:F2}%).";
                    return RedirectToAction("Index");
                }

                // 2. Save Images securely (optional in real system, some banks delete them or store in cold storage)
                string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "ekyc", user.UserID.ToString());
                Directory.CreateDirectory(uploadFolder);
                
                string idFPath = Path.Combine(uploadFolder, "id_front.jpg");
                string idBPath = Path.Combine(uploadFolder, "id_back.jpg");
                string licPath = Path.Combine(uploadFolder, "license.jpg");
                string facePath = Path.Combine(uploadFolder, "face.jpg");

                await SaveFileAsync(idFront, idFPath);
                await SaveFileAsync(idBack, idBPath);
                await SaveFileAsync(license, licPath);
                await SaveFileAsync(face, facePath);

                user.IdCardFrontUrl = $"/uploads/ekyc/{user.UserID}/id_front.jpg";
                user.IdCardBackUrl = $"/uploads/ekyc/{user.UserID}/id_back.jpg";
                user.DrivingLicenseUrl = $"/uploads/ekyc/{user.UserID}/license.jpg";
                user.FaceUrl = $"/uploads/ekyc/{user.UserID}/face.jpg";

                // Update User Info based on real AI payload (encrypted)
                if (!string.IsNullOrEmpty(idResult.CitizenId))
                    user.CitizenId = _piiSettings.Encrypt(idResult.CitizenId);
                    
                if (!string.IsNullOrEmpty(licenseResult.LicenseNumber))
                    user.DrivingLicenseNumber = _piiSettings.Encrypt(licenseResult.LicenseNumber);

                user.VerificationStatus = "Verified";
                user.IsVerified = true;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Quá trình định danh eKYC điện tử thành công! Tài khoản của bạn đã được xác thực hoàn toàn.";
                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Quá trình xác thực gặp lỗi máy chủ. Hãy thử lại.";
                return RedirectToAction("Index");
            }
        }

        private async Task SaveFileAsync(IFormFile file, string path)
        {
            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
        }
    }
}
