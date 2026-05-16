using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class RoutePrice
    {
        [Key]
        public int RoutePriceID { get; set; }

        [Required, MaxLength(100)]
        public string FromProvince { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? FromDistrict { get; set; }

        [Required, MaxLength(100)]
        public string ToProvince { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ToDistrict { get; set; }

        public int TripTypeID { get; set; }
        public TripType? TripType { get; set; }

        public int CarTypeID { get; set; }
        public CarType? CarType { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal BasePrice { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal IncludedKm { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal ExtraKmPrice { get; set; }
        public int? IncludedHours { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal? ExtraHourPrice { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal OvernightFee { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal WaitingFeePerHour { get; set; }
        public bool TollFeeIncluded { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
