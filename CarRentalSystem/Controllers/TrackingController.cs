using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Data;
using CarRentalSystem.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Globalization;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class TrackingController : Controller
    {
        private const int HistoryRetentionDays = 90;
        private const int MaxDailyHistoryPoints = 5000;
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

        [Authorize(Roles = UserRoleConstants.InternalStaff)]
        public async Task<IActionResult> Map()
        {
            var cars = await _carService.GetAllCarsAsync();
            return View(cars);
        }

        [Authorize(Roles = UserRoleConstants.InternalStaff)]
        public async Task<IActionResult> Connect()
        {
            var cars = await _carService.GetAllCarsAsync();
            return View(cars);
        }

        [Authorize(Roles = UserRoleConstants.InternalStaff)]
        [HttpGet]
        public async Task<IActionResult> History(int? carId, string? date)
        {
            var cars = await _carService.GetAllCarsAsync();
            var today = GetLocalToday();

            ViewBag.SelectedCarId = carId;
            ViewBag.SelectedDate = TryParseLocalDate(date, out var selectedDate)
                ? selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ViewBag.MinDate = today.AddDays(-HistoryRetentionDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ViewBag.MaxDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ViewBag.HistoryRetentionDays = HistoryRetentionDays;

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
            await _trackingService.CheckForAlertsAsync(tracking.CarID, tracking.Latitude, tracking.Longitude, tracking.Speed);
            await BroadcastTrackingForCarAsync(tracking.CarID);
            return Ok(new { message = "Tracking payload accepted." });
        }

        [HttpGet]
        public async Task<IActionResult> Booking(int id)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var booking = await LoadBookingForAccessAsync(id, userId, allowTrackingUpdate: false);
            if (booking == null) return Forbid();

            return View(booking);
        }

        [HttpGet]
        public async Task<IActionResult> Mobile(int bookingId)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var booking = await LoadBookingForAccessAsync(bookingId, userId, allowTrackingUpdate: true);
            if (booking == null) return Forbid();

            return View(booking);
        }

        [HttpGet]
        public async Task<IActionResult> Snapshot(int bookingId)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var booking = await LoadBookingForAccessAsync(bookingId, userId, allowTrackingUpdate: false);
            if (booking == null) return Forbid();

            return Json(await BuildTrackingSnapshotAsync(booking));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MobileUpdate([FromBody] MobileTrackingUpdateRequest request)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
            if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
                return BadRequest(new { message = "Invalid coordinates." });

            var booking = await LoadBookingForAccessAsync(request.BookingId, userId, allowTrackingUpdate: true);
            if (booking?.Car == null) return Forbid();

            var tracking = new VehicleTracking
            {
                CarID = booking.CarID,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Speed = request.Speed,
                Timestamp = DateTime.UtcNow,
                EngineStatus = "On",
                Source = "OwnerMobileWeb",
                DeviceLabel = User.Identity?.Name ?? "Owner mobile"
            };

            await _trackingService.AddTrackingAsync(tracking);
            await _trackingService.CheckForAlertsAsync(tracking.CarID, tracking.Latitude, tracking.Longitude, tracking.Speed);

            var snapshot = await BuildTrackingSnapshotAsync(booking);
            var hub = HttpContext.RequestServices.GetService<IHubContext<TrackingHub>>();
            if (hub != null)
            {
                await hub.Clients.Group(TrackingHub.BookingGroup(booking.BookingID)).SendAsync("trackingSnapshot", snapshot);
                await hub.Clients.Group(TrackingHub.CarGroup(tracking.CarID)).SendAsync("carLocation", BuildCarLocationPayload(tracking));
            }

            return Json(snapshot);
        }

        [Authorize(Roles = UserRoleConstants.InternalStaff)]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> StaffMobileUpdate([FromBody] StaffMobileTrackingUpdateRequest request)
        {
            if (request.CarId <= 0)
                return BadRequest(new { message = "Car is required." });

            if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
                return BadRequest(new { message = "Invalid coordinates." });

            var carExists = await _context.Cars.AnyAsync(c => c.CarID == request.CarId);
            if (!carExists)
                return NotFound(new { message = "Car not found." });

            var tracking = new VehicleTracking
            {
                CarID = request.CarId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Speed = Math.Max(0, request.Speed),
                AccuracyMeters = request.AccuracyMeters,
                Heading = request.Heading,
                BatteryLevel = ClampBatteryLevel(request.BatteryLevel),
                IsCharging = request.IsCharging,
                Timestamp = DateTime.UtcNow,
                EngineStatus = "On",
                Source = "StaffMobileWeb",
                DeviceLabel = BuildStaffDeviceLabel(request.DeviceLabel)
            };

            await _trackingService.AddTrackingAsync(tracking);
            await _trackingService.CheckForAlertsAsync(tracking.CarID, tracking.Latitude, tracking.Longitude, tracking.Speed);
            await BroadcastTrackingForCarAsync(tracking.CarID);

            return Json(BuildCarLocationPayload(tracking));
        }

        [Authorize(Roles = UserRoleConstants.InternalStaff)]
        [HttpGet]
        public async Task<IActionResult> GetCarLocation(int carId)
        {
            await _trackingService.CheckForGpsLostAsync(carId);
            var location = await _trackingService.GetLatestLocationAsync(carId);
            return Json(location == null ? null : BuildCarLocationPayload(location));
        }

        [Authorize(Roles = UserRoleConstants.InternalStaff)]
        [HttpGet]
        public async Task<IActionResult> GetCarHistory(int carId, int take = 200)
        {
            if (carId <= 0) return BadRequest(new { message = "Car is required." });

            await _trackingService.CheckForGpsLostAsync(carId);
            var history = (await _trackingService.GetCarHistoryAsync(carId, take))
                .OrderBy(t => t.Timestamp)
                .Select(BuildCarLocationPayload)
                .ToList();

            return Json(history);
        }

        [Authorize(Roles = UserRoleConstants.InternalStaff)]
        [HttpGet]
        public async Task<IActionResult> GetCarHistoryByDate(int carId, string date)
        {
            if (carId <= 0) return BadRequest(new { message = "Car is required." });
            if (!TryParseLocalDate(date, out var localDate))
                return BadRequest(new { message = "Invalid date. Use yyyy-MM-dd." });

            var dateValidation = ValidateHistoryDate(localDate);
            if (dateValidation != null)
                return BadRequest(dateValidation);

            var car = await _context.Cars
                .AsNoTracking()
                .Include(c => c.CarBrand)
                .FirstOrDefaultAsync(c => c.CarID == carId);
            if (car == null) return NotFound(new { message = "Car not found." });

            var (fromUtc, toUtc) = GetUtcRangeForLocalDate(localDate);
            await _trackingService.CheckForGpsLostAsync(carId);

            var rows = (await _trackingService.GetCarHistoryAsync(carId, fromUtc, toUtc, MaxDailyHistoryPoints))
                .ToList();
            var alerts = (await _trackingService.GetAlertsAsync(carId, null, null, fromUtc, toUtc))
                .ToList();

            return Json(new
            {
                car = new
                {
                    carId = car.CarID,
                    name = $"{car.CarBrand?.Name} {car.CarName}".Trim(),
                    licensePlate = car.LicensePlate
                },
                date = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                fromUtc,
                toUtc,
                summary = BuildHistorySummary(rows, alerts.Count),
                points = rows.Select(BuildCarLocationPayload).ToList(),
                alerts = alerts.Select(BuildAlertPayload).ToList()
            });
        }

        [Authorize(Roles = UserRoleConstants.InternalStaff)]
        [HttpGet]
        public async Task<IActionResult> GetAlerts(int? carId, string? type, string? status, string? from, string? to)
        {
            var fromUtc = TryParseLocalDate(from, out var fromDate)
                ? GetUtcRangeForLocalDate(fromDate).FromUtc
                : (DateTime?)null;
            var toUtc = TryParseLocalDate(to, out var toDate)
                ? GetUtcRangeForLocalDate(toDate).ToUtc
                : (DateTime?)null;

            type = NormalizeFilterValue(type);
            status = NormalizeFilterValue(status);

            var alerts = await _trackingService.GetAlertsAsync(carId, type, status, fromUtc, toUtc);
            return Json(new
            {
                alerts = alerts.Select(BuildAlertPayload).ToList()
            });
        }

        private async Task BroadcastTrackingForCarAsync(int carId)
        {
            var services = HttpContext?.RequestServices;
            if (services == null) return;

            var hub = services.GetService<IHubContext<TrackingHub>>();
            if (hub == null) return;

            var now = DateTime.UtcNow;
            var bookings = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.Car).ThenInclude(c => c!.Location)
                .Include(b => b.Car).ThenInclude(c => c!.CarBrand)
                .Where(b => b.CarID == carId &&
                    b.BookingStatus != null &&
                    (b.BookingStatus.Name == BookingStatusConstants.Confirmed || b.BookingStatus.Name == BookingStatusConstants.Active) &&
                    b.StartDate <= now.AddDays(2) &&
                    b.EndDate >= now.AddHours(-6))
                .ToListAsync();

            foreach (var booking in bookings)
            {
                var snapshot = await BuildTrackingSnapshotAsync(booking);
                await hub.Clients.Group(TrackingHub.BookingGroup(booking.BookingID)).SendAsync("trackingSnapshot", snapshot);
            }

            var latest = await _trackingService.GetLatestLocationAsync(carId);
            if (latest != null)
                await hub.Clients.Group(TrackingHub.CarGroup(carId)).SendAsync("carLocation", BuildCarLocationPayload(latest));
        }

        private async Task<Booking?> LoadBookingForAccessAsync(int bookingId, int userId, bool allowTrackingUpdate)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.Car).ThenInclude(c => c!.Location)
                .Include(b => b.Car).ThenInclude(c => c!.CarBrand)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null) return null;
            if (IsInternalUser()) return booking;

            if (allowTrackingUpdate)
                return booking.Car?.OwnerID == userId ? booking : null;

            return booking.UserID == userId || booking.Car?.OwnerID == userId ? booking : null;
        }

        private async Task<object> BuildTrackingSnapshotAsync(Booking booking)
        {
            var latest = await _trackingService.GetLatestLocationAsync(booking.CarID);
            var carBaseLat = booking.Car?.Latitude ?? booking.Car?.Location?.Latitude ?? 10.7769;
            var carBaseLng = booking.Car?.Longitude ?? booking.Car?.Location?.Longitude ?? 106.7009;
            var hasGps = latest != null;
            var lastUpdatedUtc = latest?.Timestamp;
            var isFresh = hasGps && (DateTime.UtcNow - latest!.Timestamp).TotalMinutes <= 2;
            var currentLat = latest?.Latitude ?? carBaseLat;
            var currentLng = latest?.Longitude ?? carBaseLng;
            var isOwnerDelivery = string.Equals(booking.DeliveryMode, DeliveryModeConstants.OwnerDelivery, StringComparison.OrdinalIgnoreCase)
                && booking.DeliveryLatitude.HasValue
                && booking.DeliveryLongitude.HasValue;
            var destinationLat = isOwnerDelivery ? booking.DeliveryLatitude!.Value : carBaseLat;
            var destinationLng = isOwnerDelivery ? booking.DeliveryLongitude!.Value : carBaseLng;
            var destinationAddress = isOwnerDelivery
                ? booking.DeliveryAddress
                : booking.Car?.CarAddress ?? booking.Car?.Location?.Name;
            var distanceKm = CalculateDistanceKm(currentLat, currentLng, destinationLat, destinationLng);
            var etaMinutes = distanceKm > 0 ? (int)Math.Ceiling(distanceKm / 30d * 60d) : 0;
            var currentLatText = currentLat.ToString(CultureInfo.InvariantCulture);
            var currentLngText = currentLng.ToString(CultureInfo.InvariantCulture);
            var openStreetMapUrl = $"https://www.openstreetmap.org/?mlat={currentLatText}&mlon={currentLngText}#map=16/{currentLatText}/{currentLngText}";

            return new
            {
                bookingId = booking.BookingID,
                carId = booking.CarID,
                carName = $"{booking.Car?.CarBrand?.Name} {booking.Car?.CarName}".Trim(),
                licensePlate = booking.Car?.LicensePlate,
                deliveryMode = isOwnerDelivery ? DeliveryModeConstants.OwnerDelivery : DeliveryModeConstants.CustomerPickup,
                bookingStatus = booking.BookingStatus?.Name,
                trackingStatus = !hasGps ? "fallback" : isFresh ? "live" : "stale",
                isLive = isFresh,
                lastUpdatedUtc,
                current = new
                {
                    lat = currentLat,
                    lng = currentLng,
                    speed = latest?.Speed ?? 0,
                    accuracyMeters = latest?.AccuracyMeters,
                    heading = latest?.Heading,
                    batteryLevel = latest?.BatteryLevel,
                    isCharging = latest?.IsCharging,
                    source = latest?.Source
                },
                destination = new { lat = destinationLat, lng = destinationLng, address = destinationAddress },
                distanceKm = Math.Round(distanceKm, 2),
                etaMinutes,
                quotedEtaMinutes = booking.DeliveryEtaMinutes,
                openStreetMapUrl,
                routeCoordinates = new[]
                {
                    new { lat = currentLat, lng = currentLng },
                    new { lat = destinationLat, lng = destinationLng }
                }
            };
        }

        private bool IsInternalUser()
        {
            return User.IsInRole(UserRoleConstants.Admin) ||
                   User.IsInRole(UserRoleConstants.Manager) ||
                   User.IsInRole(UserRoleConstants.Staff);
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdString, out userId);
        }

        private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double r = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180;

        public sealed class MobileTrackingUpdateRequest
        {
            public int BookingId { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double Speed { get; set; }
        }

        public sealed class StaffMobileTrackingUpdateRequest
        {
            public int CarId { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double Speed { get; set; }
            public double? AccuracyMeters { get; set; }
            public double? Heading { get; set; }
            public decimal? BatteryLevel { get; set; }
            public bool? IsCharging { get; set; }
            public string? DeviceLabel { get; set; }
        }

        private object BuildCarLocationPayload(VehicleTracking tracking)
        {
            var ageSeconds = Math.Max(0, (DateTime.UtcNow - tracking.Timestamp).TotalSeconds);
            var status = ageSeconds <= 120 ? "live" : ageSeconds <= 300 ? "stale" : "lost";

            return new
            {
                trackingId = tracking.TrackingID,
                carId = tracking.CarID,
                latitude = tracking.Latitude,
                longitude = tracking.Longitude,
                speed = tracking.Speed,
                accuracyMeters = tracking.AccuracyMeters,
                heading = tracking.Heading,
                batteryLevel = tracking.BatteryLevel,
                isCharging = tracking.IsCharging,
                source = tracking.Source,
                deviceLabel = tracking.DeviceLabel,
                engineStatus = tracking.EngineStatus,
                timestamp = tracking.Timestamp,
                status
            };
        }

        private object BuildHistorySummary(IReadOnlyList<VehicleTracking> rows, int alertsCount)
        {
            var distanceKm = 0d;
            for (var i = 1; i < rows.Count; i++)
            {
                distanceKm += CalculateDistanceKm(
                    rows[i - 1].Latitude,
                    rows[i - 1].Longitude,
                    rows[i].Latitude,
                    rows[i].Longitude);
            }

            return new
            {
                pointCount = rows.Count,
                distanceKm = Math.Round(distanceKm, 2),
                maxSpeed = rows.Count == 0 ? 0 : Math.Round(rows.Max(p => p.Speed), 1),
                averageSpeed = rows.Count == 0 ? 0 : Math.Round(rows.Average(p => p.Speed), 1),
                firstTimestamp = rows.FirstOrDefault()?.Timestamp,
                lastTimestamp = rows.LastOrDefault()?.Timestamp,
                alertsCount
            };
        }

        private object BuildAlertPayload(Alert alert)
        {
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(alert.AlertTime, DateTimeKind.Utc), GetTrackingTimeZone());
            var historyDate = localTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            return new
            {
                alertId = alert.AlertID,
                carId = alert.CarID,
                carName = $"{alert.Car?.CarBrand?.Name} {alert.Car?.CarName}".Trim(),
                licensePlate = alert.Car?.LicensePlate,
                alertType = alert.AlertType,
                alertTypeLabel = GetAlertTypeLabel(alert.AlertType),
                status = alert.Status,
                statusLabel = GetAlertStatusLabel(alert.Status),
                alertTime = alert.AlertTime,
                alertTimeLocalText = localTime.ToString("HH:mm - dd/MM/yyyy", CultureInfo.InvariantCulture),
                description = alert.Description,
                historyDate,
                historyUrl = Url.Action("History", "Tracking", new { carId = alert.CarID, date = historyDate })
            };
        }

        private object? ValidateHistoryDate(DateTime localDate)
        {
            var today = GetLocalToday();
            if (localDate.Date > today)
                return new { message = "Cannot query a future date." };

            if (localDate.Date < today.AddDays(-HistoryRetentionDays))
                return new { message = $"History lookup is limited to the last {HistoryRetentionDays} days." };

            return null;
        }

        private (DateTime FromUtc, DateTime ToUtc) GetUtcRangeForLocalDate(DateTime localDate)
        {
            var startLocal = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
            var endLocal = startLocal.AddDays(1);
            var timeZone = GetTrackingTimeZone();
            return (
                TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone),
                TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone));
        }

        private DateTime GetLocalToday()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetTrackingTimeZone()).Date;
        }

        private static bool TryParseLocalDate(string? value, out DateTime date)
        {
            return DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date);
        }

        private static string? NormalizeFilterValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return string.Equals(value, "all", StringComparison.OrdinalIgnoreCase) ? null : value;
        }

        private static string GetAlertTypeLabel(string alertType) => alertType switch
        {
            "OverSpeed" => "Quá tốc độ",
            "GpsLost" => "Mất tín hiệu GPS",
            "OffRoute" => "Rời khỏi khu vực",
            "SuspiciousStop" => "Dừng lâu bất thường",
            "TheftRisk" => "Nguy cơ mất trộm",
            "OverdueReturn" => "Quá hạn trả xe",
            "MaintenanceDue" => "Bảo dưỡng",
            "OilChangeDue" => "Thay dầu",
            "InspectionExpiring" => "Đăng kiểm",
            _ => alertType
        };

        private static string GetAlertStatusLabel(string status) => status switch
        {
            "New" => "Mới",
            "Active" => "Đang theo dõi",
            "Critical" => "Khẩn cấp",
            "Resolved" => "Đã xử lý",
            _ => status
        };

        private static TimeZoneInfo GetTrackingTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                }
                catch
                {
                    return TimeZoneInfo.Utc;
                }
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }

        private string BuildStaffDeviceLabel(string? requestedLabel)
        {
            var label = string.IsNullOrWhiteSpace(requestedLabel)
                ? User.Identity?.Name ?? "Staff mobile"
                : requestedLabel.Trim();

            return label.Length <= 120 ? label : label[..120];
        }

        private static decimal? ClampBatteryLevel(decimal? batteryLevel)
        {
            if (!batteryLevel.HasValue) return null;
            if (batteryLevel.Value < 0) return 0;
            if (batteryLevel.Value > 100) return 100;
            return decimal.Round(batteryLevel.Value, 2);
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
