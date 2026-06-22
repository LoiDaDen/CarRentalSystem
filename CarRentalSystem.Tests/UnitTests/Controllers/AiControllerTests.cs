using System.Security.Claims;
using CarRentalSystem.Controllers;
using CarRentalSystem.Models;
using CarRentalSystem.Services.Ai;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    public class AiControllerTests
    {
        [Fact]
        [Trait("Module", "OperationsCopilot")]
        [Trait("Type", "Authorization")]
        public async Task Chat_OpsMode_ReturnsUnauthorizedForGuest()
        {
            var ai = new Mock<IAiService>();
            var controller = CreateController(ai, null);

            var result = await controller.Chat(new AiChatRequest { Mode = "ops", Message = "ops snapshot" }, CancellationToken.None);

            result.Should().BeOfType<UnauthorizedObjectResult>();
            ai.Verify(s => s.ChatAsync(It.IsAny<AiChatRequest>(), It.IsAny<AiUserContext>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        [Trait("Module", "OperationsCopilot")]
        [Trait("Type", "Authorization")]
        public async Task Chat_OpsMode_ReturnsForbidForCustomer()
        {
            var ai = new Mock<IAiService>();
            var controller = CreateController(ai, UserRoleConstants.Customer);

            var result = await controller.Chat(new AiChatRequest { Mode = "ops", Message = "ops snapshot" }, CancellationToken.None);

            result.Should().BeOfType<ForbidResult>();
            ai.Verify(s => s.ChatAsync(It.IsAny<AiChatRequest>(), It.IsAny<AiUserContext>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(UserRoleConstants.Admin)]
        [InlineData(UserRoleConstants.Manager)]
        [InlineData(UserRoleConstants.Staff)]
        [Trait("Module", "OperationsCopilot")]
        [Trait("Type", "Authorization")]
        public async Task Chat_OpsMode_AllowsInternalStaff(string role)
        {
            var ai = new Mock<IAiService>();
            ai.Setup(s => s.ChatAsync(It.IsAny<AiChatRequest>(), It.IsAny<AiUserContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AiChatResponse { Reply = "ok" });
            var controller = CreateController(ai, role);

            var result = await controller.Chat(new AiChatRequest { Mode = "ops", Message = "ops snapshot" }, CancellationToken.None);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeOfType<AiChatResponse>().Which.Reply.Should().Be("ok");
            ai.Verify(s => s.ChatAsync(
                It.Is<AiChatRequest>(r => r.Mode == "ops"),
                It.Is<AiUserContext>(u => u.Role == role && u.IsInternalStaff),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private static AiController CreateController(Mock<IAiService> ai, string? role)
        {
            var httpContext = new DefaultHttpContext();
            if (!string.IsNullOrWhiteSpace(role))
            {
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Name, "Test User"),
                    new Claim(ClaimTypes.Email, "test@example.local"),
                    new Claim(ClaimTypes.Role, role)
                }, "TestAuth"));
            }

            return new AiController(ai.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
        }
    }
}
