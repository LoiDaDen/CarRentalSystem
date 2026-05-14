using CarRentalSystem.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class PaymentUrlResolverTests
    {
        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Unit")]
        public void ResolvePublicBaseUrl_UsesConfiguredPublicUrl_WhenPresent()
        {
            var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:PublicBaseUrl"] = "https://payos-dev.example/"
                    })
                    .Build())
                .BuildServiceProvider();

            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("localhost:5168");

            PaymentUrlResolver.ResolvePublicBaseUrl(context).Should().Be("https://payos-dev.example");
        }

        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Unit")]
        public void ResolvePublicBaseUrl_FallsBackToRequestHost_WhenNotConfigured()
        {
            var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
                .BuildServiceProvider();

            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("localhost:5168");

            PaymentUrlResolver.ResolvePublicBaseUrl(context).Should().Be("http://localhost:5168");
        }
    }
}
