using CarRentalSystem.Models;

namespace CarRentalSystem.ViewModels
{
    public class BlogDetailViewModel
    {
        public Blog Blog { get; set; } = new();
        public List<Blog> RelatedBlogs { get; set; } = new();
        public List<Blog> LatestBlogs { get; set; } = new();
        public List<BlogCategory> Categories { get; set; } = new();
        public List<BlogTag> PopularTags { get; set; } = new();
    }
}
