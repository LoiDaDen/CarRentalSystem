using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Review
    {
        [Key]
        public int ReviewID { get; set; }

        [Required]
        [ForeignKey("Car")]
        public int CarID { get; set; }
        public Car? Car { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserID { get; set; }
        public User? User { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Điểm đánh giá phải từ 1 đến 5 sao")]
        public int Rating { get; set; }

        [MaxLength(2000)]
        public string? Comment { get; set; }
        public bool IsApproved { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Phase 10: Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
