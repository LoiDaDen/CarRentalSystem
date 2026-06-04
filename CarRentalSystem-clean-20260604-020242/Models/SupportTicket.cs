using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class SupportTicket
    {
        [Key]
        public int SupportTicketID { get; set; }

        public int UserID { get; set; }
        public User? User { get; set; }

        public int? AssignedToUserID { get; set; }
        public User? AssignedToUser { get; set; }

        public int? BookingID { get; set; }
        public Booking? Booking { get; set; }

        public int? PrivacyRequestID { get; set; }
        public PrivacyRequest? PrivacyRequest { get; set; }

        public int? SettlementReviewID { get; set; }
        public SettlementReview? SettlementReview { get; set; }

        public int? RefundRequestID { get; set; }
        public RefundRequest? RefundRequest { get; set; }

        [MaxLength(30)]
        public string TicketType { get; set; } = SupportTicketTypeConstants.General;

        [MaxLength(20)]
        public string Priority { get; set; } = SupportTicketPriorityConstants.P2;

        [MaxLength(30)]
        public string Status { get; set; } = SupportTicketStatusConstants.Open;

        [Required, MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required, MaxLength(4000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Resolution { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FirstResponseDueAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class SupportTicketTypeConstants
    {
        public const string General = "General";
        public const string Payment = "Payment";
        public const string Booking = "Booking";
        public const string Dispute = "Dispute";
        public const string Claim = "Claim";
        public const string Emergency = "Emergency";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            General, Payment, Booking, Dispute, Claim, Emergency
        };
    }

    public static class SupportTicketStatusConstants
    {
        public const string Open = "Open";
        public const string InProgress = "InProgress";
        public const string WaitingForCustomer = "WaitingForCustomer";
        public const string Resolved = "Resolved";
        public const string Closed = "Closed";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Open, InProgress, WaitingForCustomer, Resolved, Closed
        };
    }

    public static class SupportTicketPriorityConstants
    {
        public const string P0 = "P0";
        public const string P1 = "P1";
        public const string P2 = "P2";
        public const string P3 = "P3";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            P0, P1, P2, P3
        };
    }
}
