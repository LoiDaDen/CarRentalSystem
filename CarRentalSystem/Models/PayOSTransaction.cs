using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class PayOSTransaction
    {
        [Key]
        public int PayOSTransactionID { get; set; }

        public int BookingID { get; set; }
        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }

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
