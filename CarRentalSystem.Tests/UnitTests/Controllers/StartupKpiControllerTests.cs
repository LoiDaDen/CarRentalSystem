using CarRentalSystem.Areas.Admin.Controllers;
using CarRentalSystem.Areas.Admin.Models;
using CarRentalSystem.Authorization;
using CarRentalSystem.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers;

public class StartupKpiControllerTests
{
    [Fact]
    [Trait("Module", "StartupKpi")]
    [Trait("Type", "Security")]
    public void Controller_RequiresAdminPolicy()
    {
        var attribute = typeof(StartupKpiController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attribute.Policy.Should().Be(PortalPolicyNames.RequireAdmin);
    }

    [Fact]
    [Trait("Module", "StartupKpi")]
    [Trait("Type", "Regression")]
    public async Task Index_UsesConfiguredSixMonthPeriod()
    {
        var options = new StartupKpiOptions
        {
            PeriodStartUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodMonths = 6
        };
        var snapshot = new StartupKpiSnapshot(
            options.PeriodStartUtc,
            options.PeriodStartUtc.AddMonths(6),
            0, 0, 0, 0, 0, 0m, 0m, 0m, 0, 0, 0m, 0m, 0);
        var service = new Mock<IStartupKpiService>();
        service.Setup(item => item.GetSnapshotAsync(
                options.PeriodStartUtc,
                options.PeriodStartUtc.AddMonths(6),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var controller = new StartupKpiController(service.Object, Options.Create(options));

        var result = await controller.Index();

        var model = result.Should().BeOfType<ViewResult>().Which.Model
            .Should().BeOfType<StartupKpiViewModel>().Subject;
        model.Snapshot.Should().Be(snapshot);
        service.VerifyAll();
    }
}
