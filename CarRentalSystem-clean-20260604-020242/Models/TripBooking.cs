using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class TripBooking
    {
        [Key]
        public int TripBookingID { get; set; }

        [Required]
        public int BookingID { get; set; }
        public Booking? Booking { get; set; }

        [Required]
        public int CustomerID { get; set; }
        public User? Customer { get; set; }

        public int? CarID { get; set; }
        public Car? Car { get; set; }

        public int? DriverID { get; set; }
        public Driver? Driver { get; set; }

        [Required]
        public int TripTypeID { get; set; }
        public TripType? TripType { get; set; }

        [Required, MaxLength(500)]
        public string PickupLocation { get; set; } = string.Empty;
        public double? PickupLatitude { get; set; }
        public double? PickupLongitude { get; set; }

        [MaxLength(500)]
        public string? DropoffLocation { get; set; }
        public double? DropoffLatitude { get; set; }
        public double? DropoffLongitude { get; set; }

        public DateTime PickupTime { get; set; }
        public DateTime? ReturnTime { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal EstimatedDistanceKm { get; set; }
        public int EstimatedDurationMinutes { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal BasePrice { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal DistanceFee { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TimeFee { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal DriverFee { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TollFee { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal WaitingFee { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal OvernightFee { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal ExtraKmFee { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal SurchargeFee { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal DiscountAmount { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TotalAmount { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = BookingStatusConstants.Pending;

        [MaxLength(1000)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        public ICollection<TripStop> TripStops { get; set; } = new List<TripStop>();
    }
}
