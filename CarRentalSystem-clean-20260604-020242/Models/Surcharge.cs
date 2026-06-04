using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Surcharge
    {
        [Key]
        public int SurchargeID { get; set; }

        [Required, MaxLength(40)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? Percent { get; set; }

        public int? ApplyToTripTypeID { get; set; }
        public TripType? ApplyToTripType { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
