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

        /// <summary>
        /// Tính phí cuối cùng của hợp đồng cho thuê:
        /// - TotalKm = EndKm - StartKm
        /// - FinalPrice = TotalPrice (đã cộng late fee ở controller)
        /// </summary>
        public void CalculateFees(int totalDays)
        {
            // 1. Tính quãng đường thực tế
            TotalKm = EndKm - StartKm;
            if (TotalKm < 0) TotalKm = 0;

            // 2. FinalPrice = booking's TotalPrice (đã bao gồm late fee nếu có)
            FinalPrice = Booking?.TotalPrice ?? BasePrice;
        }
    }
}
