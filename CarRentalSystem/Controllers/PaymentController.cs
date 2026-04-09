using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        private readonly PayOSClient _payOS;

        public PaymentController(IBookingService bookingService, IConfiguration configuration)
        {
            _bookingService = bookingService;
            string clientId = configuration["PayOS:ClientId"] ?? "PLACEHOLDER";
            string apiKey = configuration["PayOS:ApiKey"] ?? "PLACEHOLDER";
            string checksumKey = configuration["PayOS:ChecksumKey"] ?? "PLACEHOLDER";
            
            var options = new PayOSOptions
            {
                ClientId = clientId,
                ApiKey = apiKey,
                ChecksumKey = checksumKey
            };
            _payOS = new PayOSClient(options);
        }

        [HttpGet]
        public async Task<IActionResult> CreatePayment(int bookingId)
        {
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
        public async Task<IActionResult> Webhook([FromBody] Webhook webhookData)
        {
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
        public async Task<IActionResult> Cancel(int bookingId)
        {
            var booking = await _bookingService.GetBookingByIdAsync(bookingId);
            if (booking == null) return NotFound();
            
            return View(booking);
        }
    }
}
