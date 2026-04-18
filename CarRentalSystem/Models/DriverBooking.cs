using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class DriverBooking
    {
        [Key]
        public int DriverBookingID { get; set; }
        
        public int BookingID { get; set; }
        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }
        
        public int DriverID { get; set; }
        [ForeignKey("DriverID")]
        public Driver? Driver { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal DriverFee { get; set; }
        
        [MaxLength(30)]
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Active, Completed, Cancelled
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
