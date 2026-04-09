using CarRentalSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CarRentalSystem.Controllers
{
    public class MembershipController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MembershipController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Public: Membership tiers page
        public async Task<IActionResult> Index()
        {
            var tiers = await _context.MembershipTiers
                .OrderBy(t => t.SortOrder)
                .ToListAsync();

            // Get current user's membership info if logged in
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                var user = await _context.Users
                    .Include(u => u.MembershipTier)
                    .FirstOrDefaultAsync(u => u.UserID == userId);

                if (user != null)
                {
                    // Recalculate spending dynamically based on Bookings (Confirmed, Active, Completed)
                    var validBookings = await _context.Bookings
                        .Include(b => b.BookingStatus)
                        .Where(b => b.UserID == userId && b.BookingStatus != null &&
                                    (b.BookingStatus.Name == "Confirmed" || b.BookingStatus.Name == "Active" || b.BookingStatus.Name == "Completed"))
                        .ToListAsync();

                    decimal calculatedSpending = validBookings.Sum(b => b.TotalPrice);

                    bool needsUpdate = false;
                    if (user.TotalSpending != calculatedSpending)
                    {
                        user.TotalSpending = calculatedSpending;
                        needsUpdate = true;
                    }

                    // Check if tier needs upgrade
                    var applicableTier = tiers
                        .Where(t => t.MinSpending <= user.TotalSpending)
                        .OrderByDescending(t => t.MinSpending)
                        .FirstOrDefault();

                    if (applicableTier != null && user.MembershipTierID != applicableTier.MembershipTierID)
                    {
                        user.MembershipTierID = applicableTier.MembershipTierID;
                        user.MembershipTier = applicableTier;
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        _context.Users.Update(user);
                        await _context.SaveChangesAsync();
                    }

                    ViewBag.CurrentUser = user;
                    ViewBag.CurrentTier = user.MembershipTier;
                }
            }

            var excludedNames = new[] { "theo ngày", "ngày chẵn", "ngày lẻ", "cuối tuần" };
            var contractTypes = await _context.ContractTypes
                .Where(c => c.IsActive && !excludedNames.Contains(c.Name.ToLower()))
                .OrderBy(c => c.MinDurationDays)
                .ToListAsync();

            ViewBag.ContractTypes = contractTypes;

            return View(tiers);
        }
    }
}
