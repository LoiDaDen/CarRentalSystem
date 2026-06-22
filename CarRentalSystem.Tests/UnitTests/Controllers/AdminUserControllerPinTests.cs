using System.Security.Claims;
using CarRentalSystem.Areas.Admin.Controllers;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers;

public class AdminUserControllerPinTests
{
    [Fact]
    [Trait("Module", "AdminUsers")]
    [Trait("Type", "Regression")]
    public async Task VerifyAdminPin_UsesSharedConfiguredPin_NotPersonalAccountPin()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Pin"] = "654321"
            })
            .Build();
        var piiAccess = new Mock<IPiiAccessService>();
        var controller = CreateController(configuration, Environments.Production, piiAccess, userId: 42);

        var result = await controller.VerifyAdminPin("654321");

        result.Should().BeOfType<RedirectToActionResult>();
        piiAccess.Verify(service => service.GrantAsync(
            It.IsAny<HttpContext>(),
            42,
            "Shared admin customer-list PIN verified."), Times.Once);
    }

    [Fact]
    [Trait("Module", "AdminUsers")]
    [Trait("Type", "Regression")]
    public async Task VerifyAdminPin_UsesDefaultDevelopmentPin_WhenSharedPinIsNotConfigured()
    {
        var configuration = new ConfigurationBuilder().Build();
        var piiAccess = new Mock<IPiiAccessService>();
        var controller = CreateController(configuration, Environments.Development, piiAccess, userId: 7);

        var result = await controller.VerifyAdminPin("123456");

        result.Should().BeOfType<RedirectToActionResult>();
        piiAccess.Verify(service => service.GrantAsync(
            It.IsAny<HttpContext>(),
            7,
            "Shared admin customer-list PIN verified."), Times.Once);
    }

    private static UserController CreateController(
        IConfiguration configuration,
        string environmentName,
        Mock<IPiiAccessService> piiAccess,
        int userId)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns(environmentName);
        var services = new ServiceCollection()
            .AddSingleton(environment.Object)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                "Test"))
        };

        return new UserController(
            MockRepositoryHelper.CreateSeededDbContext(),
            Mock.Of<IPiiEncryptionService>(),
            configuration,
            Mock.Of<ICustomerRiskScoringService>(),
            piiAccess.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = Mock.Of<IUrlHelper>()
        };
    }
}
