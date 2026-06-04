using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class CarAmenityMapping
    {
        public int CarID { get; set; }
        public Car? Car { get; set; }

        public int CarAmenityID { get; set; }
        public CarAmenity? CarAmenity { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
    }
}
