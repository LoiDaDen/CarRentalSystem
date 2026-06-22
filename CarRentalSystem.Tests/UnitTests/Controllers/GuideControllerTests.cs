using System.Security.Claims;
using CarRentalSystem.Controllers;
using CarRentalSystem.Models.Tutorial;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers;

public sealed class GuideControllerTests
{
    [Fact]
    public async Task Index_UnknownRole_FallsBackToCustomerGuide()
    {
        var controller = CreateController(new Mock<ITutorialService>());

        var result = await controller.Index("unknown");

        var model = result.Should().BeOfType<ViewResult>().Subject.Model
            .Should().BeOfType<GuideViewModel>().Subject;
        model.Role.Should().Be("customer");
        model.IsInternal.Should().BeFalse();
        model.MarkdownContent.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Index_InternalGuide_ForWrongRole_IsForbidden()
    {
        var tutorials = new Mock<ITutorialService>();
        tutorials.Setup(service => service.CanAccessPanelAsync(
                It.IsAny<ClaimsPrincipal>(), "Admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = CreateController(tutorials, "Staff");

        var result = await controller.Index("admin");

        result.Should().BeOfType<StatusCodeResult>().Subject.StatusCode
            .Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Index_InternalGuide_UsesSecuredTutorialCatalog()
    {
        var response = new TutorialPanelResponse
        {
            Panel = "Admin",
            RoleName = "Admin",
            TutorialKey = "admin-panel-v1",
            Title = "Admin tutorial",
            Description = "Description",
            Steps = new[] { new TutorialStep { Key = "dashboard", Title = "Dashboard", Description = "Overview" } }
        };
        var tutorials = new Mock<ITutorialService>();
        tutorials.Setup(service => service.CanAccessPanelAsync(
                It.IsAny<ClaimsPrincipal>(), "Admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        tutorials.Setup(service => service.GetTutorialAsync(
                It.IsAny<ClaimsPrincipal>(), "Admin", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        tutorials.Setup(service => service.GetAllQuickGuidesAsync(
                It.IsAny<ClaimsPrincipal>(), "Admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TutorialQuickGuide>());
        var controller = CreateController(tutorials, "Admin");

        var result = await controller.Index("admin");

        var model = result.Should().BeOfType<ViewResult>().Subject.Model
            .Should().BeOfType<GuideViewModel>().Subject;
        model.IsInternal.Should().BeTrue();
        model.Tutorial.Should().BeSameAs(response);
    }

    private static GuideController CreateController(Mock<ITutorialService> tutorials, string? role = null)
    {
        var contentRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "CarRentalSystem"));
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.ContentRootPath).Returns(contentRoot);
        var controller = new GuideController(environment.Object, tutorials.Object);
        var claims = role == null
            ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, role) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, role == null ? null : "Test"))
            }
        };
        return controller;
    }
}
