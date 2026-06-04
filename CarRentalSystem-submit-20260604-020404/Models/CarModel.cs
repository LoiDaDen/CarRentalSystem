using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class CarModel
    {
        [Key]
        public int CarModelID { get; set; }

        [Required]
        [ForeignKey("CarBrand")]
        public int BrandID { get; set; }
        public CarBrand? CarBrand { get; set; }

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        public int? FromYear { get; set; }
        public int? ToYear { get; set; }
        public int? SeatCount { get; set; }

        [MaxLength(60)]
        public string? BodyType { get; set; }

        [MaxLength(40)]
        public string? FuelType { get; set; }

        [MaxLength(40)]
        public string? Transmission { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
