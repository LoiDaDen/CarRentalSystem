using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class CameraGateEvent
    {
        [Key]
        public int CameraGateEventID { get; set; }

        public int CameraDeviceID { get; set; }
        public CameraDevice? CameraDevice { get; set; }

        [MaxLength(120)]
        public string? ProviderEventId { get; set; }

        [Required, MaxLength(40)]
        public string LicensePlateRaw { get; set; } = string.Empty;

        [Required, MaxLength(40)]
        public string LicensePlateNormalized { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Direction { get; set; } = CameraGateDirectionConstants.Unknown;

        [Column(TypeName = "decimal(5,4)")]
        public decimal Confidence { get; set; }

        public DateTime CapturedAt { get; set; }
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(1000)]
        public string? SnapshotUrl { get; set; }

        public int? CarID { get; set; }
        public Car? Car { get; set; }

        public int? AreaID { get; set; }
        public Area? Area { get; set; }

        public int? LocationID { get; set; }
        public Location? Location { get; set; }

        [Required, MaxLength(30)]
        public string MatchStatus { get; set; } = CameraGateMatchStatusConstants.UnknownPlate;

        [MaxLength(500)]
        public string? FailureReason { get; set; }

        public bool IsDeleted { get; set; }
    }

    public static class CameraGateMatchStatusConstants
    {
        public const string Matched = "Matched";
        public const string UnknownPlate = "UnknownPlate";
        public const string LowConfidence = "LowConfidence";
        public const string AreaMismatch = "AreaMismatch";
        public const string InvalidScope = "InvalidScope";
    }
}
