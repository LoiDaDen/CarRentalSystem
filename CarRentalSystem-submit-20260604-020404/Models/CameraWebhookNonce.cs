using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class CameraWebhookNonce
    {
        [Key]
        public int CameraWebhookNonceID { get; set; }

        public int CameraDeviceID { get; set; }
        public CameraDevice? CameraDevice { get; set; }

        [Required, MaxLength(120)]
        public string Nonce { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
