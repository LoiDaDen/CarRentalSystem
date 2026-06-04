using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Blog
    {
        [Key]
        public int BlogID { get; set; }

        [Required, StringLength(220)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(260)]
        public string Slug { get; set; } = string.Empty;

        [Required, StringLength(500)]
        public string Summary { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Content { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ThumbnailUrl { get; set; }

        [StringLength(500)]
        public string? CoverImageUrl { get; set; }

        [StringLength(220)]
        public string? ThumbnailAlt { get; set; }

        [StringLength(220)]
        public string? CoverImageAlt { get; set; }

        public int CategoryID { get; set; }
        public BlogCategory? Category { get; set; }

        [Required, StringLength(120)]
        public string AuthorName { get; set; } = "CarRental Team";

        public DateTime PublishedDate { get; set; } = DateTime.UtcNow;
        public int ReadingTime { get; set; } = 5;
        public int ViewCount { get; set; }
        public bool IsFeatured { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; } = BlogStatusConstants.Draft;

        [StringLength(260)]
        public string? MetaTitle { get; set; }

        [StringLength(320)]
        public string? MetaDescription { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<BlogTagMapping> BlogTagMappings { get; set; } = new List<BlogTagMapping>();
    }
}
