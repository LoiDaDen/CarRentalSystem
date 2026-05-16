using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class TrackingIngestNonce
    {
        [Key]
        public int TrackingIngestNonceID { get; set; }

        [Required, MaxLength(120)]
        public string DeviceId { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string Nonce { get; set; } = string.Empty;

        public long TimestampUnixSeconds { get; set; }

        public int CarID { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
