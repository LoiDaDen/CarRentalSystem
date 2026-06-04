using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class LongTermRental
    {
        [Key]
        public int RentalID { get; set; }
        
        public int BookingID { get; set; }
        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositAmount { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyRate { get; set; }
        
        public int TermMonths { get; set; }
        
        public ICollection<PaymentSchedule> PaymentSchedules { get; set; } = new List<PaymentSchedule>();

        // Phase 10: Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
