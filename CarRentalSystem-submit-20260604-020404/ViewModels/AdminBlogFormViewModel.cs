using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CarRentalSystem.ViewModels
{
    public class AdminBlogFormViewModel
    {
        public int BlogID { get; set; }

        [Required, StringLength(220)]
        public string Title { get; set; } = string.Empty;

        [StringLength(260)]
        public string? Slug { get; set; }

        [Required, StringLength(500)]
        public string Summary { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public int CategoryID { get; set; }

        [Required, StringLength(120)]
        public string AuthorName { get; set; } = "CarRental Team";

        public DateTime PublishedDate { get; set; } = DateTime.UtcNow;

        [Range(1, 60)]
        public int ReadingTime { get; set; } = 5;

        public bool IsFeatured { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; } = "Draft";

        [StringLength(260)]
        public string? MetaTitle { get; set; }

        [StringLength(320)]
        public string? MetaDescription { get; set; }

        [StringLength(500)]
        public string? ThumbnailUrl { get; set; }

        [StringLength(500)]
        public string? CoverImageUrl { get; set; }

        [StringLength(220)]
        public string? ThumbnailAlt { get; set; }

        [StringLength(220)]
        public string? CoverImageAlt { get; set; }

        public string? TagsCsv { get; set; }
        public IFormFile? ThumbnailFile { get; set; }
        public IFormFile? CoverFile { get; set; }
    }
}
