using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class TripStop
    {
        [Key]
        public int TripStopID { get; set; }

        [Required]
        public int TripBookingID { get; set; }
        public TripBooking? TripBooking { get; set; }

        public int StopOrder { get; set; }

        [Required, MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }
    }
}
