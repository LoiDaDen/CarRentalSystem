using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin", "Manager", "Staff")]
    public class AreaBookingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AreaBookingController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub")
                     ?? User.FindFirst("UserID");
            return int.TryParse(claim?.Value, out var id) ? id : 0;
        }

        private string GetCurrentRole() =>
            User.FindFirst(ClaimTypes.Role)?.Value
         ?? User.FindFirst("role")?.Value ?? "";

        // GET: /Admin/AreaBooking
        public async Task<IActionResult> Index(string? status, string? search, string? date)
        {
            var role = GetCurrentRole();

            // Get areas this user can access
            if (role == "Admin" || role == "Manager")
            {
                var accessibleAreaIds = await _context.Areas
                    .Where(a => !a.IsDeleted)
                    .Select(a => a.AreaID)
                    .ToListAsync();

                var areaLocationIds = await _context.AreaLocations
                    .Where(al => accessibleAreaIds.Contains(al.AreaID) && !al.IsDeleted)
                    .Select(al => al.LocationID)
                    .ToListAsync();

                ViewBag.AreaLocationIds = areaLocationIds;
            }
            else // Staff
            {
                var userId = GetCurrentUserId();
                var staffAreas = await _context.AreaStaffs
                    .Where(asx => asx.UserID == userId && !asx.IsDeleted)
                    .Select(asx => asx.AreaID)
                    .ToListAsync();

                var areaLocationIds = await _context.AreaLocations
                    .Where(al => staffAreas.Contains(al.AreaID) && !al.IsDeleted)
                    .Select(al => al.LocationID)
                    .ToListAsync();

                ViewBag.AreaLocationIds = areaLocationIds;
            }

            var locIds = (List<int>)ViewBag.AreaLocationIds;

            // Build booking query
            var bookingQuery = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Car)
                    .ThenInclude(c => c!.CarBrand)
                .Include(b => b.PickupLocation)
                .Include(b => b.ReturnLocation)
                .Include(b => b.BookingStatus)
                .Where(b => !b.IsDeleted)
                .Where(b => locIds.Contains(b.PickupLocationID) ||
                            locIds.Contains(b.ReturnLocationID))
                .AsQueryable();

            // Filter by status
            if (!string.IsNullOrEmpty(status))
                bookingQuery = bookingQuery.Where(b => b.BookingStatus.Name == status);

            // Filter by search
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                bookingQuery = bookingQuery.Where(b =>
                    b.BookingID.ToString().ToLower().Contains(search) ||
                    (b.User != null && b.User.Name.ToLower().Contains(search)) ||
                    (b.User != null && b.User.Phone != null && b.User.Phone.Contains(search)) ||
                    (b.Car != null && b.Car.LicensePlate != null && b.Car.LicensePlate.ToLower().Contains(search)));
            }

            // Filter by date
            if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsedDate))
            {
                bookingQuery = bookingQuery.Where(b => b.StartDate.Date == parsedDate.Date);
            }

            var bookings = await bookingQuery
                .OrderByDescending(b => b.CreatedAt)
                .Take(200)
                .ToListAsync();

            // Fetch driver names for bookings with DriverBookingID
            var driverBookingIds = bookings.Where(b => b.DriverBookingID.HasValue).Select(b => b.DriverBookingID!.Value).ToList();
            var driverBookings = await _context.DriverBookings
                .Include(db => db.Driver)
                .Where(db => driverBookingIds.Contains(db.DriverBookingID))
                .ToDictionaryAsync(db => db.DriverBookingID, db => db.Driver);

            ViewBag.DriverMap = driverBookings;
            ViewBag.Status = status;
            ViewBag.Search = search;
            ViewBag.Date = date;
            ViewBag.CurrentRole = role;
            ViewBag.BookingStatuses = await _context.BookingStatuses.ToListAsync();

            return View(bookings);
        }
    }
}
