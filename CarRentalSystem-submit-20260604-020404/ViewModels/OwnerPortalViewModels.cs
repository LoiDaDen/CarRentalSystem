using CarRentalSystem.Models;

namespace CarRentalSystem.ViewModels;

public class OwnerPortalChromeViewModel
{
    public string ActiveTab { get; set; } = "Cars";
    public decimal WalletBalance { get; set; }
    public int CarsCount { get; set; }
    public IReadOnlyList<OwnerPortalTabViewModel> Tabs { get; set; } = Array.Empty<OwnerPortalTabViewModel>();
}

public record OwnerPortalTabViewModel(string Key, string Text, string Action, string Icon);

public class OwnerCarsDashboardViewModel
{
    public OwnerPortalChromeViewModel Chrome { get; set; } = new();
    public IReadOnlyList<OwnerCarCardViewModel> Cars { get; set; } = Array.Empty<OwnerCarCardViewModel>();
    public string? Query { get; set; }
    public string Service { get; set; } = "all";
    public string Status { get; set; } = "all";
    public int TotalCars { get; set; }
    public int ApprovedCars { get; set; }
    public int PendingCars { get; set; }
    public int ActiveTrips { get; set; }
    public decimal MonthRevenue { get; set; }
}

public class OwnerCarCardViewModel
{
    public int CarID { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = "/images/default-car.png";
    public string ServiceLabel { get; set; } = "Xe tự lái";
    public string StatusName { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public decimal PricePerDay { get; set; }
    public int TripCount { get; set; }
    public int ActiveBookingCount { get; set; }
    public decimal MonthRevenue { get; set; }
    public bool HasGps { get; set; }
    public bool HasEnoughImages { get; set; }
    public bool HasRequiredDocuments { get; set; }
    public string ContractSigningMode { get; set; } = CarContractSigningModes.PhysicalSelfManaged;
    public string ContractSigningModeLabel { get; set; } = "Hợp đồng giấy tự quản";
    public int Seats { get; set; }
    public string? FuelType { get; set; }
    public string? Transmission { get; set; }
    public DateTime? NextInspectionDate { get; set; }
    public string InspectionStatusLabel { get; set; } = string.Empty;
    public string InspectionStatusClass { get; set; } = "warn";
    public string InspectionStatusIcon { get; set; } = "bi-calendar-x";
}

public class OwnerCalendarViewModel
{
    public OwnerPortalChromeViewModel Chrome { get; set; } = new();
    public IReadOnlyList<OwnerCarOptionViewModel> Cars { get; set; } = Array.Empty<OwnerCarOptionViewModel>();
    public IReadOnlyList<string> Brands { get; set; } = Array.Empty<string>();
    public IReadOnlyList<OwnerCalendarBookingRowViewModel> Bookings { get; set; } = Array.Empty<OwnerCalendarBookingRowViewModel>();
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? CarId { get; set; }
    public string Brand { get; set; } = "all";
    public string Status { get; set; } = "all";
    public string? Query { get; set; }
}

public class OwnerCarOptionViewModel
{
    public int CarID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
}

public class OwnerCalendarBookingRowViewModel
{
    public int BookingID { get; set; }
    public int CarID { get; set; }
    public string CarName { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string StatusClass { get; set; } = "neutral";
    public decimal TotalPrice { get; set; }
}

public class OwnerDocumentViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-file-earmark-text";
    public string FileType { get; set; } = "PDF";
}

public class OwnerGuideViewModel
{
    public OwnerPortalChromeViewModel Chrome { get; set; } = new();
    public IReadOnlyList<OwnerDocumentViewModel> Documents { get; set; } = Array.Empty<OwnerDocumentViewModel>();
}

public class OwnerFinesViewModel
{
    public OwnerPortalChromeViewModel Chrome { get; set; } = new();
    public IReadOnlyList<OwnerCarOptionViewModel> Cars { get; set; } = Array.Empty<OwnerCarOptionViewModel>();
    public int? CarId { get; set; }
    public string? Plate { get; set; }
    public string? NormalizedPlate { get; set; }
    public bool HasQuery { get; set; }
    public bool IsPlateOwned { get; set; }
}

public class OwnerTaxInfoViewModel
{
    public OwnerPortalChromeViewModel Chrome { get; set; } = new();
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
}

public class OwnerTrackingViewModel
{
    public OwnerPortalChromeViewModel Chrome { get; set; } = new();
    public IReadOnlyList<OwnerTrackingCarViewModel> Cars { get; set; } = Array.Empty<OwnerTrackingCarViewModel>();
    public IReadOnlyList<OwnerTrackingMapPointViewModel> MapPoints { get; set; } = Array.Empty<OwnerTrackingMapPointViewModel>();
    public string? Query { get; set; }
    public string GpsFilter { get; set; } = "all";
}

public class OwnerTrackingCarViewModel
{
    public int CarID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public bool HasGps { get; set; }
    public bool IsLive { get; set; }
    public DateTime? LastUpdated { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class OwnerTrackingMapPointViewModel
{
    public int CarID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Status { get; set; } = "fallback";
    public string? LastUpdatedText { get; set; }
}

public class OwnerContractsViewModel
{
    public OwnerPortalChromeViewModel Chrome { get; set; } = new();
    public IReadOnlyList<OwnerDocumentViewModel> Templates { get; set; } = Array.Empty<OwnerDocumentViewModel>();
    public IReadOnlyList<OwnerContractRowViewModel> Contracts { get; set; } = Array.Empty<OwnerContractRowViewModel>();
    public OwnerDigitalSignatureProfileViewModel? SignatureProfile { get; set; }
    public IReadOnlyList<OwnerContractCarModeViewModel> Cars { get; set; } = Array.Empty<OwnerContractCarModeViewModel>();
    public IReadOnlyList<OwnerDigitalContractRowViewModel> DigitalContracts { get; set; } = Array.Empty<OwnerDigitalContractRowViewModel>();
}

public class OwnerContractRowViewModel
{
    public int PhysicalContractID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? CarName { get; set; }
    public string? LicensePlate { get; set; }
    public int? BookingID { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class OwnerDigitalSignatureProfileViewModel
{
    public string ProviderName { get; set; } = string.Empty;
    public string CertificateSerialNumber { get; set; } = string.Empty;
    public string CertificateSubject { get; set; } = string.Empty;
    public DateTime? CertificateValidUntil { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SignatureImageUrl { get; set; }
    public bool IsReady { get; set; }
}

public class OwnerDigitalSignatureProfileInputModel
{
    public string ProviderName { get; set; } = string.Empty;
    public string CertificateSerialNumber { get; set; } = string.Empty;
    public string CertificateSubject { get; set; } = string.Empty;
    public DateTime? CertificateValidUntil { get; set; }
}

public class OwnerContractCarModeViewModel
{
    public int CarID { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string ContractSigningMode { get; set; } = CarContractSigningModes.PhysicalSelfManaged;
    public string ContractSigningModeLabel { get; set; } = "Hợp đồng giấy tự quản";
    public bool CanEnableDigital { get; set; }
}

public class OwnerDigitalContractRowViewModel
{
    public int DigitalContractID { get; set; }
    public string ContractNo { get; set; } = string.Empty;
    public int? BookingID { get; set; }
    public int? TransactionID { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CarSummary { get; set; } = string.Empty;
    public bool IsSignedByCustomer { get; set; }
    public DateTime? CustomerSignDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OwnerPrivacyViewModel
{
    public OwnerPortalChromeViewModel Chrome { get; set; } = new();
    public DateTime LastUpdated { get; set; } = new(2026, 5, 24);
}

public class OwnerWalletViewModel
{
    public OwnerPortalChromeViewModel Chrome { get; set; } = new();
    public decimal Balance { get; set; }
    public decimal LockedBalance { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal EstimatedOwnerIncome { get; set; }
    public decimal MonthIncome { get; set; }
    public int CompletedTrips { get; set; }
    public IReadOnlyList<OwnerWalletTransactionRowViewModel> Transactions { get; set; } = Array.Empty<OwnerWalletTransactionRowViewModel>();
    public IReadOnlyList<OwnerWalletBookingRowViewModel> BookingRows { get; set; } = Array.Empty<OwnerWalletBookingRowViewModel>();
}

public class OwnerWalletTransactionRowViewModel
{
    public DateTime CreatedAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class OwnerWalletBookingRowViewModel
{
    public int BookingID { get; set; }
    public string CarName { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal RentalAmount { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal OwnerIncome { get; set; }
    public string StatusName { get; set; } = string.Empty;
}
