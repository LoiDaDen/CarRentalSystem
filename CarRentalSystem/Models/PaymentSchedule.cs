using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class PaymentSchedule
    {
        [Key]
        public int ScheduleID { get; set; }
        
        public int RentalID { get; set; }
        [ForeignKey("RentalID")]
        public LongTermRental? LongTermRental { get; set; }
        
        public DateTime DueDate { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        
        [StringLength(30)]
        public string Status { get; set; } = "Unpaid"; // Unpaid, Paid, Reminded, Overdue
        
        public DateTime? PaidAt { get; set; }

        // Phase 10: Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
