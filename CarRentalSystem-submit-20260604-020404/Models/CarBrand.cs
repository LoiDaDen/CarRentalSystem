using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class CarBrand
    {
        [Key]
        public int BrandID { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        [StringLength(255)]
        public string? LogoUrl { get; set; }

        public ICollection<CarModel> CarModels { get; set; } = new List<CarModel>();
    }
}
