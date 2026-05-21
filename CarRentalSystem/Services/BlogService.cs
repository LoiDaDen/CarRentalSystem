using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public class BlogService : IBlogService
    {
        private readonly ApplicationDbContext _context;

        public BlogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<BlogListResult> GetPublishedBlogsAsync(string? keyword, string? categorySlug, string? tagSlug, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 24);

            var query = PublishedQuery();
            query = ApplyFilters(query, keyword, categorySlug, tagSlug);

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var items = await query
                .OrderByDescending(b => b.IsFeatured)
                .ThenByDescending(b => b.PublishedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new BlogListResult(items, total, totalPages, page, pageSize);
        }

        public async Task<Blog?> GetPublishedBySlugAsync(string slug, bool increaseViewCount = true)
        {
            var blog = await PublishedQuery()
                .FirstOrDefaultAsync(b => b.Slug == slug);

            if (blog != null && increaseViewCount)
            {
                blog.ViewCount += 1;
                await _context.SaveChangesAsync();
            }

            return blog;
        }

        public async Task<Blog?> GetByIdAsync(int id)
        {
            return await _context.Blogs
                .Include(b => b.Category)
                .Include(b => b.BlogTagMappings).ThenInclude(m => m.BlogTag)
                .FirstOrDefaultAsync(b => b.BlogID == id && !b.IsDeleted);
        }

        public async Task<List<Blog>> GetAdminBlogsAsync(string? keyword = null, string? status = null, int? categoryId = null)
        {
            var query = _context.Blogs
                .Include(b => b.Category)
                .Include(b => b.BlogTagMappings).ThenInclude(m => m.BlogTag)
                .Where(b => !b.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.Trim().ToLower();
                query = query.Where(b =>
                    b.Title.ToLower().Contains(term) ||
                    b.Summary.ToLower().Contains(term) ||
                    b.BlogTagMappings.Any(m => m.BlogTag != null && m.BlogTag.Name.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(b => b.Status == status);

            if (categoryId.HasValue)
                query = query.Where(b => b.CategoryID == categoryId.Value);

            return await query
                .OrderByDescending(b => b.PublishedDate)
                .ThenByDescending(b => b.BlogID)
                .ToListAsync();
        }

        public async Task<List<BlogCategory>> GetActiveCategoriesAsync()
        {
            return await _context.BlogCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<List<BlogCategory>> GetAllCategoriesAsync()
        {
            return await _context.BlogCategories
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<List<BlogTag>> GetPopularTagsAsync(int count)
        {
            return await _context.BlogTags
                .Include(t => t.BlogTagMappings)
                .ThenInclude(m => m.Blog)
                .Where(t => t.BlogTagMappings.Any(m => m.Blog != null && !m.Blog.IsDeleted && m.Blog.Status == BlogStatusConstants.Published))
                .OrderByDescending(t => t.BlogTagMappings.Count(m => m.Blog != null && !m.Blog.IsDeleted && m.Blog.Status == BlogStatusConstants.Published))
                .ThenBy(t => t.Name)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<Blog>> GetFeaturedBlogsAsync(int count)
        {
            return await PublishedQuery()
                .Where(b => b.IsFeatured)
                .OrderByDescending(b => b.PublishedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<Blog>> GetLatestBlogsAsync(int count)
        {
            return await PublishedQuery()
                .OrderByDescending(b => b.PublishedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<Blog>> GetRelatedBlogsAsync(Blog blog, int count)
        {
            var tagIds = blog.BlogTagMappings.Select(m => m.BlogTagID).ToList();

            return await PublishedQuery()
                .Where(b => b.BlogID != blog.BlogID)
                .Where(b => b.CategoryID == blog.CategoryID || b.BlogTagMappings.Any(m => tagIds.Contains(m.BlogTagID)))
                .OrderByDescending(b => b.BlogTagMappings.Count(m => tagIds.Contains(m.BlogTagID)))
                .ThenByDescending(b => b.PublishedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<Blog> CreateBlogAsync(Blog blog, IEnumerable<string> tags)
        {
            await NormalizeBlogAsync(blog, null);
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();
            await SyncTagsAsync(blog, tags);
            await _context.SaveChangesAsync();
            return blog;
        }

        public async Task<Blog?> UpdateBlogAsync(int id, Blog blog, IEnumerable<string> tags)
        {
            var existing = await _context.Blogs
                .Include(b => b.BlogTagMappings)
                .FirstOrDefaultAsync(b => b.BlogID == id && !b.IsDeleted);
            if (existing == null) return null;

            existing.Title = blog.Title;
            existing.Slug = blog.Slug;
            existing.Summary = blog.Summary;
            existing.Content = blog.Content;
            existing.ThumbnailUrl = blog.ThumbnailUrl;
            existing.CoverImageUrl = blog.CoverImageUrl;
            existing.ThumbnailAlt = blog.ThumbnailAlt;
            existing.CoverImageAlt = blog.CoverImageAlt;
            existing.CategoryID = blog.CategoryID;
            existing.AuthorName = blog.AuthorName;
            existing.PublishedDate = blog.PublishedDate;
            existing.ReadingTime = blog.ReadingTime;
            existing.IsFeatured = blog.IsFeatured;
            existing.Status = blog.Status;
            existing.MetaTitle = blog.MetaTitle;
            existing.MetaDescription = blog.MetaDescription;
            existing.UpdatedAt = DateTime.UtcNow;

            await NormalizeBlogAsync(existing, id);
            await SyncTagsAsync(existing, tags);
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteBlogAsync(int id)
        {
            var blog = await _context.Blogs.FirstOrDefaultAsync(b => b.BlogID == id && !b.IsDeleted);
            if (blog == null) return false;

            blog.IsDeleted = true;
            blog.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleStatusAsync(int id)
        {
            var blog = await _context.Blogs.FirstOrDefaultAsync(b => b.BlogID == id && !b.IsDeleted);
            if (blog == null) return false;

            blog.Status = blog.Status == BlogStatusConstants.Published
                ? BlogStatusConstants.Draft
                : BlogStatusConstants.Published;
            blog.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public string GenerateSlug(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Guid.NewGuid().ToString("N")[..10];

            var normalized = text.Trim().ToLowerInvariant()
                .Replace("đ", "d")
                .Replace("Đ", "d")
                .Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (var c in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                    builder.Append(c);
            }

            var slug = Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"[^a-z0-9]+", "-");
            slug = Regex.Replace(slug, "-{2,}", "-").Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N")[..10] : slug;
        }

        private IQueryable<Blog> PublishedQuery()
        {
            return _context.Blogs
                .Include(b => b.Category)
                .Include(b => b.BlogTagMappings).ThenInclude(m => m.BlogTag)
                .Where(b => !b.IsDeleted && b.Status == BlogStatusConstants.Published && b.PublishedDate <= DateTime.UtcNow);
        }

        private static IQueryable<Blog> ApplyFilters(IQueryable<Blog> query, string? keyword, string? categorySlug, string? tagSlug)
        {
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.Trim().ToLower();
                query = query.Where(b =>
                    b.Title.ToLower().Contains(term) ||
                    b.Summary.ToLower().Contains(term) ||
                    b.Content.ToLower().Contains(term) ||
                    b.BlogTagMappings.Any(m => m.BlogTag != null && m.BlogTag.Name.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(categorySlug))
                query = query.Where(b => b.Category != null && b.Category.Slug == categorySlug);

            if (!string.IsNullOrWhiteSpace(tagSlug))
                query = query.Where(b => b.BlogTagMappings.Any(m => m.BlogTag != null && m.BlogTag.Slug == tagSlug));

            return query;
        }

        private async Task NormalizeBlogAsync(Blog blog, int? existingBlogId)
        {
            blog.Title = blog.Title.Trim();
            blog.Slug = await EnsureUniqueSlugAsync(string.IsNullOrWhiteSpace(blog.Slug) ? blog.Title : blog.Slug!, existingBlogId);
            blog.Summary = blog.Summary.Trim();
            blog.Content = SanitizeBlogHtml(blog.Content);
            blog.AuthorName = string.IsNullOrWhiteSpace(blog.AuthorName) ? "CarRental Team" : blog.AuthorName.Trim();
            blog.Status = blog.Status == BlogStatusConstants.Published ? BlogStatusConstants.Published : BlogStatusConstants.Draft;
            blog.ReadingTime = blog.ReadingTime <= 0 ? EstimateReadingTime(blog.Content) : blog.ReadingTime;
            blog.MetaTitle = string.IsNullOrWhiteSpace(blog.MetaTitle) ? blog.Title : blog.MetaTitle.Trim();
            blog.MetaDescription = string.IsNullOrWhiteSpace(blog.MetaDescription) ? blog.Summary : blog.MetaDescription.Trim();
            blog.ThumbnailAlt = string.IsNullOrWhiteSpace(blog.ThumbnailAlt) ? blog.Title : blog.ThumbnailAlt.Trim();
            blog.CoverImageAlt = string.IsNullOrWhiteSpace(blog.CoverImageAlt) ? blog.Title : blog.CoverImageAlt.Trim();
        }

        private async Task<string> EnsureUniqueSlugAsync(string source, int? existingBlogId)
        {
            var baseSlug = GenerateSlug(source);
            var slug = baseSlug;
            var suffix = 2;

            while (await _context.Blogs.AnyAsync(b => b.Slug == slug && (!existingBlogId.HasValue || b.BlogID != existingBlogId.Value)))
            {
                slug = $"{baseSlug}-{suffix++}";
            }

            return slug;
        }

        private async Task SyncTagsAsync(Blog blog, IEnumerable<string> tagNames)
        {
            var names = tagNames
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingMappings = await _context.BlogTagMappings
                .Where(m => m.BlogID == blog.BlogID)
                .ToListAsync();
            _context.BlogTagMappings.RemoveRange(existingMappings);

            foreach (var name in names)
            {
                var slug = GenerateSlug(name);
                var tag = await _context.BlogTags.FirstOrDefaultAsync(t => t.Slug == slug);
                if (tag == null)
                {
                    tag = new BlogTag { Name = name, Slug = slug };
                    _context.BlogTags.Add(tag);
                }

                _context.BlogTagMappings.Add(new BlogTagMapping
                {
                    BlogID = blog.BlogID,
                    BlogTag = tag
                });
            }
        }

        private static int EstimateReadingTime(string content)
        {
            var plain = Regex.Replace(content ?? string.Empty, "<.*?>", " ");
            var words = Regex.Matches(plain, @"\S+").Count;
            return Math.Max(1, (int)Math.Ceiling(words / 220.0));
        }

        private static string SanitizeBlogHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            var sanitized = Regex.Replace(html, @"<\s*(script|iframe|object|embed|form|input|button|textarea|select|meta|link|style)\b[^>]*>.*?<\s*/\s*\1\s*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            sanitized = Regex.Replace(sanitized, @"<\s*(script|iframe|object|embed|form|input|button|textarea|select|meta|link|style)\b[^>]*/?\s*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            sanitized = Regex.Replace(sanitized, @"\s+on[a-z]+\s*=\s*(['""]).*?\1", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            sanitized = Regex.Replace(sanitized, @"\s+on[a-z]+\s*=\s*[^\s>]+", string.Empty, RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"(href|src)\s*=\s*(['""])\s*javascript:.*?\2", "$1=\"#\"", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return sanitized;
        }
    }
}
