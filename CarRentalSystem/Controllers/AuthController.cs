using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace CarRentalSystem.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;
        private readonly IPiiEncryptionService _piiEncryption;

        public AuthController(IAuthService authService, IWebHostEnvironment env, IEmailService emailService, IPiiEncryptionService piiEncryption)
        {
            _authService = authService;
            _env = env;
            _emailService = emailService;
            _piiEncryption = piiEncryption;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
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

            if (user.Role == UserRoleConstants.Admin)
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(User user)
        {
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

            bool isTaken = await _authService.IsCitizenIdTakenAsync(user.CitizenId, 0);
            if (isTaken)
            {
                ViewBag.Error = "Số CMND/CCCD này đã được sử dụng bởi một tài khoản khác.";
                return View(user);
            }

            user.Role = "Customer"; // Default role
            user.IsEmailVerified = false;
            user.EmailVerificationToken = System.Guid.NewGuid().ToString();

            await _authService.RegisterAsync(user);

            // Gửi email xác thực qua Hangfire Queue (không block user)
            var verifyUrl = Url.Action("VerifyEmail", "Auth", new { userId = user.UserID, token = user.EmailVerificationToken }, Request.Scheme);
            Hangfire.BackgroundJob.Enqueue<CarRentalSystem.BackgroundJobs.EmailBackgroundJob>(
                job => job.SendVerificationEmail(user.Email, verifyUrl));

            // Chuyển hướng đến trang báo thành công
            TempData["RegisteredEmail"] = user.Email;
            return RedirectToAction("RegistrationSuccess");
        }

        [HttpGet]
        public IActionResult RegistrationSuccess()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> VerifyEmail(int userId, string token)
        {
            if (userId <= 0 || string.IsNullOrEmpty(token))
            {
                ViewBag.Error = "Link xác thực không hợp lệ.";
                return View("VerifyEmailError");
            }

            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
            {
                ViewBag.Error = "Tài khoản không tồn tại.";
                return View("VerifyEmailError");
            }

            if (user.IsEmailVerified)
            {
                TempData["SuccessMessage"] = "Tài khoản của bạn đã được xác thực trước đó. Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }

            if (user.EmailVerificationToken == token)
            {
                user.IsEmailVerified = true;
                user.EmailVerificationToken = null; // Xoá token sau khi dùng
                await _authService.UpdateUserAsync(user);

                TempData["SuccessMessage"] = "Xác thực email thành công! Bạn có thể đăng nhập ngay bây giờ.";
                return RedirectToAction("Login");
            }

            ViewBag.Error = "Token xác thực không hợp lệ hoặc đã hết hạn.";
            return View("VerifyEmailError");
        }

        [HttpPost]
        public async Task<IActionResult> ResendVerificationEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập email.";
                return RedirectToAction("Login");
            }

            var user = await _authService.GetUserByEmailAsync(email);
            if (user != null && !user.IsEmailVerified)
            {
                user.EmailVerificationToken = System.Guid.NewGuid().ToString();
                await _authService.UpdateUserAsync(user);

                var verifyUrl = Url.Action("VerifyEmail", "Auth", new { userId = user.UserID, token = user.EmailVerificationToken }, Request.Scheme);
                Hangfire.BackgroundJob.Enqueue<CarRentalSystem.BackgroundJobs.EmailBackgroundJob>(
                    job => job.SendVerificationEmail(user.Email, verifyUrl));
            }

            // Luôn báo thành công để tránh lộ thông tin email có tồn tại hay không
            TempData["SuccessMessage"] = "Nếu email tồn tại trong hệ thống và chưa xác thực, một link xác thực mới đã được gửi đi.";
            return RedirectToAction("Login");
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
                    // 🔓 Decrypt PII cho hiển thị
                    user.CitizenId = _piiEncryption.DecryptIfNotEmpty(user.CitizenId);
                    user.DrivingLicenseNumber = _piiEncryption.DecryptIfNotEmpty(user.DrivingLicenseNumber);
                    return View(user);
                }
            }
            return RedirectToAction("Login");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Profile(User updatedUser, IFormFile? uploadIdFront, IFormFile? uploadIdBack, IFormFile? uploadLicense)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId) && userId == updatedUser.UserID)
            {
                var user = await _authService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    if (!string.IsNullOrEmpty(updatedUser.CitizenId))
                    {
                        bool isTaken = await _authService.IsCitizenIdTakenAsync(updatedUser.CitizenId, userId);
                        if (isTaken)
                        {
                            TempData["ErrorMessage"] = "Số CMND/CCCD này đã được sử dụng bởi một tài khoản khác.";
                            return RedirectToAction("Profile");
                        }
                    }

                    user.Name = updatedUser.Name;
                    user.Phone = updatedUser.Phone;
                    // 🔒 Encrypt PII at rest
                    user.CitizenId = _piiEncryption.EncryptIfNotEmpty(updatedUser.CitizenId);
                    user.DrivingLicenseNumber = _piiEncryption.EncryptIfNotEmpty(updatedUser.DrivingLicenseNumber);
                    user.DateOfBirth = updatedUser.DateOfBirth;
                    user.Gender = updatedUser.Gender;
                    user.Address = updatedUser.Address;

                    // Handle eKYC Uploads
                    string uploadPath = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", "eKyc");
                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                    bool filesUploaded = false;
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
                        user.IdCardFrontUrl = $"/SecureFile/GetSecureImage?folder=eKyc&fileName={fileName}";
                        filesUploaded = true;
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
                        user.IdCardBackUrl = $"/SecureFile/GetSecureImage?folder=eKyc&fileName={fileName}";
                        filesUploaded = true;
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
                        user.DrivingLicenseUrl = $"/SecureFile/GetSecureImage?folder=eKyc&fileName={fileName}";
                        filesUploaded = true;
                    }

                    // Nếu user tải lên tài liệu mới hoặc thay đổi thông tin định danh, lập tức HỦY trạng thái đã duyệt
                    if (filesUploaded || user.CitizenId != updatedUser.CitizenId || user.Name != updatedUser.Name || user.DrivingLicenseNumber != updatedUser.DrivingLicenseNumber)
                    {
                        user.IsVerified = false; // Bắt buộc admin phải duyệt lại
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
    }
}
