using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface ICustomerSelfServiceService
    {
        Task<CustomerSelfServiceSummary> GetSummaryAsync(int userId);
    }

    public record CustomerSelfServiceSummary(
        int UserId,
        string? MembershipTier,
        int LoyaltyPoints,
        decimal TotalSpending,
        IReadOnlyList<Booking> RecentBookings,
        IReadOnlyList<Payment> RecentPayments,
        IReadOnlyList<RefundRequest> RefundRequests,
        IReadOnlyList<SupportTicket> SupportTickets);

    public class CustomerSelfServiceService : ICustomerSelfServiceService
    {
        private readonly ApplicationDbContext _context;

        public CustomerSelfServiceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CustomerSelfServiceSummary> GetSummaryAsync(int userId)
        {
            var user = await _context.Users.AsNoTracking()
                .Include(u => u.MembershipTier)
                .FirstAsync(u => u.UserID == userId);

            var bookings = await _context.Bookings.AsNoTracking()
                .Include(b => b.Car)
                .Include(b => b.BookingStatus)
                .Where(b => b.UserID == userId)
                .OrderByDescending(b => b.CreatedAt)
                .Take(20)
                .ToListAsync();

            var bookingIds = bookings.Select(b => b.BookingID).ToList();
            var payments = await _context.Payments.AsNoTracking()
                .Where(p => bookingIds.Contains(p.BookingID))
                .OrderByDescending(p => p.PaymentDate)
                .Take(20)
                .ToListAsync();

            var refunds = await _context.RefundRequests.AsNoTracking()
                .Include(r => r.Booking)
                .Where(r => r.Booking != null && r.Booking.UserID == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(20)
                .ToListAsync();

            var tickets = await _context.SupportTickets.AsNoTracking()
                .Where(t => t.UserID == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .ToListAsync();

            return new CustomerSelfServiceSummary(
                user.UserID,
                user.MembershipTier?.Name,
                user.LoyaltyPoints,
                user.TotalSpending,
                bookings,
                payments,
                refunds,
                tickets);
        }
    }
}
