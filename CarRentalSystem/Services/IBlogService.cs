using CarRentalSystem.Models;

namespace CarRentalSystem.Services
{
    public record BlogListResult(List<Blog> Items, int TotalItems, int TotalPages, int Page, int PageSize);

    public interface IBlogService
    {
        Task<BlogListResult> GetPublishedBlogsAsync(string? keyword, string? categorySlug, string? tagSlug, int page, int pageSize);
        Task<Blog?> GetPublishedBySlugAsync(string slug, bool increaseViewCount = true);
        Task<Blog?> GetByIdAsync(int id);
        Task<List<Blog>> GetAdminBlogsAsync(string? keyword = null, string? status = null, int? categoryId = null);
        Task<List<BlogCategory>> GetActiveCategoriesAsync();
        Task<List<BlogCategory>> GetAllCategoriesAsync();
        Task<List<BlogTag>> GetPopularTagsAsync(int count);
        Task<List<Blog>> GetFeaturedBlogsAsync(int count);
        Task<List<Blog>> GetLatestBlogsAsync(int count);
        Task<List<Blog>> GetRelatedBlogsAsync(Blog blog, int count);
        Task<Blog> CreateBlogAsync(Blog blog, IEnumerable<string> tags);
        Task<Blog?> UpdateBlogAsync(int id, Blog blog, IEnumerable<string> tags);
        Task<bool> DeleteBlogAsync(int id);
        Task<bool> ToggleStatusAsync(int id);
        string GenerateSlug(string text);
    }
}
