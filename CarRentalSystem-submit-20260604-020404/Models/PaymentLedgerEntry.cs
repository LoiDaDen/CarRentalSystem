using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class PaymentLedgerEntry
    {
        [Key]
        public int PaymentLedgerEntryID { get; set; }

        public int? BookingID { get; set; }
        public Booking? Booking { get; set; }

        public int? TransactionID { get; set; }
        public BookingTransaction? BookingTransaction { get; set; }

        public int? PaymentID { get; set; }
        public Payment? Payment { get; set; }

        [MaxLength(40)]
        public string Provider { get; set; } = "Internal";

        public long? OrderCode { get; set; }

        [MaxLength(40)]
        public string EntryType { get; set; } = PaymentLedgerEntryTypeConstants.DepositCharge;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "VND";

        [MaxLength(30)]
        public string Status { get; set; } = PaymentStatusConstants.Pending;

        [MaxLength(120)]
        public string? IdempotencyKey { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PostedAt { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
