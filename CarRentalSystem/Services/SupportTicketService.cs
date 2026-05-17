using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface ISupportTicketService
    {
        Task<SupportTicket> CreateAsync(SupportTicketCreateRequest request);
        Task<SupportTicketServiceResult> UpdateStatusAsync(int ticketId, string status, int actorUserId, string actorRole, string? resolution);
        Task<List<SupportTicket>> GetForUserAsync(int userId);
    }

    public record SupportTicketCreateRequest(
        int UserId,
        string TicketType,
        string Priority,
        string Subject,
        string Description,
        int? BookingId = null,
        int? PrivacyRequestId = null,
        int? SettlementReviewId = null,
        int? RefundRequestId = null);

    public record SupportTicketServiceResult(bool IsSuccess, string Message, SupportTicket? Ticket = null);

    public class SupportTicketService : ISupportTicketService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditTrailService _auditTrail;

        public SupportTicketService(ApplicationDbContext context, IAuditTrailService auditTrail)
        {
            _context = context;
            _auditTrail = auditTrail;
        }

        public async Task<SupportTicket> CreateAsync(SupportTicketCreateRequest request)
        {
            var type = SupportTicketTypeConstants.All.Contains(request.TicketType) ? request.TicketType : SupportTicketTypeConstants.General;
            var priority = SupportTicketPriorityConstants.All.Contains(request.Priority) ? request.Priority : SupportTicketPriorityConstants.P2;
            var ticket = new SupportTicket
            {
                UserID = request.UserId,
                TicketType = type,
                Priority = priority,
                Subject = request.Subject.Trim(),
                Description = request.Description.Trim(),
                BookingID = request.BookingId,
                PrivacyRequestID = request.PrivacyRequestId,
                SettlementReviewID = request.SettlementReviewId,
                RefundRequestID = request.RefundRequestId,
                FirstResponseDueAt = DateTime.UtcNow.Add(GetSla(priority))
            };

            _context.SupportTickets.Add(ticket);
            await _context.SaveChangesAsync();
            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "CreateSupportTicket",
                EntityType = nameof(SupportTicket),
                EntityId = ticket.SupportTicketID,
                UserId = request.UserId,
                Reason = request.Subject,
                NewValues = JsonSerializer.Serialize(new
                {
                    ticket.TicketType,
                    ticket.Priority,
                    ticket.Status,
                    ticket.BookingID,
                    ticket.PrivacyRequestID,
                    ticket.SettlementReviewID,
                    ticket.RefundRequestID
                })
            });

            return ticket;
        }

        public async Task<SupportTicketServiceResult> UpdateStatusAsync(int ticketId, string status, int actorUserId, string actorRole, string? resolution)
        {
            if (!SupportTicketStatusConstants.All.Contains(status))
                return new SupportTicketServiceResult(false, "Unsupported support ticket status.");

            var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.SupportTicketID == ticketId);
            if (ticket == null)
                return new SupportTicketServiceResult(false, "Support ticket not found.");

            var oldValues = JsonSerializer.Serialize(new { ticket.Status, ticket.AssignedToUserID, ticket.Resolution, ticket.ResolvedAt });
            ticket.Status = status;
            ticket.AssignedToUserID ??= actorUserId;
            ticket.UpdatedAt = DateTime.UtcNow;
            if (status is SupportTicketStatusConstants.Resolved or SupportTicketStatusConstants.Closed)
            {
                ticket.Resolution = resolution;
                ticket.ResolvedAt ??= DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "UpdateSupportTicket",
                EntityType = nameof(SupportTicket),
                EntityId = ticket.SupportTicketID,
                UserId = actorUserId,
                UserName = actorRole,
                Reason = resolution,
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new { ticket.Status, ticket.AssignedToUserID, ticket.Resolution, ticket.ResolvedAt })
            });

            return new SupportTicketServiceResult(true, "Support ticket updated.", ticket);
        }

        public Task<List<SupportTicket>> GetForUserAsync(int userId)
        {
            return _context.SupportTickets
                .AsNoTracking()
                .Where(t => t.UserID == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(200)
                .ToListAsync();
        }

        private static TimeSpan GetSla(string priority)
        {
            return priority switch
            {
                SupportTicketPriorityConstants.P0 => TimeSpan.FromMinutes(15),
                SupportTicketPriorityConstants.P1 => TimeSpan.FromHours(4),
                SupportTicketPriorityConstants.P3 => TimeSpan.FromDays(3),
                _ => TimeSpan.FromDays(1)
            };
        }
    }
}
