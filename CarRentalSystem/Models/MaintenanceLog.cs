using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class MaintenanceLog
    {
        [Key]
        public int LogID { get; set; }
        public int CarID { get; set; }
        [ForeignKey("CarID")]
        public Car? Car { get; set; }
        
        [Required]
        public DateTime ServiceDate { get; set; }
        
        [Required]
        [StringLength(500)]
        public string Description { get; set; }
        
        public decimal Cost { get; set; }
        public int OdometerAtService { get; set; }
        
        [StringLength(100)]
        public string ServiceCenter { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
