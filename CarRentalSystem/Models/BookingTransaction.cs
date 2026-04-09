using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class BookingTransaction
    {
        [Key]
        public int TransactionID { get; set; }
        
        public int UserID { get; set; }
        [ForeignKey("UserID")]
        public User? User { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        
        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Paid, Failed
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }
        
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public DigitalContract? DigitalContract { get; set; }
    }
}
