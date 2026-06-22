using System.ComponentModel.DataAnnotations;
using CarRentalSystem.Models;

namespace CarRentalSystem.ViewModels.Cameras
{
    public class CameraDeviceInputModel : IValidatableObject
    {
        public int CameraDeviceID { get; set; }

        [Required, MaxLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(80)]
        public string? Provider { get; set; }

        public int? AreaID { get; set; }
        public int? LocationID { get; set; }

        [Required, MaxLength(20)]
        public string DefaultDirection { get; set; } = CameraGateDirectionConstants.Unknown;

        [MaxLength(1000)]
        public string? LiveViewUrl { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = CameraDeviceStatusConstants.Active;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!AreaID.HasValue)
                yield return new ValidationResult("Phải chọn khu vực trước khi chọn địa điểm.", new[] { nameof(AreaID) });

            if (!new[] { CameraGateDirectionConstants.In, CameraGateDirectionConstants.Out, CameraGateDirectionConstants.Unknown }.Contains(DefaultDirection))
                yield return new ValidationResult("Hướng cổng không hợp lệ.", new[] { nameof(DefaultDirection) });

            if (!new[] { CameraDeviceStatusConstants.Active, CameraDeviceStatusConstants.Inactive }.Contains(Status))
                yield return new ValidationResult("Trạng thái camera không hợp lệ.", new[] { nameof(Status) });

            if (!string.IsNullOrWhiteSpace(LiveViewUrl) && !Uri.TryCreate(LiveViewUrl, UriKind.Absolute, out _))
                yield return new ValidationResult("Live link NVR phải là URL tuyệt đối.", new[] { nameof(LiveViewUrl) });
        }
    }

    public class CameraDeviceListItem
    {
        public int CameraDeviceID { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Provider { get; set; }
        public int? AreaID { get; set; }
        public string? AreaName { get; set; }
        public int? LocationID { get; set; }
        public string? LocationName { get; set; }
        public string DefaultDirection { get; set; } = CameraGateDirectionConstants.Unknown;
        public string Status { get; set; } = CameraDeviceStatusConstants.Active;
        public string? LiveViewUrl { get; set; }
        public int EventCountToday { get; set; }
    }

    public class CameraIndexViewModel
    {
        public CameraDeviceInputModel NewCamera { get; set; } = new();
        public List<CameraDeviceListItem> Cameras { get; set; } = new();
    }

    public class CameraGateEventListItem
    {
        public int CameraGateEventID { get; set; }
        public string CameraCode { get; set; } = string.Empty;
        public string CameraName { get; set; } = string.Empty;
        public string LicensePlateRaw { get; set; } = string.Empty;
        public string LicensePlateNormalized { get; set; } = string.Empty;
        public string Direction { get; set; } = CameraGateDirectionConstants.Unknown;
        public decimal Confidence { get; set; }
        public DateTime CapturedAt { get; set; }
        public string? SnapshotUrl { get; set; }
        public int? CarID { get; set; }
        public string? CarName { get; set; }
        public string? CarLicensePlate { get; set; }
        public string? AreaName { get; set; }
        public string? LocationName { get; set; }
        public string MatchStatus { get; set; } = CameraGateMatchStatusConstants.UnknownPlate;
        public string? FailureReason { get; set; }
        public string? LiveViewUrl { get; set; }
    }

    public class CameraMonitorViewModel
    {
        public List<CameraGateEventListItem> Events { get; set; } = new();
        public int InsideCount { get; set; }
        public int TodayCount { get; set; }
        public int UnknownPlateCount { get; set; }
        public int LowConfidenceCount { get; set; }
        public int AreaMismatchCount { get; set; }
    }
}
