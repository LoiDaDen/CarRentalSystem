using CarRentalSystem.Models;

namespace CarRentalSystem.ViewModels
{
    public class BlogListViewModel
    {
        public List<Blog> Blogs { get; set; } = new();
        public Blog? FeaturedBlog { get; set; }
        public List<BlogCategory> Categories { get; set; } = new();
        public List<BlogTag> PopularTags { get; set; } = new();
        public List<Blog> LatestBlogs { get; set; } = new();
        public string? CurrentKeyword { get; set; }
        public string? CurrentCategorySlug { get; set; }
        public string? CurrentTagSlug { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalItems { get; set; }
        public int PageSize { get; set; } = 9;
    }
}
