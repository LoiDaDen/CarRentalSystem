using CarRentalSystem.Data;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class EkycController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEkycService _ekycService;
        private readonly IPiiEncryptionService _piiEncryption;
        private readonly IWebHostEnvironment _env;

        public EkycController(
            ApplicationDbContext context,
            IEkycService ekycService,
            IPiiEncryptionService piiEncryption,
            IWebHostEnvironment env)
        {
            _context = context;
            _ekycService = ekycService;
            _piiEncryption = piiEncryption;
            _env = env;
        }

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

        /// <summary>
        /// AJAX: OCR ảnh CCCD
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScanIdCard(IFormFile idFront, IFormFile idBack)
        {
            // Bat buoc phai co ca mat truoc va mat sau CCCD
            if (idFront == null)
                return Json(new { success = false, message = "Vui long tai len anh CCCD mat truoc." });
            if (idBack == null)
                return Json(new { success = false, message = "Vui long tai len anh CCCD mat sau." });

            var formatError = ValidateImageFormat(idFront);
            if (!string.IsNullOrEmpty(formatError))
                return Json(new { success = false, message = "Anh CCCD mat truoc: " + formatError });
            formatError = ValidateImageFormat(idBack);
            if (!string.IsNullOrEmpty(formatError))
                return Json(new { success = false, message = "Anh CCCD mat sau: " + formatError });

            try
            {
                var result = await _ekycService.ExtractIdCardAsync(idFront, idBack);
                if (!result.IsSuccess)
                    return Json(new { success = false, message = result.Message });

                return Json(new
                {
                    success = true,
                    citizenId = result.CitizenId ?? "",
                    fullName = result.FullName ?? "",
                    dateOfBirth = result.DateOfBirth ?? "",
                    gender = result.Gender ?? "",
                    address = result.Address ?? "",
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi OCR: " + ex.Message });
            }
        }

        /// <summary>
        /// AJAX: OCR ảnh GPLX
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScanLicense(IFormFile license)
        {
            if (license == null)
                return Json(new { success = false, message = "Vui long tai len anh GPLX." });

            var formatError = ValidateImageFormat(license);
            if (!string.IsNullOrEmpty(formatError))
                return Json(new { success = false, message = "Anh GPLX: " + formatError });

            try
            {
                var result = await _ekycService.ExtractDrivingLicenseAsync(license);
                if (!result.IsSuccess)
                    return Json(new { success = false, message = result.Message });

                return Json(new
                {
                    success = true,
                    licenseNumber = result.LicenseNumber ?? "",
                    licenseClass = result.Class ?? "",
                    fullName = result.FullName ?? "",
                    dateOfBirth = result.DateOfBirth ?? "",
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi OCR GPLX: " + ex.Message });
            }
        }

        /// <summary>
        /// AJAX: So sánh khuôn mặt selfie vs CCCD
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyFace(IFormFile face, IFormFile idCard)
        {
            if (face == null)
                return Json(new { success = false, message = "Vui long tai len anh selfie." });
            if (idCard == null)
                return Json(new { success = false, message = "Vui long tai len anh CCCD mat truoc." });

            var formatError = ValidateImageFormat(face);
            if (!string.IsNullOrEmpty(formatError))
                return Json(new { success = false, message = "Anh selfie: " + formatError });
            formatError = ValidateImageFormat(idCard);
            if (!string.IsNullOrEmpty(formatError))
                return Json(new { success = false, message = "Anh CCCD: " + formatError });

            try
            {
                var result = await _ekycService.CompareFaceAsync(face, idCard);
                if (!result.IsSuccess)
                    return Json(new { success = false, message = result.Message });

                return Json(new
                {
                    success = true,
                    isMatch = result.IsMatch,
                    similarity = result.SimilarityPercent,
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi so sánh khuôn mặt: " + ex.Message });
            }
        }

        /// <summary>
        /// Submit: Full verification pipeline
        /// 1. Lưu ảnh
        /// 2. OCR CCCD + GPLX
        /// 3. Face Compare: Selfie vs CCCD, GPLX vs CCCD
        /// 4. Cross-check: Profile vs CCCD
        /// 5. Duplicate check: CCCD/GPLX đã dùng bởi tài khoản nào chưa?
        /// Kết quả: Verified / Flagged / Rejected
        /// </summary>
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

            // --- Thu thap loi validate ---
            var validationErrors = new List<string>();

            // Bat buoc: CCCD mat truoc
            if (idFront == null || idFront.Length == 0)
                validationErrors.Add("Anh CCCD mat truoc la bat buoc.");
            else
            {
                var err = ValidateImageFormat(idFront); if (!string.IsNullOrEmpty(err)) validationErrors.Add("CCCD mat truoc: " + err);
                err = ValidateFileSize(idFront); if (!string.IsNullOrEmpty(err)) validationErrors.Add("CCCD mat truoc: " + err);
            }

            // Bat buoc: CCCD mat sau
            if (idBack == null || idBack.Length == 0)
                validationErrors.Add("Anh CCCD mat sau la bat buoc.");
            else
            {
                var err = ValidateImageFormat(idBack); if (!string.IsNullOrEmpty(err)) validationErrors.Add("CCCD mat sau: " + err);
                err = ValidateFileSize(idBack); if (!string.IsNullOrEmpty(err)) validationErrors.Add("CCCD mat sau: " + err);
            }

            // Bat buoc: GPLX mat truoc
            if (license == null || license.Length == 0)
                validationErrors.Add("Anh GPLX mat truoc la bat buoc.");
            else
            {
                var err = ValidateImageFormat(license); if (!string.IsNullOrEmpty(err)) validationErrors.Add("GPLX mat truoc: " + err);
                err = ValidateFileSize(license); if (!string.IsNullOrEmpty(err)) validationErrors.Add("GPLX mat truoc: " + err);
            }

            // Bat buoc: GPLX mat sau
            if (licenseBack == null || licenseBack.Length == 0)
                validationErrors.Add("Anh GPLX mat sau la bat buoc.");
            else
            {
                var err = ValidateImageFormat(licenseBack); if (!string.IsNullOrEmpty(err)) validationErrors.Add("GPLX mat sau: " + err);
                err = ValidateFileSize(licenseBack); if (!string.IsNullOrEmpty(err)) validationErrors.Add("GPLX mat sau: " + err);
            }

            // Bat buoc: Selfie
            if (face == null || face.Length == 0)
                validationErrors.Add("Anh selfie la bat buoc.");
            else
            {
                var err = ValidateImageFormat(face); if (!string.IsNullOrEmpty(err)) validationErrors.Add("Anh selfie: " + err);
                err = ValidateFileSize(face); if (!string.IsNullOrEmpty(err)) validationErrors.Add("Anh selfie: " + err);
            }

            if (validationErrors.Count > 0)
            {
                TempData["ErrorMessage"] = "Vui long kiem tra lai cac truong sau:";
                TempData["EkycWarnings"] = string.Join("|", validationErrors);
                return RedirectToAction("Index");
            }

            try
            {
                // --- Lưu ảnh ---
                string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "ekyc", user.UserID.ToString());
                Directory.CreateDirectory(uploadFolder);

                if (idFront != null && idFront.Length > 0)
                {
                    string idFPath = Path.Combine(uploadFolder, "id_front" + Path.GetExtension(idFront.FileName));
                    await SaveFileAsync(idFront, idFPath);
                    user.IdCardFrontUrl = $"/uploads/ekyc/{user.UserID}/id_front{Path.GetExtension(idFront.FileName)}";
                }
                if (idBack != null && idBack.Length > 0)
                {
                    string idBPath = Path.Combine(uploadFolder, "id_back" + Path.GetExtension(idBack.FileName));
                    await SaveFileAsync(idBack, idBPath);
                    user.IdCardBackUrl = $"/uploads/ekyc/{user.UserID}/id_back{Path.GetExtension(idBack.FileName)}";
                }
                if (license != null && license.Length > 0)
                {
                    string licPath = Path.Combine(uploadFolder, "license_front" + Path.GetExtension(license.FileName));
                    await SaveFileAsync(license, licPath);
                    user.DrivingLicenseUrl = $"/uploads/ekyc/{user.UserID}/license_front{Path.GetExtension(license.FileName)}";
                }
                if (licenseBack != null && licenseBack.Length > 0)
                {
                    string licBPath = Path.Combine(uploadFolder, "license_back" + Path.GetExtension(licenseBack.FileName));
                    await SaveFileAsync(licenseBack, licBPath);
                    user.DrivingLicenseBackUrl = $"/uploads/ekyc/{user.UserID}/license_back{Path.GetExtension(licenseBack.FileName)}";
                }
                if (face != null && face.Length > 0)
                {
                    string facePath = Path.Combine(uploadFolder, "face" + Path.GetExtension(face.FileName));
                    await SaveFileAsync(face, facePath);
                    user.FaceUrl = $"/uploads/ekyc/{user.UserID}/face{Path.GetExtension(face.FileName)}";
                }

                // --- OCR CCCD ---
                string? citizenId = null, fullNameCccd = null, dobCccd = null, genderCccd = null, addressCccd = null;
                bool cccdReadSuccess = false;

                if (idFront != null || idBack != null)
                {
                    var idResult = await _ekycService.ExtractIdCardAsync(idFront, idBack);
                    if (idResult.IsSuccess && !string.IsNullOrEmpty(idResult.CitizenId))
                    {
                        cccdReadSuccess = true;
                        citizenId = idResult.CitizenId;
                        fullNameCccd = idResult.FullName;
                        dobCccd = idResult.DateOfBirth;
                        genderCccd = idResult.Gender;
                        addressCccd = idResult.Address;
                    }
                }

                // --- OCR GPLX ---
                string? licenseNumber = null;
                bool licReadSuccess = false;

                if (license != null)
                {
                    var licResult = await _ekycService.ExtractDrivingLicenseAsync(license);
                    if (licResult.IsSuccess && !string.IsNullOrEmpty(licResult.LicenseNumber))
                    {
                        licReadSuccess = true;
                        licenseNumber = licResult.LicenseNumber;
                    }
                }

                // --- THU THẬP TẤT CẢ CẢNH BÁO & LỖI ---
                var errors = new List<string>(); // Lỗi nghiêm trọng → Reject
                var warnings = new List<string>(); // Cảnh báo nhẹ → Flag

                // ============================================================
                // BƯỚC 1: Kiểm tra trùng lặp CCCD/GPLX trên toàn hệ thống
                // ============================================================
                if (cccdReadSuccess && !string.IsNullOrEmpty(citizenId))
                {
                    var cccdHash = ComputePiiHash(citizenId);
                    bool cccdExists = await _context.Users
                        .AnyAsync(u => u.UserID != userId && u.CitizenIdHash == cccdHash);
                    if (cccdExists)
                    {
                        errors.Add($"Số CCCD '{MaskPii(citizenId)}' đã được sử dụng bởi tài khoản khác. Mỗi CCCD chỉ được đăng ký 1 tài khoản.");
                    }
                }

                if (licReadSuccess && !string.IsNullOrEmpty(licenseNumber))
                {
                    var licHash = ComputePiiHash(licenseNumber);
                    bool licExists = await _context.Users
                        .AnyAsync(u => u.UserID != userId && u.DrivingLicenseHash == licHash);
                    if (licExists)
                    {
                        errors.Add($"Số GPLX '{MaskPii(licenseNumber)}' đã được sử dụng bởi tài khoản khác. Mỗi GPLX chỉ được đăng ký 1 tài khoản.");
                    }
                }

                // ============================================================
                // BƯỚC 2: Face Compare — Selfie vs CCCD (BẮT BUỘC)
                // ============================================================
                if (face != null && idFront != null)
                {
                    var faceResult = await _ekycService.CompareFaceAsync(face, idFront);
                    if (faceResult.IsSuccess)
                    {
                        if (!faceResult.IsMatch)
                        {
                            errors.Add($"Khuôn mặt trong ảnh selfie không khớp với ảnh CCCD (độ tương đồng: {faceResult.SimilarityPercent:F1}%, ngưỡng yêu cầu: 80%). Đây có thể là tài khoản giả mạo.");
                        }
                        // isMatch = true → không thêm warning, đây là kết quả tích cực
                    }
                    else
                    {
                        errors.Add($"Không thể so sánh khuôn mặt. Lỗi: {faceResult.Message}");
                    }
                }
                else if (face == null && idFront != null)
                {
                    warnings.Add("Bạn chưa tải lên ảnh selfie. Để xác thực chính xác hơn, vui lòng bổ sung ảnh selfie.");
                }

                // ============================================================
                // BƯỚC 3: Face Compare — Selfie vs GPLX (nếu có ảnh GPLX)
                // ============================================================
                if (face != null && license != null)
                {
                    var faceLicResult = await _ekycService.CompareFaceAsync(face, license);
                    if (faceLicResult.IsSuccess)
                    {
                        if (!faceLicResult.IsMatch)
                        {
                            errors.Add($"Khuôn mặt trong ảnh selfie không khớp với ảnh GPLX (độ tương đồng: {faceLicResult.SimilarityPercent:F1}%). Đây có thể là GPLX không thuộc về bạn.");
                        }
                        // isMatch = true → không thêm warning, đây là kết quả tích cực
                    }
                    else
                    {
                        errors.Add($"Không thể so sánh selfie với GPLX. Lỗi: {faceLicResult.Message}");
                    }
                }

                // ============================================================
                // BƯỚC 4: Cross-check Profile vs CCCD
                // ============================================================
                if (cccdReadSuccess)
                {
                    // 4a) So sánh tên
                    if (!string.IsNullOrEmpty(fullNameCccd) && !string.IsNullOrEmpty(user.Name))
                    {
                        var normProfile = NormalizeVietnamese(user.Name.Trim());
                        var normCccd = NormalizeVietnamese(fullNameCccd.Trim());
                        if (!normProfile.Equals(normCccd))
                        {
                            // Cho phép trường hợp tên trên CCCD dài hơn (có thể do viết đầy đủ)
                            bool isSubset = normCccd.Contains(normProfile) && normCccd.Length - normProfile.Length <= 10;
                            if (!isSubset)
                            {
                                errors.Add($"Tên trên CCCD ('{fullNameCccd}') không khớp với tên trong hồ sơ ('{user.Name}'). Vui lòng cập nhật hồ sơ cho đúng.");
                            }
                            else
                            {
                                warnings.Add($"Tên hồ sơ ('{user.Name}') khác biệt nhẹ so với CCCD ('{fullNameCccd}') — có thể do tên viết tắt/đầy đủ.");
                            }
                        }
                    }

                    // 4b) So sánh ngày sinh
                    if (!string.IsNullOrEmpty(dobCccd) && user.DateOfBirth.HasValue)
                    {
                        if (DateTime.TryParse(dobCccd, out var cccdDob))
                        {
                            if (Math.Abs((cccdDob.Date - user.DateOfBirth.Value.Date).TotalDays) > 365)
                            {
                                errors.Add($"Ngày sinh trên CCCD ({cccdDob:dd/MM/yyyy}) không khớp với ngày sinh trong hồ sơ ({user.DateOfBirth:dd/MM/yyyy}). Sự khác biệt trên 1 năm.");
                            }
                        }
                    }

                    // 4c) So sánh giới tính
                    if (!string.IsNullOrEmpty(genderCccd) && !string.IsNullOrEmpty(user.Gender))
                    {
                        var normGenderCccd = NormalizeVietnamese(genderCccd.Trim());
                        var normGenderProfile = NormalizeVietnamese(user.Gender.Trim());
                        if (!normGenderCccd.Equals(normGenderProfile))
                        {
                            errors.Add($"Giới tính trên CCCD ('{genderCccd}') không khớp với giới tính trong hồ sơ ('{user.Gender}').");
                        }
                    }
                }

                // ============================================================
                // BƯỚC 5: Quyết định trạng thái cuối cùng
                // ============================================================
                bool hasErrors = errors.Count > 0;
                bool hasWarnings = warnings.Count > 0;

                if (hasErrors)
                {
                    // ❌ REJECT — Có lỗi nghiêm trọng
                    user.VerificationStatus = "Rejected";
                    user.IsVerified = false;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    TempData["ErrorMessage"] = "Xác thực thất bại. Hệ thống phát hiện các vấn đề sau:";
                    TempData["EkycWarnings"] = string.Join("|", errors);
                    return RedirectToAction("Index");
                }

                if (!cccdReadSuccess)
                {
                    // ⚠️ CCCD không đọc được → Pending
                    user.VerificationStatus = "Pending";
                    user.IsVerified = false;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    TempData["WarningMessage"] = "Hồ sơ đã được gửi nhưng không đọc được CCCD. Hệ thống sẽ xác minh trong 24h.";
                    return RedirectToAction("Index");
                }

                // Lưu thông tin đã OCR vào user
                if (!string.IsNullOrWhiteSpace(citizenId))
                    user.CitizenId = _piiEncryption.Encrypt(citizenId);
                if (!string.IsNullOrWhiteSpace(fullNameCccd))
                    user.Name = fullNameCccd.Trim();
                if (!string.IsNullOrWhiteSpace(genderCccd))
                    user.Gender = genderCccd.Trim();
                if (!string.IsNullOrWhiteSpace(addressCccd))
                    user.Address = addressCccd.Trim();
                if (!string.IsNullOrWhiteSpace(dobCccd) && DateTime.TryParse(dobCccd, out var dob))
                    user.DateOfBirth = dob;
                if (!string.IsNullOrWhiteSpace(licenseNumber))
                    user.DrivingLicenseNumber = _piiEncryption.Encrypt(licenseNumber);

                if (hasWarnings)
                {
                    // ⚠️ FLAGGED — CCCD đọc được, mặt khớp, nhưng có cảnh báo nhẹ
                    user.VerificationStatus = "Flagged";
                    user.IsVerified = false;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    TempData["WarningMessage"] = "Hồ sơ đã được gửi. Hệ thống ghi nhận một số thông tin cần xác minh thêm.";
                    TempData["EkycWarnings"] = string.Join("|", warnings);
                }
                else
                {
                    // ✅ VERIFIED — Tất cả kiểm tra đều pass
                    user.VerificationStatus = "Verified";
                    user.IsVerified = true;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Xác thực thành công! Hệ thống đã tự động xác minh danh tính từ CCCD của bạn.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Quá trình tải lên gặp lỗi: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Che giấu PII — chỉ hiện 4 ký tự cuối
        /// </summary>
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

        private static string? ValidateImageFormat(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;
            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
                return "File khong co duoi. Chi chap nhan: JPG, JPEG, PNG, WEBP.";
            if (!AllowedExtensions.Contains(ext))
                return $"Dinh dang '{ext}' khong duoc chap nhan. Chi chap nhan: JPG, JPEG, PNG, WEBP.";
            return null;
        }

        private static string? ValidateFileSize(IFormFile file)
        {
            if (file == null) return null;
            if (file.Length > MaxFileSizeBytes)
                return $"File qua lon ({file.Length / 1024 / 1024:F1}MB). Toi da 5MB.";
            return null;
        }

        private static string MaskPii(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "••••••••";
            if (value.Length <= 4) return new string('•', value.Length);
            return new string('•', value.Length - 4) + value[^4..];
        }

        private static string ComputePiiHash(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string NormalizeVietnamese(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string[] signs = { "aAeEoOuUiIyY", "aAeEoOuUiIyY", "aAeEoOuUiIyY", "aAeEoOuUiIyY", "aAeEoOuUiIyY", "aAeEoOuUiIyY", "aAeEoOuUiIyY", "aAeEoOuUiIyY" };
            string[] chars = { "àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩ", "ÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨ", "èéẹẻẽêềếệểễìíịỉĩđÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨĐ", "ÌÍỊỈĨìíịỉĩÌÍỊỈĨ", "òóọỏõôồốộổỗăằắặẳẵơờớợởỡùúụủũưừứựửữìíịỉĩýỳỵỷỹ", "ÒÓỌỎÕÔỒỐỘỔỖĂẰẮẶẲẴƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮÌÍỊỈĨÝỲỴỶỸ", "đĐ", "Đ" };
            string result = text.ToLowerInvariant();
            for (int i = 0; i < chars.Length; i++)
                for (int j = 0; j < chars[i].Length; j++)
                    result = result.Replace(chars[i][j], signs[0][i]);
            return result.Trim();
        }

        private async Task SaveFileAsync(IFormFile file, string path)
        {
            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
        }
    }
}
