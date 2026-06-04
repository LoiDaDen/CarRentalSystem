using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using CarRentalSystem.Data;
using CarRentalSystem.Helpers;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface ICameraAnprService
    {
        Task<CameraAnprWebhookResult> HandleWebhookAsync(CameraAnprWebhookEnvelope envelope, CancellationToken cancellationToken = default);
        string GenerateWebhookSecret();
        string ProtectWebhookSecret(string rawSecret);
    }

    public sealed record CameraAnprWebhookEnvelope(
        string? CameraCode,
        string? Timestamp,
        string? Nonce,
        string? Signature,
        string RawBody);

    public sealed record CameraAnprWebhookResult(
        CameraAnprWebhookOutcome Outcome,
        string Message,
        int? CameraGateEventID = null,
        string? MatchStatus = null);

    public enum CameraAnprWebhookOutcome
    {
        Accepted,
        Duplicate,
        BadRequest,
        Unauthorized,
        Forbidden
    }

    public sealed class CameraAnprEventRequest
    {
        public string? ProviderEventId { get; set; }
        public string? LicensePlate { get; set; }
        public string? Direction { get; set; }
        public DateTime? CapturedAt { get; set; }
        public decimal? Confidence { get; set; }
        public string? SnapshotUrl { get; set; }
    }

    public class CameraAnprService : ICameraAnprService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IPiiEncryptionService _piiEncryption;
        private readonly IAuditTrailService _auditTrail;
        private readonly ILogger<CameraAnprService> _logger;

        public CameraAnprService(
            ApplicationDbContext context,
            IConfiguration configuration,
            IPiiEncryptionService piiEncryption,
            IAuditTrailService auditTrail,
            ILogger<CameraAnprService> logger)
        {
            _context = context;
            _configuration = configuration;
            _piiEncryption = piiEncryption;
            _auditTrail = auditTrail;
            _logger = logger;
        }

        public string GenerateWebhookSecret()
        {
            return "cam_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        }

        public string ProtectWebhookSecret(string rawSecret)
        {
            if (string.IsNullOrWhiteSpace(rawSecret))
                throw new ArgumentException("Webhook secret must not be empty.", nameof(rawSecret));

            return _piiEncryption.Encrypt(rawSecret.Trim());
        }

        public async Task<CameraAnprWebhookResult> HandleWebhookAsync(CameraAnprWebhookEnvelope envelope, CancellationToken cancellationToken = default)
        {
            var cameraCode = NormalizeCameraCode(envelope.CameraCode);
            if (string.IsNullOrWhiteSpace(cameraCode) ||
                string.IsNullOrWhiteSpace(envelope.Timestamp) ||
                string.IsNullOrWhiteSpace(envelope.Nonce) ||
                string.IsNullOrWhiteSpace(envelope.Signature) ||
                string.IsNullOrWhiteSpace(envelope.RawBody))
            {
                return new CameraAnprWebhookResult(CameraAnprWebhookOutcome.BadRequest, "Missing camera webhook headers or body.");
            }

            if (!TryParseTimestamp(envelope.Timestamp!, out var timestamp))
                return new CameraAnprWebhookResult(CameraAnprWebhookOutcome.BadRequest, "Invalid camera timestamp.");

            var maxSkewSeconds = _configuration.GetValue("Camera:MaxWebhookClockSkewSeconds", 300);
            if (Math.Abs((DateTimeOffset.UtcNow - timestamp).TotalSeconds) > maxSkewSeconds)
                return new CameraAnprWebhookResult(CameraAnprWebhookOutcome.Unauthorized, "Expired camera timestamp.");

            var camera = await _context.CameraDevices
                .Include(c => c.Location)
                .FirstOrDefaultAsync(c => c.Code == cameraCode, cancellationToken);

            if (camera == null)
                return new CameraAnprWebhookResult(CameraAnprWebhookOutcome.Unauthorized, "Unknown camera.");

            if (camera.Status != CameraDeviceStatusConstants.Active)
                return new CameraAnprWebhookResult(CameraAnprWebhookOutcome.Forbidden, "Camera is inactive.");

            var rawSecret = _piiEncryption.Decrypt(camera.EncryptedWebhookSecret);
            if (!IsValidSignature(rawSecret, cameraCode, envelope.Timestamp!.Trim(), envelope.Nonce!.Trim(), envelope.RawBody, envelope.Signature!))
                return new CameraAnprWebhookResult(CameraAnprWebhookOutcome.Unauthorized, "Invalid camera signature.");

            await RemoveExpiredNoncesAsync(cancellationToken);
            if (await _context.CameraWebhookNonces.AnyAsync(n => n.CameraDeviceID == camera.CameraDeviceID && n.Nonce == envelope.Nonce!.Trim(), cancellationToken))
                return new CameraAnprWebhookResult(CameraAnprWebhookOutcome.Unauthorized, "Replayed camera nonce.");

            CameraAnprEventRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<CameraAnprEventRequest>(envelope.RawBody, JsonOptions);
            }
            catch (JsonException)
            {
                return new CameraAnprWebhookResult(CameraAnprWebhookOutcome.BadRequest, "Invalid camera payload JSON.");
            }

            var validationError = ValidateRequest(request);
            if (validationError != null)
                return new CameraAnprWebhookResult(CameraAnprWebhookOutcome.BadRequest, validationError);

            var providerEventId = request!.ProviderEventId?.Trim();
            if (!string.IsNullOrWhiteSpace(providerEventId) &&
                await _context.CameraGateEvents.AnyAsync(e => e.CameraDeviceID == camera.CameraDeviceID && e.ProviderEventId == providerEventId, cancellationToken))
            {
                return new CameraAnprWebhookResult(CameraAnprWebhookOutcome.Duplicate, "Duplicate camera event ignored.");
            }

            _context.CameraWebhookNonces.Add(new CameraWebhookNonce
            {
                CameraDeviceID = camera.CameraDeviceID,
                Nonce = envelope.Nonce!.Trim(),
                Timestamp = timestamp.UtcDateTime,
                CreatedAt = DateTime.UtcNow
            });

            var normalizedPlate = LicensePlateNormalizer.Normalize(request.LicensePlate);
            var direction = NormalizeDirection(request.Direction, camera.DefaultDirection);
            var confidence = request.Confidence!.Value;
            var capturedAt = DateTime.SpecifyKind(request.CapturedAt!.Value, DateTimeKind.Utc);
            var minConfidence = _configuration.GetValue("Camera:MinPlateConfidence", 0.80m);

            var scope = await ResolveCameraScopeAsync(camera, cancellationToken);
            var car = await _context.Cars
                .FirstOrDefaultAsync(c => c.LicensePlateNormalized == normalizedPlate, cancellationToken);

            var match = await DetermineMatchAsync(camera, scope, car, confidence, minConfidence, cancellationToken);
            var gateEvent = new CameraGateEvent
            {
                CameraDeviceID = camera.CameraDeviceID,
                ProviderEventId = string.IsNullOrWhiteSpace(providerEventId) ? null : providerEventId,
                LicensePlateRaw = request.LicensePlate!.Trim(),
                LicensePlateNormalized = normalizedPlate,
                Direction = direction,
                Confidence = confidence,
                CapturedAt = capturedAt,
                ReceivedAt = DateTime.UtcNow,
                SnapshotUrl = request.SnapshotUrl?.Trim(),
                CarID = car?.CarID,
                AreaID = scope.AreaID,
                LocationID = scope.LocationID,
                MatchStatus = match.Status,
                FailureReason = match.Reason
            };

            _context.CameraGateEvents.Add(gateEvent);
            await _context.SaveChangesAsync(cancellationToken);

            if (match.Status == CameraGateMatchStatusConstants.Matched && car != null &&
                (direction == CameraGateDirectionConstants.In || direction == CameraGateDirectionConstants.Out))
            {
                await UpsertPresenceAsync(car.CarID, scope.AreaID, scope.LocationID, direction, capturedAt, gateEvent.CameraGateEventID, cancellationToken);
            }
            else if (match.Status == CameraGateMatchStatusConstants.AreaMismatch && car != null)
            {
                await CreateAreaMismatchAlertAsync(car, gateEvent, match.Reason, cancellationToken);
            }

            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "CameraGateEventReceived",
                EntityType = nameof(CameraGateEvent),
                EntityId = gateEvent.CameraGateEventID,
                NewValues = JsonSerializer.Serialize(new
                {
                    gateEvent.CameraDeviceID,
                    gateEvent.CarID,
                    gateEvent.AreaID,
                    gateEvent.LocationID,
                    gateEvent.Direction,
                    gateEvent.MatchStatus,
                    gateEvent.Confidence
                })
            }, cancellationToken);

            return new CameraAnprWebhookResult(CameraAnprWebhookOutcome.Accepted, "Camera event accepted.", gateEvent.CameraGateEventID, gateEvent.MatchStatus);
        }

        public static string ComputeSignature(string rawSecret, string cameraCode, string timestamp, string nonce, string rawBody)
        {
            var canonical = $"{NormalizeCameraCode(cameraCode)}.{timestamp.Trim()}.{nonce.Trim()}.{rawBody}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(rawSecret));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }

        private static bool IsValidSignature(string rawSecret, string cameraCode, string timestamp, string nonce, string rawBody, string signature)
        {
            var expectedHex = ComputeSignature(rawSecret, cameraCode, timestamp, nonce, rawBody);
            var supplied = signature.Trim();
            if (supplied.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
                supplied = supplied["sha256=".Length..];

            if (!IsHex(supplied) || supplied.Length != expectedHex.Length)
                return false;

            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expectedHex),
                Convert.FromHexString(supplied));
        }

        private static string? ValidateRequest(CameraAnprEventRequest? request)
        {
            if (request == null)
                return "Missing camera payload.";
            if (string.IsNullOrWhiteSpace(request.LicensePlate))
                return "License plate is required.";
            if (LicensePlateNormalizer.Normalize(request.LicensePlate).Length == 0)
                return "License plate is invalid.";
            if (!request.CapturedAt.HasValue)
                return "CapturedAt is required.";
            if (!request.Confidence.HasValue || request.Confidence.Value < 0m || request.Confidence.Value > 1m)
                return "Confidence must be between 0 and 1.";
            if (!string.IsNullOrWhiteSpace(request.SnapshotUrl) &&
                !Uri.TryCreate(request.SnapshotUrl, UriKind.Absolute, out _))
                return "SnapshotUrl must be an absolute URL.";
            return null;
        }

        private async Task<CameraScope> ResolveCameraScopeAsync(CameraDevice camera, CancellationToken cancellationToken)
        {
            var areaId = camera.AreaID;
            var locationId = camera.LocationID;
            var isValid = areaId.HasValue || locationId.HasValue;
            string? reason = null;

            if (locationId.HasValue)
            {
                var mappedAreaId = await _context.AreaLocations
                    .Where(al => al.LocationID == locationId.Value)
                    .Select(al => (int?)al.AreaID)
                    .FirstOrDefaultAsync(cancellationToken);

                if (!areaId.HasValue)
                    areaId = mappedAreaId;
                else if (mappedAreaId.HasValue && mappedAreaId.Value != areaId.Value)
                {
                    isValid = false;
                    reason = "Camera location is not mapped to the configured area.";
                }
            }

            return new CameraScope(areaId, locationId, isValid, reason);
        }

        private async Task<CameraMatch> DetermineMatchAsync(CameraDevice camera, CameraScope scope, Car? car, decimal confidence, decimal minConfidence, CancellationToken cancellationToken)
        {
            if (!scope.IsValid)
                return new CameraMatch(CameraGateMatchStatusConstants.InvalidScope, scope.FailureReason);
            if (confidence < minConfidence)
                return new CameraMatch(CameraGateMatchStatusConstants.LowConfidence, $"Confidence {confidence:0.0000} is below threshold {minConfidence:0.0000}.");
            if (car == null)
                return new CameraMatch(CameraGateMatchStatusConstants.UnknownPlate, "No active car matched the detected license plate.");

            if (scope.LocationID.HasValue && car.LocationID != scope.LocationID.Value)
                return new CameraMatch(CameraGateMatchStatusConstants.AreaMismatch, $"Car belongs to location {car.LocationID}, but camera is configured for location {scope.LocationID.Value}.");

            if (scope.AreaID.HasValue)
            {
                var carInArea = await _context.AreaLocations
                    .AnyAsync(al => al.AreaID == scope.AreaID.Value && al.LocationID == car.LocationID, cancellationToken);
                if (!carInArea)
                    return new CameraMatch(CameraGateMatchStatusConstants.AreaMismatch, $"Car location {car.LocationID} is not mapped to area {scope.AreaID.Value}.");
            }

            return new CameraMatch(CameraGateMatchStatusConstants.Matched, null);
        }

        private async Task UpsertPresenceAsync(int carId, int? areaId, int? locationId, string direction, DateTime eventAt, int eventId, CancellationToken cancellationToken)
        {
            var presence = await _context.VehicleAreaPresences
                .FirstOrDefaultAsync(p => p.CarID == carId && p.AreaID == areaId && p.LocationID == locationId, cancellationToken);

            if (presence == null)
            {
                presence = new VehicleAreaPresence
                {
                    CarID = carId,
                    AreaID = areaId,
                    LocationID = locationId
                };
                _context.VehicleAreaPresences.Add(presence);
            }

            presence.IsInside = direction == CameraGateDirectionConstants.In;
            presence.LastDirection = direction;
            presence.LastEventAt = eventAt;
            presence.LastCameraGateEventID = eventId;
            presence.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task CreateAreaMismatchAlertAsync(Car car, CameraGateEvent gateEvent, string? reason, CancellationToken cancellationToken)
        {
            _context.Alerts.Add(new Alert
            {
                CarID = car.CarID,
                AlertType = "CameraAreaMismatch",
                Status = "Active",
                AlertTime = DateTime.UtcNow,
                Description = $"Camera ANPR mismatch for plate {gateEvent.LicensePlateRaw}. {reason}"
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task RemoveExpiredNoncesAsync(CancellationToken cancellationToken)
        {
            var cutoff = DateTime.UtcNow.AddDays(-1);
            var expired = await _context.CameraWebhookNonces
                .Where(n => n.CreatedAt < cutoff)
                .ToListAsync(cancellationToken);
            if (expired.Count == 0)
                return;

            _context.CameraWebhookNonces.RemoveRange(expired);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static bool TryParseTimestamp(string value, out DateTimeOffset timestamp)
        {
            value = value.Trim();
            if (long.TryParse(value, out var unixSeconds))
            {
                timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                return true;
            }

            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out timestamp);
        }

        private static string NormalizeDirection(string? requestedDirection, string defaultDirection)
        {
            var direction = string.IsNullOrWhiteSpace(requestedDirection) ? defaultDirection : requestedDirection;
            return direction.Trim().ToLowerInvariant() switch
            {
                "in" or "entry" or "enter" or "vao" or "vào" => CameraGateDirectionConstants.In,
                "out" or "exit" or "leave" or "ra" => CameraGateDirectionConstants.Out,
                _ => CameraGateDirectionConstants.Unknown
            };
        }

        private static string NormalizeCameraCode(string? cameraCode)
        {
            return string.IsNullOrWhiteSpace(cameraCode) ? string.Empty : cameraCode.Trim().ToUpperInvariant();
        }

        private static bool IsHex(string value)
        {
            return value.All(c =>
                c is >= '0' and <= '9' ||
                c is >= 'a' and <= 'f' ||
                c is >= 'A' and <= 'F');
        }

        private sealed record CameraScope(int? AreaID, int? LocationID, bool IsValid, string? FailureReason);
        private sealed record CameraMatch(string Status, string? Reason);
    }
}
