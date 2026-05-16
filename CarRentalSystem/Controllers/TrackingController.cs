using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TrackingController : Controller
    {
        private readonly ITrackingService _trackingService;
        private readonly ICarService _carService;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<TrackingController> _logger;

        public TrackingController(
            ITrackingService trackingService,
            ICarService carService,
            ApplicationDbContext context,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<TrackingController> logger)
        {
            _trackingService = trackingService;
            _carService = carService;
            _context = context;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        public async Task<IActionResult> Map()
        {
            var cars = await _carService.GetAllCarsAsync();
            return View(cars);
        }

        public async Task<IActionResult> Connect()
        {
            var cars = await _carService.GetAllCarsAsync();
            return View(cars);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> ReceiveData([FromBody] VehicleTracking tracking)
        {
            if (tracking == null || tracking.CarID == 0) return BadRequest("Invalid tracking payload.");
            var signature = await ValidateDeviceSignatureAsync(tracking);
            if (!signature.IsValid)
                return Unauthorized(new { message = "Invalid tracking device signature" });

            if (!await RecordNonceAsync(signature.DeviceId!, signature.Nonce!, signature.TimestampUnixSeconds, tracking.CarID))
                return Unauthorized(new { message = "Invalid tracking device signature" });

            tracking.Timestamp = DateTime.UtcNow;
            await _trackingService.AddTrackingAsync(tracking);
            await _trackingService.CheckForAlertsAsync(tracking.CarID, tracking.Latitude, tracking.Longitude);
            return Ok(new { message = "Tracking payload accepted." });
        }

        [HttpGet]
        public async Task<IActionResult> GetCarLocation(int carId)
        {
            var location = await _trackingService.GetLatestLocationAsync(carId);
            return Json(location);
        }

        private async Task<DeviceSignatureValidation> ValidateDeviceSignatureAsync(VehicleTracking tracking)
        {
            var secret = _configuration["Tracking:IngestSecret"];
            if (string.IsNullOrWhiteSpace(secret))
                return _environment.IsDevelopment()
                    ? DeviceSignatureValidation.Valid("development", Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    : DeviceSignatureValidation.Invalid;

            var deviceId = Request.Headers["X-Device-Id"].ToString();
            var timestampText = Request.Headers["X-Timestamp"].ToString();
            var nonce = Request.Headers["X-Nonce"].ToString();
            var signature = Request.Headers["X-Signature"].ToString();
            if (string.IsNullOrWhiteSpace(deviceId) ||
                string.IsNullOrWhiteSpace(timestampText) ||
                string.IsNullOrWhiteSpace(nonce) ||
                string.IsNullOrWhiteSpace(signature))
            {
                return DeviceSignatureValidation.Invalid;
            }

            if (!long.TryParse(timestampText, out var unixSeconds))
                return DeviceSignatureValidation.Invalid;

            var sentAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            if (Math.Abs((DateTimeOffset.UtcNow - sentAt).TotalMinutes) > 5)
                return DeviceSignatureValidation.Invalid;

            if (await _context.TrackingIngestNonces.AnyAsync(n => n.DeviceId == deviceId && n.Nonce == nonce))
            {
                _logger.LogWarning("Rejected replayed tracking payload for car {CarId} from device {DeviceId}.", tracking.CarID, deviceId);
                return DeviceSignatureValidation.Invalid;
            }

            var payload = $"{deviceId}.{timestampText}.{nonce}.{tracking.CarID}.{tracking.Latitude:F6}.{tracking.Longitude:F6}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var actualBytes = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
            var isValid = expectedBytes.Length == actualBytes.Length &&
                CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
            if (!isValid)
                _logger.LogWarning("Rejected tracking payload for car {CarId} from device {DeviceId}.", tracking.CarID, deviceId);

            return isValid
                ? DeviceSignatureValidation.Valid(deviceId, nonce, unixSeconds)
                : DeviceSignatureValidation.Invalid;
        }

        private async Task<bool> RecordNonceAsync(string deviceId, string nonce, long timestampUnixSeconds, int carId)
        {
            var cutoff = DateTime.UtcNow.AddHours(-24);
            var expiredNonces = await _context.TrackingIngestNonces
                .Where(n => n.CreatedAt < cutoff)
                .ToListAsync();
            if (expiredNonces.Count > 0)
                _context.TrackingIngestNonces.RemoveRange(expiredNonces);

            _context.TrackingIngestNonces.Add(new TrackingIngestNonce
            {
                DeviceId = deviceId,
                Nonce = nonce,
                TimestampUnixSeconds = timestampUnixSeconds,
                CarID = carId,
                CreatedAt = DateTime.UtcNow
            });
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Rejected replayed tracking nonce for car {CarId} from device {DeviceId}.", carId, deviceId);
                return false;
            }
        }

        private sealed record DeviceSignatureValidation(bool IsValid, string? DeviceId, string? Nonce, long TimestampUnixSeconds)
        {
            public static DeviceSignatureValidation Invalid { get; } = new(false, null, null, 0);
            public static DeviceSignatureValidation Valid(string deviceId, string nonce, long timestampUnixSeconds) =>
                new(true, deviceId, nonce, timestampUnixSeconds);
        }
    }
}
