using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class BlogCategory
    {
        [Key]
        public int BlogCategoryID { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(160)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Blog> Blogs { get; set; } = new List<Blog>();
    }
}
