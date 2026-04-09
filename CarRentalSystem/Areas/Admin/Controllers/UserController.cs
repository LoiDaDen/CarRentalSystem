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
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, string? role)
        {
            ViewBag.Search = search;
            ViewBag.ActiveRole = role;

            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalAdmins = await _context.Users.CountAsync(u => u.Role == "Admin");
            ViewBag.TotalCustomers = await _context.Users.CountAsync(u => u.Role == "Customer");
            ViewBag.TotalPartners = await _context.Users.CountAsync(u => u.Role == "Partner");

            var users = _context.Users
                .Include(u => u.Bookings)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                users = users.Where(u => u.Name.Contains(search) || u.Email.Contains(search));

            if (!string.IsNullOrEmpty(role))
                users = users.Where(u => u.Role == role);

            var list = await users.OrderByDescending(u => u.CreatedAt).ToListAsync();
            return View(list);
        }

        public async Task<IActionResult> PartnerRequests()
        {
            var requests = await _context.Users
                .Where(u => u.Role == "Partner" && !u.IsVerified)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
            return View(requests);
        }

        [HttpPost]
        public async Task<IActionResult> SetRole(int id, string role)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.Role = role;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã cập nhật vai trò của {user.Name} thành {role}.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleVerify(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsVerified = !user.IsVerified;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã cập nhật trạng thái eKYC của {user.Name}.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleBlacklist(int id, string? reason)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsBlacklisted = !user.IsBlacklisted;
                user.BlacklistReason = user.IsBlacklisted ? (reason ?? "Vi phạm chính sách nền tảng") : null;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã cập nhật trạng thái Blacklist của {user.Name}.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa tài khoản người dùng.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
