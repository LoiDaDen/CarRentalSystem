using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class PaymentWebhookEvent
    {
        [Key]
        public int PaymentWebhookEventID { get; set; }

        [MaxLength(40)]
        public string Provider { get; set; } = "PayOS";

        [MaxLength(160)]
        public string EventKey { get; set; } = string.Empty;

        [MaxLength(128)]
        public string PayloadHash { get; set; } = string.Empty;

        public long? OrderCode { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = "Received";

        [Column(TypeName = "nvarchar(max)")]
        public string? RawPayload { get; set; }

        [MaxLength(1000)]
        public string? Error { get; set; }

        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
    }
}
