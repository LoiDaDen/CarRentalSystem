using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Booking
    {
        [Key]
        public int BookingID { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserID { get; set; }

        [Required]
        [ForeignKey("Car")]
        public int CarID { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [ForeignKey("PickupLocation")]
        public int PickupLocationID { get; set; }
        public Location? PickupLocation { get; set; }

        [Required]
        [ForeignKey("ReturnLocation")]
        public int ReturnLocationID { get; set; }
        public Location? ReturnLocation { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int TotalHours { get; set; }

        [Required]
        [ForeignKey("BookingStatus")]
        public int BookingStatusID { get; set; }
        public BookingStatus? BookingStatus { get; set; }

        [StringLength(20)]
        public string InsuranceTier { get; set; } = "Basic"; // Basic, Premium, Full Cover

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserID")]
        public User? User { get; set; }
        [ForeignKey("CarID")]
        public Car? Car { get; set; }

        // Phase 10: Soft Delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public Payment? Payment { get; set; }
        public RentalContract? RentalContract { get; set; }
        public DigitalContract? DigitalContract { get; set; }
        public Route? Route { get; set; }

        // Phase 2: Risk Management & Deposit
        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositAmount { get; set; } = 0m;

        [StringLength(20)]
        public string DepositStatus { get; set; } = "Pending"; // Pending, Holding, Refunded, Deducted
        public DateTime? ActualReturnDate { get; set; }
        public DateTime? ActualPickupDate { get; set; }
        public ICollection<HandoverRecord>? HandoverRecords { get; set; } = new List<HandoverRecord>();

        // Phase 3: Promotions, Add-ons & Delivery
        [ForeignKey("Promotion")]
        public int? PromotionID { get; set; }
        public Promotion? Promotion { get; set; }

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0m;

        [MaxLength(255)]
        public string? DeliveryAddress { get; set; }

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryFee { get; set; } = 0m;

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AddOnFee { get; set; } = 0m;

        [MaxLength(1000)]
        public string? CancelReason { get; set; }

        [MaxLength(2000)]
        public string? AdminNotes { get; set; }

        public ICollection<BookingAddOn>? BookingAddOns { get; set; } = new List<BookingAddOn>();

        // Phase 5: Contract Types & Extended Fields
        [ForeignKey("ContractType")]
        public int? ContractTypeID { get; set; }
        public ContractType? ContractType { get; set; }

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LateFeeAmount { get; set; } = 0;

        // Driver
        public int? DriverBookingID { get; set; }

        [ForeignKey("TransactionID")]
        public int? TransactionID { get; set; }
        public BookingTransaction? BookingTransaction { get; set; }

        // Shipping
        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(10,2)")]
        public decimal ShippingDistance { get; set; } = 0; // km

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingFee { get; set; } = 0;

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal FuelFee { get; set; } = 0;
    }
}
