using CarRentalSystem.Data;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class EkycController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPiiEncryptionService _piiEncryption;
        private readonly IWebHostEnvironment _env;

        public EkycController(ApplicationDbContext context, IPiiEncryptionService piiEncryption, IWebHostEnvironment env)
        {
            _context = context;
            _piiEncryption = piiEncryption;
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
        // Upload ảnh CCCD/GPLX - Admin tự xác nhận
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(
            IFormFile? idFront, 
            IFormFile? idBack, 
            IFormFile? license, 
            IFormFile? licenseBack,
            IFormFile? face)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            // Kiểm tra có upload ít nhất 1 ảnh
            if (idFront == null && idBack == null && license == null && licenseBack == null && face == null)
            {
                TempData["ErrorMessage"] = "Vui lòng tải lên ít nhất 1 hình ảnh.";
                return RedirectToAction("Index");
            }

            try
            {
                string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "ekyc", user.UserID.ToString());
                Directory.CreateDirectory(uploadFolder);

                // Lưu CCCD mặt trước
                if (idFront != null && idFront.Length > 0)
                {
                    string idFPath = Path.Combine(uploadFolder, "id_front" + Path.GetExtension(idFront.FileName));
                    await SaveFileAsync(idFront, idFPath);
                    user.IdCardFrontUrl = $"/uploads/ekyc/{user.UserID}/id_front{Path.GetExtension(idFront.FileName)}";
                }

                // Lưu CCCD mặt sau
                if (idBack != null && idBack.Length > 0)
                {
                    string idBPath = Path.Combine(uploadFolder, "id_back" + Path.GetExtension(idBack.FileName));
                    await SaveFileAsync(idBack, idBPath);
                    user.IdCardBackUrl = $"/uploads/ekyc/{user.UserID}/id_back{Path.GetExtension(idBack.FileName)}";
                }

                // Lưu GPLX mặt trước
                if (license != null && license.Length > 0)
                {
                    string licPath = Path.Combine(uploadFolder, "license_front" + Path.GetExtension(license.FileName));
                    await SaveFileAsync(license, licPath);
                    user.DrivingLicenseUrl = $"/uploads/ekyc/{user.UserID}/license_front{Path.GetExtension(license.FileName)}";
                }

                // Lưu GPLX mặt sau
                if (licenseBack != null && licenseBack.Length > 0)
                {
                    string licBPath = Path.Combine(uploadFolder, "license_back" + Path.GetExtension(licenseBack.FileName));
                    await SaveFileAsync(licenseBack, licBPath);
                    user.DrivingLicenseBackUrl = $"/uploads/ekyc/{user.UserID}/license_back{Path.GetExtension(licenseBack.FileName)}";
                }

                // Lưu ảnh chân dung
                if (face != null && face.Length > 0)
                {
                    string facePath = Path.Combine(uploadFolder, "face" + Path.GetExtension(face.FileName));
                    await SaveFileAsync(face, facePath);
                    user.FaceUrl = $"/uploads/ekyc/{user.UserID}/face{Path.GetExtension(face.FileName)}";
                }

                // Set status = Pending để Admin duyệt
                user.VerificationStatus = "Pending";
                user.IsVerified = false;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Hồ sơ eKYC đã được gửi thành công! Vui lòng chờ Admin xác nhận.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Quá trình tải lên gặp lỗi: " + ex.Message;
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
