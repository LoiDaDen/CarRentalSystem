using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Booking
    {
        [Key]
        public int BookingID { get; set; }
        public int UserID { get; set; }
        public int CarID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int PickupLocationID { get; set; }
        [ForeignKey("PickupLocationID")]
        public Location? PickupLocation { get; set; }

        public int ReturnLocationID { get; set; }
        [ForeignKey("ReturnLocationID")]
        public Location? ReturnLocation { get; set; }
        
        public decimal TotalPrice { get; set; }
        public int TotalHours { get; set; }
        
        public int BookingStatusID { get; set; }
        [ForeignKey("BookingStatusID")]
        public BookingStatus? BookingStatus { get; set; }
        
        [StringLength(20)]
        public string InsuranceTier { get; set; } = "Basic"; // Basic, Premium, Full Cover
        
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
        public decimal DepositAmount { get; set; } = 0m;
        [MaxLength(20)]
        public string DepositStatus { get; set; } = "Pending"; // Pending, Holding, Refunded, Deducted
        public DateTime? ActualReturnDate { get; set; }
        public DateTime? ActualPickupDate { get; set; }
        public ICollection<HandoverRecord>? HandoverRecords { get; set; } = new List<HandoverRecord>();

        // Phase 3: Promotions, Add-ons & Delivery
        public int? PromotionID { get; set; }
        [ForeignKey("PromotionID")]
        public Promotion? Promotion { get; set; }
        public decimal DiscountAmount { get; set; } = 0m;

        [MaxLength(255)]
        public string? DeliveryAddress { get; set; }
        public decimal DeliveryFee { get; set; } = 0m;
        public decimal AddOnFee { get; set; } = 0m;
        public string? CancelReason { get; set; }
        public string? AdminNotes { get; set; }

        public ICollection<BookingAddOn>? BookingAddOns { get; set; } = new List<BookingAddOn>();

        // Phase 5: Contract Types & Extended Fields
        public int? ContractTypeID { get; set; }
        [ForeignKey("ContractTypeID")]
        public ContractType? ContractType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LateFeeAmount { get; set; } = 0;

        // Driver
        public int? DriverBookingID { get; set; }

        public int? TransactionID { get; set; }
        [ForeignKey("TransactionID")]
        public BookingTransaction? BookingTransaction { get; set; }

        // Shipping
        [Column(TypeName = "decimal(10,2)")]
        public decimal ShippingDistance { get; set; } = 0; // km

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingFee { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal FuelFee { get; set; } = 0;
    }
}
