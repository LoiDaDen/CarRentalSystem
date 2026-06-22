using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Threading.Tasks;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = UserRoleConstants.InternalStaff)]
    public class AlertController : Controller
    {
        private readonly ITrackingService _trackingService;
        private readonly ICarService _carService;

        public AlertController(ITrackingService trackingService, ICarService carService)
        {
            _trackingService = trackingService;
            _carService = carService;
        }

        public async Task<IActionResult> Index(int? carId, string? type, string? status, string? from, string? to)
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
            ViewBag.Cars = await _carService.GetAllCarsAsync();
            ViewBag.SelectedCarId = carId;
            ViewBag.SelectedType = type ?? "all";
            ViewBag.SelectedStatus = status ?? "all";
            ViewBag.From = from;
            ViewBag.To = to;

            return View(alerts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id)
        {
            await _trackingService.ResolveAlertAsync(id);
            return RedirectToAction(nameof(Index));
        }

        private static (DateTime FromUtc, DateTime ToUtc) GetUtcRangeForLocalDate(DateTime localDate)
        {
            var startLocal = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
            var endLocal = startLocal.AddDays(1);
            var timeZone = GetTrackingTimeZone();
            return (
                TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone),
                TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone));
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
    }
}
