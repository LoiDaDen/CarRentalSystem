using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class Location
    {
        [Key]
        public int LocationID { get; set; }

        [Required]
        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(200)]
        public string? Address { get; set; }

        [Required]
        [Range(-90, 90)]
        public double Latitude { get; set; } = 10.7769; // Default to SG

        [Required]
        [Range(-180, 180)]
        public double Longitude { get; set; } = 106.7009;

        public ICollection<Car>? Cars { get; set; } = new List<Car>();

        // Phase 10: Area Management
        public ICollection<AreaLocation>? AreaLocations { get; set; } = new List<AreaLocation>();
    }
}
