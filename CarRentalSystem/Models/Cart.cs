using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Cart
    {
        [Key]
        public int CartID { get; set; }
        
        public int UserID { get; set; }
        [ForeignKey("UserID")]
        public User? User { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ExpiresAt { get; set; } // e.g., 15 mins hold time
        
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}
