using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface ITrackingService
    {
        Task AddTrackingAsync(VehicleTracking tracking);
        Task<IEnumerable<VehicleTracking>> GetCarHistoryAsync(int carId, int take = 200);
        Task<IEnumerable<VehicleTracking>> GetCarHistoryAsync(int carId, DateTime fromUtc, DateTime toUtc, int take = 5000);
        Task<VehicleTracking> GetLatestLocationAsync(int carId);
        Task AddAlertAsync(Alert alert);
        Task<IEnumerable<Alert>> GetActiveAlertsAsync();
        Task<IEnumerable<Alert>> GetAlertsAsync(int? carId, string? alertType, string? status, DateTime? fromUtc, DateTime? toUtc);
        Task ResolveAlertAsync(int alertId);
        Task CheckForAlertsAsync(int carId, double currentLat, double currentLng, double? speed = null);
        Task CheckForGpsLostAsync(int carId);
    }

    public class TrackingService : ITrackingService
    {
        private const double OverSpeedThresholdKmh = 80d;
        private const double OffRouteRadiusKm = 5d;
        private static readonly TimeSpan GpsLostAfter = TimeSpan.FromMinutes(5);

        private readonly IRepository<VehicleTracking> _trackingRepo;
        private readonly IRepository<Alert> _alertRepo;
        private readonly ApplicationDbContext _context;

        public TrackingService(
            IRepository<VehicleTracking> trackingRepo,
            IRepository<Alert> alertRepo,
            ApplicationDbContext context)
        {
            _trackingRepo = trackingRepo;
            _alertRepo = alertRepo;
            _context = context;
        }

        public async Task AddTrackingAsync(VehicleTracking tracking)
        {
            await _trackingRepo.AddAsync(tracking);
            await _trackingRepo.SaveChangesAsync();
        }

        public async Task<IEnumerable<VehicleTracking>> GetCarHistoryAsync(int carId, int take = 200)
        {
            take = Math.Clamp(take, 1, 500);

            return await _trackingRepo.Query()
                .AsNoTracking()
                .Where(t => t.CarID == carId)
                .OrderByDescending(t => t.Timestamp)
                .Take(take)
                .ToListAsync();
        }

        public async Task<IEnumerable<VehicleTracking>> GetCarHistoryAsync(int carId, DateTime fromUtc, DateTime toUtc, int take = 5000)
        {
            take = Math.Clamp(take, 1, 5000);

            return await _trackingRepo.Query()
                .AsNoTracking()
                .Where(t => t.CarID == carId && t.Timestamp >= fromUtc && t.Timestamp < toUtc)
                .OrderBy(t => t.Timestamp)
                .Take(take)
                .ToListAsync();
        }

        public async Task<VehicleTracking> GetLatestLocationAsync(int carId)
        {
            return await _trackingRepo.Query()
                .AsNoTracking()
                .Where(t => t.CarID == carId)
                .OrderByDescending(t => t.Timestamp)
                .FirstOrDefaultAsync();
        }

        public async Task AddAlertAsync(Alert alert)
        {
            await _alertRepo.AddAsync(alert);
            await _alertRepo.SaveChangesAsync();
        }

        public async Task<IEnumerable<Alert>> GetActiveAlertsAsync()
        {
            return await _alertRepo.Query()
                .Include(a => a.Car).ThenInclude(c => c!.CarBrand)
                .Where(a => a.Status != "Resolved")
                .OrderByDescending(a => a.AlertTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Alert>> GetAlertsAsync(int? carId, string? alertType, string? status, DateTime? fromUtc, DateTime? toUtc)
        {
            var query = _alertRepo.Query()
                .AsNoTracking()
                .Include(a => a.Car).ThenInclude(c => c!.CarBrand)
                .AsQueryable();

            if (carId.HasValue && carId.Value > 0)
                query = query.Where(a => a.CarID == carId.Value);

            if (!string.IsNullOrWhiteSpace(alertType))
                query = query.Where(a => a.AlertType == alertType);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(a => a.Status == status);

            if (fromUtc.HasValue)
                query = query.Where(a => a.AlertTime >= fromUtc.Value);

            if (toUtc.HasValue)
                query = query.Where(a => a.AlertTime < toUtc.Value);

            return await query
                .OrderByDescending(a => a.AlertTime)
                .Take(500)
                .ToListAsync();
        }

        public async Task ResolveAlertAsync(int alertId)
        {
            var alert = await _alertRepo.GetByIdAsync(alertId);
            if (alert != null)
            {
                alert.Status = "Resolved";
                _alertRepo.Update(alert);
                await _alertRepo.SaveChangesAsync();
            }
        }

        public async Task CheckForAlertsAsync(int carId, double currentLat, double currentLng, double? speed = null)
        {
            await CheckSpeedAlertAsync(carId, speed);
            await CheckOffRouteAlertAsync(carId, currentLat, currentLng);
            await CheckSuspiciousStopAlertAsync(carId);
            await CheckForGpsLostAsync(carId);
        }

        public async Task CheckForGpsLostAsync(int carId)
        {
            var latest = await GetLatestLocationAsync(carId);
            if (latest == null)
                return;

            if (DateTime.UtcNow - latest.Timestamp > GpsLostAfter)
            {
                await AddAlertIfMissingAsync(
                    carId,
                    "GpsLost",
                    $"Vehicle has not sent GPS data for more than {GpsLostAfter.TotalMinutes:0} minutes.");
                return;
            }

            await ResolveOpenAlertsAsync(carId, "GpsLost");
        }

        private async Task CheckSpeedAlertAsync(int carId, double? speed)
        {
            if (!speed.HasValue)
                return;

            if (speed.Value > OverSpeedThresholdKmh)
            {
                await AddAlertIfMissingAsync(
                    carId,
                    "OverSpeed",
                    $"Vehicle speed is {speed.Value:0.0} km/h, above the {OverSpeedThresholdKmh:0} km/h limit.");
                return;
            }

            await ResolveOpenAlertsAsync(carId, "OverSpeed");
        }

        private async Task CheckOffRouteAlertAsync(int carId, double currentLat, double currentLng)
        {
            var car = await _context.Cars
                .AsNoTracking()
                .Include(c => c.Location)
                .FirstOrDefaultAsync(c => c.CarID == carId);

            if (car == null)
                return;

            var baseLat = car.Latitude ?? car.Location?.Latitude;
            var baseLng = car.Longitude ?? car.Location?.Longitude;
            if (!baseLat.HasValue || !baseLng.HasValue)
                return;

            var distanceKm = CalculateDistance(currentLat, currentLng, baseLat.Value, baseLng.Value);
            if (distanceKm > OffRouteRadiusKm)
            {
                await AddAlertIfMissingAsync(
                    carId,
                    "OffRoute",
                    $"Vehicle is {distanceKm:0.0} km away from its registered operating area.");
                return;
            }

            await ResolveOpenAlertsAsync(carId, "OffRoute");
        }

        private async Task CheckSuspiciousStopAlertAsync(int carId)
        {
            var lastTwoTrackings = await _trackingRepo.Query()
                .AsNoTracking()
                .Where(t => t.CarID == carId)
                .OrderByDescending(t => t.Timestamp)
                .Take(2)
                .ToListAsync();

            if (lastTwoTrackings.Count != 2)
                return;

            var timeDiff = (lastTwoTrackings[0].Timestamp - lastTwoTrackings[1].Timestamp).TotalHours;
            var dist = CalculateDistance(
                lastTwoTrackings[0].Latitude,
                lastTwoTrackings[0].Longitude,
                lastTwoTrackings[1].Latitude,
                lastTwoTrackings[1].Longitude);

            if (timeDiff > 2 && dist < 0.1)
            {
                await AddAlertIfMissingAsync(
                    carId,
                    "SuspiciousStop",
                    "Vehicle has not moved for over 2 hours.");
            }
        }

        private async Task AddAlertIfMissingAsync(int carId, string alertType, string description)
        {
            var exists = await _alertRepo.Query()
                .AnyAsync(a => a.CarID == carId && a.AlertType == alertType && a.Status != "Resolved");

            if (exists)
                return;

            await AddAlertAsync(new Alert
            {
                CarID = carId,
                AlertType = alertType,
                Status = "New",
                Description = description,
                AlertTime = DateTime.UtcNow
            });
        }

        private async Task ResolveOpenAlertsAsync(int carId, string alertType)
        {
            var alerts = await _alertRepo.Query()
                .Where(a => a.CarID == carId && a.AlertType == alertType && a.Status != "Resolved")
                .ToListAsync();

            if (alerts.Count == 0)
                return;

            foreach (var alert in alerts)
            {
                alert.Status = "Resolved";
                _alertRepo.Update(alert);
            }

            await _alertRepo.SaveChangesAsync();
        }

        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double radiusKm = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return radiusKm * c;
        }

        private static double ToRadians(double deg) => deg * (Math.PI / 180);
    }
}
