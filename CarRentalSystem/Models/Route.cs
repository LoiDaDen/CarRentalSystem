using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Route
    {
        [Key]
        public int RouteID { get; set; }
        public int BookingID { get; set; }
        [Required, MaxLength(200)]
        public string StartLocation { get; set; }
        [Required, MaxLength(200)]
        public string EndLocation { get; set; }
        public string? RouteData { get; set; } // JSON coords

        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }
    }
}
