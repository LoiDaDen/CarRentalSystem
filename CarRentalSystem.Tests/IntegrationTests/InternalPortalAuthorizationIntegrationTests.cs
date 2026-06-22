using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CarRentalSystem.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CarRentalSystem.Tests.IntegrationTests;

public sealed class InternalPortalWebAppFactory : TestWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = InternalPortalTestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = InternalPortalTestAuthHandler.SchemeName;
                    options.DefaultForbidScheme = InternalPortalTestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, InternalPortalTestAuthHandler>(
                    InternalPortalTestAuthHandler.SchemeName,
                    _ => { });
        });
    }
}

public sealed class InternalPortalTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "InternalPortalTest";

    public InternalPortalTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var roleValue))
            return Task.FromResult(AuthenticateResult.NoResult());

        var role = roleValue.ToString();
        var userId = role == UserRoleConstants.Admin ? "1" : role == UserRoleConstants.Manager ? "20" : "21";
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, $"{role} Integration"),
                new Claim(ClaimTypes.Role, role)
            },
            SchemeName);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public sealed class InternalPortalAuthorizationIntegrationTests : IClassFixture<InternalPortalWebAppFactory>
{
    private readonly InternalPortalWebAppFactory _factory;

    public InternalPortalAuthorizationIntegrationTests(InternalPortalWebAppFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(UserRoleConstants.Admin, "/Admin/Dashboard", "Admin")]
    [InlineData(UserRoleConstants.Manager, "/Manager/Dashboard", "Manager Portal")]
    [InlineData(UserRoleConstants.Staff, "/Staff/Dashboard", "Staff Portal")]
    [Trait("Module", "InternalPortal")]
    [Trait("Type", "Integration")]
    public async Task Dashboard_ForMatchingRole_ReturnsOwnPortal(string role, string path, string expectedText)
    {
        using var client = CreateClient(role);

        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(expectedText);
    }

    [Theory]
    [InlineData(UserRoleConstants.Staff, "/Admin/Dashboard")]
    [InlineData(UserRoleConstants.Staff, "/Manager/Dashboard")]
    [InlineData(UserRoleConstants.Manager, "/Admin/Dashboard")]
    [InlineData(UserRoleConstants.Customer, "/Staff/Dashboard")]
    [InlineData(UserRoleConstants.Customer, "/Manager/Dashboard")]
    [Trait("Module", "InternalPortal")]
    [Trait("Type", "Integration")]
    public async Task CrossAreaRequest_IsForbidden(string role, string path)
    {
        using var client = CreateClient(role);

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/Staff/Dashboard")]
    [InlineData("/Manager/Dashboard")]
    [Trait("Module", "InternalPortal")]
    [Trait("Type", "Integration")]
    public async Task AnonymousPortalRequest_IsChallenged(string path)
    {
        using var client = CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRoleConstants.Admin, "Admin")]
    [InlineData(UserRoleConstants.Manager, "Manager")]
    [InlineData(UserRoleConstants.Staff, "Staff")]
    [Trait("Module", "Tutorial")]
    [Trait("Type", "Integration")]
    public async Task TutorialApi_ForMatchingRole_ReturnsOwnPanel(string role, string panel)
    {
        using var client = CreateClient(role);

        var response = await client.GetAsync($"/Tutorial/GetTutorial?panel={panel}&manual=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain($"\"panel\":\"{panel}\"");
    }

    [Theory]
    [InlineData(UserRoleConstants.Admin, "Staff")]
    [InlineData(UserRoleConstants.Manager, "Admin")]
    [InlineData(UserRoleConstants.Staff, "Manager")]
    [Trait("Module", "Tutorial")]
    [Trait("Type", "Integration")]
    public async Task TutorialApi_ForOtherInternalRole_IsForbidden(string role, string panel)
    {
        using var client = CreateClient(role);

        var response = await client.GetAsync($"/Tutorial/GetTutorial?panel={panel}&manual=true");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(UserRoleConstants.Staff, "/Staff/Dashboard", "Staff Portal")]
    [InlineData(UserRoleConstants.Manager, "/Manager/Dashboard", "Manager Portal")]
    [Trait("Module", "InternalPortal")]
    [Trait("Type", "Integration")]
    public async Task PortalHtml_DoesNotExposeAdminOnlyNavigation(string role, string path, string portalName)
    {
        using var client = CreateClient(role);

        var html = await (await client.GetAsync(path)).Content.ReadAsStringAsync();

        html.Should().Contain(portalName);
        html.Should().NotContain("Admin Portal");
        html.Should().NotContain("User Management");
        html.Should().NotContain("Enterprise");
        html.Should().NotContain("System Settings");
        html.Should().NotContain("SRE");
    }

    private HttpClient CreateClient(string? role = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (role != null)
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }
}
