using CarRentalSystem.Data;
using CarRentalSystem.Helpers;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class CameraAnprServiceTests
    {
        [Theory]
        [InlineData("51A-123.45", "51A12345")]
        [InlineData(" 51a 12345 ", "51A12345")]
        [InlineData("51A12345", "51A12345")]
        [Trait("Module", "Camera")]
        [Trait("Type", "Unit")]
        public void NormalizeLicensePlate_ShouldRemoveSeparatorsAndUppercase(string input, string expected)
        {
            LicensePlateNormalizer.Normalize(input).Should().Be(expected);
        }

        [Fact]
        [Trait("Module", "Camera")]
        [Trait("Type", "Unit")]
        public async Task HandleWebhook_ShouldCreateMatchedEventAndUpdatePresence()
        {
            using var context = CreateContextWithCamera(out var secret);
            var service = CreateService(context);
            var envelope = CreateEnvelope(secret, "evt-1", "51A-123.45", "In", 0.93m);

            var result = await service.HandleWebhookAsync(envelope);

            result.Outcome.Should().Be(CameraAnprWebhookOutcome.Accepted);
            result.MatchStatus.Should().Be(CameraGateMatchStatusConstants.Matched);
            var gateEvent = await context.CameraGateEvents.SingleAsync();
            gateEvent.CarID.Should().Be(1);
            gateEvent.AreaID.Should().Be(1);
            gateEvent.LocationID.Should().Be(1);
            var presence = await context.VehicleAreaPresences.SingleAsync();
            presence.CarID.Should().Be(1);
            presence.IsInside.Should().BeTrue();
            presence.LastDirection.Should().Be(CameraGateDirectionConstants.In);
        }

        [Fact]
        [Trait("Module", "Camera")]
        [Trait("Type", "Unit")]
        public async Task HandleWebhook_ShouldRejectInvalidSignature()
        {
            using var context = CreateContextWithCamera(out var secret);
            var service = CreateService(context);
            var envelope = CreateEnvelope(secret, "evt-invalid", "51A-123.45", "In", 0.93m) with
            {
                Signature = "sha256=" + new string('0', 64)
            };

            var result = await service.HandleWebhookAsync(envelope);

            result.Outcome.Should().Be(CameraAnprWebhookOutcome.Unauthorized);
            context.CameraGateEvents.Should().BeEmpty();
        }

        [Fact]
        [Trait("Module", "Camera")]
        [Trait("Type", "Unit")]
        public async Task HandleWebhook_ShouldRejectExpiredTimestamp()
        {
            using var context = CreateContextWithCamera(out var secret);
            var service = CreateService(context);
            var envelope = CreateEnvelope(secret, "evt-expired", "51A-123.45", "In", 0.93m, timestamp: DateTimeOffset.UtcNow.AddMinutes(-20));

            var result = await service.HandleWebhookAsync(envelope);

            result.Outcome.Should().Be(CameraAnprWebhookOutcome.Unauthorized);
            result.Message.Should().Be("Expired camera timestamp.");
            context.CameraGateEvents.Should().BeEmpty();
        }

        [Fact]
        [Trait("Module", "Camera")]
        [Trait("Type", "Unit")]
        public async Task HandleWebhook_ShouldRejectReplayedNonce()
        {
            using var context = CreateContextWithCamera(out var secret);
            var service = CreateService(context);
            var nonce = "nonce-replay";
            var first = CreateEnvelope(secret, "evt-replay-1", "51A-123.45", "In", 0.93m, nonce: nonce);
            var second = CreateEnvelope(secret, "evt-replay-2", "51A-123.45", "In", 0.93m, nonce: nonce);

            (await service.HandleWebhookAsync(first)).Outcome.Should().Be(CameraAnprWebhookOutcome.Accepted);
            var replay = await service.HandleWebhookAsync(second);

            replay.Outcome.Should().Be(CameraAnprWebhookOutcome.Unauthorized);
            replay.Message.Should().Be("Replayed camera nonce.");
            context.CameraGateEvents.Should().HaveCount(1);
        }

        [Fact]
        [Trait("Module", "Camera")]
        [Trait("Type", "Unit")]
        public async Task HandleWebhook_ShouldUseCameraDefaultDirection_WhenPayloadDirectionMissing()
        {
            using var context = CreateContextWithCamera(out var secret, defaultDirection: CameraGateDirectionConstants.Out);
            var service = CreateService(context);
            var envelope = CreateEnvelope(secret, "evt-fallback", "51A-123.45", null, 0.93m);

            var result = await service.HandleWebhookAsync(envelope);

            result.Outcome.Should().Be(CameraAnprWebhookOutcome.Accepted);
            var presence = await context.VehicleAreaPresences.SingleAsync();
            presence.IsInside.Should().BeFalse();
            presence.LastDirection.Should().Be(CameraGateDirectionConstants.Out);
        }

        [Fact]
        [Trait("Module", "Camera")]
        [Trait("Type", "Unit")]
        public async Task HandleWebhook_ShouldStoreLowConfidenceWithoutUpdatingPresence()
        {
            using var context = CreateContextWithCamera(out var secret);
            var service = CreateService(context);
            var envelope = CreateEnvelope(secret, "evt-low", "51A-123.45", "In", 0.42m);

            var result = await service.HandleWebhookAsync(envelope);

            result.Outcome.Should().Be(CameraAnprWebhookOutcome.Accepted);
            result.MatchStatus.Should().Be(CameraGateMatchStatusConstants.LowConfidence);
            context.CameraGateEvents.Single().CarID.Should().Be(1);
            context.VehicleAreaPresences.Should().BeEmpty();
        }

        [Fact]
        [Trait("Module", "Camera")]
        [Trait("Type", "Unit")]
        public async Task HandleWebhook_ShouldStoreUnknownPlateWithoutAlert()
        {
            using var context = CreateContextWithCamera(out var secret);
            var service = CreateService(context);
            var envelope = CreateEnvelope(secret, "evt-unknown", "99Z-999.99", "In", 0.93m);

            var result = await service.HandleWebhookAsync(envelope);

            result.Outcome.Should().Be(CameraAnprWebhookOutcome.Accepted);
            result.MatchStatus.Should().Be(CameraGateMatchStatusConstants.UnknownPlate);
            context.CameraGateEvents.Single().CarID.Should().BeNull();
            context.Alerts.Should().BeEmpty();
        }

        [Fact]
        [Trait("Module", "Camera")]
        [Trait("Type", "Unit")]
        public async Task HandleWebhook_ShouldCreateAlert_WhenMatchedCarBelongsToAnotherLocation()
        {
            using var context = CreateContextWithCamera(out var secret);
            context.Cars.Add(new Car
            {
                CarID = 50,
                CarName = "Mismatch Car",
                BrandID = 1,
                LicensePlate = "30F-33333",
                VIN = "VIN0000000000050",
                PricePerDay = 900000,
                PricePerHour = 100000,
                CarStatusID = 1,
                LocationID = 2,
                CarTypeID = 1,
                Seats = 5
            });
            await context.SaveChangesAsync();
            var service = CreateService(context);
            var envelope = CreateEnvelope(secret, "evt-mismatch", "30F-33333", "In", 0.93m);

            var result = await service.HandleWebhookAsync(envelope);

            result.Outcome.Should().Be(CameraAnprWebhookOutcome.Accepted);
            result.MatchStatus.Should().Be(CameraGateMatchStatusConstants.AreaMismatch);
            context.VehicleAreaPresences.Should().BeEmpty();
            var alert = context.Alerts.Single();
            alert.CarID.Should().Be(50);
            alert.AlertType.Should().Be("CameraAreaMismatch");
        }

        private static ApplicationDbContext CreateContextWithCamera(out string secret, string defaultDirection = CameraGateDirectionConstants.In)
        {
            var context = MockRepositoryHelper.CreateSeededDbContext();
            context.Areas.Add(new Area
            {
                AreaID = 1,
                Name = "Depot HCM",
                Code = "HCM",
                Province = "Hồ Chí Minh",
                Status = "active"
            });
            context.AreaLocations.Add(new AreaLocation { AreaID = 1, LocationID = 1 });
            secret = "cam_test_secret";
            context.CameraDevices.Add(new CameraDevice
            {
                CameraDeviceID = 1,
                Code = "GATE-IN-01",
                Name = "Gate In",
                Provider = "Test ANPR",
                AreaID = 1,
                LocationID = 1,
                DefaultDirection = defaultDirection,
                Status = CameraDeviceStatusConstants.Active,
                EncryptedWebhookSecret = secret
            });
            context.SaveChanges();
            return context;
        }

        private static CameraAnprService CreateService(ApplicationDbContext context)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Camera:MinPlateConfidence"] = "0.80",
                    ["Camera:MaxWebhookClockSkewSeconds"] = "300"
                })
                .Build();

            return new CameraAnprService(
                context,
                configuration,
                new PlainTextPiiEncryptionService(),
                new NoopAuditTrailService(),
                NullLogger<CameraAnprService>.Instance);
        }

        private static CameraAnprWebhookEnvelope CreateEnvelope(
            string secret,
            string providerEventId,
            string plate,
            string? direction,
            decimal confidence,
            string nonce = "nonce-1",
            DateTimeOffset? timestamp = null)
        {
            var capturedAt = DateTime.UtcNow.ToString("O");
            var directionJson = direction == null ? "null" : $"\"{direction}\"";
            var body = $"{{\"providerEventId\":\"{providerEventId}\",\"licensePlate\":\"{plate}\",\"direction\":{directionJson},\"capturedAt\":\"{capturedAt}\",\"confidence\":{confidence.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"snapshotUrl\":\"https://nvr.example.test/snapshots/{providerEventId}.jpg\"}}";
            var ts = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString();
            var signature = "sha256=" + CameraAnprService.ComputeSignature(secret, "GATE-IN-01", ts, nonce, body);
            return new CameraAnprWebhookEnvelope("GATE-IN-01", ts, nonce, signature, body);
        }

        private sealed class PlainTextPiiEncryptionService : IPiiEncryptionService
        {
            public string Encrypt(string plainText) => plainText;
            public string Decrypt(string cipherText) => cipherText;
            public string? EncryptIfNotEmpty(string? value) => value;
            public string? DecryptIfNotEmpty(string? value) => value;
        }

        private sealed class NoopAuditTrailService : IAuditTrailService
        {
            public Task<AuditLog> RecordAsync(AuditLog log, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(log);
            }

            public Task<AuditChainVerificationResult> VerifyAsync(DateTime? from = null, DateTime? to = null, string? entityType = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new AuditChainVerificationResult(true, 0, null, "Noop"));
            }

            public string ComputeRecordHash(AuditLog log) => "";
        }
    }
}
