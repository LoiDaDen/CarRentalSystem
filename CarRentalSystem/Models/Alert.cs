using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Alert
    {
        [Key]
        public int AlertID { get; set; }
        public int CarID { get; set; }
        [Required, MaxLength(50)]
        public string AlertType { get; set; } // OffRoute, Overdue, GpsLost, SuspiciousStop, TheftRisk
        public DateTime AlertTime { get; set; } = DateTime.UtcNow;
        [Required, MaxLength(20)]
        public string Status { get; set; }
        public string? Description { get; set; }

        [ForeignKey("CarID")]
        public Car? Car { get; set; }

        // Phase 10: Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
