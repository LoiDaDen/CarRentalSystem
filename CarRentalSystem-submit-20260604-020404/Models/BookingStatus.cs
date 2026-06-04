using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class BookingStatus
    {
        [Key]
        public int BookingStatusID { get; set; }

        [Required, MaxLength(50)]
        public required string Name { get; set; }

        public ICollection<Booking>? Bookings { get; set; } = new List<Booking>();
    }
}
