using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers
{
    [ApiController]
    [Route("api/blogs")]
    public class BlogApiController : ControllerBase
    {
        private readonly IBlogService _blogService;

        public BlogApiController(IBlogService blogService)
        {
            _blogService = blogService;
        }

        [HttpGet]
        public async Task<IActionResult> GetBlogs([FromQuery] string? keyword, [FromQuery] string? category, [FromQuery] string? tag, [FromQuery] int page = 1)
        {
            var result = await _blogService.GetPublishedBlogsAsync(keyword, category, tag, page, 12);
            return Ok(new
            {
                result.TotalItems,
                result.TotalPages,
                result.Page,
                items = result.Items.Select(b => ToDto(b))
            });
        }

        [HttpGet("featured")]
        public async Task<IActionResult> Featured()
        {
            var items = await _blogService.GetFeaturedBlogsAsync(6);
            return Ok(items.Select(b => ToDto(b)));
        }

        [HttpGet("latest")]
        public async Task<IActionResult> Latest()
        {
            var items = await _blogService.GetLatestBlogsAsync(6);
            return Ok(items.Select(b => ToDto(b)));
        }

        [HttpGet("category/{categorySlug}")]
        public async Task<IActionResult> ByCategory(string categorySlug)
        {
            var result = await _blogService.GetPublishedBlogsAsync(null, categorySlug, null, 1, 24);
            return Ok(result.Items.Select(b => ToDto(b)));
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? keyword)
        {
            var result = await _blogService.GetPublishedBlogsAsync(keyword, null, null, 1, 24);
            return Ok(result.Items.Select(b => ToDto(b)));
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> Detail(string slug)
        {
            var blog = await _blogService.GetPublishedBySlugAsync(slug);
            return blog == null ? NotFound() : Ok(ToDto(blog, includeContent: true));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BlogApiRequest request)
        {
            var blog = FromRequest(request);
            var created = await _blogService.CreateBlogAsync(blog, request.Tags ?? Array.Empty<string>());
            return CreatedAtAction(nameof(Detail), new { slug = created.Slug }, ToDto(created, true));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] BlogApiRequest request)
        {
            var updated = await _blogService.UpdateBlogAsync(id, FromRequest(request), request.Tags ?? Array.Empty<string>());
            return updated == null ? NotFound() : Ok(ToDto(updated, true));
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            return await _blogService.DeleteBlogAsync(id) ? NoContent() : NotFound();
        }

        private static object ToDto(Blog blog, bool includeContent = false)
        {
            return new
            {
                id = blog.BlogID,
                title = blog.Title,
                slug = blog.Slug,
                summary = blog.Summary,
                content = includeContent ? blog.Content : null,
                thumbnailUrl = blog.ThumbnailUrl,
                coverImageUrl = blog.CoverImageUrl,
                categoryId = blog.CategoryID,
                categoryName = blog.Category?.Name,
                categorySlug = blog.Category?.Slug,
                tags = blog.BlogTagMappings.Select(m => m.BlogTag?.Name).Where(t => t != null),
                blog.AuthorName,
                blog.PublishedDate,
                blog.ReadingTime,
                blog.ViewCount,
                blog.IsFeatured,
                blog.Status,
                blog.MetaTitle,
                blog.MetaDescription
            };
        }

        private static Blog FromRequest(BlogApiRequest request)
        {
            return new Blog
            {
                Title = request.Title,
                Slug = request.Slug ?? string.Empty,
                Summary = request.Summary,
                Content = request.Content,
                ThumbnailUrl = request.ThumbnailUrl,
                CoverImageUrl = request.CoverImageUrl,
                ThumbnailAlt = request.ThumbnailAlt,
                CoverImageAlt = request.CoverImageAlt,
                CategoryID = request.CategoryId,
                AuthorName = request.AuthorName ?? "CarRental Team",
                PublishedDate = request.PublishedDate ?? DateTime.UtcNow,
                ReadingTime = request.ReadingTime ?? 0,
                ViewCount = request.ViewCount ?? 0,
                IsFeatured = request.IsFeatured,
                Status = request.Status ?? BlogStatusConstants.Draft,
                MetaTitle = request.MetaTitle,
                MetaDescription = request.MetaDescription
            };
        }

        public class BlogApiRequest
        {
            public string Title { get; set; } = string.Empty;
            public string? Slug { get; set; }
            public string Summary { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string? ThumbnailUrl { get; set; }
            public string? CoverImageUrl { get; set; }
            public string? ThumbnailAlt { get; set; }
            public string? CoverImageAlt { get; set; }
            public int CategoryId { get; set; }
            public string? AuthorName { get; set; }
            public DateTime? PublishedDate { get; set; }
            public int? ReadingTime { get; set; }
            public int? ViewCount { get; set; }
            public bool IsFeatured { get; set; }
            public string? Status { get; set; }
            public string? MetaTitle { get; set; }
            public string? MetaDescription { get; set; }
            public string[]? Tags { get; set; }
        }
    }
}
