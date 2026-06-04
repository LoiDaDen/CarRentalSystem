using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class BlogTag
    {
        [Key]
        public int BlogTagID { get; set; }

        [Required, StringLength(80)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(120)]
        public string Slug { get; set; } = string.Empty;

        public ICollection<BlogTagMapping> BlogTagMappings { get; set; } = new List<BlogTagMapping>();
    }
}
