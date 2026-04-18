using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using CarRentalSystem.Controllers;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
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
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _mockAuthService = new Mock<IAuthService>();
            _mockEnv = new Mock<IWebHostEnvironment>();
            _mockEmailService = new Mock<IEmailService>();
            _mockPiiEncryption = new Mock<IPiiEncryptionService>();

            _controller = new AuthController(_mockAuthService.Object, _mockEnv.Object, _mockEmailService.Object, _mockPiiEncryption.Object);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("http://localhost/verify");
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
        public async Task Login_Post_ReturnsViewWithError_WhenAuthFails()
        {
            // Arrange
            _mockAuthService.Setup(s => s.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
                            .ReturnsAsync((User?)null);

            // Act
            var result = await _controller.Login("test@user.com", "wrongpass") as ViewResult;

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
            var result = await _controller.Login("test@test.com", "pass") as ViewResult;

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
            var newUser = new User { Email = "fail@test.com", Name = "Test", PasswordHash = "x" };

            // Act
            var result = await _controller.Register(newUser) as ViewResult;

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
            var result = await _controller.Register(new User { Email = "exist@test.com", Name = "Test", PasswordHash = "x" }) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.ViewData["Error"].Should().Be("Email already registered");
        }
    }
}
