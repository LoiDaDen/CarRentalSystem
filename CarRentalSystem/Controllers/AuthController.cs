using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace CarRentalSystem.Controllers
{
    public class AuthController : Controller
    {
        private const string PendingMfaUserIdSessionKey = "PrivilegedMfaUserId";
        private const string PendingMfaOtpHashSessionKey = "PrivilegedMfaOtpHash";
        private const string PendingMfaExpiresAtSessionKey = "PrivilegedMfaExpiresAt";
        private const string PendingMfaReturnUrlSessionKey = "PrivilegedMfaReturnUrl";
        private const string PendingMfaAttemptCountSessionKey = "PrivilegedMfaAttemptCount";
        private const string PendingMfaEmailSessionKey = "PrivilegedMfaEmail";
        private static readonly TimeSpan PrivilegedMfaTtl = TimeSpan.FromMinutes(5);

        private readonly IAuthService _authService;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;
        private readonly IPiiEncryptionService _piiEncryption;
        private readonly ApplicationDbContext _context;
        private readonly IRecaptchaService _recaptchaService;
        private readonly IReferralService _referralService;
        private readonly IConfiguration _configuration;
        private readonly IAuditTrailService _auditTrail;

        public AuthController(IAuthService authService, IWebHostEnvironment env, IEmailService emailService, IPiiEncryptionService piiEncryption, ApplicationDbContext context, IRecaptchaService recaptchaService, IReferralService referralService, IConfiguration configuration, IAuditTrailService auditTrail)
        {
            _authService = authService;
            _env = env;
            _emailService = emailService;
            _piiEncryption = piiEncryption;
            _context = context;
            _recaptchaService = recaptchaService;
            _referralService = referralService;
            _configuration = configuration;
            _auditTrail = auditTrail;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity.IsAuthenticated)
            {
                if (IsSafeLocalReturnUrl(returnUrl))
                    return LocalRedirect(returnUrl!);

                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = IsSafeLocalReturnUrl(returnUrl) ? returnUrl : null;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            if (!string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(provider, "Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Nha cung cap SSO khong hop le.";
                return RedirectToAction(nameof(Login));
            }

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, provider);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
        {
            var result = await HttpContext.AuthenticateAsync("External");
            if (!result.Succeeded || result.Principal == null)
            {
                await _auditTrail.RecordAsync(new AuditLog
                {
                    Action = "ExternalLoginFailed",
                    EntityType = "Auth",
                    Reason = "External provider did not return an authenticated principal."
                });
                TempData["ErrorMessage"] = "Dang nhap SSO khong thanh cong.";
                return RedirectToAction(nameof(Login));
            }

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            await HttpContext.SignOutAsync("External");
            if (string.IsNullOrWhiteSpace(email))
            {
                await _auditTrail.RecordAsync(new AuditLog
                {
                    Action = "ExternalLoginFailed",
                    EntityType = "Auth",
                    Reason = "External provider did not return email."
                });
                TempData["ErrorMessage"] = "Tai khoan SSO khong co email hop le.";
                return RedirectToAction(nameof(Login));
            }

            var user = await _authService.GetUserByEmailAsync(email);
            if (user == null || !IsPrivilegedRole(user.Role) || !user.IsEmailVerified || user.IsLocked || user.IsBlacklisted)
            {
                await _auditTrail.RecordAsync(new AuditLog
                {
                    Action = "ExternalLoginRejected",
                    EntityType = "Auth",
                    EntityId = user?.UserID,
                    UserId = user?.UserID,
                    Reason = "SSO is restricted to existing verified privileged users.",
                    NewValues = System.Text.Json.JsonSerializer.Serialize(new { Email = email, Role = user?.Role })
                });
                TempData["ErrorMessage"] = "SSO chi danh cho tai khoan quan tri/nhan su da duoc xac thuc.";
                return RedirectToAction(nameof(Login));
            }

            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "ExternalLoginAccepted",
                EntityType = nameof(User),
                EntityId = user.UserID,
                UserId = user.UserID,
                Reason = "Privileged SSO principal matched an existing verified user."
            });

            if (RequiresPrivilegedMfa(user))
            {
                await BeginPrivilegedMfaChallengeAsync(user, returnUrl);
                return RedirectToAction(nameof(VerifyMfa));
            }

            await SignInUserAsync(user);
            return RedirectAfterLogin(user, returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? recaptchaToken, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = IsSafeLocalReturnUrl(returnUrl) ? returnUrl : null;

            if (!await ValidateRecaptchaForViewAsync(recaptchaToken, "login", "Dang nhap khong thanh cong do xac thuc bao mat that bai."))
                return View();

            // ✅ TẠM THỜI COMMENT reCAPTCHA vì không hoạt động trên localhost
            // if (string.IsNullOrEmpty(recaptchaToken))
            // {
            //     ViewBag.Error = "Vui lòng xác nhận reCAPTCHA trước khi đăng nhập.";
            //     return View();
            // }

            // var recaptchaResult = await _recaptchaService.VerifyAsync(recaptchaToken, "login");
            // if (!recaptchaResult.Success)
            // {
            //     ViewBag.RecaptchaError = "Xác thực reCAPTCHA thất bại. Vui lòng thử lại.";
            //     ViewBag.Error = "Đăng nhập không thành công do xác thực bảo mật thất bại.";
            //     return View();
            // }

            var user = await _authService.AuthenticateAsync(email, password);
            if (user == null)
            {
                ViewBag.Error = "Email hoặc mật khẩu không chính xác.";
                return View();
            }

            if (!user.IsEmailVerified)
            {
                ViewBag.Error = "Vui lòng kiểm tra hộp thư email và xác thực tài khoản trước khi đăng nhập. Hoặc đăng nhập khi đã xác thực.";
                return View();
            }

            if (user.IsLocked)
            {
                ViewBag.Error = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên để được hỗ trợ.";
                return View();
            }

            if (user.IsBlacklisted)
            {
                ViewBag.Error = $"Tài khoản của bạn đã bị chặn. Lý do: {user.BlacklistReason ?? "Không xác định"}. Vui lòng liên hệ quản trị viên.";
                return View();
            }

            if (RequiresPrivilegedMfa(user))
            {
                await BeginPrivilegedMfaChallengeAsync(user, returnUrl);
                return RedirectToAction("VerifyMfa");
            }

            await SignInUserAsync(user);
            return RedirectAfterLogin(user, returnUrl);
        }

        private bool IsSafeLocalReturnUrl(string? returnUrl)
        {
            return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl);
        }

        private bool ShouldVerifyRecaptcha()
        {
            return _env.IsProduction() || _configuration.GetValue("Security:RequireRecaptcha", false);
        }

        private async Task<bool> ValidateRecaptchaForViewAsync(string? recaptchaToken, string action, string errorMessage)
        {
            if (!ShouldVerifyRecaptcha())
                return true;

            var result = await _recaptchaService.VerifyAsync(recaptchaToken ?? string.Empty, action);
            if (result.Success)
                return true;

            ViewBag.RecaptchaError = "Xac thuc bao mat that bai. Vui long thu lai.";
            ViewBag.Error = errorMessage;
            return false;
        }

        private async Task<bool> ValidateRecaptchaForRedirectAsync(string? recaptchaToken, string action)
        {
            if (!ShouldVerifyRecaptcha())
                return true;

            var result = await _recaptchaService.VerifyAsync(recaptchaToken ?? string.Empty, action);
            if (result.Success)
                return true;

            TempData["ErrorMessage"] = "Xac thuc bao mat that bai. Vui long thu lai.";
            return false;
        }

        [HttpGet]
        public IActionResult VerifyMfa()
        {
            if (!HttpContext.Session.GetInt32(PendingMfaUserIdSessionKey).HasValue)
                return RedirectToAction("Login");

            ViewBag.Email = HttpContext.Session.GetString(PendingMfaEmailSessionKey);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyMfa(string otp)
        {
            var pendingUserId = HttpContext.Session.GetInt32(PendingMfaUserIdSessionKey);
            var expectedHash = HttpContext.Session.GetString(PendingMfaOtpHashSessionKey);
            var expiresAtText = HttpContext.Session.GetString(PendingMfaExpiresAtSessionKey);

            if (!pendingUserId.HasValue || string.IsNullOrWhiteSpace(expectedHash) || string.IsNullOrWhiteSpace(expiresAtText))
                return RedirectToAction("Login");

            if (!DateTime.TryParse(expiresAtText, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAt) ||
                expiresAt < DateTime.UtcNow)
            {
                ClearPrivilegedMfaSession();
                TempData["ErrorMessage"] = "Ma MFA da het han. Vui long dang nhap lai.";
                return RedirectToAction("Login");
            }

            var attempts = HttpContext.Session.GetInt32(PendingMfaAttemptCountSessionKey) ?? 0;
            if (attempts >= 5)
            {
                ClearPrivilegedMfaSession();
                TempData["ErrorMessage"] = "Qua nhieu lan nhap sai ma MFA. Vui long dang nhap lai.";
                return RedirectToAction("Login");
            }

            var normalizedOtp = otp?.Trim() ?? string.Empty;
            if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedOtp, "^\\d{6}$") ||
                !string.Equals(ComputeHash(normalizedOtp), expectedHash, StringComparison.Ordinal))
            {
                HttpContext.Session.SetInt32(PendingMfaAttemptCountSessionKey, attempts + 1);
                ViewBag.Email = HttpContext.Session.GetString(PendingMfaEmailSessionKey);
                ViewBag.Error = "Ma MFA khong dung. Vui long kiem tra email va thu lai.";
                return View();
            }

            var user = await _authService.GetUserByIdAsync(pendingUserId.Value);
            if (user == null || !RequiresPrivilegedMfa(user) || !user.IsEmailVerified || user.IsLocked || user.IsBlacklisted)
            {
                ClearPrivilegedMfaSession();
                TempData["ErrorMessage"] = "Phien MFA khong hop le. Vui long dang nhap lai.";
                return RedirectToAction("Login");
            }

            var returnUrl = HttpContext.Session.GetString(PendingMfaReturnUrlSessionKey);
            ClearPrivilegedMfaSession();

            await SignInUserAsync(user);
            return RedirectAfterLogin(user, returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendMfa()
        {
            var pendingUserId = HttpContext.Session.GetInt32(PendingMfaUserIdSessionKey);
            if (!pendingUserId.HasValue)
                return RedirectToAction("Login");

            var user = await _authService.GetUserByIdAsync(pendingUserId.Value);
            if (user == null || !RequiresPrivilegedMfa(user))
            {
                ClearPrivilegedMfaSession();
                return RedirectToAction("Login");
            }

            await BeginPrivilegedMfaChallengeAsync(user, HttpContext.Session.GetString(PendingMfaReturnUrlSessionKey));
            TempData["SuccessMessage"] = "Ma MFA moi da duoc gui den email cua ban.";
            return RedirectToAction("VerifyMfa");
        }

        private Task BeginPrivilegedMfaChallengeAsync(User user, string? returnUrl)
        {
            var otp = GenerateOtp();
            HttpContext.Session.SetInt32(PendingMfaUserIdSessionKey, user.UserID);
            HttpContext.Session.SetString(PendingMfaOtpHashSessionKey, ComputeHash(otp));
            HttpContext.Session.SetString(PendingMfaExpiresAtSessionKey, DateTime.UtcNow.Add(PrivilegedMfaTtl).ToString("O"));
            HttpContext.Session.SetString(PendingMfaEmailSessionKey, user.Email);
            HttpContext.Session.SetInt32(PendingMfaAttemptCountSessionKey, 0);

            if (IsSafeLocalReturnUrl(returnUrl))
                HttpContext.Session.SetString(PendingMfaReturnUrlSessionKey, returnUrl!);
            else
                HttpContext.Session.Remove(PendingMfaReturnUrlSessionKey);

            Hangfire.BackgroundJob.Enqueue<CarRentalSystem.BackgroundJobs.EmailBackgroundJob>(
                job => job.SendOtpEmail(user.Email, otp));

            return Task.CompletedTask;
        }

        private void ClearPrivilegedMfaSession()
        {
            HttpContext.Session.Remove(PendingMfaUserIdSessionKey);
            HttpContext.Session.Remove(PendingMfaOtpHashSessionKey);
            HttpContext.Session.Remove(PendingMfaExpiresAtSessionKey);
            HttpContext.Session.Remove(PendingMfaReturnUrlSessionKey);
            HttpContext.Session.Remove(PendingMfaAttemptCountSessionKey);
            HttpContext.Session.Remove(PendingMfaEmailSessionKey);
        }

        private bool RequiresPrivilegedMfa(User user)
        {
            if (!IsPrivilegedRole(user.Role))
                return false;

            var configRequiresMfa = HttpContext.RequestServices.GetService(typeof(IConfiguration)) is IConfiguration configuration &&
                configuration.GetValue("Security:RequirePrivilegedMfa", false);

            return IsProductionEnvironment() || configRequiresMfa;
        }

        private bool IsProductionEnvironment() =>
            string.Equals(_env.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase);

        private static bool IsPrivilegedRole(string? role) =>
            string.Equals(role, UserRoleConstants.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, UserRoleConstants.Manager, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, UserRoleConstants.Staff, StringComparison.OrdinalIgnoreCase);

        private async Task SignInUserAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, "Cookies");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("Cookies", principal);
        }

        private IActionResult RedirectAfterLogin(User user, string? returnUrl)
        {
            if (IsSafeLocalReturnUrl(returnUrl))
                return LocalRedirect(returnUrl!);

            if (user.Role == UserRoleConstants.Admin)
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

            if (user.Role == UserRoleConstants.Manager || user.Role == UserRoleConstants.Staff)
                return RedirectToAction("Index", "AreaDashboard", new { area = "Admin" });

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? recaptchaToken)
        {
            if (!await ValidateRecaptchaForViewAsync(recaptchaToken, "register", "Dang ky khong thanh cong do xac thuc bao mat that bai."))
                return View(model);

            // ✅ TẠM THỜI COMMENT reCAPTCHA vì không hoạt động trên localhost
            // if (string.IsNullOrEmpty(recaptchaToken))
            // {
            //     ViewBag.Error = "Vui lòng xác nhận reCAPTCHA trước khi đăng ký.";
            //     return View(user);
            // }

            // var recaptchaResult = await _recaptchaService.VerifyAsync(recaptchaToken, "register");
            // if (!recaptchaResult.Success)
            // {
            //     ViewBag.RecaptchaError = "Xác thực reCAPTCHA thất bại. Vui lòng thử lại.";
            //     ViewBag.Error = "Đăng ký không thành công do xác thực bảo mật thất bại.";
            //     return View(user);
            // }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                ViewBag.Error = string.Join("; ", errors);
                return View(model);
            }

            var newName = model.Name.Trim();
            var newEmail = model.Email.Trim();
            var newPhone = model.Phone.Trim();

            var registeredUser = await _authService.GetUserByEmailAsync(newEmail);
            if (registeredUser != null)
            {
                ViewBag.Error = "Email nay da duoc dang ky.";
                return View(model);
            }

            if (await _authService.IsPhoneTakenAsync(newPhone, 0))
            {
                ViewBag.Error = "So dien thoai nay da duoc su dung.";
                return View(model);
            }

            User? referrer = null;
            if (!string.IsNullOrWhiteSpace(model.ReferralCode))
            {
                referrer = await _referralService.GetVerifiedReferrerAsync(model.ReferralCode);
                if (referrer == null)
                {
                    ViewBag.Error = "Mã giới thiệu không hợp lệ hoặc tài khoản giới thiệu chưa xác thực eKYC.";
                    return View(model);
                }
            }

            var user = new User
            {
                Name = newName,
                Email = newEmail,
                Phone = newPhone,
                PhoneHash = ComputeHash(newPhone),
                PasswordHash = model.Password,
                Role = "Customer",
                IsEmailVerified = false,
                VerificationStatus = "Pending",
                ReferredByUserID = referrer?.UserID
            };

            user.EmailVerificationToken = GenerateOtp();
            user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(5);

            await _authService.RegisterAsync(user);

            Hangfire.BackgroundJob.Enqueue<CarRentalSystem.BackgroundJobs.EmailBackgroundJob>(
                job => job.SendOtpEmail(user.Email, user.EmailVerificationToken));

            TempData["RegisteredEmail"] = user.Email;
            return RedirectToAction("VerifyEmailStep2", new { email = user.Email });
        }

        /*
            ModelState.Remove("Role"); // Role is assigned programmatically
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                var errorMsg = string.Join("; ", errors);
                System.Console.WriteLine($"Registration Failed Validation: {errorMsg}");
                ViewBag.Error = errorMsg;
                return View(user);
            }

            var existingUser = await _authService.GetUserByEmailAsync(user.Email);
            if (existingUser != null)
            {
                ViewBag.Error = "Email already registered";
                return View(user);
            }

            if (string.IsNullOrEmpty(user.CitizenId) || string.IsNullOrEmpty(user.Gender) ||
                user.DateOfBirth == null || string.IsNullOrEmpty(user.Address) ||
                string.IsNullOrEmpty(user.DrivingLicenseNumber) || string.IsNullOrEmpty(user.Phone))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ tất cả các thông tin.";
                return View(user);
            }

            // Tuổi phải >= 18
            var age = (DateTime.Today - user.DateOfBirth.Value.Date).TotalDays / 365.25;
            if (age < 18)
            {
                ViewBag.Error = $"Bạn phải từ 18 tuổi trở lên để đăng ký. Tuổi hiện tại: {age:F1} tuổi.";
                return View(user);
            }

            bool isTaken = await _authService.IsCitizenIdTakenAsync(user.CitizenId, 0);
            if (isTaken)
            {
                ViewBag.Error = "Số CMND/CCCD này đã được sử dụng bởi một tài khoản khác.";
                return View(user);
            }

            // 🔒 Validate GPLX = đúng 12 chữ số + check trùng
            var gplx = user.DrivingLicenseNumber?.Trim() ?? "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(gplx, @"^\d{12}$"))
            {
                ViewBag.Error = "Số Giấy Phép Lái Xe phải đúng 12 chữ số.";
                return View(user);
            }
            bool gplxTaken = await _authService.IsDrivingLicenseTakenAsync(gplx, 0);
            if (gplxTaken)
            {
                ViewBag.Error = "Số Giấy Phép Lái Xe này đã được sử dụng bởi một tài khoản khác.";
                return View(user);
            }

            // 🔒 Validate SĐT + check trùng
            var phone = user.Phone?.Trim() ?? "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^0\d{9,10}$"))
            {
                ViewBag.Error = "Số điện thoại phải bắt đầu bằng 0 và có 10-11 chữ số.";
                return View(user);
            }
            bool phoneTaken = await _authService.IsPhoneTakenAsync(phone, 0);
            if (phoneTaken)
            {
                ViewBag.Error = "Số điện thoại này đã được sử dụng bởi một tài khoản khác.";
                return View(user);
            }

            // Lay plaintext truoc khi encrypt
            var plaintextCitizenId = user.CitizenId?.Trim() ?? "";

            // 🔒 Encrypt PII at rest trước khi lưu
            user.CitizenId = _piiEncryption.EncryptIfNotEmpty(user.CitizenId);
            user.DrivingLicenseNumber = _piiEncryption.EncryptIfNotEmpty(user.DrivingLicenseNumber);

            // 🔐 Compute SHA-256 hash từ PLAINTEXT (khong phai ciphertext)
            user.CitizenIdHash = ComputeHash(plaintextCitizenId);
            user.DrivingLicenseHash = ComputeHash(gplx);
            user.PhoneHash = ComputeHash(phone);
            user.Phone = phone;

            user.Role = "Customer"; // Default role
            user.IsEmailVerified = false;

            // Tạo OTP 6 số và thời hạn 5 phút
            user.EmailVerificationToken = GenerateOtp();
            user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(5);

            await _authService.RegisterAsync(user);

            // Gửi email OTP qua Hangfire Queue (không block user)
            Hangfire.BackgroundJob.Enqueue<CarRentalSystem.BackgroundJobs.EmailBackgroundJob>(
                job => job.SendOtpEmail(user.Email, user.EmailVerificationToken));

            // Lưu email vào TempData để truyền sang trang xác thực OTP
            TempData["RegisteredEmail"] = user.Email;
            return RedirectToAction("VerifyEmailStep2", new { email = user.Email });
        }
        */

        public async Task<IActionResult> RegistrationSuccess()
        {
            var email = TempData["RegisteredEmail"]?.ToString();
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login");

            ViewBag.RegisteredEmail = email;
            return View();
        }

        /// <summary>
        /// Trang 1: Nhập email để kiểm tra và gửi OTP
        /// </summary>
        [HttpGet]
        public IActionResult VerifyEmailStep1()
        {
            return View();
        }

        /// <summary>
        /// Xử lý kiểm tra email - Trang 1
        /// Kiểm tra email có tồn tại và trạng thái xác thực
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckEmail(string email, string? recaptchaToken)
        {
            if (!await ValidateRecaptchaForRedirectAsync(recaptchaToken, "verify_email"))
                return RedirectToAction("VerifyEmailStep1");

            // ✅ TẠM THỜI COMMENT reCAPTCHA vì không hoạt động trên localhost
            // if (string.IsNullOrEmpty(recaptchaToken))
            // {
            //     TempData["ErrorMessage"] = "Vui lòng xác nhận reCAPTCHA trước khi tiếp tục.";
            //     return RedirectToAction("VerifyEmailStep1");
            // }

            // var recaptchaResult = await _recaptchaService.VerifyAsync(recaptchaToken, "verify_email");
            // if (!recaptchaResult.Success)
            // {
            //     TempData["ErrorMessage"] = "Xác thực bảo mật thất bại. Vui lòng thử lại.";
            //     return RedirectToAction("VerifyEmailStep1");
            // }

            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập địa chỉ email.";
                return RedirectToAction("VerifyEmailStep1");
            }

            var user = await _authService.GetUserByEmailAsync(email);

            if (user == null)
            {
                // Email không tồn tại trong hệ thống
                TempData["ErrorMessage"] = "Email này không tồn tại trong hệ thống. Vui lòng kiểm tra lại.";
                return RedirectToAction("VerifyEmailStep1");
            }

            if (user.IsEmailVerified)
            {
                // Email đã được xác thực
                TempData["ErrorMessage"] = "Tài khoản này đã được xác thực trước đó. Vui lòng đăng nhập.";
                return RedirectToAction("VerifyEmailStep1");
            }

            // Email tồn tại và chưa xác thực -> Tạo OTP và gửi email
            var newOtp = GenerateOtp();
            user.EmailVerificationToken = newOtp;
            user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(5);
            await _authService.UpdateUserAsync(user);

            // Gửi email OTP qua Hangfire Queue
            Hangfire.BackgroundJob.Enqueue<CarRentalSystem.BackgroundJobs.EmailBackgroundJob>(
                job => job.SendOtpEmail(user.Email, newOtp));

            // Lưu email vào TempData và chuyển sang trang 2
            TempData["RegisteredEmail"] = email;
            return RedirectToAction("VerifyEmailStep2", new { email = email });
        }

        /// <summary>
        /// Trang 2: Nhập mã OTP để xác thực
        /// </summary>
        [HttpGet]
        public IActionResult VerifyEmailStep2(string? email)
        {
            // Ưu tiên lấy từ TempData, nếu không có thì lấy từ query string
            var emailToUse = TempData["RegisteredEmail"]?.ToString() ?? email;
            
            if (string.IsNullOrEmpty(emailToUse))
            {
                return RedirectToAction("VerifyEmailStep1");
            }
            
            // Lưu lại vào TempData để dùng cho POST
            TempData["RegisteredEmail"] = emailToUse;
            ViewBag.Email = emailToUse;
            return View();
        }

        /// <summary>
        /// Xử lý xác thực OTP - Trang 2
        /// Sử dụng atomic update để tránh race condition (OTP chỉ được sử dụng 1 lần)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmailStep2(string email, string otp)
        {
            ViewBag.Email = email;
            TempData["RegisteredEmail"] = email;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(otp))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ email và mã OTP.";
                return View();
            }

            // Normalize OTP (trim whitespace)
            var normalizedOtp = otp.Trim();

            var user = await _authService.GetUserByEmailAsync(email);
            if (user == null)
            {
                ViewBag.Error = "Email không tồn tại trong hệ thống.";
                return View();
            }

            if (user.IsEmailVerified)
            {
                TempData["SuccessMessage"] = "Tài khoản của bạn đã được xác thực trước đó. Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }

            if (!string.Equals(user.EmailVerificationToken, normalizedOtp, StringComparison.Ordinal))
            {
                ViewBag.Error = "Mã OTP không đúng. Vui lòng kiểm tra lại email.";
                return View();
            }

            if (user.EmailVerificationTokenExpiry == null || user.EmailVerificationTokenExpiry < DateTime.UtcNow)
            {
                ViewBag.Error = "Mã OTP đã hết hạn. Vui lòng nhấn \"Gửi lại mã\" để nhận mã mới.";
                return View();
            }

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;
            await _authService.UpdateUserAsync(user);

            TempData["SuccessMessage"] = "Xác thực email thành công! Bạn có thể đăng nhập ngay bây giờ.";
            return RedirectToAction("Login");
        }

        /// <summary>
        /// Gửi lại mã OTP
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtpStep2(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập email.";
                return RedirectToAction("VerifyEmailStep1");
            }

            TempData["RegisteredEmail"] = email;

            var user = await _authService.GetUserByEmailAsync(email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Email này không tồn tại trong hệ thống.";
                return RedirectToAction("VerifyEmailStep1");
            }

            if (user.IsEmailVerified)
            {
                TempData["ErrorMessage"] = "Tài khoản này đã được xác thực. Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }

            var newOtp = GenerateOtp();
            user.EmailVerificationToken = newOtp;
            user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(5);
            await _authService.UpdateUserAsync(user);

            Hangfire.BackgroundJob.Enqueue<CarRentalSystem.BackgroundJobs.EmailBackgroundJob>(
                job => job.SendOtpEmail(user.Email, newOtp));

            TempData["SuccessMessage"] = "Mã OTP mới đã được gửi đến email của bạn. Mã có hiệu lực trong 5 phút.";
            return RedirectToAction("VerifyEmailStep2");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            if (!ModelState.IsValid)
                return View(model);

            var email = model.Email.Trim();
            var user = await _authService.GetUserByEmailAsync(email);
            if (user == null)
            {
                ViewBag.Error = "Email này không tồn tại trong hệ thống. Vui lòng kiểm tra lại.";
                return View(model);
            }

            var otp = GenerateOtp();
            user.PasswordResetToken = otp;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(5);
            await _authService.UpdateUserAsync(user);

            Hangfire.BackgroundJob.Enqueue<CarRentalSystem.BackgroundJobs.EmailBackgroundJob>(
                job => job.SendPasswordResetOtpEmail(user.Email, otp));

            TempData["PasswordResetEmail"] = email;
            TempData["SuccessMessage"] = "Mã OTP đặt lại mật khẩu đã được gửi đến email của bạn. Mã có hiệu lực trong 5 phút.";
            return RedirectToAction("ResetPassword", new { email });
        }

        [HttpGet]
        public IActionResult ResetPassword(string? email)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            var emailToUse = TempData["PasswordResetEmail"]?.ToString() ?? email;
            if (string.IsNullOrWhiteSpace(emailToUse))
                return RedirectToAction("ForgotPassword");

            TempData["PasswordResetEmail"] = emailToUse;
            return View(new ResetPasswordViewModel { Email = emailToUse });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            TempData["PasswordResetEmail"] = model.Email;

            if (!ModelState.IsValid)
                return View(model);

            var email = model.Email.Trim();
            var otp = model.Otp.Trim();
            var user = await _authService.GetUserByEmailAsync(email);
            if (user == null)
            {
                ViewBag.Error = "Email này không tồn tại trong hệ thống. Vui lòng kiểm tra lại.";
                return View(model);
            }

            if (!string.Equals(user.PasswordResetToken, otp, StringComparison.Ordinal))
            {
                ViewBag.Error = "Mã OTP không đúng. Vui lòng kiểm tra lại email.";
                return View(model);
            }

            if (user.PasswordResetTokenExpiry == null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
            {
                ViewBag.Error = "Mã OTP đã hết hạn. Vui lòng yêu cầu gửi mã mới.";
                return View(model);
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            await _authService.UpdateUserAsync(user);

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới.";
            return RedirectToAction("Login");
        }

        /// <summary>
        /// Trang xác thực OTP - hiển thị sau khi đăng ký hoặc khi user nhấn "xác thực email"
        /// </summary>
        [HttpGet]
        public IActionResult VerifyEmail()
        {
            // Lấy email từ TempData (đăng ký thành công)
            var email = TempData["RegisteredEmail"]?.ToString();
            ViewBag.Email = email;
            return View();
        }

        /// <summary>
        /// Xử lý xác thực OTP do người dùng nhập vào.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(string email, string otp)
        {
            // Giữ email để hiển thị lại form nếu có lỗi
            ViewBag.Email = email;
            TempData["RegisteredEmail"] = email;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(otp))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ email và mã OTP.";
                return View();
            }

            var user = await _authService.GetUserByEmailAsync(email);
            if (user == null)
            {
                ViewBag.Error = "Email không tồn tại trong hệ thống.";
                return View();
            }

            if (user.IsEmailVerified)
            {
                TempData["SuccessMessage"] = "Tài khoản của bạn đã được xác thực trước đó. Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }

            // Kiểm tra OTP có khớp không
            if (user.EmailVerificationToken != otp)
            {
                ViewBag.Error = "Mã OTP không đúng. Vui lòng kiểm tra lại email.";
                return View();
            }

            // Kiểm tra OTP đã hết hạn chưa
            if (user.EmailVerificationTokenExpiry == null || user.EmailVerificationTokenExpiry < DateTime.UtcNow)
            {
                ViewBag.Error = "Mã OTP đã hết hạn. Vui lòng nhấn \"Gửi lại mã\" để nhận mã mới.";
                return View();
            }

            // Xác thực thành công
            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;
            await _authService.UpdateUserAsync(user);

            TempData["SuccessMessage"] = "Xác thực email thành công! Bạn có thể đăng nhập ngay bây giờ.";
            return RedirectToAction("Login");
        }

        /// <summary>
        /// Gửi lại mã OTP xác thực.
        /// Chỉ gửi cho email có trong hệ thống và CHƯA xác thực.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập email.";
                return RedirectToAction("VerifyEmail");
            }

            // Giữ email để hiển thị lại trên form
            TempData["RegisteredEmail"] = email;

            var user = await _authService.GetUserByEmailAsync(email);
            if (user == null)
            {
                // Email không tồn tại trong hệ thống
                TempData["ErrorMessage"] = "Email này không tồn tại trong hệ thống. Vui lòng kiểm tra lại.";
                return RedirectToAction("VerifyEmail");
            }

            if (user.IsEmailVerified)
            {
                // Tài khoản đã xác thực rồi
                TempData["InfoMessage"] = "Tài khoản này đã được xác thực trước đó. Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }

            // Tạo OTP 6 số mới
            var newOtp = GenerateOtp();
            user.EmailVerificationToken = newOtp;
            user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(5);
            await _authService.UpdateUserAsync(user);

            // Gửi email OTP qua Hangfire Queue
            Hangfire.BackgroundJob.Enqueue<CarRentalSystem.BackgroundJobs.EmailBackgroundJob>(
                job => job.SendOtpEmail(user.Email, newOtp));

            TempData["SuccessMessage"] = "Mã OTP mới đã được gửi đến email của bạn. Mã có hiệu lực trong 5 phút.";
            return RedirectToAction("VerifyEmail");
        }

        private static string GenerateOtp()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var number = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
            return number.ToString("D6");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("Cookies");
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId))
            {
                var user = await _authService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    if (user.IsVerified && string.IsNullOrWhiteSpace(user.ReferralCode))
                    {
                        await _referralService.EnsureReferralCodeAsync(user.UserID);
                        user = await _authService.GetUserByIdAsync(userId);
                    }

                    // 🔓 Decrypt PII — chỉ hiện đầy đủ cho Admin
                    var decryptedCitizenId = _piiEncryption.DecryptIfNotEmpty(user.CitizenId);
                    var decryptedGplx = _piiEncryption.DecryptIfNotEmpty(user.DrivingLicenseNumber);

                    if (User.IsInRole("Admin"))
                    {
                        // Admin: xem đầy đủ
                        user.CitizenId = decryptedCitizenId;
                        user.DrivingLicenseNumber = decryptedGplx;
                    }
                    else
                    {
                        // User thường: che hoàn toàn nếu có dữ liệu, trống nếu không có
                        user.CitizenId = string.IsNullOrEmpty(decryptedCitizenId) ? null : "●●●●●●●●";
                        user.DrivingLicenseNumber = string.IsNullOrEmpty(decryptedGplx) ? null : "●●●●●●●●";
                    }
                    return View(user);
                }
            }
            return RedirectToAction("Login");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(User updatedUser, IFormFile? uploadIdFront, IFormFile? uploadIdBack, IFormFile? uploadLicense)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId) && userId == updatedUser.UserID)
            {
                var user = await _authService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    // =========================================
                    // PII UPDATE: Chi update neu co gia tri moi THUC SU
                    // Khong re-encrypt masked value (●●●●●●●●)
                    // =========================================
                    const string MaskedPlaceholder = "●●●●●●●●";
                    bool profileChanged = false;

                    // Lay plaintext CCCD neu user nhap gia tri moi (khong phai mask)
                    var newCitizenId = updatedUser.CitizenId?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(newCitizenId) && newCitizenId != MaskedPlaceholder)
                    {
                        bool isTaken = await _authService.IsCitizenIdTakenAsync(newCitizenId, userId);
                        if (isTaken)
                        {
                            TempData["ErrorMessage"] = "Số CMND/CCCD này đã được sử dụng bởi một tài khoản khác.";
                            return RedirectToAction("Profile");
                        }
                        user.CitizenId = _piiEncryption.Encrypt(newCitizenId);
                        user.CitizenIdHash = ComputeHash(newCitizenId);
                        profileChanged = true;
                    }

                    // Lấy plaintext GPLX nếu user nhập giá trị mới
                    var newGplx = updatedUser.DrivingLicenseNumber?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(newGplx) && newGplx != MaskedPlaceholder)
                    {
                        // Validate GPLX format
                        if (!System.Text.RegularExpressions.Regex.IsMatch(newGplx, @"^\d{12}$"))
                        {
                            TempData["ErrorMessage"] = "Số Giấy Phép Lái Xe phải đúng 12 chữ số.";
                            return RedirectToAction("Profile");
                        }
                        bool gplxTaken = await _authService.IsDrivingLicenseTakenAsync(newGplx, userId);
                        if (gplxTaken)
                        {
                            TempData["ErrorMessage"] = "Số Giấy Phép Lái Xe này đã được sử dụng bởi một tài khoản khác.";
                            return RedirectToAction("Profile");
                        }
                        user.DrivingLicenseNumber = _piiEncryption.Encrypt(newGplx);
                        user.DrivingLicenseHash = ComputeHash(newGplx);
                        profileChanged = true;
                    }

                    // 🔒 Validate SĐT format + check trùng
                    var newPhone = updatedUser.Phone?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(newPhone) &&
                        !System.Text.RegularExpressions.Regex.IsMatch(newPhone, @"^0\d{9,10}$"))
                    {
                        TempData["ErrorMessage"] = "Số điện thoại phải bắt đầu bằng 0 và có 10-11 chữ số.";
                        return RedirectToAction("Profile");
                    }
                    bool phoneTaken = await _authService.IsPhoneTakenAsync(newPhone, userId);
                    if (phoneTaken)
                    {
                        TempData["ErrorMessage"] = "Số điện thoại này đã được sử dụng bởi một tài khoản khác.";
                        return RedirectToAction("Profile");
                    }

                    user.Name = updatedUser.Name;
                    user.Phone = newPhone;
                    user.PhoneHash = ComputeHash(newPhone);

                    // Update non-PII fields
                    user.DateOfBirth = updatedUser.DateOfBirth;
                    user.Gender = updatedUser.Gender;
                    user.Address = updatedUser.Address;

                    // Track profile changes for IsVerified reset
                    bool identityDocChanged = false;
                    bool filesUploaded = false;
                    var uploadPath = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", "eKyc", userId.ToString());
                    Directory.CreateDirectory(uploadPath);
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                    const long maxFileSize = 5 * 1024 * 1024; // 🔒 5MB max per file

                    // Helper: validate file before upload
                    string? ValidateFile(IFormFile? file, string label)
                    {
                        if (file == null || file.Length == 0) return null;
                        if (file.Length > maxFileSize) return $"{label} vượt quá giới hạn 5MB.";
                        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                            return $"Định dạng file {label} không hợp lệ. Chỉ chấp nhận JPG, PNG, PDF.";
                        return null;
                    }

                    // Validate all files first before saving any
                    var frontErr = ValidateFile(uploadIdFront, "CCCD mặt trước");
                    var backErr = ValidateFile(uploadIdBack, "CCCD mặt sau");
                    var licErr = ValidateFile(uploadLicense, "Bằng lái xe");
                    var firstErr = frontErr ?? backErr ?? licErr;
                    if (firstErr != null)
                    {
                        TempData["ErrorMessage"] = firstErr;
                        return RedirectToAction("Profile");
                    }

                    if (uploadIdFront != null && uploadIdFront.Length > 0)
                    {
                        var ext = Path.GetExtension(uploadIdFront.FileName).ToLowerInvariant();
                        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                        {
                            TempData["ErrorMessage"] = "Định dạng file CCCD mặt trước không hợp lệ. Chỉ chấp nhận JPG, PNG, PDF.";
                            return RedirectToAction("Profile");
                        }
                        string fileName = Guid.NewGuid().ToString() + ext;
                        string filePath = Path.Combine(uploadPath, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create)) { await uploadIdFront.CopyToAsync(stream); }
                        user.IdCardFrontUrl = $"/SecureFile/GetSecureImage?folder=eKyc&fileName={userId}/id_front{Path.GetExtension(uploadIdFront.FileName)}";
                        identityDocChanged = true;
                    }

                    if (uploadIdBack != null && uploadIdBack.Length > 0)
                    {
                        var ext = Path.GetExtension(uploadIdBack.FileName).ToLowerInvariant();
                        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                        {
                            TempData["ErrorMessage"] = "Định dạng file CCCD mặt sau không hợp lệ. Chỉ chấp nhận JPG, PNG, PDF.";
                            return RedirectToAction("Profile");
                        }
                        string fileName = Guid.NewGuid().ToString() + ext;
                        string filePath = Path.Combine(uploadPath, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create)) { await uploadIdBack.CopyToAsync(stream); }
                        user.IdCardBackUrl = $"/SecureFile/GetSecureImage?folder=eKyc&fileName={userId}/id_back{Path.GetExtension(uploadIdBack.FileName)}";
                        identityDocChanged = true;
                    }

                    if (uploadLicense != null && uploadLicense.Length > 0)
                    {
                        var ext = Path.GetExtension(uploadLicense.FileName).ToLowerInvariant();
                        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                        {
                            TempData["ErrorMessage"] = "Định dạng file Bằng lái xe không hợp lệ. Chỉ chấp nhận JPG, PNG, PDF.";
                            return RedirectToAction("Profile");
                        }
                        string fileName = Guid.NewGuid().ToString() + ext;
                        string filePath = Path.Combine(uploadPath, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create)) { await uploadLicense.CopyToAsync(stream); }
                        user.DrivingLicenseUrl = $"/SecureFile/GetSecureImage?folder=eKyc&fileName={userId}/license_front{Path.GetExtension(uploadLicense.FileName)}";
                        identityDocChanged = true;
                    }

                    // =========================================
                    // IsVerified Reset: Chỉ reset khi có thay đổi THỰC SỰ
                    // =========================================
                    if (identityDocChanged || profileChanged)
                    {
                        user.IsVerified = false; // Bat buoc admin phai duyet lai eKYC
                    }
                    
                    await _authService.UpdateUserAsync(user);
                    TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";
                    return RedirectToAction("Profile");
                }
            }
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        /// <summary>
        /// Che bớt thông tin PII — chỉ hiện 4 ký tự cuối.
        /// Ví dụ: "079123456789" → "********6789"
        /// </summary>
        private static string? MaskPii(string? value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.Length <= 4) return new string('*', value.Length);
            return new string('*', value.Length - 4) + value[^4..];
        }

        private static string ComputeHash(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        // ═══════════════════════════════════════════════
        // 🔐 PII PIN — Mã PIN 6 số để xem CCCD/GPLX
        // ═══════════════════════════════════════════════

        /// <summary>
        /// Kiểm tra user đã đặt mã PIN chưa.
        /// </summary>
        [Authorize]
        [HttpGet]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> HasPiiPin()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _authService.GetUserByIdAsync(userId);
            return Json(new { hasPin = !string.IsNullOrEmpty(user?.PiiPinHash) });
        }

        /// <summary>
        /// Đặt/Đổi mã PIN 6 số.
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> SetPiiPin([FromBody] SetPinRequest request)
        {
            if (string.IsNullOrEmpty(request?.Pin) || !System.Text.RegularExpressions.Regex.IsMatch(request.Pin, @"^\d{6}$"))
            {
                return Json(new { success = false, message = "Mã PIN phải đúng 6 chữ số." });
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null) return Json(new { success = false, message = "Không tìm thấy tài khoản." });

            // Nếu đã có PIN cũ, yêu cầu xác nhận PIN cũ trước
            if (!string.IsNullOrEmpty(user.PiiPinHash))
            {
                if (string.IsNullOrEmpty(request.OldPin) || !BCrypt.Net.BCrypt.Verify(request.OldPin, user.PiiPinHash))
                {
                    return Json(new { success = false, message = "Mã PIN cũ không đúng." });
                }
            }

            user.PiiPinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin);
            await _authService.UpdateUserAsync(user);
            return Json(new { success = true, message = "Đã đặt mã PIN thành công!" });
        }

        /// <summary>
        /// Xác thực mã PIN → trả về CCCD/GPLX đã giải mã.
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> VerifyPiiPin([FromBody] VerifyPinRequest request)
        {
            if (string.IsNullOrEmpty(request?.Pin))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã PIN." });
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null) return Json(new { success = false, message = "Không tìm thấy tài khoản." });

            if (string.IsNullOrEmpty(user.PiiPinHash))
            {
                return Json(new { success = false, message = "Bạn chưa đặt mã PIN. Vui lòng đặt mã PIN trước." });
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Pin, user.PiiPinHash))
            {
                return Json(new { success = false, message = "Mã PIN không đúng." });
            }

            // ✅ PIN đúng → trả về PII đã giải mã
            return Json(new
            {
                success = true,
                citizenId = _piiEncryption.DecryptIfNotEmpty(user.CitizenId) ?? "",
                drivingLicenseNumber = _piiEncryption.DecryptIfNotEmpty(user.DrivingLicenseNumber) ?? ""
            });
        }

        /// <summary>
        /// BACKFILL: Chạy một lần để compute SHA-256 hashes cho dữ liệu cũ.
        /// Tài khoản cũ có CitizenIdHash = null sẽ được backfill.
        /// Chi Admin mới được gọi.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> BackfillPiiHashes()
        {
            var users = await _context.Users
                .Where(u => u.CitizenIdHash == null)
                .ToListAsync();

            int count = 0;
            foreach (var user in users)
            {
                bool changed = false;

                // CCCD
                if (!string.IsNullOrEmpty(user.CitizenId))
                {
                    var val = user.CitizenId.Trim();
                    string? plaintextCccd = null;
                    try { plaintextCccd = _piiEncryption.DecryptIfNotEmpty(val); } catch { }
                    // Nếu decrypt trả về khác giá trị gốc → đã encrypt. Nếu giống → có thể là plaintext
                    if (!string.IsNullOrEmpty(plaintextCccd) && plaintextCccd != val)
                    {
                        user.CitizenIdHash = ComputeHash(plaintextCccd.Trim());
                        changed = true;
                    }
                    else if (!string.IsNullOrEmpty(plaintextCccd) && plaintextCccd.Length >= 9 && plaintextCccd.Length <= 15)
                    {
                        // Giá trị 9-15 ký tự → có thể là CCCD plaintext cũ
                        user.CitizenIdHash = ComputeHash(plaintextCccd.Trim());
                        changed = true;
                    }
                }

                // GPLX
                if (!string.IsNullOrEmpty(user.DrivingLicenseNumber))
                {
                    var val = user.DrivingLicenseNumber.Trim();
                    string? plaintextLic = null;
                    try { plaintextLic = _piiEncryption.DecryptIfNotEmpty(val); } catch { }
                    if (!string.IsNullOrEmpty(plaintextLic) && plaintextLic != val)
                    {
                        user.DrivingLicenseHash = ComputeHash(plaintextLic.Trim());
                        changed = true;
                    }
                    else if (!string.IsNullOrEmpty(plaintextLic) && plaintextLic.Length == 12 && plaintextLic.All(char.IsDigit))
                    {
                        user.DrivingLicenseHash = ComputeHash(plaintextLic.Trim());
                        changed = true;
                    }
                }

                // Phone
                if (!string.IsNullOrEmpty(user.Phone))
                {
                    user.PhoneHash = ComputeHash(user.Phone.Trim());
                    changed = true;
                }

                if (changed) count++;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Da backfill {count}/{users.Count} tai khoan." });
        }
    }

    // DTO cho API
    public class SetPinRequest
    {
        public string? Pin { get; set; }
        public string? OldPin { get; set; }
    }

    public class VerifyPinRequest
    {
        public string? Pin { get; set; }
    }
}
