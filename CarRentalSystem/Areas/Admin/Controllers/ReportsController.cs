using CarRentalSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CarRentalSystem.Areas.Admin.Models;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            var bookings = await _context.Bookings
                .Include(b => b.Car)
                .Include(b => b.User)
                .Include(b => b.BookingStatus)
                .ToListAsync();

            var completedBookings = bookings.Where(b => b.BookingStatus?.Name == "Completed" || b.BookingStatus?.Name == "Confirmed").ToList();

            // KPI stats
            var monthRevenue = completedBookings
                .Where(b => b.CreatedAt >= startOfMonth)
                .Sum(b => b.TotalPrice);

            var newBookingsThisMonth = bookings.Count(b => b.CreatedAt >= startOfMonth);

            var cars = await _context.Cars.Include(c => c.CarStatus).ToListAsync();
            var maintenanceLogs = await _context.MaintenanceLogs.Include(m => m.Car).ToListAsync();

            // Revenue last 12 months
            var last12 = Enumerable.Range(0, 12)
                .Select(i => now.AddMonths(-11 + i))
                .ToList();

            var revenueLabels = last12.Select(d => d.ToString("MM/yyyy")).ToList();
            var revenueData = last12.Select(d =>
                completedBookings
                    .Where(b => b.CreatedAt.Month == d.Month && b.CreatedAt.Year == d.Year)
                    .Sum(b => b.TotalPrice) / 1000000m
            ).ToList();

            // Booking status breakdown
            var statusGroups = new Dictionary<string, int>
            {
                { "Pending", bookings.Count(b => b.BookingStatus?.Name == "Pending") },
                { "Confirmed", bookings.Count(b => b.BookingStatus?.Name == "Confirmed") },
                { "Completed", bookings.Count(b => b.BookingStatus?.Name == "Completed") },
                { "Cancelled", bookings.Count(b => b.BookingStatus?.Name == "Cancelled") }
            };

            // Top 5 cars
            var topCars = bookings
                .GroupBy(b => b.CarID)
                .Select(g => new TopCarViewModel
                {
                    Car = g.First().Car,
                    RentalCount = g.Count(),
                    TotalRevenue = g.Where(b => b.BookingStatus?.Name == "Completed" || b.BookingStatus?.Name == "Confirmed").Sum(b => b.TotalPrice)
                })
                .OrderByDescending(x => x.RentalCount)
                .Take(5)
                .ToList();

            // Top 5 customers
            var topCustomers = bookings
                .GroupBy(b => b.UserID)
                .Select(g => new TopCustomerViewModel
                {
                    User = g.First().User,
                    BookingCount = g.Count(),
                    TotalSpent = g.Where(b => b.BookingStatus?.Name == "Completed" || b.BookingStatus?.Name == "Confirmed").Sum(b => b.TotalPrice)
                })
                .OrderByDescending(x => x.TotalSpent)
                .Take(5)
                .ToList();

            // Maintenance cost this year
            var maintenanceCost = maintenanceLogs
                .Where(m => m.ServiceDate.Year == now.Year)
                .Sum(m => m.Cost);

            ViewBag.MonthRevenue = monthRevenue;
            ViewBag.NewBookingsThisMonth = newBookingsThisMonth;
            ViewBag.RentedCars = cars.Count(c => c.CarStatus?.Name == "Rented");
            ViewBag.MaintenanceCars = cars.Count(c => c.CarStatus?.Name == "Maintenance");
            ViewBag.TotalRevenue = completedBookings.Sum(b => b.TotalPrice);
            ViewBag.TotalBookings = bookings.Count;
            ViewBag.MaintenanceCostYear = maintenanceCost;

            ViewBag.RevenueLabels = revenueLabels;
            ViewBag.RevenueData = revenueData;
            ViewBag.StatusGroups = statusGroups;
            ViewBag.TopCars = topCars;
            ViewBag.TopCustomers = topCustomers;
            ViewBag.MaintenanceLogs = maintenanceLogs.OrderByDescending(m => m.ServiceDate).Take(5).ToList();

            return View();
        }
    }
}
