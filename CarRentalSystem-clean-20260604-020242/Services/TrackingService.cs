using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Services
{
    public interface ITrackingService
    {
        Task AddTrackingAsync(VehicleTracking tracking);
        Task<IEnumerable<VehicleTracking>> GetCarHistoryAsync(int carId);
        Task<VehicleTracking> GetLatestLocationAsync(int carId);
        Task AddAlertAsync(Alert alert);
        Task<IEnumerable<Alert>> GetActiveAlertsAsync();
        Task ResolveAlertAsync(int alertId);
        Task CheckForAlertsAsync(int carId, double currentLat, double currentLng);
    }

    public class TrackingService : ITrackingService
    {
        private readonly IRepository<VehicleTracking> _trackingRepo;
        private readonly IRepository<Alert> _alertRepo;

        public TrackingService(IRepository<VehicleTracking> trackingRepo, IRepository<Alert> alertRepo)
        {
            _trackingRepo = trackingRepo;
            _alertRepo = alertRepo;
        }

        public async Task AddTrackingAsync(VehicleTracking tracking)
        {
            await _trackingRepo.AddAsync(tracking);
            await _trackingRepo.SaveChangesAsync();
        }

        public async Task<IEnumerable<VehicleTracking>> GetCarHistoryAsync(int carId)
        {
            return await _trackingRepo.Query()
                .Where(t => t.CarID == carId)
                .OrderByDescending(t => t.Timestamp)
                .ToListAsync();
        }

        public async Task<VehicleTracking> GetLatestLocationAsync(int carId)
        {
            return await _trackingRepo.Query()
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
                .Include(a => a.Car)
                .Where(a => a.Status != "Resolved")
                .OrderByDescending(a => a.AlertTime)
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

        public async Task CheckForAlertsAsync(int carId, double currentLat, double currentLng)
        {
            var latestBooking = await _trackingRepo.Query()
                .Where(t => t.CarID == carId)
                .SelectMany(t => t.Car.Bookings)
                .Where(b => b.BookingStatus != null && b.BookingStatus.Name == "Confirmed")
                .OrderByDescending(b => b.StartDate)
                .Include(b => b.Route)
                .FirstOrDefaultAsync();

            if (latestBooking == null) return;

            // 1. Check for Off-Route
            if (latestBooking.Route != null && !string.IsNullOrEmpty(latestBooking.Route.RouteData))
            {
                // Simple version: check distance to Start/End if no complex path
                double distToStart = CalculateDistance(currentLat, currentLng, 10.762622, 106.660172); // Mock coords for demo
                if (distToStart > 5) 
                {
                    await AddAlertAsync(new Alert { CarID = carId, AlertType = "OffRoute", Status = "New", Description = "Vehicle is >5km away from planned route." });
                }
            }

            // 2. Check for Suspicious Stop (> 2 hours)
            var lastTwoTrackings = await _trackingRepo.Query()
                .Where(t => t.CarID == carId)
                .OrderByDescending(t => t.Timestamp)
                .Take(2)
                .ToListAsync();

            if (lastTwoTrackings.Count == 2)
            {
                var timeDiff = (lastTwoTrackings[0].Timestamp - lastTwoTrackings[1].Timestamp).TotalHours;
                var dist = CalculateDistance(lastTwoTrackings[0].Latitude, lastTwoTrackings[0].Longitude, lastTwoTrackings[1].Latitude, lastTwoTrackings[1].Longitude);
                
                if (timeDiff > 2 && dist < 0.1) // Hasn't moved more than 100m in 2 hours
                {
                    await AddAlertAsync(new Alert { CarID = carId, AlertType = "SuspiciousStop", Status = "New", Description = "Vehicle has not moved for over 2 hours." });
                }
            }
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Radius of the earth in km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double deg) => deg * (Math.PI / 180);
    }
}
