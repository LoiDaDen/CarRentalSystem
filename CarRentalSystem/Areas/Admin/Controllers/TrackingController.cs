using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class TrackingController : Controller
    {
        private readonly ITrackingService _trackingService;

        public TrackingController(ITrackingService trackingService)
        {
            _trackingService = trackingService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveData([FromBody] VehicleTracking tracking)
        {
            tracking.Timestamp = DateTime.UtcNow;
            await _trackingService.AddTrackingAsync(tracking);
            
            // Trigger alert check
            await _trackingService.CheckForAlertsAsync(tracking.CarID, tracking.Latitude, tracking.Longitude);
            
            return Ok();
        }

        [HttpGet]
        public IActionResult GetLiveLocations()
        {
            // Simulation logic: return slightly randomized coordinates near HCM
            var random = new Random();
            var locations = new[]
            {
                new { name = "Toyota Vios G", lat = 10.7769 + (random.NextDouble() - 0.5) * 0.01, lng = 106.7009 + (random.NextDouble() - 0.5) * 0.01, status = "Moving" },
                new { name = "Honda City RS", lat = 10.7589 + (random.NextDouble() - 0.5) * 0.01, lng = 106.6759 + (random.NextDouble() - 0.5) * 0.01, status = "Idle" },
                new { name = "Ford Raptor", lat = 10.7834 + (random.NextDouble() - 0.5) * 0.02, lng = 106.6644 + (random.NextDouble() - 0.5) * 0.02, status = "Moving" }
            };
            return Json(locations);
        }

        [HttpGet]
        public async Task<IActionResult> GetCarLocation(int carId)
        {
            var location = await _trackingService.GetLatestLocationAsync(carId);
            return Json(location);
        }
    }
}
