using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class RefundRequest
    {
        [Key]
        public int RefundRequestID { get; set; }

        public int BookingID { get; set; }
        public Booking? Booking { get; set; }

        public int? PaymentLedgerEntryID { get; set; }
        public PaymentLedgerEntry? PaymentLedgerEntry { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Status { get; set; } = RefundRequestStatusConstants.PendingReview;

        public int? ApprovedByUserID { get; set; }
        public User? ApprovedByUser { get; set; }

        public int? PaidByUserID { get; set; }
        public User? PaidByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAt { get; set; }
        public DateTime? PaidAt { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
