using System.Collections.Generic;
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
            _context = MockRepositoryHelper.CreateSeededDbContext();

            _controller = new AuthController(
                _mockAuthService.Object,
                _mockEnv.Object,
                _mockEmailService.Object,
                _mockPiiEncryption.Object,
                _context,
                _mockRecaptchaService.Object,
                _mockReferralService.Object);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("http://localhost/verify");
            mockUrlHelper.Setup(u => u.IsLocalUrl(It.IsAny<string>()))
                .Returns<string>(url => !string.IsNullOrWhiteSpace(url) && url.StartsWith("/") && !url.StartsWith("//"));
            _controller.Url = mockUrlHelper.Object;
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
            result!.ViewData["Error"].Should().Be("Email nay da duoc dang ky.");
        }
    }
}
