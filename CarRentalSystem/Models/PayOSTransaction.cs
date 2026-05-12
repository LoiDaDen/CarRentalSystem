using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class PayOSTransaction
    {
        [Key]
        public int PayOSTransactionID { get; set; }

        public int? BookingID { get; set; }
        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }

        public int? BookingHoldID { get; set; }
        [ForeignKey("BookingHoldID")]
        public BookingHold? BookingHold { get; set; }

        public int? UserID { get; set; }
        [ForeignKey("UserID")]
        public User? User { get; set; }

        public int? PaymentID { get; set; }
        [ForeignKey("PaymentID")]
        public Payment? Payment { get; set; }

        public long OrderCode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required, MaxLength(30)]
        public string Status { get; set; } = "Pending";

        [MaxLength(255)]
        public string? CheckoutUrl { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? QrCode { get; set; }

        [MaxLength(30)]
        public string? Bin { get; set; }

        [MaxLength(50)]
        public string? AccountNumber { get; set; }

        [MaxLength(120)]
        public string? AccountName { get; set; }

        [MaxLength(100)]
        public string? PaymentLinkId { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "VND";

        [MaxLength(50)]
        public string? PayOSCode { get; set; }

        [MaxLength(255)]
        public string? PayOSDescription { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? WebhookPayload { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? FailedAt { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}
