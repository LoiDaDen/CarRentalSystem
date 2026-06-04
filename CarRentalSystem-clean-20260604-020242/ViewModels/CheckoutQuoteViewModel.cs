namespace CarRentalSystem.ViewModels
{
    public class CheckoutQuoteLineItem
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public int? AddOnID { get; set; }
    }

    public class CheckoutQuoteViewModel
    {
        public int UserID { get; set; }
        public int CarID { get; set; }
        public int? ServiceTypeID { get; set; }
        public string ServiceTypeCode { get; set; } = "SELF_DRIVE";
        public int? CustomerDrivingLicenseID { get; set; }
        public string CarName { get; set; } = string.Empty;
        public string CarBrand { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalHours { get; set; }
        public int RentalDays { get; set; }
        public int PickupLocationID { get; set; }
        public int ReturnLocationID { get; set; }
        public string? PickupLocationName { get; set; }
        public decimal BaseRentalAmount { get; set; }
        public decimal ServiceFee { get; set; }
        public decimal DeliveryFee { get; set; }
        public string? DeliveryAddress { get; set; }
        public string DeliveryMode { get; set; } = "CustomerPickup";
        public double? DeliveryLatitude { get; set; }
        public double? DeliveryLongitude { get; set; }
        public decimal DeliveryDistanceKm { get; set; }
        public int? DeliveryEtaMinutes { get; set; }
        public DateTime? DeliveryQuotedAt { get; set; }
        public decimal InsuranceFee { get; set; }
        public string InsuranceTier { get; set; } = "Basic";
        public int? ContractTypeID { get; set; }
        public bool IsLongTerm { get; set; }
        public int? LongTermMonths { get; set; }
        public decimal AddOnFee { get; set; }
        public decimal DriverFee { get; set; }
        public int? DriverID { get; set; }
        public string? DriverName { get; set; }
        public int? PromotionID { get; set; }
        public decimal MembershipDiscountAmount { get; set; }
        public decimal PromotionDiscountAmount { get; set; }
        public decimal ReferralDiscountAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalBeforeDiscount { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal HoldingFee { get; set; }
        public decimal RemainingRentalAmount { get; set; }
        public decimal CollateralAmount { get; set; }
        public int? TermsDocumentVersionID { get; set; }
        public int? PrivacyNoticeVersionID { get; set; }
        public DateTime? SelfDriveAgreementAcceptedAt { get; set; }
        public string? SelfDriveAgreementIpAddress { get; set; }
        public string? SelfDriveAgreementUserAgent { get; set; }
        public List<CheckoutQuoteLineItem> Items { get; set; } = new();
        public List<CheckoutQuoteLineItem> AddOns { get; set; } = new();
    }
}
