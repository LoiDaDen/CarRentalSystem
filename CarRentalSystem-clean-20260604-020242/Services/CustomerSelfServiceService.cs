using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface ICustomerSelfServiceService
    {
        Task<CustomerSelfServiceSummary> GetSummaryAsync(int userId);
    }

    public record CustomerSelfServiceBookingSummary(
        int BookingID,
        int UserID,
        int CarID,
        string? CarName,
        DateTime StartDate,
        DateTime EndDate,
        decimal TotalPrice,
        string? BookingStatus,
        DateTime CreatedAt);

    public record CustomerSelfServicePaymentSummary(
        int PaymentID,
        int BookingID,
        decimal Amount,
        string PaymentMethod,
        DateTime PaymentDate,
        string PaymentStatus);

    public record CustomerSelfServiceRefundSummary(
        int RefundRequestID,
        int BookingID,
        decimal Amount,
        string Reason,
        string Status,
        DateTime CreatedAt,
        DateTime? ApprovedAt,
        DateTime? PaidAt);

    public record CustomerSelfServiceSupportTicketSummary(
        int SupportTicketID,
        int? BookingID,
        string TicketType,
        string Priority,
        string Status,
        string Subject,
        DateTime CreatedAt,
        DateTime? FirstResponseDueAt,
        DateTime? ResolvedAt);

    public record CustomerSelfServiceSummary(
        int UserId,
        string? MembershipTier,
        int LoyaltyPoints,
        decimal TotalSpending,
        IReadOnlyList<CustomerSelfServiceBookingSummary> RecentBookings,
        IReadOnlyList<CustomerSelfServicePaymentSummary> RecentPayments,
        IReadOnlyList<CustomerSelfServiceRefundSummary> RefundRequests,
        IReadOnlyList<CustomerSelfServiceSupportTicketSummary> SupportTickets);

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
                .Where(b => b.UserID == userId)
                .OrderByDescending(b => b.CreatedAt)
                .Take(20)
                .Select(b => new CustomerSelfServiceBookingSummary(
                    b.BookingID,
                    b.UserID,
                    b.CarID,
                    b.Car != null ? b.Car.CarName : null,
                    b.StartDate,
                    b.EndDate,
                    b.TotalPrice,
                    b.BookingStatus != null ? b.BookingStatus.Name : null,
                    b.CreatedAt))
                .ToListAsync();

            var bookingIds = bookings.Select(b => b.BookingID).ToList();
            var payments = await _context.Payments.AsNoTracking()
                .Where(p => bookingIds.Contains(p.BookingID))
                .OrderByDescending(p => p.PaymentDate)
                .Take(20)
                .Select(p => new CustomerSelfServicePaymentSummary(
                    p.PaymentID,
                    p.BookingID,
                    p.Amount,
                    p.PaymentMethod,
                    p.PaymentDate,
                    p.PaymentStatus))
                .ToListAsync();

            var refunds = await _context.RefundRequests.AsNoTracking()
                .Where(r => r.Booking != null && r.Booking.UserID == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(20)
                .Select(r => new CustomerSelfServiceRefundSummary(
                    r.RefundRequestID,
                    r.BookingID,
                    r.Amount,
                    r.Reason,
                    r.Status,
                    r.CreatedAt,
                    r.ApprovedAt,
                    r.PaidAt))
                .ToListAsync();

            var tickets = await _context.SupportTickets.AsNoTracking()
                .Where(t => t.UserID == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .Select(t => new CustomerSelfServiceSupportTicketSummary(
                    t.SupportTicketID,
                    t.BookingID,
                    t.TicketType,
                    t.Priority,
                    t.Status,
                    t.Subject,
                    t.CreatedAt,
                    t.FirstResponseDueAt,
                    t.ResolvedAt))
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
