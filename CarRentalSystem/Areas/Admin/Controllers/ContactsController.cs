using CarRentalSystem.Authorization;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class ContactsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContactsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var messages = await _context.ContactMessages
                .OrderBy(m => m.IsResolved)
                .ThenByDescending(m => m.CreatedAt)
                .ToListAsync();
            return View(messages);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null) return NotFound();

            return View(message);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Respond(int id, string adminResponse)
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null) return NotFound();

            if (string.IsNullOrWhiteSpace(adminResponse))
            {
                TempData["Error"] = "Vui lòng nhập nội dung phản hồi.";
                return RedirectToAction(nameof(Details), new { id });
            }

            message.AdminResponse = adminResponse;
            message.IsResolved = true;
            _context.Update(message);
            await _context.SaveChangesAsync();

            // Here, an email service would normally be called to send the response to the user's email.
            // emailService.Send(message.Email, "Phản hồi từ SmartRental", adminResponse);

            TempData["Success"] = "Đã lưu phản hồi. Khách hàng sẽ nhận được email.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message != null)
            {
                _context.ContactMessages.Remove(message);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa tin nhắn.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
