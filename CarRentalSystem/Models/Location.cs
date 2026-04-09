using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class Location
    {
        [Key]
        public int LocationID { get; set; }

        [Required, MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(200)]
        public string? Address { get; set; }

        public double Latitude { get; set; } = 10.7769; // Default to SG
        public double Longitude { get; set; } = 106.7009;

        public ICollection<Car>? Cars { get; set; } = new List<Car>();
    }
}
