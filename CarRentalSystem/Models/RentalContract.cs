using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class RentalContract
    {
        [Key]
        public int ContractID { get; set; }
        public int BookingID { get; set; }
        public decimal BasePrice { get; set; }
        public decimal StartKm { get; set; }
        public decimal EndKm { get; set; }
        public decimal TotalKm { get; set; }
        public decimal FinalPrice { get; set; }

        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }

        public void CalculateFees(int totalDays)
        {
            FinalPrice = Booking?.TotalPrice ?? 0;
        }
    }
}
