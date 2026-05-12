using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public PaymentController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index()
        {
            var payments = await _context.Payments
                .Include(p => p.Booking)
                    .ThenInclude(b => b.User)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Car)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            return View(payments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.Booking)
                    .ThenInclude(b => b.User)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Car)
                .FirstOrDefaultAsync(p => p.PaymentID == id);

            if (payment == null)
                return NotFound();

            var booking = payment.Booking;
            if (booking != null && booking.DepositAmount > 0)
            {
                booking.DepositStatus = "Holding";
            }

            payment.PaymentStatus = "Paid";
            payment.PaymentDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Gui email xac nhan cho khach hang
            if (booking != null)
            {
                _ = _emailService.SendBookingConfirmationEmailAsync(booking, payment.Amount);
            }

            TempData["Success"] = $"Đã xác nhận thanh toán cho đơn #{payment.BookingID}";
            return RedirectToAction(nameof(Index));
        }
    }
}
