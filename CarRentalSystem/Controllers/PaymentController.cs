using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly CarRentalSystem.Data.ApplicationDbContext _context;
        private readonly PayOSClient _payOS;

        public PaymentController(IBookingService bookingService, CarRentalSystem.Data.ApplicationDbContext context, IConfiguration configuration)
        {
            _bookingService = bookingService;
            _context = context;
            string clientId = configuration["PayOS:ClientId"] ?? "";
            string apiKey = configuration["PayOS:ApiKey"] ?? "";
            string checksumKey = configuration["PayOS:ChecksumKey"] ?? "";

            if (!string.IsNullOrWhiteSpace(clientId) &&
                !string.IsNullOrWhiteSpace(apiKey) &&
                !string.IsNullOrWhiteSpace(checksumKey))
            {
                var options = new PayOSOptions
                {
                    ClientId = clientId,
                    ApiKey = apiKey,
                    ChecksumKey = checksumKey
                };
                _payOS = new PayOSClient(options);
            }
        }

        [HttpGet]
        public async Task<IActionResult> CreatePayment(int bookingId)
        {
            if (_payOS == null)
            {
                TempData["Error"] = "Cổng thanh toán PayOS hiện chưa được cấu hình. Vui lòng thiết lập biến môi trường.";
                return RedirectToAction("Deposit", "Booking", new { id = bookingId });
            }

            var booking = await _bookingService.GetBookingByIdAsync(bookingId);
            if (booking == null) return NotFound();

            // Calculate deposit (20%)
            long depositAmount = (long)(booking.TotalPrice * 0.2m);
            
            // Create Items for PayOS
            var paymentData = new List<PaymentLinkItem> {
                new PaymentLinkItem {
                    Name = "Tiền cọc thuê xe " + booking.Car?.CarName,
                    Quantity = 1,
                    Price = depositAmount
                }
            };

            string domain = $"{Request.Scheme}://{Request.Host}";
            var paymentLinkRequest = new CreatePaymentLinkRequest {
                OrderCode = (long)booking.BookingID,
                Amount = depositAmount,
                Description = $"Coc xe {booking.BookingID}",
                Items = paymentData,
                CancelUrl = $"{domain}/Payment/Cancel?bookingId={bookingId}",
                ReturnUrl = $"{domain}/Payment/Success?bookingId={bookingId}"
            };

            try
            {
                var response = await _payOS.PaymentRequests.CreateAsync(paymentLinkRequest);
                return Redirect(response.CheckoutUrl);
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối PayOS: " + ex.Message;
                return RedirectToAction("Deposit", "Booking", new { id = bookingId });
            }
        }

        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken] // PayOS gửi POST webhook, không có CSRF token
        public async Task<IActionResult> Webhook([FromBody] Webhook webhookData)
        {
            if (_payOS == null) return BadRequest(new { message = "PayOS not configured" });
            if (webhookData == null) return BadRequest();

            try
            {
                var data = await _payOS.Webhooks.VerifyAsync(webhookData);

                if (data.Code == "00")
                {
                    int bookingId = (int)data.OrderCode;
                    await _bookingService.UpdateBookingStatusAsync(bookingId, "Confirmed");
                }
                
                return Ok(new { message = "success" });
            }
            catch (System.Exception)
            {
                return BadRequest();
            }
        }

        [HttpGet]
        public async Task<IActionResult> Success(int bookingId)
        {
            var booking = await _bookingService.GetBookingByIdAsync(bookingId);
            if (booking == null) return NotFound();
            
            // Auto-update status on return if still Draft
            if (booking.BookingStatus?.Name == "Draft")
            {
                await _bookingService.UpdateBookingStatusAsync(bookingId, "Pending");
            }
            
            return View(booking);
        }

        [HttpGet]
        public async Task<IActionResult> SuccessTransaction(int transactionId)
        {
            var transaction = await _context.BookingTransactions
                .Include(t => t.Bookings).ThenInclude(b => b.BookingStatus)
                .FirstOrDefaultAsync(t => t.TransactionID == transactionId);
            
            if (transaction == null) return NotFound();
            
            return View(transaction);
        }

        [HttpGet]
        public async Task<IActionResult> Cancel(int bookingId)
        {
            var booking = await _bookingService.GetBookingByIdAsync(bookingId);
            if (booking == null) return NotFound();
            
            return View(booking);
        }
    }
}
