using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class AvailabilitySchedule
    {
        [Key]
        public int ScheduleID { get; set; }
        
        // Dynamic referencing (CarID or DriverID)
        public int EntityID { get; set; }
        
        [StringLength(20)]
        public string EntityType { get; set; } = "Car"; // "Car" or "Driver"
        
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        
        [StringLength(20)]
        public string Status { get; set; } = "Reserved"; // "Reserved" (in cart), "Booked" (paid), "Unavailable" (maintenance/etc.)
    }
}
