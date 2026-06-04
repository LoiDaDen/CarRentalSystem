using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class CarType
    {
        [Key]
        public int CarTypeID { get; set; }

        [Required, MaxLength(50)]
        public required string Name { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        public ICollection<Car>? Cars { get; set; } = new List<Car>();
    }
}
