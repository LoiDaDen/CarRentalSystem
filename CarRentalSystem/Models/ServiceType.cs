using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class ServiceType
    {
        [Key]
        public int ServiceTypeID { get; set; }

        [Required, MaxLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Car> Cars { get; set; } = new List<Car>();
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }

    public static class ServiceTypeConstants
    {
        public const string SelfDrive = "SELF_DRIVE";
        public const string WithDriver = "WITH_DRIVER";
    }
}
