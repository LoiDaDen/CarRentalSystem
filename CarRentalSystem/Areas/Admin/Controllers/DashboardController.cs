using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CarRentalSystem.Services;
using CarRentalSystem.Areas.Admin.Models;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ICarService _carService;
        private readonly IBookingService _bookingService;

        public DashboardController(ICarService carService, IBookingService bookingService)
        {
            _carService = carService;
            _bookingService = bookingService;
        }

        public async Task<IActionResult> Index()
        {
            var cars = (await _carService.GetAllCarsAsync())?.ToList() ?? new List<CarRentalSystem.Models.Car>();
            var allBookings = await _bookingService.GetAllBookingsAsync() ?? new List<CarRentalSystem.Models.Booking>();

            // Filter out draft bookings (unpaid)
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

            // Monthly Revenue (Last 6 months)
            var last6Months = Enumerable.Range(0, 6)
                .Select(i => DateTime.UtcNow.AddMonths(-i))
                .OrderBy(d => d)
                .ToList();

            viewModel.RevenueLabels = last6Months.Select(d => d.ToString("MM/yyyy")).ToList();
            viewModel.RevenueData = last6Months.Select(d =>
                bookings.Where(b => b.CreatedAt.Month == d.Month && b.CreatedAt.Year == d.Year && (b.BookingStatus?.Name == "Completed" || b.BookingStatus?.Name == "Confirmed"))
                        .Sum(b => b.TotalPrice) / 1000000
            ).ToList();

            // Top Cars (Most rented) - Only count Completed rentals
            viewModel.TopCars = bookings.Where(b => b.BookingStatus?.Name == "Completed")
                .GroupBy(b => b.CarID)
                .Select(g => new TopCarViewModel {
                    Car = g.FirstOrDefault()?.Car, // Null-safe
                    RentalCount = g.Count(),
                    TotalRevenue = ((decimal?)g.Sum(b => b.TotalPrice)) ?? 0m
                })
                .Where(x => x.Car != null) // Filter out null cars
                .OrderByDescending(x => x.RentalCount)
                .Take(3)
                .ToList();

            // ═══════════════════════════════════════════════
            // 📊 ADDITIONAL DATA FOR VIEWS
            // ═══════════════════════════════════════════════
            ViewBag.MaintenanceCostYear = 0; // TODO: Query from MaintenanceService
            ViewBag.MaintenanceLogs = new List<CarRentalSystem.Models.MaintenanceLog>(); // TODO: Query from MaintenanceService
            ViewBag.TopCustomers = new List<CarRentalSystem.Areas.Admin.Models.TopCustomerViewModel>(); // TODO: Query from CustomerService

            // ═══════════════════════════════════════════════
            // 📈 BOOKING CHART DATA (by Status)
            // ═══════════════════════════════════════════════
            viewModel.BookingLabels = bookings
                .GroupBy(b => b.BookingStatus?.Name ?? "Unknown")
                .Select(g => g.Key)
                .ToList();
            viewModel.BookingData = bookings
                .GroupBy(b => b.BookingStatus?.Name ?? "Unknown")
                .Select(g => g.Count())
                .ToList();

            // ═══════════════════════════════════════════════
            // 🚗 CAR CHART DATA (by Status)
            // ═══════════════════════════════════════════════
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
