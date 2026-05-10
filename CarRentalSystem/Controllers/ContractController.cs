using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ContractController : Controller
    {
        private readonly IBookingService _bookingService;

        public ContractController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpGet]
        public async Task<IActionResult> Create(int bookingId)
        {
            var booking = await _bookingService.GetBookingByIdAsync(bookingId);
            if (booking == null) return NotFound();

            var contract = new RentalContract
            {
                BookingID = booking.BookingID
            };

            ViewBag.Booking = booking;
            return View(contract);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RentalContract contract)
        {
            var booking = await _bookingService.GetBookingByIdAsync(contract.BookingID);
            
            int totalDays = (int)(booking.EndDate - booking.StartDate).TotalDays;
            if(totalDays <= 0) totalDays = 1;

            contract.TotalKm = contract.EndKm - contract.StartKm;
            contract.FinalPrice = booking.TotalPrice;

            await _bookingService.CreateContractAsync(contract);
            await _bookingService.UpdateBookingStatusAsync(booking.BookingID, "Completed");

            return RedirectToAction("Manage", "Booking");
        }
    }
}
