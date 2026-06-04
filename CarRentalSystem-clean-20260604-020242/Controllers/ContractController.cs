using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ContractController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly IBookingTransitionService _transitionService;

        public ContractController(IBookingService bookingService, IBookingTransitionService transitionService)
        {
            _bookingService = bookingService;
            _transitionService = transitionService;
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
            if (!ModelState.IsValid)
            {
                ViewBag.Booking = await _bookingService.GetBookingByIdAsync(contract.BookingID);
                return View(contract);
            }

            var booking = await _bookingService.GetBookingByIdAsync(contract.BookingID);
            if (booking == null)
            {
                TempData["Error"] = "Không tìm thấy đơn đặt xe.";
                return RedirectToAction("Manage", "Booking");
            }

            if (contract.EndKm < contract.StartKm)
            {
                ModelState.AddModelError(nameof(contract.EndKm), "Số km kết thúc phải ≥ số km bắt đầu.");
                ViewBag.Booking = booking;
                return View(contract);
            }

            contract.TotalKm = contract.EndKm - contract.StartKm;
            contract.FinalPrice = booking.TotalPrice;

            await _bookingService.CreateContractAsync(contract);

            var actorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? actorId = int.TryParse(actorIdClaim, out var aid) ? aid : null;

            var transition = await _transitionService.TransitionAsync(
                booking.BookingID,
                BookingStatusConstants.Completed,
                actorId,
                "Admin",
                "Admin tạo hợp đồng thuê và hoàn tất đơn.");

            if (!transition.IsSuccess)
            {
                TempData["Error"] = transition.ErrorMessage ?? "Không thể hoàn tất đơn ở trạng thái hiện tại.";
                return RedirectToAction("Manage", "Booking");
            }

            TempData["Success"] = "Đã tạo hợp đồng và hoàn tất đơn thuê.";
            return RedirectToAction("Manage", "Booking");
        }
    }
}
