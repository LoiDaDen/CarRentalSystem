using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class PartnerPayoutStatement
    {
        [Key]
        public int PartnerPayoutStatementID { get; set; }

        public int PartnerUserID { get; set; }
        public User? PartnerUser { get; set; }

        public DateTime PeriodStartUtc { get; set; }
        public DateTime PeriodEndUtc { get; set; }

        public int CompletedBookingCount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossBookingAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PlatformCommissionAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OwnerPayoutAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeductionAmount { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = PartnerPayoutStatementStatusConstants.Draft;

        [MaxLength(120)]
        public string IdempotencyKey { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string BookingBreakdownJson { get; set; } = "[]";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAtUtc { get; set; }
        public DateTime? PaidAtUtc { get; set; }
    }

    public static class PartnerPayoutStatementStatusConstants
    {
        public const string Draft = "Draft";
        public const string Approved = "Approved";
        public const string Paid = "Paid";
        public const string Cancelled = "Cancelled";
    }
}
