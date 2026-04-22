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
            var cars = (await _carService.GetAllCarsAsync())?.ToList() ?? new List<CarRentalSystem.Models.Car>();
            var allBookings = await _bookingService.GetAllBookingsAsync() ?? new List<CarRentalSystem.Models.Booking>();

            var bookings = allBookings.Where(b => b.BookingStatus?.Name != "Draft").ToList();

            var viewModel = new DashboardViewModel
            {
                TotalCars = cars.Count,
                AvailableCars = cars.Count(c => c.CarStatus?.Name == "Available"),
                RentedCars = cars.Count(c => c.CarStatus?.Name == "Rented"),
                TotalBookings = bookings.Count,
                TotalRevenue = bookings.Where(b => b.BookingStatus?.Name == "Completed" || b.BookingStatus?.Name == "Confirmed").Sum(b => b.TotalPrice),
                TotalHeldDepositsAmount = bookings.Where(b => b.DepositStatus == "Holding" && b.BookingStatus?.Name != "Completed").Sum(b => b.DepositAmount),
                TotalHeldDepositsCount = bookings.Count(b => b.DepositStatus == "Holding" && b.BookingStatus?.Name != "Completed"),
                RecentBookings = bookings.OrderByDescending(b => b.CreatedAt).Take(7).ToList(),
                StatusGroups = bookings
                    .GroupBy(b => b.BookingStatus?.Name ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            var last6Months = Enumerable.Range(0, 6)
                .Select(i => DateTime.UtcNow.AddMonths(-i))
                .OrderBy(d => d)
                .ToList();

            viewModel.RevenueLabels = last6Months.Select(d => d.ToString("MM/yyyy")).ToList();
            viewModel.RevenueData = last6Months.Select(d =>
                bookings.Where(b => b.CreatedAt.Month == d.Month && b.CreatedAt.Year == d.Year && (b.BookingStatus?.Name == "Completed" || b.BookingStatus?.Name == "Confirmed"))
                        .Sum(b => b.TotalPrice) / 1000000
            ).ToList();

            viewModel.TopCars = bookings.Where(b => b.BookingStatus?.Name == "Completed")
                .GroupBy(b => b.CarID)
                .Select(g => new TopCarViewModel {
                    Car = g.FirstOrDefault()?.Car,
                    RentalCount = g.Count(),
                    TotalRevenue = ((decimal?)g.Sum(b => b.TotalPrice)) ?? 0m
                })
                .Where(x => x.Car != null)
                .OrderByDescending(x => x.RentalCount)
                .Take(3)
                .ToList();

            ViewBag.MaintenanceCostYear = 0;
            ViewBag.MaintenanceLogs = new List<CarRentalSystem.Models.MaintenanceLog>();
            ViewBag.TopCustomers = new List<TopCustomerViewModel>();

            viewModel.BookingLabels = bookings
                .GroupBy(b => b.BookingStatus?.Name ?? "Unknown")
                .Select(g => g.Key)
                .ToList();
            viewModel.BookingData = bookings
                .GroupBy(b => b.BookingStatus?.Name ?? "Unknown")
                .Select(g => g.Count())
                .ToList();

            viewModel.CarLabels = cars
                .GroupBy(c => c.CarStatus?.Name ?? "Unknown")
                .Select(g => g.Key)
                .ToList();
            viewModel.CarData = cars
                .GroupBy(c => c.CarStatus?.Name ?? "Unknown")
                .Select(g => g.Count())
                .ToList();

            return View(viewModel);
        }
    }
}
