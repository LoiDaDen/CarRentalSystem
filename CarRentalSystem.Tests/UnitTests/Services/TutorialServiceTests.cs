using System.Security.Claims;
using CarRentalSystem.Models;
using CarRentalSystem.Models.Tutorial;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services;

public sealed class TutorialServiceTests
{
    [Theory]
    [InlineData("Admin", 10)]
    [InlineData("Partner", 8)]
    [InlineData("Staff", 7)]
    [InlineData("Manager", 7)]
    public async Task Catalogs_AreCompleteAndHaveExpectedSteps(string panel, int expectedSteps)
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var user = CreatePrincipal(1, panel == "Partner" ? UserRoleConstants.Customer : panel);

        var tutorial = await service.GetTutorialAsync(user, panel, manual: true);

        tutorial.Should().NotBeNull();
        tutorial!.Steps.Should().HaveCount(expectedSteps);
        tutorial.Steps.Should().OnlyContain(step =>
            !string.IsNullOrWhiteSpace(step.Key) &&
            !string.IsNullOrWhiteSpace(step.Title) &&
            !string.IsNullOrWhiteSpace(step.Description));
    }

    [Fact]
    public async Task PartnerAccess_RequiresApprovedPartnerFlag()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        (await service.CanAccessPanelAsync(CreatePrincipal(1, UserRoleConstants.Customer), "Partner")).Should().BeTrue();
        (await service.CanAccessPanelAsync(CreatePrincipal(2, UserRoleConstants.Customer), "Partner")).Should().BeFalse();
    }

    [Fact]
    public async Task Finish_SuppressesAutomaticTutorial_ButManualReplayStillWorks()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var admin = CreatePrincipal(3, UserRoleConstants.Admin);
        var tutorial = await service.GetTutorialAsync(admin, "Admin", manual: false);

        var updated = await service.UpdateProgressAsync(admin, new TutorialProgressUpdateRequest
        {
            Panel = "Admin",
            TutorialKey = tutorial!.TutorialKey,
            Action = TutorialProgressActions.Finish
        });

        updated.Should().BeTrue();
        (await service.GetTutorialAsync(admin, "Admin", manual: false)).Should().BeNull();
        (await service.GetTutorialAsync(admin, "Admin", manual: true)).Should().NotBeNull();
        context.UserTutorialProgresses.Should().ContainSingle(progress =>
            progress.UserID == 3 && progress.IsCompleted && progress.CompletedAt.HasValue);
    }

    [Fact]
    public async Task QuickGuide_ResolvesExactActionThenControllerFallback()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var partner = CreatePrincipal(1, UserRoleConstants.Customer);

        var exact = await service.GetQuickGuideAsync(partner, "Partner", "", "OwnerCars", "Create");
        var fallback = await service.GetQuickGuideAsync(partner, "Partner", "", "OwnerCars", "TaxInfo");

        exact!.Title.Should().Be("Đăng xe cho thuê");
        fallback!.Title.Should().Be("Hướng dẫn Partner");
    }

    [Fact]
    public async Task WrongRole_CannotLoadOrUpdateOtherPanel()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var staff = CreatePrincipal(4, UserRoleConstants.Staff);

        (await service.GetTutorialAsync(staff, "Admin", manual: true)).Should().BeNull();
        (await service.UpdateProgressAsync(staff, new TutorialProgressUpdateRequest
        {
            Panel = "Admin",
            TutorialKey = "admin-panel-v1",
            Action = TutorialProgressActions.Finish
        })).Should().BeFalse();
    }

    private static CarRentalSystem.Data.ApplicationDbContext CreateContext()
    {
        var context = MockRepositoryHelper.CreateInMemoryDbContext();
        context.Users.AddRange(
            new User { UserID = 1, Name = "Owner", Email = "owner@test.local", PasswordHash = "hash", Role = UserRoleConstants.Customer, IsPartnerApproved = true },
            new User { UserID = 2, Name = "Customer", Email = "customer@test.local", PasswordHash = "hash", Role = UserRoleConstants.Customer },
            new User { UserID = 3, Name = "Admin", Email = "admin@test.local", PasswordHash = "hash", Role = UserRoleConstants.Admin },
            new User { UserID = 4, Name = "Staff", Email = "staff@test.local", PasswordHash = "hash", Role = UserRoleConstants.Staff });
        context.SaveChanges();
        return context;
    }

    private static TutorialService CreateService(CarRentalSystem.Data.ApplicationDbContext context)
    {
        var contentRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "CarRentalSystem"));
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.ContentRootPath).Returns(contentRoot);
        environment.SetupGet(item => item.EnvironmentName).Returns("Testing");
        return new TutorialService(context, environment.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    private static ClaimsPrincipal CreatePrincipal(int userId, string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "Test"));
}
