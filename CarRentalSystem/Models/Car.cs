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
        [Required, MaxLength(100)]
        public required string CarName { get; set; }
        
        [MaxLength(500)]
        public string? ImageURL { get; set; }
        
        public int BrandID { get; set; }
        [ForeignKey("BrandID")]
        public CarBrand? CarBrand { get; set; }
        
        public int Seats { get; set; }
        [Required, MaxLength(20)]
        public required string LicensePlate { get; set; }
        [Required, MaxLength(17)]
        public required string VIN { get; set; }
        public decimal PricePerDay { get; set; }
        public decimal PricePerHour { get; set; }
        public int CarStatusID { get; set; }
        [ForeignKey("CarStatusID")]
        public CarStatus? CarStatus { get; set; }
        
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Professional Filtering & Enterprise Features
        [MaxLength(50)]
        public string FuelType { get; set; } = "Xăng"; // Xăng, Dầu, Điện
        [MaxLength(50)]
        public string Transmission { get; set; } = "Tự động"; // Số sàn, Tự động

        [MaxLength(50)]
        public string? FuelConsumption { get; set; } = "8L/100km"; // Mức tiêu hao nhiên liệu mặc định
        
        public string? Features { get; set; } // Comma-separated amenities: Bluetooth, GPS, BackCamera...
        
        public int CarTypeID { get; set; }
        [ForeignKey("CarTypeID")]
        public CarType? CarType { get; set; }
        
        public int LocationID { get; set; }
        [ForeignKey("LocationID")]
        public Location? Location { get; set; }

        [MaxLength(300)]
        public string? CarAddress { get; set; } // Specific pick-up address for the car
        public double? Latitude { get; set; } // Map latitude
        public double? Longitude { get; set; } // Map longitude

        public bool IsPremium { get; set; } = false; // Xe xịn
        public decimal? DiscountPrice { get; set; } // Sale price
        
        [StringLength(20)]
        public string InsuranceTier { get; set; } = "Basic"; // Basic, Premium, Full Cover
        public DateTime? NextMaintenanceDate { get; set; }
        public int CurrentOdometer { get; set; }

        public ICollection<CarImage>? CarImages { get; set; } = new List<CarImage>();
        public ICollection<Booking>? Bookings { get; set; } = new List<Booking>();
        public ICollection<VehicleTracking>? Trackings { get; set; } = new List<VehicleTracking>();
        public ICollection<Alert>? Alerts { get; set; } = new List<Alert>();
        public ICollection<Review>? Reviews { get; set; } = new List<Review>();
        public ICollection<MaintenanceLog>? MaintenanceLogs { get; set; } = new List<MaintenanceLog>();

        // Phase 2: Business Fees
        public decimal OverTimeFee { get; set; } = 100000m;  // 100k per extra hour
        public decimal CleaningFee { get; set; } = 150000m;  // Base cleaning fee
        public decimal DefaultDeposit { get; set; } = 15000000m; // Default 15-30M VND deposit

        // Phase 3: Maintenance & Reviews
        public DateTime? NextInspectionDate { get; set; }
        public int? NextOilChangeOdo { get; set; }

        // Phase 4: P2P Host Engine (Marketplace)
        public int? OwnerID { get; set; } // Nullable. If null -> Car belongs to the Platform (B2C)
        [ForeignKey("OwnerID")]
        public User? Owner { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal CommissionRate { get; set; } = 20.00m; // Platform takes 20% commission by default

        // Phase 5: Electric Vehicle Support
        public bool IsElectric { get; set; } = false;
        public int? BatteryCapacity { get; set; } // kWh
        public int? ChargeCyclesEstimate { get; set; } // Estimated charge cycles for rental period
        public int? RangePerCharge { get; set; } // km per full charge

        // Phase 5: Partner Discount
        [Column(TypeName = "decimal(5,2)")]
        public decimal? PartnerDiscountPercent { get; set; } // Partner can set discount, validate <= regular price
    }
}
