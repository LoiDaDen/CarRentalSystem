using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TrackingController : Controller
    {
        private readonly ITrackingService _trackingService;
        private readonly ICarService _carService;

        public TrackingController(ITrackingService trackingService, ICarService carService)
        {
            _trackingService = trackingService;
            _carService = carService;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Map()
        {
            var cars = await _carService.GetAllCarsAsync();
            return View(cars);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Connect()
        {
            var cars = await _carService.GetAllCarsAsync();
            return View(cars);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> ReceiveData([FromBody] VehicleTracking tracking)
        {
            if (tracking == null || tracking.CarID == 0) return BadRequest("Dữ liệu không hợp lệ");

            tracking.Timestamp = DateTime.UtcNow;
            await _trackingService.AddTrackingAsync(tracking);
            
            // Tự động kiểm tra cảnh báo (Off-route, Suspicious Stop)
            await _trackingService.CheckForAlertsAsync(tracking.CarID, tracking.Latitude, tracking.Longitude);
            
            return Ok(new { message = "Đã nhận dữ liệu thành công" });
        }
        
        [HttpGet]
        public async Task<IActionResult> GetCarLocation(int carId)
        {
            var location = await _trackingService.GetLatestLocationAsync(carId);
            return Json(location);
        }
    }
}
