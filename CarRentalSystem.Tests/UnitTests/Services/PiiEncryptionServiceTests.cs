using CarRentalSystem.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class PiiEncryptionServiceTests
    {
        [Fact]
        [Trait("Module", "Security")]
        [Trait("Type", "Unit")]
        public void Decrypt_RoundTripsProtectedPii()
        {
            using var keys = new TempDirectory();
            var logger = new ListLogger<PiiEncryptionService>();
            var service = CreateService(keys.Path, logger);

            var encrypted = service.Encrypt("079200001234");
            var decrypted = service.Decrypt(encrypted);

            encrypted.Should().StartWith("CfDJ8");
            decrypted.Should().Be("079200001234");
            logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
        }

        [Fact]
        [Trait("Module", "Security")]
        [Trait("Type", "Unit")]
        public void Decrypt_LegacyPlaintext_ReturnsPlaintextWithoutWarning()
        {
            using var keys = new TempDirectory();
            var logger = new ListLogger<PiiEncryptionService>();
            var service = CreateService(keys.Path, logger);

            var decrypted = service.Decrypt("012345678901");

            decrypted.Should().Be("012345678901");
            logger.Entries.Should().NotContain(e => e.Level == LogLevel.Warning);
        }

        [Fact]
        [Trait("Module", "Security")]
        [Trait("Type", "Unit")]
        public void Decrypt_ProtectedPayloadFromDifferentKeyRing_ReturnsEmptyAndWarnsOnce()
        {
            using var sourceKeys = new TempDirectory();
            using var targetKeys = new TempDirectory();
            var sourceService = CreateService(sourceKeys.Path, new ListLogger<PiiEncryptionService>());
            var targetLogger = new ListLogger<PiiEncryptionService>();
            var targetService = CreateService(targetKeys.Path, targetLogger);
            var encryptedWithDifferentKeyRing = sourceService.Encrypt("079200001234");

            var firstResult = targetService.Decrypt(encryptedWithDifferentKeyRing);
            var secondResult = targetService.Decrypt(encryptedWithDifferentKeyRing);

            firstResult.Should().BeEmpty();
            secondResult.Should().BeEmpty();
            targetLogger.Entries.Count(e => e.Level == LogLevel.Warning).Should().Be(1);
            targetLogger.Entries.Count(e => e.Level == LogLevel.Debug).Should().Be(1);
            targetLogger.Entries.Single(e => e.Level == LogLevel.Warning).Message
                .Should().Contain("DataProtection:KeysPath/key ring");
        }

        private static PiiEncryptionService CreateService(
            string keyRingPath,
            ILogger<PiiEncryptionService> logger,
            string environmentName = "Development")
        {
            var provider = DataProtectionProvider.Create(new DirectoryInfo(keyRingPath));
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);

            return new PiiEncryptionService(provider, logger, environment.Object);
        }

        private sealed class TempDirectory : IDisposable
        {
            public TempDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "carrental-pii-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
        }

        private sealed class ListLogger<T> : ILogger<T>
        {
            public List<LogEntry> Entries { get; } = new();

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }

        private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
    }
}
