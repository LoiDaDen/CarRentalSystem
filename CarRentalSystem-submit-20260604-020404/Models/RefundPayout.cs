using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class RefundPayout
    {
        [Key]
        public int RefundPayoutID { get; set; }

        public int RefundRequestID { get; set; }
        public RefundRequest? RefundRequest { get; set; }

        [MaxLength(80)]
        public string ReferenceId { get; set; } = string.Empty;

        [MaxLength(120)]
        public string IdempotencyKey { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? PayOSPayoutId { get; set; }

        [MaxLength(40)]
        public string Provider { get; set; } = "PayOS";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "VND";

        [MaxLength(40)]
        public string ApprovalState { get; set; } = "Pending";

        [MaxLength(40)]
        public string TransactionState { get; set; } = "Pending";

        public int AttemptCount { get; set; }

        [MaxLength(1000)]
        public string? LastError { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? EncryptedRecipientBin { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? EncryptedRecipientAccountNumber { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? EncryptedRecipientAccountName { get; set; }

        [MaxLength(40)]
        public string? MaskedRecipientAccount { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? RawResponse { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? FailedAt { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
