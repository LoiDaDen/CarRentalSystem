using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using CarRentalSystem.Data;
using CarRentalSystem.Controllers;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using CarRentalSystem.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    /// <summary>
    /// Unit tests cho AuthController — Login, Register, VerifyEmail
    /// </summary>
    public class AuthControllerTests
    {
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly Mock<IWebHostEnvironment> _mockEnv;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IPiiEncryptionService> _mockPiiEncryption;
        private readonly Mock<IRecaptchaService> _mockRecaptchaService;
        private readonly Mock<IReferralService> _mockReferralService;
        private readonly Mock<IAuditTrailService> _mockAuditTrail;
        private readonly Mock<ILegalComplianceService> _mockLegalCompliance;
        private readonly Mock<IFileUploadValidationService> _mockFileUploadValidation;
        private readonly Mock<ILogger<AuthController>> _mockLogger;
        private readonly Mock<IAuthenticationService> _mockAuthenticationService;
        private readonly ApplicationDbContext _context;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _mockAuthService = new Mock<IAuthService>();
            _mockEnv = new Mock<IWebHostEnvironment>();
            _mockEmailService = new Mock<IEmailService>();
            _mockPiiEncryption = new Mock<IPiiEncryptionService>();
            _mockRecaptchaService = new Mock<IRecaptchaService>();
            _mockReferralService = new Mock<IReferralService>();
            _mockAuditTrail = new Mock<IAuditTrailService>();
            _mockLegalCompliance = new Mock<ILegalComplianceService>();
            _mockFileUploadValidation = new Mock<IFileUploadValidationService>();
            _mockLogger = new Mock<ILogger<AuthController>>();
            _mockEmailService
                .Setup(s => s.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockAuthenticationService = new Mock<IAuthenticationService>();
            _mockAuthenticationService
                .Setup(s => s.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);
            _mockAuditTrail
                .Setup(s => s.RecordAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AuditLog log, CancellationToken _) => log);
            _context = MockRepositoryHelper.CreateSeededDbContext();
            _mockLegalCompliance
                .Setup(s => s.RecordConsentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<HttpContext>(), It.IsAny<string?>()))
                .ReturnsAsync((int userId, string documentCode, string purposeCode, bool isGranted, string source, HttpContext context, string? evidence) =>
                    new ConsentRecord { UserID = userId, PurposeCode = purposeCode, IsGranted = isGranted, Source = source });

            _controller = CreateController(requireRecaptcha: false);
        }

        private AuthController CreateController(
            bool requireRecaptcha,
            bool requirePrivilegedMfa = false,
            string environmentName = "Development",
            string[]? mfaExemptEmails = null,
            string[]? adminMfaExemptEmails = null)
        {
            var settings = new Dictionary<string, string?>
            {
                ["Security:RequireRecaptcha"] = requireRecaptcha.ToString(),
                ["Security:RequirePrivilegedMfa"] = requirePrivilegedMfa.ToString()
            };
            if (mfaExemptEmails != null)
            {
                for (var i = 0; i < mfaExemptEmails.Length; i++)
                    settings[$"Security:MfaExemptEmails:{i}"] = mfaExemptEmails[i];
            }
            if (adminMfaExemptEmails != null)
            {
                for (var i = 0; i < adminMfaExemptEmails.Length; i++)
                    settings[$"Security:AdminMfaExemptEmails:{i}"] = adminMfaExemptEmails[i];
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
            _mockEnv.Setup(e => e.EnvironmentName).Returns(environmentName);

            var controller = new AuthController(
                _mockAuthService.Object,
                _mockEnv.Object,
                _mockEmailService.Object,
                _mockPiiEncryption.Object,
                _context,
                _mockRecaptchaService.Object,
                _mockReferralService.Object,
                configuration,
                _mockAuditTrail.Object,
                _mockLegalCompliance.Object,
                _mockFileUploadValidation.Object,
                new IdentityHashService(configuration),
                new PasswordHashService(),
                Mock.Of<IPiiAccessService>(),
                _mockLogger.Object);
            var httpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection()
                    .AddSingleton<IConfiguration>(configuration)
                    .AddSingleton(_mockAuthenticationService.Object)
                    .BuildServiceProvider()
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("http://localhost/verify");
            mockUrlHelper.Setup(u => u.IsLocalUrl(It.IsAny<string>()))
                .Returns<string>(url => !string.IsNullOrWhiteSpace(url) && url.StartsWith("/") && !url.StartsWith("//"));
            controller.Url = mockUrlHelper.Object;

            return controller;
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Login_Post_ReturnsViewWithError_WhenRecaptchaFails()
        {
            // Arrange
            var controller = CreateController(requireRecaptcha: true);
            _mockRecaptchaService.Setup(s => s.VerifyAsync("bad-token", "login"))
                                 .ReturnsAsync(new RecaptchaVerifyResult { Success = false });

            // Act
            var result = await controller.Login("test@user.com", "pass", "bad-token") as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.ViewData["Error"].Should().Be("Đăng nhập không thành công do xác thực bảo mật thất bại.");
            _mockAuthService.Verify(s => s.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Login_Post_WithRememberMe_CreatesPersistentCookie()
        {
            AuthenticationProperties? capturedProperties = null;
            _mockAuthenticationService
                .Setup(s => s.SignInAsync(It.IsAny<HttpContext>(), "Cookies", It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
                .Callback<HttpContext, string, ClaimsPrincipal, AuthenticationProperties>((_, _, _, properties) => capturedProperties = properties)
                .Returns(Task.CompletedTask);

            var user = new User
            {
                UserID = 10,
                Email = "customer@test.com",
                Name = "Customer",
                PasswordHash = "x",
                Role = UserRoleConstants.Customer,
                IsEmailVerified = true
            };
            _mockAuthService.Setup(s => s.AuthenticateAsync(user.Email, "pass")).ReturnsAsync(user);

            var result = await _controller.Login(user.Email, "pass", null, rememberMe: true) as RedirectToActionResult;

            result.Should().NotBeNull();
            capturedProperties.Should().NotBeNull();
            capturedProperties!.IsPersistent.Should().BeTrue();
            capturedProperties.ExpiresUtc.Should().NotBeNull();
            capturedProperties.ExpiresUtc!.Value.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(29));
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Login_Post_WithoutRememberMe_CreatesSessionCookie()
        {
            AuthenticationProperties? capturedProperties = null;
            _mockAuthenticationService
                .Setup(s => s.SignInAsync(It.IsAny<HttpContext>(), "Cookies", It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
                .Callback<HttpContext, string, ClaimsPrincipal, AuthenticationProperties>((_, _, _, properties) => capturedProperties = properties)
                .Returns(Task.CompletedTask);

            var user = new User
            {
                UserID = 11,
                Email = "customer2@test.com",
                Name = "Customer 2",
                PasswordHash = "x",
                Role = UserRoleConstants.Customer,
                IsEmailVerified = true
            };
            _mockAuthService.Setup(s => s.AuthenticateAsync(user.Email, "pass")).ReturnsAsync(user);

            var result = await _controller.Login(user.Email, "pass", null) as RedirectToActionResult;

            result.Should().NotBeNull();
            capturedProperties.Should().NotBeNull();
            capturedProperties!.IsPersistent.Should().BeFalse();
            capturedProperties.ExpiresUtc.Should().BeNull();
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Login_Post_ManagerInMfaExemptEmails_SkipsMfaAndAudits()
        {
            var controller = CreateController(
                requireRecaptcha: false,
                requirePrivilegedMfa: true,
                environmentName: "Production",
                mfaExemptEmails: new[] { "manager@test.com" });
            var user = new User
            {
                UserID = 12,
                Email = "manager@test.com",
                Name = "Manager",
                PasswordHash = "x",
                Role = UserRoleConstants.Manager,
                IsEmailVerified = true
            };
            _mockAuthService.Setup(s => s.AuthenticateAsync(user.Email, "pass")).ReturnsAsync(user);
            _mockRecaptchaService
                .Setup(s => s.VerifyAsync(It.IsAny<string>(), "login"))
                .ReturnsAsync(new RecaptchaVerifyResult { Success = true });

            var result = await controller.Login(user.Email, "pass", null, rememberMe: true) as RedirectResult;

            result.Should().NotBeNull();
            result!.Url.Should().Be("/Manager/Dashboard");
            _mockAuditTrail.Verify(s => s.RecordAsync(
                It.Is<AuditLog>(log => log.Action == "PrivilegedMfaSkipped" && log.UserId == user.UserID),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Login_Post_AdminInAdminMfaExemptEmails_SkipsMfaAndAudits()
        {
            var controller = CreateController(
                requireRecaptcha: false,
                requirePrivilegedMfa: true,
                environmentName: "Production",
                adminMfaExemptEmails: new[] { "hloi78274@gmail.com" });
            var user = new User
            {
                UserID = 13,
                Email = "hloi78274@gmail.com",
                Name = "Admin",
                PasswordHash = "x",
                Role = UserRoleConstants.Admin,
                IsEmailVerified = true
            };
            _mockAuthService.Setup(s => s.AuthenticateAsync(user.Email, "pass")).ReturnsAsync(user);
            _mockRecaptchaService
                .Setup(s => s.VerifyAsync(It.IsAny<string>(), "login"))
                .ReturnsAsync(new RecaptchaVerifyResult { Success = true });

            var result = await controller.Login(user.Email, "pass", null, rememberMe: true) as RedirectResult;

            result.Should().NotBeNull();
            result!.Url.Should().Be("/Admin/Dashboard");
            _mockAuditTrail.Verify(s => s.RecordAsync(
                It.Is<AuditLog>(log => log.Action == "PrivilegedMfaSkipped" && log.UserId == user.UserID),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(UserRoleConstants.Manager, "Manager")]
        [InlineData(UserRoleConstants.Staff, "Staff")]
        [Trait("Module", "Auth")]
        [Trait("Type", "Regression")]
        public async Task Login_Post_InternalPortalRole_RedirectsToItsOwnDashboard(string role, string expectedArea)
        {
            var user = new User
            {
                UserID = 20,
                Email = $"{role.ToLowerInvariant()}@test.com",
                Name = role,
                PasswordHash = "x",
                Role = role,
                IsEmailVerified = true
            };
            _mockAuthService.Setup(s => s.AuthenticateAsync(user.Email, "pass")).ReturnsAsync(user);

            var result = await _controller.Login(user.Email, "pass", null) as RedirectResult;

            result.Should().NotBeNull();
            result!.Url.Should().Be($"/{expectedArea}/Dashboard");
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Security")]
        public async Task Login_Post_ManagerWithAdminReturnUrl_RedirectsToManagerDashboard()
        {
            var user = new User
            {
                UserID = 21,
                Email = "manager-return-url@test.com",
                Name = "Manager",
                PasswordHash = "x",
                Role = UserRoleConstants.Manager,
                IsEmailVerified = true
            };
            _mockAuthService.Setup(s => s.AuthenticateAsync(user.Email, "pass")).ReturnsAsync(user);

            var result = await _controller.Login(user.Email, "pass", null, returnUrl: "/Admin/Dashboard") as RedirectResult;

            result.Should().NotBeNull();
            result!.Url.Should().Be("/Manager/Dashboard");
        }

        // =============================================
        // LOGIN TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public void Login_Get_ReturnsView_WhenNotAuthenticated()
        {
            // Arrange
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = _controller.Login();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public void Login_Get_PreservesLocalReturnUrl_ForExternalPaymentReturn()
        {
            // Arrange
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = _controller.Login("/Payment/Hold?orderCode=123") as ViewResult;

            // Assert
            result.Should().NotBeNull();
            _controller.ViewData["ReturnUrl"].Should().Be("/Payment/Hold?orderCode=123");
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Login_Post_ReturnsViewWithError_WhenAuthFails()
        {
            // Arrange
            _mockAuthService.Setup(s => s.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
                            .ReturnsAsync((User?)null);

            // Act
            var result = await _controller.Login("test@user.com", "wrongpass", null) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.ViewData["Error"].Should().Be("Email hoặc mật khẩu không chính xác.");
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Login_Post_ReturnsViewWithError_WhenEmailNotVerified()
        {
            // Arrange
            var user = new User { Email = "test@test.com", Name = "Test", PasswordHash = "x", IsEmailVerified = false };
            _mockAuthService.Setup(s => s.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
                            .ReturnsAsync(user);

            // Act
            var result = await _controller.Login("test@test.com", "pass", null) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.ViewData["Error"]!.ToString().Should().ContainEquivalentOf("vui lòng kiểm tra hộp thư email");
        }

        // =============================================
        // REGISTER TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public void RegisterViewModel_RejectsInvalidSensitiveIdentifiers()
        {
            var model = new RegisterViewModel
            {
                Name = "Test",
                Email = "test@example.com",
                Phone = "abc",
                Password = "secret1",
                ConfirmPassword = "secret1",
                DateOfBirth = DateTime.Today.AddYears(-20),
                CitizenId = "123",
                DrivingLicenseNumber = "bad-license"
            };
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            var isValid = Validator.TryValidateObject(
                model,
                new ValidationContext(model),
                results,
                validateAllProperties: true);

            isValid.Should().BeFalse();
            var memberNames = results.SelectMany(r => r.MemberNames).ToList();
            memberNames.Should().Contain(nameof(RegisterViewModel.Phone));
            memberNames.Should().Contain(nameof(RegisterViewModel.CitizenId));
            memberNames.Should().Contain(nameof(RegisterViewModel.DrivingLicenseNumber));
        }

        [Theory]
        [InlineData("short")]
        [InlineData("alllowercase1!")]
        [InlineData("ALLUPPERCASE1!")]
        [InlineData("NoNumber!")]
        [InlineData("NoSpecial1")]
        public void RegisterViewModel_RejectsWeakPasswords(string password)
        {
            var model = new RegisterViewModel { Password = password };
            var property = new ValidationContext(model) { MemberName = nameof(RegisterViewModel.Password) };
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            Validator.TryValidateProperty(model.Password, property, results).Should().BeFalse();
        }

        [Fact]
        public void RegisterViewModel_AcceptsStrongPassword()
        {
            var model = new RegisterViewModel { Password = "Strong@123" };
            var property = new ValidationContext(model) { MemberName = nameof(RegisterViewModel.Password) };
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            Validator.TryValidateProperty(model.Password, property, results).Should().BeTrue();
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Register_Post_ReturnsViewWithError_WhenUnder18()
        {
            var model = new RegisterViewModel
            {
                Email = "young@test.com",
                Name = "Young User",
                Phone = "0900000000",
                Password = "secret1",
                ConfirmPassword = "secret1",
                DateOfBirth = DateTime.Today.AddYears(-17),
                CitizenId = "012345678901",
                DrivingLicenseNumber = "123456789012"
            };

            var result = await _controller.Register(model, null) as ViewResult;

            result.Should().NotBeNull();
            result!.ViewData["Error"]!.ToString().Should().Contain("18");
            _mockAuthService.Verify(s => s.RegisterAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Register_Post_ReturnsViewWithError_WhenModelStateInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("Email", "Required");
            var newUser = new RegisterViewModel { Email = "fail@test.com", Name = "Test", Phone = "0900000000", Password = "secret1", ConfirmPassword = "secret1" };

            // Act
            var result = await _controller.Register(newUser, null) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.ViewData["Error"].Should().NotBeNull();
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Register_Post_ReturnsViewWithError_WhenEmailExists()
        {
            // Arrange
            var existingUser = new User { Email = "exist@test.com", Name = "Test", PasswordHash = "x" };
            _mockAuthService.Setup(s => s.GetUserByEmailAsync("exist@test.com")).ReturnsAsync(existingUser);

            // Act
            var result = await _controller.Register(new RegisterViewModel { Email = "exist@test.com", Name = "Test", Phone = "0900000000", Password = "secret1", ConfirmPassword = "secret1" }, null) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.ViewData["Error"].Should().Be("Email này đã được đăng ký.");
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Register_Post_ReturnsViewWithError_WhenRecaptchaFails()
        {
            // Arrange
            var controller = CreateController(requireRecaptcha: true);
            var model = new RegisterViewModel
            {
                Email = "new@test.com",
                Name = "Test",
                Phone = "0900000000",
                Password = "secret1",
                ConfirmPassword = "secret1"
            };
            _mockRecaptchaService.Setup(s => s.VerifyAsync("bad-token", "register"))
                                 .ReturnsAsync(new RecaptchaVerifyResult { Success = false });

            // Act
            var result = await controller.Register(model, "bad-token") as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.ViewData["Error"].Should().Be("Đăng ký không thành công do xác thực bảo mật thất bại.");
            _mockAuthService.Verify(s => s.GetUserByEmailAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
