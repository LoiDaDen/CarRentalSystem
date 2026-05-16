using CarRentalSystem.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class ConfigurationGuardTests
    {
        [Fact]
        [Trait("Module", "Configuration")]
        [Trait("Type", "Unit")]
        public void ValidateProductionConfiguration_RejectsMissingRecaptchaSecret()
        {
            var values = ValidProductionSettings();
            values["Recaptcha:SecretKey"] = "";

            var act = () => ConfigurationGuard.ValidateProductionConfiguration(BuildConfiguration(values), ProductionEnvironment());

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Recaptcha:SecretKey*");
        }

        [Theory]
        [InlineData("")]
        [InlineData("*")]
        [InlineData("https://*.example.com")]
        [Trait("Module", "Configuration")]
        [Trait("Type", "Unit")]
        public void ValidateProductionConfiguration_RejectsUnsafeCorsOrigins(string origin)
        {
            var values = ValidProductionSettings();
            values["Cors:AllowedOrigins:0"] = origin;

            var act = () => ConfigurationGuard.ValidateProductionConfiguration(BuildConfiguration(values), ProductionEnvironment());

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Cors:AllowedOrigins*wildcards*");
        }

        [Theory]
        [InlineData("Snapshot:AllowUnsafeDownload")]
        [InlineData("Snapshot:AllowUnsafeRestore")]
        [Trait("Module", "Configuration")]
        [Trait("Type", "Unit")]
        public void ValidateProductionConfiguration_RejectsUnsafeSnapshotFlags(string key)
        {
            var values = ValidProductionSettings();
            values[key] = "true";

            var act = () => ConfigurationGuard.ValidateProductionConfiguration(BuildConfiguration(values), ProductionEnvironment());

            act.Should().Throw<InvalidOperationException>()
                .WithMessage($"*{key}*false in Production*");
        }

        [Fact]
        [Trait("Module", "Configuration")]
        [Trait("Type", "Unit")]
        public void ValidateProductionConfiguration_RejectsRequiredPrivilegedSsoWithoutProvider()
        {
            var values = ValidProductionSettings();
            values["Security:RequirePrivilegedSso"] = "true";

            var act = () => ConfigurationGuard.ValidateProductionConfiguration(BuildConfiguration(values), ProductionEnvironment());

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*RequirePrivilegedSso*Google or Microsoft*");
        }

        [Fact]
        [Trait("Module", "Configuration")]
        [Trait("Type", "Unit")]
        public void ValidateProductionConfiguration_RejectsOpenTelemetryWithoutEndpoint()
        {
            var values = ValidProductionSettings();
            values["Observability:OpenTelemetry:Enabled"] = "true";
            values["Observability:OpenTelemetry:OtlpEndpoint"] = "";

            var act = () => ConfigurationGuard.ValidateProductionConfiguration(BuildConfiguration(values), ProductionEnvironment());

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*OpenTelemetry*OtlpEndpoint*");
        }

        [Fact]
        [Trait("Module", "Configuration")]
        [Trait("Type", "Unit")]
        public void ValidateProductionConfiguration_RejectsApiDocsWithoutAuth()
        {
            var values = ValidProductionSettings();
            values["ApiDocs:Enabled"] = "true";
            values["ApiDocs:RequireAuth"] = "false";

            var act = () => ConfigurationGuard.ValidateProductionConfiguration(BuildConfiguration(values), ProductionEnvironment());

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*ApiDocs:RequireAuth*true*");
        }

        [Fact]
        [Trait("Module", "Configuration")]
        [Trait("Type", "Unit")]
        public void Localization_DefaultsToVietnameseAndSupportsEnglish()
        {
            var values = ValidProductionSettings();
            var configuration = BuildConfiguration(values);

            configuration["Localization:DefaultCulture"].Should().Be("vi-VN");
            configuration.GetSection("Localization:SupportedCultures").Get<string[]>()
                .Should().Contain(new[] { "vi-VN", "en-US" });
        }

        private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        private static IWebHostEnvironment ProductionEnvironment()
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(e => e.EnvironmentName).Returns("Production");
            return environment.Object;
        }

        private static Dictionary<string, string?> ValidProductionSettings()
        {
            return new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=sql.example;Database=CarRental;User Id=app;Password=strong;Encrypt=True",
                ["Cors:AllowedOrigins:0"] = "https://app.example.com",
                ["Tracking:IngestSecret"] = "long-random-tracking-secret",
                ["DataProtection:KeysPath"] = "C:\\keys",
                ["PayOS:ClientId"] = "payos-client",
                ["PayOS:ApiKey"] = "payos-api-key",
                ["PayOS:ChecksumKey"] = "payos-checksum",
                ["SmtpSettings:Host"] = "smtp.example.com",
                ["SmtpSettings:User"] = "mailer@example.com",
                ["SmtpSettings:Pass"] = "smtp-password",
                ["Recaptcha:SecretKey"] = "recaptcha-secret",
                ["FptAi:ApiKey"] = "fpt-key",
                ["Security:RequirePrivilegedMfa"] = "true",
                ["Security:RequireRecaptcha"] = "true",
                ["Security:RequirePrivilegedSso"] = "false",
                ["Audit:HashChainEnabled"] = "true",
                ["Snapshot:AllowUnsafeDownload"] = "false",
                ["Snapshot:AllowUnsafeRestore"] = "false",
                ["Sre:SloAvailabilityTarget"] = "99.9",
                ["Sre:RtoHours"] = "4",
                ["Sre:RpoHours"] = "1",
                ["Observability:OpenTelemetry:Enabled"] = "false",
                ["Observability:OpenTelemetry:ServiceName"] = "CarRentalSystem",
                ["Observability:OpenTelemetry:OtlpEndpoint"] = "",
                ["ApiDocs:Enabled"] = "false",
                ["ApiDocs:RequireAuth"] = "true",
                ["Localization:DefaultCulture"] = "vi-VN",
                ["Localization:SupportedCultures:0"] = "vi-VN",
                ["Localization:SupportedCultures:1"] = "en-US",
                ["Analytics:Enabled"] = "false",
                ["Analytics:Provider"] = "Noop"
            };
        }
    }
}
