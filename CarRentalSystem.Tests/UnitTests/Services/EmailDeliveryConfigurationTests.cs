using CarRentalSystem.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class EmailDeliveryConfigurationTests
    {
        [Fact]
        [Trait("Module", "Email")]
        [Trait("Type", "Regression")]
        public void ShouldUseLocalSink_DevelopmentWithConfiguredSmtp_UsesRealSender()
        {
            var configuration = Configuration(
                ("Email:DeliveryMode", "Local"),
                ("SmtpSettings:Host", "smtp.example.test"),
                ("SmtpSettings:User", "sender@example.test"),
                ("SmtpSettings:Pass", "app-password"));

            var result = EmailDeliveryConfiguration.ShouldUseLocalSink(
                configuration,
                Environment("Development"),
                isAutomatedTestEnvironment: false);

            result.Should().BeFalse();
        }

        [Fact]
        [Trait("Module", "Email")]
        [Trait("Type", "Regression")]
        public void ShouldUseLocalSink_DevelopmentWithoutSmtp_UsesLocalSink()
        {
            var configuration = Configuration(("Email:DeliveryMode", "Local"));

            var result = EmailDeliveryConfiguration.ShouldUseLocalSink(
                configuration,
                Environment("Development"),
                isAutomatedTestEnvironment: false);

            result.Should().BeTrue();
        }

        [Fact]
        [Trait("Module", "Email")]
        [Trait("Type", "Regression")]
        public void ShouldUseLocalSink_AutomatedTestWithConfiguredSmtp_StillUsesLocalSink()
        {
            var configuration = Configuration(
                ("Email:DeliveryMode", "Smtp"),
                ("SmtpSettings:Host", "smtp.example.test"),
                ("SmtpSettings:User", "sender@example.test"),
                ("SmtpSettings:Pass", "app-password"));

            var result = EmailDeliveryConfiguration.ShouldUseLocalSink(
                configuration,
                Environment("Playwright"),
                isAutomatedTestEnvironment: true);

            result.Should().BeTrue();
        }

        private static IConfiguration Configuration(params (string Key, string Value)[] values)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values.ToDictionary(item => item.Key, item => (string?)item.Value))
                .Build();
        }

        private static IWebHostEnvironment Environment(string name)
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(value => value.EnvironmentName).Returns(name);
            return environment.Object;
        }
    }
}
