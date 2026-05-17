using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CarRentalSystem.Services;
using CarRentalSystem.Areas.Admin.Models;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using CarRentalSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ICarService _carService;
        private readonly IBookingService _bookingService;
        private readonly ApplicationDbContext _context;

        public DashboardController(
            ICarService carService,
            IBookingService bookingService,
            ApplicationDbContext context)
        {
            _carService = carService;
            _bookingService = bookingService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var hiddenStatuses = new[] { "Draft", "Unpaid" };
            var revenueStatuses = new[] { "Completed", "Confirmed" };

            var bookingsQuery = _context.Bookings
                .AsNoTracking()
                .Include(b => b.BookingStatus)
                .Where(b => b.BookingStatus == null || !hiddenStatuses.Contains(b.BookingStatus.Name));

            var viewModel = new DashboardViewModel
            {
                TotalCars = await _context.Cars.AsNoTracking().CountAsync(c => !c.IsDeleted),
                AvailableCars = await _context.Cars.AsNoTracking().CountAsync(c => !c.IsDeleted && c.CarStatus != null && c.CarStatus.Name == "Available"),
                RentedCars = await _context.Cars.AsNoTracking().CountAsync(c => !c.IsDeleted && c.CarStatus != null && c.CarStatus.Name == "Rented"),
                TotalBookings = await bookingsQuery.CountAsync(),
                TotalRevenue = await bookingsQuery.Where(b => b.BookingStatus != null && revenueStatuses.Contains(b.BookingStatus.Name)).SumAsync(b => b.TotalPrice),
                TotalHeldDepositsAmount = await bookingsQuery.Where(b => b.DepositStatus == "Holding" && (b.BookingStatus == null || b.BookingStatus.Name != "Completed")).SumAsync(b => b.DepositAmount),
                TotalHeldDepositsCount = await bookingsQuery.CountAsync(b => b.DepositStatus == "Holding" && (b.BookingStatus == null || b.BookingStatus.Name != "Completed")),
                RecentBookings = await bookingsQuery
                    .Include(b => b.User)
                    .Include(b => b.Car)
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(7)
                    .ToListAsync(),
                StatusGroups = await bookingsQuery
                    .GroupBy(b => b.BookingStatus != null ? b.BookingStatus.Name : "Unknown")
                    .ToDictionaryAsync(g => g.Key, g => g.Count())
            };

            var last6Months = Enumerable.Range(0, 6)
                .Select(i => DateTime.UtcNow.AddMonths(-i))
                .OrderBy(d => d)
                .ToList();

            viewModel.RevenueLabels = last6Months.Select(d => d.ToString("MM/yyyy")).ToList();
            var revenueStart = new DateTime(last6Months.First().Year, last6Months.First().Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var revenueRows = await bookingsQuery
                .Where(b => b.CreatedAt >= revenueStart && b.BookingStatus != null && revenueStatuses.Contains(b.BookingStatus.Name))
                .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Revenue = g.Sum(b => b.TotalPrice) })
                .ToListAsync();
            viewModel.RevenueData = last6Months
                .Select(d => (revenueRows.FirstOrDefault(r => r.Year == d.Year && r.Month == d.Month)?.Revenue ?? 0m) / 1000000)
                .ToList();

            var topCarIds = await bookingsQuery.Where(b => b.BookingStatus != null && b.BookingStatus.Name == "Completed")
                .GroupBy(b => b.CarID)
                .Select(g => new {
                    CarID = g.Key,
                    RentalCount = g.Count(),
                    TotalRevenue = g.Sum(b => b.TotalPrice)
                })
                .OrderByDescending(x => x.RentalCount)
                .Take(3)
                .ToListAsync();
            var topCarLookup = await _context.Cars.AsNoTracking()
                .Include(c => c.CarBrand)
                .Where(c => topCarIds.Select(t => t.CarID).Contains(c.CarID))
                .ToDictionaryAsync(c => c.CarID);
            viewModel.TopCars = topCarIds
                .Where(x => topCarLookup.ContainsKey(x.CarID))
                .Select(x => new TopCarViewModel
                {
                    Car = topCarLookup[x.CarID],
                    RentalCount = x.RentalCount,
                    TotalRevenue = x.TotalRevenue
                })
                .ToList();

            ViewBag.MaintenanceCostYear = 0;
            ViewBag.MaintenanceLogs = new List<CarRentalSystem.Models.MaintenanceLog>();
            ViewBag.TopCustomers = new List<TopCustomerViewModel>();

            viewModel.BookingLabels = viewModel.StatusGroups.Keys.ToList();
            viewModel.BookingData = viewModel.StatusGroups.Values.ToList();

            var carGroups = await _context.Cars.AsNoTracking()
                .Where(c => !c.IsDeleted)
                .GroupBy(c => c.CarStatus != null ? c.CarStatus.Name : "Unknown")
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            viewModel.CarLabels = carGroups.Select(g => g.Status).ToList();
            viewModel.CarData = carGroups.Select(g => g.Count).ToList();

            return View(viewModel);
        }
    }
}
