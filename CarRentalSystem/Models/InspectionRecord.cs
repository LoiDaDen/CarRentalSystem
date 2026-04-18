using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class InspectionRecord
    {
        [Key]
        public int InspectionID { get; set; }
        
        public int BookingID { get; set; }
        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }
        
        public int? InspectorID { get; set; } // Nullable, Admin/Inspector
        [ForeignKey("InspectorID")]
        public User? Inspector { get; set; }
        
        public string? PhotosJson { get; set; } // Array of photo URLs
        
        [StringLength(1000)]
        public string? ReportDetails { get; set; }
        
        public bool IsApproved { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? InspectedAt { get; set; }
    }
}
