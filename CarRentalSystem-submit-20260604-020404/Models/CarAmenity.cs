using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class CarAmenity
    {
        [Key]
        public int CarAmenityID { get; set; }

        [Required]
        [MaxLength(60)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(80)]
        public string? Icon { get; set; }

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<CarAmenityMapping> CarAmenityMappings { get; set; } = new List<CarAmenityMapping>();
    }
}
