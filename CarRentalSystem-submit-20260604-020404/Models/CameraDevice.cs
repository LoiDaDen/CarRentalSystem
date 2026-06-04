using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class CameraDevice
    {
        [Key]
        public int CameraDeviceID { get; set; }

        [Required, MaxLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(80)]
        public string? Provider { get; set; }

        public int? AreaID { get; set; }
        [ForeignKey(nameof(AreaID))]
        public Area? Area { get; set; }

        public int? LocationID { get; set; }
        [ForeignKey(nameof(LocationID))]
        public Location? Location { get; set; }

        [Required, MaxLength(20)]
        public string DefaultDirection { get; set; } = CameraGateDirectionConstants.Unknown;

        [MaxLength(1000)]
        public string? LiveViewUrl { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = CameraDeviceStatusConstants.Active;

        [Required, MaxLength(2000)]
        public string EncryptedWebhookSecret { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        public ICollection<CameraGateEvent> GateEvents { get; set; } = new List<CameraGateEvent>();
    }

    public static class CameraDeviceStatusConstants
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
    }

    public static class CameraGateDirectionConstants
    {
        public const string In = "In";
        public const string Out = "Out";
        public const string Unknown = "Unknown";
    }
}
