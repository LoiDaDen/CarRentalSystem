using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
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
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<TrackingController> _logger;

        public TrackingController(
            ITrackingService trackingService,
            ICarService carService,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<TrackingController> logger)
        {
            _trackingService = trackingService;
            _carService = carService;
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
            if (!IsValidDeviceSignature(tracking))
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

        private bool IsValidDeviceSignature(VehicleTracking tracking)
        {
            var secret = _configuration["Tracking:IngestSecret"];
            if (string.IsNullOrWhiteSpace(secret))
                return _environment.IsDevelopment();

            var deviceId = Request.Headers["X-Device-Id"].ToString();
            var timestampText = Request.Headers["X-Timestamp"].ToString();
            var signature = Request.Headers["X-Signature"].ToString();
            if (string.IsNullOrWhiteSpace(deviceId) ||
                string.IsNullOrWhiteSpace(timestampText) ||
                string.IsNullOrWhiteSpace(signature))
            {
                return false;
            }

            if (!long.TryParse(timestampText, out var unixSeconds))
                return false;

            var sentAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            if (Math.Abs((DateTimeOffset.UtcNow - sentAt).TotalMinutes) > 5)
                return false;

            var payload = $"{deviceId}.{timestampText}.{tracking.CarID}.{tracking.Latitude:F6}.{tracking.Longitude:F6}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var actualBytes = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
            var isValid = expectedBytes.Length == actualBytes.Length &&
                CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
            if (!isValid)
                _logger.LogWarning("Rejected tracking payload for car {CarId} from device {DeviceId}.", tracking.CarID, deviceId);

            return isValid;
        }
    }
}
