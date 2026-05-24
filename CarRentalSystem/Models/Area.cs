// CarRentalSystem/Models/Area.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Area
    {
        [Key]
        public int AreaID { get; set; }

        [Required, MaxLength(100)]
        public required string Name { get; set; }

        [Required, MaxLength(20)]
        public required string Code { get; set; }

        [Required, MaxLength(100)]
        public required string Province { get; set; }

        [MaxLength(300)]
        public string? Address { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public int? ManagerID { get; set; }
        [ForeignKey("ManagerID")]
        public User? Manager { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "active";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        public ICollection<AreaLocation> AreaLocations { get; set; } = new List<AreaLocation>();
        public ICollection<AreaStaff> AreaStaffs { get; set; } = new List<AreaStaff>();
        public ICollection<CameraDevice> CameraDevices { get; set; } = new List<CameraDevice>();
        public ICollection<CameraGateEvent> CameraGateEvents { get; set; } = new List<CameraGateEvent>();
        public ICollection<VehicleAreaPresence> VehicleAreaPresences { get; set; } = new List<VehicleAreaPresence>();

        [NotMapped]
        public int TotalCars =>
            AreaLocations
                .SelectMany(al => al.Location?.Cars ?? Enumerable.Empty<Car>())
                .Count();

        [NotMapped]
        public int AvailableCars =>
            AreaLocations
                .SelectMany(al => al.Location?.Cars ?? Enumerable.Empty<Car>())
                .Where(c => c.CarStatusID == 1)
                .Count();
    }
}
