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

        public double? AccuracyMeters { get; set; }

        public double? Heading { get; set; }
        
        // Phase 6: Advanced Telematics
        [Column(TypeName = "decimal(5,2)")]
        public decimal? FuelLevel { get; set; } // Percentage 0-100
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? BatteryState { get; set; } // Percentage 0-100 for EVs

        [Column(TypeName = "decimal(5,2)")]
        public decimal? BatteryLevel { get; set; } // Phone/tracker battery percentage 0-100

        public bool? IsCharging { get; set; }

        [StringLength(20)]
        public string EngineStatus { get; set; } = "Off"; // On, Off, Blocked

        [StringLength(40)]
        public string Source { get; set; } = "HardwareGps";

        [StringLength(120)]
        public string? DeviceLabel { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [ForeignKey("CarID")]
        public Car? Car { get; set; }

        // Phase 10: Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
