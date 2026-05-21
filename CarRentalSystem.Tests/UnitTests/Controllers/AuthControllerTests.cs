using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CarRentalSystem.Data;
using CarRentalSystem.Controllers;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using CarRentalSystem.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
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
            _context = MockRepositoryHelper.CreateSeededDbContext();
            _mockLegalCompliance
                .Setup(s => s.RecordConsentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<HttpContext>(), It.IsAny<string?>()))
                .ReturnsAsync((int userId, string documentCode, string purposeCode, bool isGranted, string source, HttpContext context, string? evidence) =>
                    new ConsentRecord { UserID = userId, PurposeCode = purposeCode, IsGranted = isGranted, Source = source });

            _controller = CreateController(requireRecaptcha: false);
        }

        private AuthController CreateController(bool requireRecaptcha)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:RequireRecaptcha"] = requireRecaptcha.ToString()
                })
                .Build();

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
                _mockLegalCompliance.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

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
