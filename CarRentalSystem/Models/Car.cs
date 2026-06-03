using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Car
    {
        [Key]
        public int CarID { get; set; }

        [Required]
        [MaxLength(100)]
        public required string CarName { get; set; }

        [MaxLength(500)]
        public string? ImageURL { get; set; }

        [Required]
        [ForeignKey("CarBrand")]
        public int BrandID { get; set; }
        public CarBrand? CarBrand { get; set; }

        [ForeignKey("CarModel")]
        public int? CarModelID { get; set; }
        public CarModel? CarModel { get; set; }

        [Required]
        [Range(1, 50)]
        public int Seats { get; set; }

        [Required]
        [MaxLength(20)]
        public required string LicensePlate { get; set; }

        [MaxLength(40)]
        public string? LicensePlateNormalized { get; set; }

        [Required]
        [MaxLength(17)]
        [StringLength(17, MinimumLength = 17, ErrorMessage = "VIN phải đủ 17 ký tự")]
        public required string VIN { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Giá thuê ngày phải lớn hơn 0")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePerDay { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Giá thuê giờ phải lớn hơn 0")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePerHour { get; set; }

        [Required]
        [ForeignKey("CarStatus")]
        public int CarStatusID { get; set; }
        public CarStatus? CarStatus { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Professional Filtering & Enterprise Features
        [Required]
        [MaxLength(50)]
        public string FuelType { get; set; } = "Xăng"; // Xăng, Dầu, Điện

        [Required]
        [MaxLength(50)]
        public string Transmission { get; set; } = "Tự động"; // Số sàn, Tự động

        [MaxLength(50)]
        public string? FuelConsumption { get; set; } = "8L/100km"; // Mức tiêu hao nhiên liệu mặc định

        public string? Features { get; set; } // Comma-separated amenities: Bluetooth, GPS, BackCamera...

        [Required]
        [ForeignKey("CarType")]
        public int CarTypeID { get; set; }
        public CarType? CarType { get; set; }

        [ForeignKey("ServiceType")]
        public int? ServiceTypeID { get; set; }
        public ServiceType? ServiceType { get; set; }

        public bool IsSelfDriveAllowed { get; set; } = true;
        public bool IsWithDriverAllowed { get; set; } = false;

        [MaxLength(30)]
        public string TransportPermitStatus { get; set; } = "NotRequired";

        [MaxLength(50)]
        public string? BadgeType { get; set; }

        public DateTime? BadgeExpiryDate { get; set; }
        public bool IsApproved { get; set; } = true;

        [Required]
        [ForeignKey("Location")]
        public int LocationID { get; set; }
        public Location? Location { get; set; }

        [MaxLength(300)]
        public string? CarAddress { get; set; } // Specific pick-up address for the car

        [Range(-90, 90)]
        public double? Latitude { get; set; } // Map latitude

        [Range(-180, 180)]
        public double? Longitude { get; set; } // Map longitude

        public bool IsPremium { get; set; } = false; // Xe xịn

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? DiscountPrice { get; set; } // Sale price

        [StringLength(20)]
        public string InsuranceTier { get; set; } = "Basic"; // Basic, Premium, Full Cover

        public DateTime? NextMaintenanceDate { get; set; }

        [Range(0, int.MaxValue)]
        public int CurrentOdometer { get; set; }

        public ICollection<CarImage>? CarImages { get; set; } = new List<CarImage>();
        public ICollection<CarAmenityMapping> CarAmenityMappings { get; set; } = new List<CarAmenityMapping>();
        public ICollection<Booking>? Bookings { get; set; } = new List<Booking>();
        public ICollection<VehicleTracking>? Trackings { get; set; } = new List<VehicleTracking>();
        public ICollection<CameraGateEvent>? CameraGateEvents { get; set; } = new List<CameraGateEvent>();
        public ICollection<VehicleAreaPresence>? AreaPresences { get; set; } = new List<VehicleAreaPresence>();
        public ICollection<Alert>? Alerts { get; set; } = new List<Alert>();
        public ICollection<Review>? Reviews { get; set; } = new List<Review>();
        public ICollection<MaintenanceLog>? MaintenanceLogs { get; set; } = new List<MaintenanceLog>();

        // Phase 2: Business Fees
        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal OverTimeFee { get; set; } = 100000m;  // 100k per extra hour

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CleaningFee { get; set; } = 150000m;  // Base cleaning fee

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultDeposit { get; set; } = 15000000m; // Default 15-30M VND deposit

        // Phase 3: Maintenance & Reviews
        public DateTime? NextInspectionDate { get; set; }
        public int? NextOilChangeOdo { get; set; }

        // Phase 4: P2P Host Engine (Marketplace)
        [ForeignKey("Owner")]
        public int? OwnerID { get; set; } // Nullable. If null -> Car belongs to the Platform (B2C)
        public User? Owner { get; set; }

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal CommissionRate { get; set; } = 20.00m; // Platform charges 20% commission by default

        // Phase 5: Electric Vehicle Support
        public bool IsElectric { get; set; } = false;
        public int? BatteryCapacity { get; set; } // kWh
        public int? ChargeCyclesEstimate { get; set; } // Estimated charge cycles for rental period
        public int? RangePerCharge { get; set; } // km per full charge

        // Phase 5: Partner Discount
        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal? PartnerDiscountPercent { get; set; } // Partner can set discount, validate <= regular price

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal WeeklyDiscountPercent { get; set; }

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal MonthlyDiscountPercent { get; set; }

        public bool DeliveryEnabled { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal MaxDeliveryDistanceKm { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryFeePerKm { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal FreeDeliveryRadiusKm { get; set; }

        public bool MileageLimitEnabled { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal DailyMileageLimitKm { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExtraMileageFeePerKm { get; set; }

        [MaxLength(1200)]
        public string? RentalTerms { get; set; }

        [MaxLength(40)]
        public string ContractSigningMode { get; set; } = CarContractSigningModes.PhysicalSelfManaged;

        // Phase 10: Soft Delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
