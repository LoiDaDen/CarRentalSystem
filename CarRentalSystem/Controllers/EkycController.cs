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
        private readonly IReferralService _referralService;
        private readonly IFileUploadValidationService _fileUploadValidation;

        public EkycController(
            ApplicationDbContext context,
            IEkycService ekycService,
            IPiiEncryptionService piiEncryption,
            IWebHostEnvironment env,
            IReferralService referralService,
            IFileUploadValidationService fileUploadValidation)
        {
            _context = context;
            _ekycService = ekycService;
            _piiEncryption = piiEncryption;
            _env = env;
            _referralService = referralService;
            _fileUploadValidation = fileUploadValidation;
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
        /// AJAX: OCR áº£nh CCCD
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScanIdCard(IFormFile idFront, IFormFile idBack)
        {
            // Bat buoc phai co ca mat truoc va mat sau CCCD
            if (idFront == null)
                return Json(new { success = false, message = "Vui lÃ²ng táº£i lÃªn áº£nh CCCD máº·t trÆ°á»›c." });
            if (idBack == null)
                return Json(new { success = false, message = "Vui lÃ²ng táº£i lÃªn áº£nh CCCD máº·t sau." });

            var formatError = await ValidateUploadedImageAsync(idFront);
            if (!string.IsNullOrEmpty(formatError))
                return Json(new { success = false, message = "Anh CCCD mat truoc: " + formatError });
            formatError = await ValidateUploadedImageAsync(idBack);
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
            catch (Exception)
            {
                return Json(new { success = false, message = "Khong the xu ly eKYC luc nay. Vui long thu lai sau." });
            }
        }

        /// <summary>
        /// AJAX: OCR áº£nh GPLX
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScanLicense(IFormFile license)
        {
            if (license == null)
                return Json(new { success = false, message = "Vui lÃ²ng táº£i lÃªn áº£nh GPLX." });

            var formatError = await ValidateUploadedImageAsync(license);
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
            catch (Exception)
            {
                return Json(new { success = false, message = "Khong the xu ly eKYC luc nay. Vui long thu lai sau." });
            }
        }

        /// <summary>
        /// AJAX: So sÃ¡nh khuÃ´n máº·t selfie vs CCCD
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyFace(IFormFile face, IFormFile idCard)
        {
            if (face == null)
                return Json(new { success = false, message = "Vui lÃ²ng táº£i lÃªn áº£nh selfie." });
            if (idCard == null)
                return Json(new { success = false, message = "Vui lÃ²ng táº£i lÃªn áº£nh CCCD máº·t trÆ°á»›c." });

            var formatError = await ValidateUploadedImageAsync(face);
            if (!string.IsNullOrEmpty(formatError))
                return Json(new { success = false, message = "Anh selfie: " + formatError });
            formatError = await ValidateUploadedImageAsync(idCard);
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
            catch (Exception)
            {
                return Json(new { success = false, message = "Khong the xu ly eKYC luc nay. Vui long thu lai sau." });
            }
        }

        /// <summary>
        /// Submit: Full verification pipeline
        /// 1. LÆ°u áº£nh
        /// 2. OCR CCCD + GPLX
        /// 3. Face Compare: Selfie vs CCCD, GPLX vs CCCD
        /// 4. Cross-check: Profile vs CCCD
        /// 5. Duplicate check: CCCD/GPLX Ä‘Ã£ dÃ¹ng bá»Ÿi tÃ i khoáº£n nÃ o chÆ°a?
        /// Káº¿t quáº£: Verified / Flagged / Rejected
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(
            IFormFile? idFront,
            IFormFile? idBack,
            IFormFile? license,
            IFormFile? licenseBack,
            IFormFile? face,
            string? returnUrl)
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
                var err = await ValidateUploadedImageAsync(idFront); if (!string.IsNullOrEmpty(err)) validationErrors.Add("CCCD mat truoc: " + err);
            }

            // Bat buoc: CCCD mat sau
            if (idBack == null || idBack.Length == 0)
                validationErrors.Add("Anh CCCD mat sau la bat buoc.");
            else
            {
                var err = await ValidateUploadedImageAsync(idBack); if (!string.IsNullOrEmpty(err)) validationErrors.Add("CCCD mat sau: " + err);
            }

            // Bat buoc: GPLX mat truoc
            if (license == null || license.Length == 0)
                validationErrors.Add("Anh GPLX mat truoc la bat buoc.");
            else
            {
                var err = await ValidateUploadedImageAsync(license); if (!string.IsNullOrEmpty(err)) validationErrors.Add("GPLX mat truoc: " + err);
            }

            // Bat buoc: GPLX mat sau
            if (licenseBack == null || licenseBack.Length == 0)
                validationErrors.Add("Anh GPLX mat sau la bat buoc.");
            else
            {
                var err = await ValidateUploadedImageAsync(licenseBack); if (!string.IsNullOrEmpty(err)) validationErrors.Add("GPLX mat sau: " + err);
            }

            // Bat buoc: Selfie
            if (face == null || face.Length == 0)
                validationErrors.Add("Anh selfie la bat buoc.");
            else
            {
                var err = await ValidateUploadedImageAsync(face); if (!string.IsNullOrEmpty(err)) validationErrors.Add("Anh selfie: " + err);
            }

            if (validationErrors.Count > 0)
            {
                TempData["ErrorMessage"] = "Vui lÃ²ng kiá»ƒm tra láº¡i cÃ¡c trÆ°á»ng sau:";
                TempData["EkycWarnings"] = string.Join("|", validationErrors);
                return RedirectAfterEkyc(returnUrl);
            }

            try
            {
                // --- LÆ°u áº£nh vÃ o App_Data/SecureUploads (PRIVATE, khÃ´ng public) ---
                string uploadFolder = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", "eKyc", user.UserID.ToString());
                Directory.CreateDirectory(uploadFolder);

                if (idFront != null && idFront.Length > 0)
                {
                    string idFPath = Path.Combine(uploadFolder, "id_front" + Path.GetExtension(idFront.FileName));
                    await SaveFileAsync(idFront, idFPath);
                    user.IdCardFrontUrl = $"/SecureFile/GetSecureImage?folder=eKyc&fileName={user.UserID}/id_front{Path.GetExtension(idFront.FileName)}";
                }
                if (idBack != null && idBack.Length > 0)
                {
                    string idBPath = Path.Combine(uploadFolder, "id_back" + Path.GetExtension(idBack.FileName));
                    await SaveFileAsync(idBack, idBPath);
                    user.IdCardBackUrl = $"/SecureFile/GetSecureImage?folder=eKyc&fileName={user.UserID}/id_back{Path.GetExtension(idBack.FileName)}";
                }
                if (license != null && license.Length > 0)
                {
                    string licPath = Path.Combine(uploadFolder, "license_front" + Path.GetExtension(license.FileName));
                    await SaveFileAsync(license, licPath);
                    user.DrivingLicenseUrl = $"/SecureFile/GetSecureImage?folder=eKyc&fileName={user.UserID}/license_front{Path.GetExtension(license.FileName)}";
                }
                if (licenseBack != null && licenseBack.Length > 0)
                {
                    string licBPath = Path.Combine(uploadFolder, "license_back" + Path.GetExtension(licenseBack.FileName));
                    await SaveFileAsync(licenseBack, licBPath);
                    user.DrivingLicenseBackUrl = $"/SecureFile/GetSecureImage?folder=eKyc&fileName={user.UserID}/license_back{Path.GetExtension(licenseBack.FileName)}";
                }
                if (face != null && face.Length > 0)
                {
                    string facePath = Path.Combine(uploadFolder, "face" + Path.GetExtension(face.FileName));
                    await SaveFileAsync(face, facePath);
                    user.FaceUrl = $"/SecureFile/GetSecureImage?folder=eKyc&fileName={user.UserID}/face{Path.GetExtension(face.FileName)}";
                }

                // --- OCR CCCD ---
                var providerWarnings = new List<string>();
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
                    else if (idResult.IsProviderError)
                    {
                        providerWarnings.Add($"Dich vu OCR CCCD chua san sang: {idResult.Message}");
                    }
                    else
                    {
                        providerWarnings.Add($"Chua doc duoc CCCD tu anh da tai len: {idResult.Message}");
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
                    else if (licResult.IsProviderError)
                    {
                        providerWarnings.Add($"Dich vu OCR GPLX chua san sang: {licResult.Message}");
                    }
                    else
                    {
                        providerWarnings.Add($"Chua doc duoc GPLX tu anh da tai len: {licResult.Message}");
                    }
                }

                // --- THU THáº¬P Táº¤T Cáº¢ Cáº¢NH BÃO & Lá»–I ---
                var errors = new List<string>(); // Lá»—i nghiÃªm trá»ng â†’ Reject
                var warnings = new List<string>(); // Cáº£nh bÃ¡o nháº¹ â†’ Flag
                warnings.AddRange(providerWarnings);

                // ============================================================
                // BÆ¯á»šC 1: Kiá»ƒm tra trÃ¹ng láº·p CCCD/GPLX trÃªn toÃ n há»‡ thá»‘ng
                // ============================================================
                if (cccdReadSuccess && !string.IsNullOrEmpty(citizenId))
                {
                    var cccdHash = ComputePiiHash(citizenId);
                    bool cccdExists = await _context.Users
                        .AnyAsync(u => u.UserID != userId && u.CitizenIdHash == cccdHash);
                    if (cccdExists)
                    {
                        errors.Add($"Sá»‘ CCCD '{MaskPii(citizenId)}' Ä‘Ã£ Ä‘Æ°á»£c sá»­ dá»¥ng bá»Ÿi tÃ i khoáº£n khÃ¡c. Má»—i CCCD chá»‰ Ä‘Æ°á»£c Ä‘Äƒng kÃ½ 1 tÃ i khoáº£n.");
                    }
                }

                if (licReadSuccess && !string.IsNullOrEmpty(licenseNumber))
                {
                    var licHash = ComputePiiHash(licenseNumber);
                    bool licExists = await _context.Users
                        .AnyAsync(u => u.UserID != userId && u.DrivingLicenseHash == licHash);
                    if (licExists)
                    {
                        errors.Add($"Sá»‘ GPLX '{MaskPii(licenseNumber)}' Ä‘Ã£ Ä‘Æ°á»£c sá»­ dá»¥ng bá»Ÿi tÃ i khoáº£n khÃ¡c. Má»—i GPLX chá»‰ Ä‘Æ°á»£c Ä‘Äƒng kÃ½ 1 tÃ i khoáº£n.");
                    }
                }

                // ============================================================
                // BÆ¯á»šC 2: Face Compare â€” Selfie vs CCCD (Báº®T BUá»˜C)
                // ============================================================
                EkycFaceResult? faceResult = null;
                if (face != null && idFront != null)
                {
                    faceResult = await _ekycService.CompareFaceAsync(face, idFront);
                    if (faceResult.IsSuccess)
                    {
                        if (!faceResult.IsMatch)
                        {
                            errors.Add($"KhuÃ´n máº·t trong áº£nh selfie khÃ´ng khá»›p vá»›i áº£nh CCCD (Ä‘á»™ tÆ°Æ¡ng Ä‘á»“ng: {faceResult.SimilarityPercent:F1}%, ngÆ°á»¡ng yÃªu cáº§u: 80%). ÄÃ¢y cÃ³ thá»ƒ lÃ  tÃ i khoáº£n giáº£ máº¡o.");
                        }
                        // isMatch = true â†’ khÃ´ng thÃªm warning, Ä‘Ã¢y lÃ  káº¿t quáº£ tÃ­ch cá»±c
                    }
                    else
                    {
                        errors.Add($"KhÃ´ng thá»ƒ so sÃ¡nh khuÃ´n máº·t. Lá»—i: {faceResult.Message}");
                    }
                }
                else if (face == null && idFront != null)
                {
                    warnings.Add("Báº¡n chÆ°a táº£i lÃªn áº£nh selfie. Äá»ƒ xÃ¡c thá»±c chÃ­nh xÃ¡c hÆ¡n, vui lÃ²ng bá»• sung áº£nh selfie.");
                }

                // ============================================================
                // BÆ¯á»šC 3: Face Compare â€” Selfie vs GPLX (náº¿u cÃ³ áº£nh GPLX)
                // ============================================================
                if (faceResult?.IsProviderError == true && errors.Count > 0)
                {
                    errors.RemoveAt(errors.Count - 1);
                    warnings.Add($"Chua the so sanh khuon mat voi CCCD: {faceResult.Message}. Ho so se duoc giu de xac minh lai.");
                }

                EkycFaceResult? faceLicResult = null;
                if (face != null && license != null)
                {
                    faceLicResult = await _ekycService.CompareFaceAsync(face, license);
                    if (faceLicResult.IsSuccess)
                    {
                        if (!faceLicResult.IsMatch)
                        {
                            errors.Add($"KhuÃ´n máº·t trong áº£nh selfie khÃ´ng khá»›p vá»›i áº£nh GPLX (Ä‘á»™ tÆ°Æ¡ng Ä‘á»“ng: {faceLicResult.SimilarityPercent:F1}%). ÄÃ¢y cÃ³ thá»ƒ lÃ  GPLX khÃ´ng thuá»™c vá» báº¡n.");
                        }
                        // isMatch = true â†’ khÃ´ng thÃªm warning, Ä‘Ã¢y lÃ  káº¿t quáº£ tÃ­ch cá»±c
                    }
                    else
                    {
                        errors.Add($"KhÃ´ng thá»ƒ so sÃ¡nh selfie vá»›i GPLX. Lá»—i: {faceLicResult.Message}");
                    }
                }

                // ============================================================
                // BÆ¯á»šC 4: Cross-check Profile vs CCCD
                // ============================================================
                if (faceLicResult?.IsProviderError == true && errors.Count > 0)
                {
                    errors.RemoveAt(errors.Count - 1);
                    warnings.Add($"Chua the so sanh selfie voi GPLX: {faceLicResult.Message}. Ho so se duoc giu de xac minh lai.");
                }

                if (cccdReadSuccess)
                {
                    // 4a) So sÃ¡nh tÃªn
                    if (!string.IsNullOrEmpty(fullNameCccd) && !string.IsNullOrEmpty(user.Name))
                    {
                        var normProfile = NormalizeVietnamese(user.Name.Trim());
                        var normCccd = NormalizeVietnamese(fullNameCccd.Trim());
                        if (!normProfile.Equals(normCccd))
                        {
                            // Cho phÃ©p trÆ°á»ng há»£p tÃªn trÃªn CCCD dÃ i hÆ¡n (cÃ³ thá»ƒ do viáº¿t Ä‘áº§y Ä‘á»§)
                            bool isSubset = normCccd.Contains(normProfile) && normCccd.Length - normProfile.Length <= 10;
                            if (!isSubset)
                            {
                                errors.Add($"TÃªn trÃªn CCCD ('{fullNameCccd}') khÃ´ng khá»›p vá»›i tÃªn trong há»“ sÆ¡ ('{user.Name}'). Vui lÃ²ng cáº­p nháº­t há»“ sÆ¡ cho Ä‘Ãºng.");
                            }
                            else
                            {
                                warnings.Add($"TÃªn há»“ sÆ¡ ('{user.Name}') khÃ¡c biá»‡t nháº¹ so vá»›i CCCD ('{fullNameCccd}') â€” cÃ³ thá»ƒ do tÃªn viáº¿t táº¯t/Ä‘áº§y Ä‘á»§.");
                            }
                        }
                    }

                    // 4b) So sÃ¡nh ngÃ y sinh
                    if (!string.IsNullOrEmpty(dobCccd) && user.DateOfBirth.HasValue)
                    {
                        if (DateTime.TryParse(dobCccd, out var cccdDob))
                        {
                            if (Math.Abs((cccdDob.Date - user.DateOfBirth.Value.Date).TotalDays) > 365)
                            {
                                errors.Add($"NgÃ y sinh trÃªn CCCD ({cccdDob:dd/MM/yyyy}) khÃ´ng khá»›p vá»›i ngÃ y sinh trong há»“ sÆ¡ ({user.DateOfBirth:dd/MM/yyyy}). Sá»± khÃ¡c biá»‡t trÃªn 1 nÄƒm.");
                            }
                        }
                    }

                    // 4c) So sÃ¡nh giá»›i tÃ­nh
                    if (!string.IsNullOrEmpty(genderCccd) && !string.IsNullOrEmpty(user.Gender))
                    {
                        var normGenderCccd = NormalizeVietnamese(genderCccd.Trim());
                        var normGenderProfile = NormalizeVietnamese(user.Gender.Trim());
                        if (!normGenderCccd.Equals(normGenderProfile))
                        {
                            errors.Add($"Giá»›i tÃ­nh trÃªn CCCD ('{genderCccd}') khÃ´ng khá»›p vá»›i giá»›i tÃ­nh trong há»“ sÆ¡ ('{user.Gender}').");
                        }
                    }
                }

                // ============================================================
                // BÆ¯á»šC 5: Quyáº¿t Ä‘á»‹nh tráº¡ng thÃ¡i cuá»‘i cÃ¹ng
                // ============================================================
                bool hasErrors = errors.Count > 0;
                bool hasWarnings = warnings.Count > 0;

                if (hasErrors)
                {
                    // âŒ REJECT â€” CÃ³ lá»—i nghiÃªm trá»ng
                    user.VerificationStatus = "Rejected";
                    user.IsVerified = false;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    TempData["ErrorMessage"] = "XÃ¡c thá»±c tháº¥t báº¡i. Há»‡ thá»‘ng phÃ¡t hiá»‡n cÃ¡c váº¥n Ä‘á» sau:";
                    TempData["EkycWarnings"] = string.Join("|", errors);
                    return RedirectAfterEkyc(returnUrl);
                }

                if (!cccdReadSuccess)
                {
                    // âš ï¸ CCCD khÃ´ng Ä‘á»c Ä‘Æ°á»£c â†’ Pending
                    user.VerificationStatus = "Pending";
                    user.IsVerified = false;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    TempData["WarningMessage"] = "Há»“ sÆ¡ Ä‘Ã£ Ä‘Æ°á»£c gá»­i nhÆ°ng khÃ´ng Ä‘á»c Ä‘Æ°á»£c CCCD. Há»‡ thá»‘ng sáº½ xÃ¡c minh trong 24h.";
                    return RedirectAfterEkyc(returnUrl);
                }

                // LÆ°u thÃ´ng tin Ä‘Ã£ OCR vÃ o user
                if (!string.IsNullOrWhiteSpace(citizenId))
                {
                    user.CitizenId = _piiEncryption.Encrypt(citizenId);
                    user.CitizenIdHash = ComputePiiHash(citizenId);
                }
                if (!string.IsNullOrWhiteSpace(fullNameCccd))
                    user.Name = fullNameCccd.Trim();
                if (!string.IsNullOrWhiteSpace(genderCccd))
                    user.Gender = genderCccd.Trim();
                if (!string.IsNullOrWhiteSpace(addressCccd))
                    user.Address = addressCccd.Trim();
                if (!string.IsNullOrWhiteSpace(dobCccd) && DateTime.TryParse(dobCccd, out var dob))
                    user.DateOfBirth = dob;
                if (!string.IsNullOrWhiteSpace(licenseNumber))
                {
                    user.DrivingLicenseNumber = _piiEncryption.Encrypt(licenseNumber);
                    user.DrivingLicenseHash = ComputePiiHash(licenseNumber);
                }

                if (hasWarnings)
                {
                    // âš ï¸ FLAGGED â€” CCCD Ä‘á»c Ä‘Æ°á»£c, máº·t khá»›p, nhÆ°ng cÃ³ cáº£nh bÃ¡o nháº¹
                    user.VerificationStatus = "Flagged";
                    user.IsVerified = false;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    TempData["WarningMessage"] = "Há»“ sÆ¡ Ä‘Ã£ Ä‘Æ°á»£c gá»­i. Há»‡ thá»‘ng ghi nháº­n má»™t sá»‘ thÃ´ng tin cáº§n xÃ¡c minh thÃªm.";
                    TempData["EkycWarnings"] = string.Join("|", warnings);
                }
                else
                {
                    // âœ… VERIFIED â€” Táº¥t cáº£ kiá»ƒm tra Ä‘á»u pass
                    user.VerificationStatus = "Verified";
                    user.IsVerified = true;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                    await _referralService.EnsureReferralCodeAsync(user.UserID);

                    TempData["SuccessMessage"] = "XÃ¡c thá»±c thÃ nh cÃ´ng! Há»‡ thá»‘ng Ä‘Ã£ tá»± Ä‘á»™ng xÃ¡c minh danh tÃ­nh tá»« CCCD cá»§a báº¡n.";
                }

                return RedirectAfterEkyc(returnUrl);
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Qua trinh tai len gap loi. Vui long thu lai sau.";
                return RedirectAfterEkyc(returnUrl);
            }
        }

        /// <summary>
        /// Che giáº¥u PII â€” chá»‰ hiá»‡n 4 kÃ½ tá»± cuá»‘i
        /// </summary>
        private IActionResult RedirectAfterEkyc(string? returnUrl)
        {
            if (TempData.ContainsKey("ErrorMessage") || TempData.ContainsKey("WarningMessage") || TempData.ContainsKey("EkycWarnings"))
            {
                TempData["ShowEkycModal"] = "true";
                TempData["EkycReturnUrl"] = returnUrl;
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Profile", "Auth");
        }

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

        private async Task<string?> ValidateUploadedImageAsync(IFormFile file)
        {
            var formatError = ValidateImageFormat(file);
            if (!string.IsNullOrEmpty(formatError))
                return formatError;

            var sizeError = ValidateFileSize(file);
            if (!string.IsNullOrEmpty(sizeError))
                return sizeError;

            if (!await _fileUploadValidation.IsAllowedImageAsync(file, MaxFileSizeBytes))
                return "File khong dung dinh dang anh hop le hoac noi dung khong khop duoi file.";

            return null;
        }

        private static string MaskPii(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "********";
            if (value.Length <= 4) return new string('*', value.Length);
            return new string('*', value.Length - 4) + value[^4..];
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
            string[] chars = { "Ã Ã¡áº¡áº£Ã£Ã¢áº§áº¥áº­áº©áº«Äƒáº±áº¯áº·áº³áºµÃ¨Ã©áº¹áº»áº½Ãªá»áº¿á»‡á»ƒá»…Ã¬Ã­á»‹á»‰Ä©", "Ã€Ãáº áº¢ÃƒÃ‚áº¦áº¤áº¬áº¨áºªÄ‚áº°áº®áº¶áº²áº´ÃˆÃ‰áº¸áººáº¼ÃŠá»€áº¾á»†á»‚á»„ÃŒÃá»Šá»ˆÄ¨", "Ã¨Ã©áº¹áº»áº½Ãªá»áº¿á»‡á»ƒá»…Ã¬Ã­á»‹á»‰Ä©Ä‘ÃˆÃ‰áº¸áººáº¼ÃŠá»€áº¾á»†á»‚á»„ÃŒÃá»Šá»ˆÄ¨Ä", "ÃŒÃá»Šá»ˆÄ¨Ã¬Ã­á»‹á»‰Ä©ÃŒÃá»Šá»ˆÄ¨", "Ã²Ã³á»á»ÃµÃ´á»“á»‘á»™á»•á»—Äƒáº±áº¯áº·áº³áºµÆ¡á»á»›á»£á»Ÿá»¡Ã¹Ãºá»¥á»§Å©Æ°á»«á»©á»±á»­á»¯Ã¬Ã­á»‹á»‰Ä©Ã½á»³á»µá»·á»¹", "Ã’Ã“á»Œá»ŽÃ•Ã”á»’á»á»˜á»”á»–Ä‚áº°áº®áº¶áº²áº´Æ á»œá»šá»¢á»žá» Ã™Ãšá»¤á»¦Å¨Æ¯á»ªá»¨á»°á»¬á»®ÃŒÃá»Šá»ˆÄ¨Ãá»²á»´á»¶á»¸", "Ä‘Ä", "Ä" };
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
