using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class VehicleTracking
    {
        [Key]
        public int TrackingID { get; set; }
        public int CarID { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; }
        
        // Phase 6: Advanced Telematics
        [Column(TypeName = "decimal(5,2)")]
        public decimal? FuelLevel { get; set; } // Percentage 0-100
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? BatteryState { get; set; } // Percentage 0-100 for EVs
        
        [StringLength(20)]
        public string EngineStatus { get; set; } = "Off"; // On, Off, Blocked
        
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [ForeignKey("CarID")]
        public Car Car { get; set; }
    }
}
