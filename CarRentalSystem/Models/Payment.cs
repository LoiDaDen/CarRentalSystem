using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Payment
    {
        [Key]
        public int PaymentID { get; set; }
        public int BookingID { get; set; }
        public decimal Amount { get; set; }
        [Required, MaxLength(50)]
        public string PaymentMethod { get; set; } // Cash, Online
        public DateTime PaymentDate { get; set; } = DateTime.Now;
        [Required, MaxLength(20)]
        public string PaymentStatus { get; set; } // Paid, Pending, Failed

        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }
    }
}
