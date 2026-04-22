using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Review
    {
        [Key]
        public int ReviewID { get; set; }
        
        public int CarID { get; set; }
        [ForeignKey("CarID")]
        public Car? Car { get; set; }
        
        public int UserID { get; set; }
        [ForeignKey("UserID")]
        public User? User { get; set; }
        
        [Range(1, 5)]
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public bool IsApproved { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Phase 10: Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
