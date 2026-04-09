using CarRentalSystem.Models;
using CarRentalSystem.Services;
using System;
using System.Threading.Tasks;

namespace CarRentalSystem.Helpers
{
    public static class DistanceCalculator
    {
        // Haversine formula
        public static double CalculateDistanceInKm(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Radius of the earth in km
            var dLat = Deg2Rad(lat2 - lat1);
            var dLon = Deg2Rad(lon2 - lon1);
            var a =
              Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
              Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) *
              Math.Sin(dLon / 2) * Math.Sin(dLon / 2)
              ;
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c; // Distance in km
            return d;
        }

        private static double Deg2Rad(double deg)
        {
            return deg * (Math.PI / 180);
        }
    }

    public class SecurityMonitorService
    {
        private readonly ITrackingService _trackingService;

        public SecurityMonitorService(ITrackingService trackingService)
        {
            _trackingService = trackingService;
        }

        public async Task CheckRouteDeviationAsync(VehicleTracking currentPoint, Models.Route allowedRoute)
        {
            // Simplified: check if distance to start or end > 5km as a basic proxy for "off route"
            // For a real app, parse allowedRoute.RouteData (JSON array of lat/lngs) and find shortest distance from currentPoint to the polyline.
            
            // Simulation logic
            bool isOffRoute = false; 

            if (isOffRoute)
            {
                await TriggerAlertAsync(currentPoint.CarID, "OffRoute", "Vehicle is more than 5km away from the allowed route.");
            }
        }

        public async Task CheckTheftRisksAsync(VehicleTracking tracking)
        {
            // Simulate Suspicious Stop
            if (tracking.Speed == 0)
            {
                // Logic to check if stopped for > 2 hours
                // await TriggerAlertAsync(tracking.CarID, "SuspiciousStop", "Vehicle stopped for extended period in unauthorized zone.");
            }

            // Simulate GPS Signal Lost
            // If latest tracking timestamp is > 1 hour old, trigger "GpsLost"
        }

        private async Task TriggerAlertAsync(int carId, string alertType, string description)
        {
            var alert = new Alert
            {
                CarID = carId,
                AlertType = alertType,
                Description = description,
                Status = "Active",
                AlertTime = DateTime.Now
            };
            await _trackingService.AddAlertAsync(alert);
        }
    }
}
