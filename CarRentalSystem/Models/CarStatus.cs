using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class CarStatus
    {
        [Key]
        public int CarStatusID { get; set; }

        [Required, MaxLength(50)]
        public required string Name { get; set; }

        public ICollection<Car>? Cars { get; set; } = new List<Car>();
    }
}
