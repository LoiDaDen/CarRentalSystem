using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models;

public static class CarChangeOperationConstants
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
}

public static class CarChangeRequestStatusConstants
{
    public const string PendingStaff = "PendingStaff";
    public const string PendingManager = "PendingManager";
    public const string PendingAdmin = "PendingAdmin";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class CarChangeApprovalStageConstants
{
    public const string Staff = "Staff";
    public const string Manager = "Manager";
    public const string Admin = "Admin";
}

public static class CarChangeApprovalDecisionConstants
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public sealed class CarChangeRequest
{
    [Key]
    public int CarChangeRequestID { get; set; }

    [ForeignKey(nameof(Car))]
    public int CarID { get; set; }
    public Car? Car { get; set; }

    [ForeignKey(nameof(Owner))]
    public int OwnerID { get; set; }
    public User? Owner { get; set; }

    [ForeignKey(nameof(Area))]
    public int? AreaID { get; set; }
    public Area? Area { get; set; }

    [Required, MaxLength(20)]
    public string Operation { get; set; } = CarChangeOperationConstants.Create;

    [Required, MaxLength(30)]
    public string Status { get; set; } = CarChangeRequestStatusConstants.PendingStaff;

    [Required, MaxLength(20)]
    public string CurrentStage { get; set; } = CarChangeApprovalStageConstants.Staff;

    public string? SnapshotJson { get; set; }

    [MaxLength(500)]
    public string? OwnerNote { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ICollection<CarChangeApprovalStep> ApprovalSteps { get; set; } = new List<CarChangeApprovalStep>();
}

public sealed class CarChangeApprovalStep
{
    [Key]
    public int CarChangeApprovalStepID { get; set; }

    [ForeignKey(nameof(Request))]
    public int CarChangeRequestID { get; set; }
    public CarChangeRequest? Request { get; set; }

    [Required, MaxLength(20)]
    public string Stage { get; set; } = CarChangeApprovalStageConstants.Staff;

    [Required, MaxLength(20)]
    public string Decision { get; set; } = CarChangeApprovalDecisionConstants.Pending;

    [ForeignKey(nameof(Actor))]
    public int? ActorUserID { get; set; }
    public User? Actor { get; set; }

    public DateTime? DecidedAt { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class CarChangeSnapshot
{
    public string LicensePlate { get; set; } = string.Empty;
    public string? VIN { get; set; }
    public int BrandID { get; set; }
    public int? CarModelID { get; set; }
    public string CarName { get; set; } = string.Empty;
    public int Seats { get; set; }
    public int ManufactureYear { get; set; }
    public string Transmission { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public string? FuelConsumption { get; set; }
    public string? Description { get; set; }
    public DateTime? NextInspectionDate { get; set; }
    public int? ServiceTypeID { get; set; }
    public decimal PricePerDay { get; set; }
    public decimal WeeklyDiscountPercent { get; set; }
    public decimal MonthlyDiscountPercent { get; set; }
    public bool DeliveryEnabled { get; set; }
    public decimal MaxDeliveryDistanceKm { get; set; }
    public decimal DeliveryFeePerKm { get; set; }
    public decimal FreeDeliveryRadiusKm { get; set; }
    public bool MileageLimitEnabled { get; set; }
    public decimal DailyMileageLimitKm { get; set; }
    public decimal ExtraMileageFeePerKm { get; set; }
    public string? RentalTerms { get; set; }
    public string ContractSigningMode { get; set; } = CarContractSigningModes.PhysicalSelfManaged;
    public int LocationID { get; set; }
    public string? CarAddress { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int CarTypeID { get; set; }
    public bool IsSelfDriveAllowed { get; set; } = true;
    public bool IsWithDriverAllowed { get; set; }
    public string TransportPermitStatus { get; set; } = "NotRequired";
    public decimal CommissionRate { get; set; } = 20m;
    public List<int> AmenityIDs { get; set; } = new();
    public List<string> ImageUrls { get; set; } = new();
}
