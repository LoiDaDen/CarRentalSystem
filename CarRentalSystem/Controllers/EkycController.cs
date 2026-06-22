using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class EkycController : Controller
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly IEkycService _ekycService;
        private readonly IPiiEncryptionService _piiEncryption;
        private readonly IWebHostEnvironment _env;
        private readonly IReferralService _referralService;
        private readonly IFileUploadValidationService _fileUploadValidation;
        private readonly ILegalComplianceService _legalCompliance;
        private readonly IIdentityHashService _identityHash;
        private readonly ICustomerDrivingLicenseSyncService _drivingLicenseSync;

        public EkycController(
            ApplicationDbContext context,
            IEkycService ekycService,
            IPiiEncryptionService piiEncryption,
            IWebHostEnvironment env,
            IReferralService referralService,
            IFileUploadValidationService fileUploadValidation,
            ILegalComplianceService legalCompliance,
            IIdentityHashService identityHash,
            ICustomerDrivingLicenseSyncService drivingLicenseSync)
        {
            _context = context;
            _ekycService = ekycService;
            _piiEncryption = piiEncryption;
            _env = env;
            _referralService = referralService;
            _fileUploadValidation = fileUploadValidation;
            _legalCompliance = legalCompliance;
            _identityHash = identityHash;
            _drivingLicenseSync = drivingLicenseSync;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound();

            ViewBag.Status = user.VerificationStatus;
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScanIdCard(IFormFile idFront, IFormFile idBack, bool EkycPrivacyConsent, bool EkycBiometricConsent)
        {
            if (!EkycPrivacyConsent || !EkycBiometricConsent)
                return Json(new { success = false, message = "Vui lòng đồng ý thông báo xử lý dữ liệu cá nhân và xử lý sinh trắc học trước khi quét CCCD." });

            if (idFront == null)
                return Json(new { success = false, message = "Vui lòng tải lên ảnh CCCD mặt trước." });
            if (idBack == null)
                return Json(new { success = false, message = "Vui lòng tải lên ảnh CCCD mặt sau." });

            var formatError = await ValidateUploadedImageAsync(idFront);
            if (!string.IsNullOrEmpty(formatError))
                return Json(new { success = false, message = "Ảnh CCCD mặt trước: " + formatError });

            formatError = await ValidateUploadedImageAsync(idBack);
            if (!string.IsNullOrEmpty(formatError))
                return Json(new { success = false, message = "Ảnh CCCD mặt sau: " + formatError });

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
                return Json(new { success = false, message = "Không thể xử lý eKYC lúc này. Vui lòng thử lại sau." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScanLicense(IFormFile license, bool EkycPrivacyConsent)
        {
            if (!EkycPrivacyConsent)
                return Json(new { success = false, message = "Vui lòng đồng ý thông báo xử lý dữ liệu cá nhân trước khi quét GPLX." });

            if (license == null)
                return Json(new { success = false, message = "Vui lòng tải lên ảnh GPLX." });

            var formatError = await ValidateUploadedImageAsync(license);
            if (!string.IsNullOrEmpty(formatError))
                return Json(new { success = false, message = "Ảnh GPLX: " + formatError });

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
                return Json(new { success = false, message = "Không thể xử lý eKYC lúc này. Vui lòng thử lại sau." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyFace(IFormFile face, IFormFile idCard, bool EkycPrivacyConsent, bool EkycBiometricConsent)
        {
            if (!EkycPrivacyConsent || !EkycBiometricConsent)
                return Json(new { success = false, message = "Vui lòng đồng ý xử lý dữ liệu cá nhân và dữ liệu sinh trắc học trước khi xác minh khuôn mặt." });

            if (face == null)
                return Json(new { success = false, message = "Vui lòng chụp ảnh selfie." });
            if (idCard == null)
                return Json(new { success = false, message = "Vui lòng tải lên ảnh CCCD mặt trước." });

            var formatError = await ValidateUploadedImageAsync(face);
            if (!string.IsNullOrEmpty(formatError))
                return Json(new { success = false, message = "Ảnh selfie: " + formatError });

            formatError = await ValidateUploadedImageAsync(idCard);
            if (!string.IsNullOrEmpty(formatError))
                return Json(new { success = false, message = "Ảnh CCCD: " + formatError });

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
                return Json(new { success = false, message = "Không thể xử lý eKYC lúc này. Vui lòng thử lại sau." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMobileSession(string? returnUrl)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var userExists = await _context.Users.AnyAsync(u => u.UserID == userId.Value);
            if (!userExists)
                return NotFound();

            var now = DateTime.UtcNow;
            var activeSessions = await _context.EkycMobileSessions
                .Where(s => s.UserID == userId.Value &&
                            (s.Status == EkycMobileSessionStatuses.Waiting || s.Status == EkycMobileSessionStatuses.Failed) &&
                            s.ExpiresAtUtc > now)
                .ToListAsync();

            foreach (var activeSession in activeSessions)
            {
                activeSession.Status = EkycMobileSessionStatuses.Expired;
                activeSession.FailureReason = "Phiên đã được thay bằng mã QR mới.";
            }

            var token = CreateMobileToken();
            var session = new EkycMobileSession
            {
                UserID = userId.Value,
                TokenHash = HashMobileToken(token),
                Status = EkycMobileSessionStatuses.Waiting,
                ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(15)
            };

            _context.EkycMobileSessions.Add(session);
            await _context.SaveChangesAsync();

            var mobileUrl = Url.Action(nameof(Mobile), "Ekyc", new { token }, Request.Scheme) ?? $"/Ekyc/Mobile/{token}";
            return Json(new
            {
                success = true,
                sessionId = session.EkycMobileSessionID,
                mobileUrl,
                qrDataUri = CreateQrCodeDataUri(mobileUrl),
                expiresAtUtc = session.ExpiresAtUtc,
                status = session.Status,
                statusText = GetMobileSessionStatusText(session.Status)
            });
        }

        [HttpGet]
        public async Task<IActionResult> MobileSessionStatus(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var session = await _context.EkycMobileSessions.FirstOrDefaultAsync(s => s.EkycMobileSessionID == id);
            if (session == null || session.UserID != userId.Value)
                return NotFound();

            await ExpireSessionIfNeededAsync(session);
            session.LastPolledAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                status = session.Status,
                statusText = GetMobileSessionStatusText(session.Status),
                message = session.FailureReason,
                warnings = ParseWarnings(session.WarningsJson),
                expiresAtUtc = session.ExpiresAtUtc,
                completedAtUtc = session.CompletedAtUtc
            });
        }

        [AllowAnonymous]
        [HttpGet("Ekyc/Mobile/{token}")]
        public async Task<IActionResult> Mobile(string token)
        {
            var session = await FindMobileSessionByTokenAsync(token);
            if (session == null)
                return View(CreateUnavailableMobileModel(token, "Mã QR eKYC không hợp lệ hoặc đã được thay bằng mã mới."));

            await ExpireSessionIfNeededAsync(session);
            await _context.SaveChangesAsync();

            return View(ToMobileViewModel(token, session));
        }

        [AllowAnonymous]
        [HttpPost("Ekyc/Mobile/{token}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Mobile(
            string token,
            IFormFile? idFront,
            IFormFile? idBack,
            IFormFile? license,
            IFormFile? licenseBack,
            IFormFile? face,
            bool EkycPrivacyConsent,
            bool EkycBiometricConsent)
        {
            var session = await FindMobileSessionByTokenAsync(token);
            if (session == null)
                return View(CreateUnavailableMobileModel(token, "Mã QR eKYC không hợp lệ hoặc đã được thay bằng mã mới."));

            await ExpireSessionIfNeededAsync(session);
            if (session.Status == EkycMobileSessionStatuses.Expired || EkycMobileSessionStatuses.IsTerminal(session.Status))
            {
                await _context.SaveChangesAsync();
                return View(ToMobileViewModel(token, session));
            }

            session.AttemptCount++;
            session.MobileUserAgent = Truncate(Request.Headers.UserAgent.ToString(), 500);
            session.Status = EkycMobileSessionStatuses.Processing;
            session.FailureReason = "Điện thoại đã gửi hồ sơ. DriveGo đang xử lý OCR và đối chiếu khuôn mặt.";
            await _context.SaveChangesAsync();

            var result = await ProcessEkycSubmissionAsync(
                session.UserID,
                idFront,
                idBack,
                license,
                licenseBack,
                face,
                EkycPrivacyConsent,
                EkycBiometricConsent,
                "EkycMobile",
                "Ekyc/Mobile",
                session.EkycMobileSessionID.ToString());

            session.FailureReason = result.Message;
            session.WarningsJson = JsonSerializer.Serialize(result.Messages);

            if (result.IsValidationFailure)
            {
                session.Status = EkycMobileSessionStatuses.Waiting;
                await _context.SaveChangesAsync();
                return View(ToMobileViewModel(token, session));
            }

            session.CompletedAtUtc = DateTime.UtcNow;
            session.Status = result.FinalVerificationStatus switch
            {
                "Verified" => EkycMobileSessionStatuses.Verified,
                "Rejected" => EkycMobileSessionStatuses.Rejected,
                "Pending" => EkycMobileSessionStatuses.PendingReview,
                _ => EkycMobileSessionStatuses.Failed
            };
            await _context.SaveChangesAsync();

            return View(ToMobileViewModel(token, session));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(
            IFormFile? idFront,
            IFormFile? idBack,
            IFormFile? license,
            IFormFile? licenseBack,
            IFormFile? face,
            bool EkycPrivacyConsent,
            bool EkycBiometricConsent,
            string? returnUrl)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var result = await ProcessEkycSubmissionAsync(
                userId.Value,
                idFront,
                idBack,
                license,
                licenseBack,
                face,
                EkycPrivacyConsent,
                EkycBiometricConsent,
                "Ekyc",
                "Ekyc/Submit",
                null);

            ApplySubmissionTempData(result);
            return RedirectAfterEkyc(returnUrl);
        }

        private async Task<EkycSubmissionResult> ProcessEkycSubmissionAsync(
            int userId,
            IFormFile? idFront,
            IFormFile? idBack,
            IFormFile? license,
            IFormFile? licenseBack,
            IFormFile? face,
            bool privacyConsent,
            bool biometricConsent,
            string source,
            string formName,
            string? mobileSessionId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return EkycSubmissionResult.Error("Không tìm thấy tài khoản eKYC.", "Failed");

            var validationErrors = await ValidateSubmissionInputsAsync(
                idFront,
                idBack,
                license,
                licenseBack,
                face,
                privacyConsent,
                biometricConsent);

            if (validationErrors.Count > 0)
                return EkycSubmissionResult.Validation("Vui lòng kiểm tra lại các trường sau:", validationErrors);

            var consentEvidence = JsonSerializer.Serialize(new
            {
                Form = formName,
                Vendor = "FPT.AI",
                MobileSessionId = mobileSessionId,
                Files = new[] { "CCCDFront", "CCCDBack", "DrivingLicenseFront", "DrivingLicenseBack", "Selfie" },
                Purposes = new[] { "IdentityVerification", "DrivingLicenseValidation", "FaceMatch" }
            });

            await _legalCompliance.RecordConsentAsync(userId, LegalDocumentCodes.PrivacyNotice, ConsentPurposeCodes.EkycIdentityVerification, privacyConsent, source, HttpContext, consentEvidence);
            await _legalCompliance.RecordConsentAsync(userId, LegalDocumentCodes.EkycBiometricConsent, ConsentPurposeCodes.EkycBiometricProcessing, biometricConsent, source, HttpContext, consentEvidence);

            try
            {
                await SaveEkycFilesAsync(user, idFront!, idBack!, license!, licenseBack!, face!);

                var providerWarnings = new List<string>();
                string? citizenId = null;
                string? fullNameCccd = null;
                string? dobCccd = null;
                string? genderCccd = null;
                string? addressCccd = null;
                string? licenseNumber = null;
                var cccdReadSuccess = false;
                var licReadSuccess = false;

                var idResult = await _ekycService.ExtractIdCardAsync(idFront!, idBack!);
                if (idResult.IsSuccess && !string.IsNullOrWhiteSpace(idResult.CitizenId))
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
                    providerWarnings.Add($"Dịch vụ OCR CCCD chưa sẵn sàng: {idResult.Message}");
                }
                else
                {
                    providerWarnings.Add($"Chưa đọc được CCCD từ ảnh đã tải lên: {idResult.Message}");
                }

                var licenseResult = await _ekycService.ExtractDrivingLicenseAsync(license!);
                if (licenseResult.IsSuccess && !string.IsNullOrWhiteSpace(licenseResult.LicenseNumber))
                {
                    licReadSuccess = true;
                    licenseNumber = licenseResult.LicenseNumber;
                }
                else if (licenseResult.IsProviderError)
                {
                    providerWarnings.Add($"Dịch vụ OCR GPLX chưa sẵn sàng: {licenseResult.Message}");
                }
                else
                {
                    providerWarnings.Add($"Chưa đọc được GPLX từ ảnh đã tải lên: {licenseResult.Message}");
                }

                var errors = new List<string>();
                var warnings = new List<string>(providerWarnings);

                if (cccdReadSuccess && !string.IsNullOrWhiteSpace(citizenId))
                {
                    var cccdHashes = _identityHash.HashesForLookup(citizenId);
                    var cccdExists = await _context.Users.AnyAsync(u =>
                        u.UserID != userId &&
                        u.CitizenIdHash != null &&
                        cccdHashes.Contains(u.CitizenIdHash));

                    if (cccdExists)
                        errors.Add($"Số CCCD '{MaskPii(citizenId)}' đã được sử dụng bởi tài khoản khác. Mỗi CCCD chỉ được đăng ký 1 tài khoản.");
                }

                if (licReadSuccess && !string.IsNullOrWhiteSpace(licenseNumber))
                {
                    var licenseHashes = _identityHash.HashesForLookup(licenseNumber);
                    var licenseExists = await _context.Users.AnyAsync(u =>
                        u.UserID != userId &&
                        u.DrivingLicenseHash != null &&
                        licenseHashes.Contains(u.DrivingLicenseHash));

                    if (licenseExists)
                        errors.Add($"Số GPLX '{MaskPii(licenseNumber)}' đã được sử dụng bởi tài khoản khác. Mỗi GPLX chỉ được đăng ký 1 tài khoản.");
                }

                var faceResult = await _ekycService.CompareFaceAsync(face!, idFront!);
                if (faceResult.IsSuccess)
                {
                    if (!faceResult.IsMatch)
                        errors.Add($"Khuôn mặt trong ảnh selfie không khớp với ảnh CCCD (độ tương đồng: {faceResult.SimilarityPercent:F1}%, ngưỡng yêu cầu: 80%).");
                }
                else if (faceResult.IsProviderError)
                {
                    warnings.Add($"Chưa thể so sánh khuôn mặt với CCCD: {faceResult.Message}. Hồ sơ sẽ được giữ để xác minh lại.");
                }
                else
                {
                    errors.Add($"Không thể so sánh khuôn mặt. Lỗi: {faceResult.Message}");
                }

                var faceLicenseResult = await _ekycService.CompareFaceAsync(face!, license!);
                if (faceLicenseResult.IsSuccess)
                {
                    if (!faceLicenseResult.IsMatch)
                        errors.Add($"Khuôn mặt trong ảnh selfie không khớp với ảnh GPLX (độ tương đồng: {faceLicenseResult.SimilarityPercent:F1}%).");
                }
                else if (faceLicenseResult.IsProviderError)
                {
                    warnings.Add($"Chưa thể so sánh selfie với GPLX: {faceLicenseResult.Message}. Hồ sơ sẽ được giữ để xác minh lại.");
                }
                else
                {
                    errors.Add($"Không thể so sánh selfie với GPLX. Lỗi: {faceLicenseResult.Message}");
                }

                if (cccdReadSuccess)
                    AddProfileCrossCheckMessages(user, fullNameCccd, dobCccd, genderCccd, errors, warnings);

                if (errors.Count > 0)
                {
                    user.VerificationStatus = "Rejected";
                    user.IsVerified = false;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                    return EkycSubmissionResult.Error("Xác thực thất bại. Hệ thống phát hiện các vấn đề sau:", "Rejected", errors);
                }

                if (!cccdReadSuccess)
                {
                    user.VerificationStatus = "Pending";
                    user.IsVerified = false;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                    return EkycSubmissionResult.Warning("Hồ sơ đã được gửi nhưng không đọc được CCCD. Hệ thống sẽ xác minh trong 24h.", "Pending", warnings);
                }

                ApplyExtractedIdentity(user, citizenId, fullNameCccd, dobCccd, genderCccd, addressCccd, licenseNumber);

                if (warnings.Count > 0)
                {
                    user.VerificationStatus = "Pending";
                    user.IsVerified = false;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                    return EkycSubmissionResult.Warning("Hồ sơ đã được gửi. Hệ thống ghi nhận một số thông tin cần xác minh thêm.", "Pending", warnings);
                }

                user.VerificationStatus = "Verified";
                user.IsVerified = true;
                _context.Users.Update(user);
                await _drivingLicenseSync.UpsertFromEkycSuccessAsync(
                    user,
                    licenseNumber,
                    user.DrivingLicenseUrl!,
                    user.DrivingLicenseBackUrl ?? user.DrivingLicenseUrl!);
                await _referralService.EnsureReferralCodeAsync(user.UserID);

                return EkycSubmissionResult.Success("Xác thực thành công! Hệ thống đã tự động xác minh danh tính từ CCCD của bạn.", "Verified");
            }
            catch (Exception)
            {
                return EkycSubmissionResult.Error("Quá trình tải lên gặp lỗi. Vui lòng thử lại sau.", "Failed");
            }
        }

        private async Task<List<string>> ValidateSubmissionInputsAsync(
            IFormFile? idFront,
            IFormFile? idBack,
            IFormFile? license,
            IFormFile? licenseBack,
            IFormFile? face,
            bool privacyConsent,
            bool biometricConsent)
        {
            var validationErrors = new List<string>();

            if (!privacyConsent)
                validationErrors.Add("Bạn phải đồng ý thông báo xử lý dữ liệu cá nhân trước khi gửi eKYC.");
            if (!biometricConsent)
                validationErrors.Add("Bạn phải đồng ý xử lý ảnh khuôn mặt/sinh trắc học và gửi dữ liệu cho FPT.AI để xác minh eKYC.");

            await ValidateRequiredImageAsync(idFront, "CCCD mặt trước", validationErrors);
            await ValidateRequiredImageAsync(idBack, "CCCD mặt sau", validationErrors);
            await ValidateRequiredImageAsync(license, "GPLX mặt trước", validationErrors);
            await ValidateRequiredImageAsync(licenseBack, "GPLX mặt sau", validationErrors);
            await ValidateRequiredImageAsync(face, "Ảnh selfie", validationErrors);

            return validationErrors;
        }

        private async Task ValidateRequiredImageAsync(IFormFile? file, string label, List<string> validationErrors)
        {
            if (file == null || file.Length == 0)
            {
                validationErrors.Add($"{label} là bắt buộc.");
                return;
            }

            var err = await ValidateUploadedImageAsync(file);
            if (!string.IsNullOrEmpty(err))
                validationErrors.Add($"{label}: {err}");
        }

        private async Task SaveEkycFilesAsync(User user, IFormFile idFront, IFormFile idBack, IFormFile license, IFormFile licenseBack, IFormFile face)
        {
            var uploadFolder = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", "eKyc", user.UserID.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(uploadFolder);

            user.IdCardFrontUrl = await SaveEkycFileAsync(user, uploadFolder, idFront, "id_front");
            user.IdCardBackUrl = await SaveEkycFileAsync(user, uploadFolder, idBack, "id_back");
            user.DrivingLicenseUrl = await SaveEkycFileAsync(user, uploadFolder, license, "license_front");
            user.DrivingLicenseBackUrl = await SaveEkycFileAsync(user, uploadFolder, licenseBack, "license_back");
            user.FaceUrl = await SaveEkycFileAsync(user, uploadFolder, face, "face");
        }

        private async Task<string> SaveEkycFileAsync(User user, string uploadFolder, IFormFile file, string stem)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var path = Path.Combine(uploadFolder, stem + extension);
            await SaveFileAsync(file, path);
            return $"/SecureFile/GetSecureImage?folder=eKyc&fileName={user.UserID}/{stem}{extension}";
        }

        private void AddProfileCrossCheckMessages(
            User user,
            string? fullNameCccd,
            string? dobCccd,
            string? genderCccd,
            List<string> errors,
            List<string> warnings)
        {
            if (!string.IsNullOrWhiteSpace(fullNameCccd) && !string.IsNullOrWhiteSpace(user.Name))
            {
                var profileName = NormalizeVietnamese(user.Name);
                var cccdName = NormalizeVietnamese(fullNameCccd);
                if (!profileName.Equals(cccdName, StringComparison.Ordinal))
                {
                    var isSubset = cccdName.Contains(profileName, StringComparison.Ordinal) && cccdName.Length - profileName.Length <= 10;
                    if (!isSubset)
                        errors.Add($"Tên trên CCCD ('{fullNameCccd}') không khớp với tên trong hồ sơ ('{user.Name}'). Vui lòng cập nhật hồ sơ cho đúng.");
                    else
                        warnings.Add($"Tên hồ sơ ('{user.Name}') khác biệt nhẹ so với CCCD ('{fullNameCccd}').");
                }
            }

            if (!string.IsNullOrWhiteSpace(dobCccd) && user.DateOfBirth.HasValue && TryParseVietnameseDate(dobCccd, out var cccdDob))
            {
                if (Math.Abs((cccdDob.Date - user.DateOfBirth.Value.Date).TotalDays) > 365)
                    errors.Add($"Ngày sinh trên CCCD ({cccdDob:dd/MM/yyyy}) không khớp với ngày sinh trong hồ sơ ({user.DateOfBirth:dd/MM/yyyy}). Sự khác biệt trên 1 năm.");
            }

            if (!string.IsNullOrWhiteSpace(genderCccd) && !string.IsNullOrWhiteSpace(user.Gender))
            {
                var cccdGender = NormalizeVietnamese(genderCccd);
                var profileGender = NormalizeVietnamese(user.Gender);
                if (!cccdGender.Equals(profileGender, StringComparison.Ordinal))
                    errors.Add($"Giới tính trên CCCD ('{genderCccd}') không khớp với giới tính trong hồ sơ ('{user.Gender}').");
            }
        }

        private void ApplyExtractedIdentity(
            User user,
            string? citizenId,
            string? fullNameCccd,
            string? dobCccd,
            string? genderCccd,
            string? addressCccd,
            string? licenseNumber)
        {
            if (!string.IsNullOrWhiteSpace(citizenId))
            {
                user.CitizenId = _piiEncryption.Encrypt(citizenId);
                user.CitizenIdHash = _identityHash.HashForStorage(citizenId);
            }

            if (!string.IsNullOrWhiteSpace(fullNameCccd))
                user.Name = fullNameCccd.Trim();
            if (!string.IsNullOrWhiteSpace(genderCccd))
                user.Gender = genderCccd.Trim();
            if (!string.IsNullOrWhiteSpace(addressCccd))
                user.Address = addressCccd.Trim();
            if (!string.IsNullOrWhiteSpace(dobCccd) && TryParseVietnameseDate(dobCccd, out var dob))
                user.DateOfBirth = dob;
            if (!string.IsNullOrWhiteSpace(licenseNumber))
            {
                user.DrivingLicenseNumber = _piiEncryption.Encrypt(licenseNumber);
                user.DrivingLicenseHash = _identityHash.HashForStorage(licenseNumber);
            }
        }

        private void ApplySubmissionTempData(EkycSubmissionResult result)
        {
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = result.Message;
                return;
            }

            if (result.IsWarning)
                TempData["WarningMessage"] = result.Message;
            else
                TempData["ErrorMessage"] = result.Message;

            if (result.Messages.Count > 0)
                TempData["EkycWarnings"] = string.Join("|", result.Messages);
        }

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

        private int? GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdStr, out var userId) ? userId : null;
        }

        private static string CreateMobileToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return WebEncoders.Base64UrlEncode(bytes);
        }

        private static string HashMobileToken(string token)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string CreateQrCodeDataUri(string mobileUrl)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(mobileUrl, QRCodeGenerator.ECCLevel.Q);
            var svg = new SvgQRCode(data).GetGraphic(5);
            return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        }

        private async Task<EkycMobileSession?> FindMobileSessionByTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var tokenHash = HashMobileToken(token);
            return await _context.EkycMobileSessions.FirstOrDefaultAsync(s => s.TokenHash == tokenHash);
        }

        private async Task ExpireSessionIfNeededAsync(EkycMobileSession session)
        {
            if (EkycMobileSessionStatuses.IsTerminal(session.Status))
                return;

            if (session.ExpiresAtUtc > DateTime.UtcNow)
                return;

            session.Status = EkycMobileSessionStatuses.Expired;
            session.FailureReason = "Mã QR đã hết hạn. Vui lòng quay lại máy tính và tạo mã mới.";
            await Task.CompletedTask;
        }

        private static EkycMobileViewModel ToMobileViewModel(string token, EkycMobileSession session)
        {
            var warnings = ParseWarnings(session.WarningsJson);
            var isAvailable = (session.Status == EkycMobileSessionStatuses.Waiting || session.Status == EkycMobileSessionStatuses.Failed) &&
                              session.ExpiresAtUtc > DateTime.UtcNow;

            return new EkycMobileViewModel
            {
                Token = token,
                Status = session.Status,
                StatusText = GetMobileSessionStatusText(session.Status),
                ExpiresAtUtc = session.ExpiresAtUtc,
                IsAvailable = isAvailable,
                IsCompleted = EkycMobileSessionStatuses.IsTerminal(session.Status),
                Message = session.FailureReason,
                Warnings = warnings
            };
        }

        private static EkycMobileViewModel CreateUnavailableMobileModel(string token, string message) => new()
        {
            Token = token,
            Status = EkycMobileSessionStatuses.Failed,
            StatusText = "Liên kết không hợp lệ",
            IsAvailable = false,
            Message = message
        };

        private static IReadOnlyList<string> ParseWarnings(string? warningsJson)
        {
            if (string.IsNullOrWhiteSpace(warningsJson))
                return Array.Empty<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(warningsJson) ?? new List<string>();
            }
            catch (JsonException)
            {
                return warningsJson.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }

        private static string GetMobileSessionStatusText(string? status) => status switch
        {
            EkycMobileSessionStatuses.Waiting => "Đang chờ thao tác trên điện thoại",
            EkycMobileSessionStatuses.Processing => "Đang xử lý hồ sơ",
            EkycMobileSessionStatuses.Verified => "Đã xác thực",
            EkycMobileSessionStatuses.PendingReview => "Chờ duyệt thủ công",
            EkycMobileSessionStatuses.Rejected => "Bị từ chối",
            EkycMobileSessionStatuses.Expired => "Hết hạn",
            EkycMobileSessionStatuses.Failed => "Cần thử lại",
            _ => "Không xác định"
        };

        private static string? ValidateImageFormat(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return null;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
                return "File không có đuôi. Chỉ chấp nhận: JPG, JPEG, PNG, WEBP.";
            if (!AllowedExtensions.Contains(ext))
                return $"Định dạng '{ext}' không được chấp nhận. Chỉ chấp nhận: JPG, JPEG, PNG, WEBP.";

            return null;
        }

        private static string? ValidateFileSize(IFormFile file)
        {
            if (file == null)
                return null;
            if (file.Length > MaxFileSizeBytes)
                return $"File quá lớn ({file.Length / 1024 / 1024:F1}MB). Tối đa 5MB.";

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
                return "File không đúng định dạng ảnh hợp lệ hoặc nội dung không khớp đuôi file.";

            return null;
        }

        private static string MaskPii(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "********";
            if (value.Length <= 4)
                return new string('*', value.Length);

            return new string('*', value.Length - 4) + value[^4..];
        }

        private static string NormalizeVietnamese(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                    builder.Append(character);
            }

            return builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd')
                .Trim();
        }

        private static bool TryParseVietnameseDate(string value, out DateTime date)
        {
            return DateTime.TryParseExact(value, new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                   DateTime.TryParse(value, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out date);
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Length <= maxLength ? value : value[..maxLength];
        }

        private async Task SaveFileAsync(IFormFile file, string path)
        {
            await using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
        }

        private sealed class EkycSubmissionResult
        {
            public string Message { get; init; } = string.Empty;
            public string FinalVerificationStatus { get; init; } = string.Empty;
            public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
            public bool IsValidationFailure { get; init; }
            public bool IsSuccess { get; init; }
            public bool IsWarning { get; init; }

            public static EkycSubmissionResult Validation(string message, IReadOnlyList<string> messages) => new()
            {
                Message = message,
                FinalVerificationStatus = "ValidationFailed",
                Messages = messages,
                IsValidationFailure = true
            };

            public static EkycSubmissionResult Error(string message, string status, IReadOnlyList<string>? messages = null) => new()
            {
                Message = message,
                FinalVerificationStatus = status,
                Messages = messages ?? Array.Empty<string>()
            };

            public static EkycSubmissionResult Warning(string message, string status, IReadOnlyList<string>? messages = null) => new()
            {
                Message = message,
                FinalVerificationStatus = status,
                Messages = messages ?? Array.Empty<string>(),
                IsWarning = true
            };

            public static EkycSubmissionResult Success(string message, string status) => new()
            {
                Message = message,
                FinalVerificationStatus = status,
                IsSuccess = true
            };
        }
    }
}
