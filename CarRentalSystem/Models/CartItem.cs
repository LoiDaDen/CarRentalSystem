using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class CartItem
    {
        [Key]
        public int CartItemID { get; set; }

        public int CartID { get; set; }
        [ForeignKey("CartID")]
        public Cart? Cart { get; set; }

        public int CarID { get; set; }
        [ForeignKey("CarID")]
        public Car? Car { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool WithDriver { get; set; } = false;

        public int? DriverID { get; set; }
        [ForeignKey("DriverID")]
        public Driver? Driver { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedPrice { get; set; }

        // Thêm properties mới
        public int? ContractTypeID { get; set; }
        [ForeignKey("ContractTypeID")]
        public ContractType? ContractType { get; set; }

        // AddOns được chọn (lưu dạng string - danh sách AddOn IDs)
        public string? SelectedAddOns { get; set; }

        // Computed property để lấy danh sách AddOn IDs
        [NotMapped]
        public List<int> AddOnIDs
        {
            get
            {
                if (string.IsNullOrEmpty(SelectedAddOns)) return new List<int>();
                return SelectedAddOns.Split(',').Select(int.Parse).ToList();
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
