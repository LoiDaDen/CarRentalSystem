using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class SettlementReview
    {
        [Key]
        public int SettlementReviewID { get; set; }

        public int BookingID { get; set; }
        public Booking? Booking { get; set; }

        public int? HandoverID { get; set; }
        public HandoverRecord? HandoverRecord { get; set; }

        [MaxLength(40)]
        public string ReviewType { get; set; } = SettlementReviewTypeConstants.Other;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "VND";

        [MaxLength(30)]
        public string Status { get; set; } = SettlementReviewStatusConstants.PendingReview;

        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;

        public int? RequestedByUserID { get; set; }
        public User? RequestedByUser { get; set; }

        public int? ReviewedByUserID { get; set; }
        public User? ReviewedByUser { get; set; }

        public int? PaymentLedgerEntryID { get; set; }
        public PaymentLedgerEntry? PaymentLedgerEntry { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }

    public static class SettlementReviewTypeConstants
    {
        public const string Late = "Late";
        public const string Fuel = "Fuel";
        public const string Cleaning = "Cleaning";
        public const string Damage = "Damage";
        public const string LostItem = "LostItem";
        public const string Toll = "Toll";
        public const string Violation = "Violation";
        public const string Other = "Other";
    }

    public static class SettlementReviewStatusConstants
    {
        public const string PendingReview = "PendingReview";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }
}
