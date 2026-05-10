using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class CartItem
    {
        [Key]
        public int CartItemID { get; set; }

        [Required]
        [ForeignKey("Cart")]
        public int CartID { get; set; }
        public Cart? Cart { get; set; }

        [Required]
        [ForeignKey("Car")]
        public int CarID { get; set; }
        public Car? Car { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public bool WithDriver { get; set; } = false;

        [ForeignKey("Driver")]
        public int? DriverID { get; set; }
        public Driver? Driver { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedPrice { get; set; }

        // Thêm properties mới
        [ForeignKey("ContractType")]
        public int? ContractTypeID { get; set; }
        public ContractType? ContractType { get; set; }

        // AddOns được chọn (lưu dạng string - danh sách AddOn IDs)
        [MaxLength(500)]
        public string? SelectedAddOns { get; set; }

        // Computed property để lấy danh sách AddOn IDs
        [NotMapped]
        public List<int> AddOnIDs
        {
            get
            {
                if (string.IsNullOrEmpty(SelectedAddOns)) return new List<int>();
                try
                {
                    return SelectedAddOns.Split(',').Select(int.Parse).ToList();
                }
                catch
                {
                    return new List<int>();
                }
            }
            set
            {
                SelectedAddOns = string.Join(",", value);
            }
        }

        // Phase 10: Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
