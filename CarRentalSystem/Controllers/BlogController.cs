using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers
{
    [Route("blog")]
    public class BlogController : Controller
    {
        private readonly IBlogService _blogService;

        public BlogController(IBlogService blogService)
        {
            _blogService = blogService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? keyword, string? category, string? tag, int page = 1)
        {
            const int pageSize = 9;
            var result = await _blogService.GetPublishedBlogsAsync(keyword, category, tag, page, pageSize);
            var featured = await _blogService.GetFeaturedBlogsAsync(1);

            var vm = new BlogListViewModel
            {
                Blogs = result.Items,
                FeaturedBlog = featured.FirstOrDefault(),
                Categories = await _blogService.GetActiveCategoriesAsync(),
                PopularTags = await _blogService.GetPopularTagsAsync(12),
                LatestBlogs = await _blogService.GetLatestBlogsAsync(5),
                CurrentKeyword = keyword,
                CurrentCategorySlug = category,
                CurrentTagSlug = tag,
                CurrentPage = result.Page,
                TotalPages = result.TotalPages,
                TotalItems = result.TotalItems,
                PageSize = result.PageSize
            };

            ViewData["Title"] = "Blog thuê xe tự lái";
            ViewData["MetaDescription"] = "Cập nhật kinh nghiệm thuê xe, mẹo lái xe an toàn, hướng dẫn đặt xe và các gợi ý du lịch tự lái.";
            return View(vm);
        }

        [HttpGet("category/{categorySlug}")]
        public IActionResult Category(string categorySlug)
        {
            return RedirectToAction(nameof(Index), new { category = categorySlug });
        }

        [HttpGet("tag/{tagSlug}")]
        public IActionResult Tag(string tagSlug)
        {
            return RedirectToAction(nameof(Index), new { tag = tagSlug });
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> Detail(string slug)
        {
            var blog = await _blogService.GetPublishedBySlugAsync(slug);
            if (blog == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("NotFound");
            }

            var vm = new BlogDetailViewModel
            {
                Blog = blog,
                RelatedBlogs = await _blogService.GetRelatedBlogsAsync(blog, 3),
                LatestBlogs = await _blogService.GetLatestBlogsAsync(5),
                Categories = await _blogService.GetActiveCategoriesAsync(),
                PopularTags = await _blogService.GetPopularTagsAsync(12)
            };

            ViewData["Title"] = blog.MetaTitle ?? blog.Title;
            ViewData["MetaDescription"] = blog.MetaDescription ?? blog.Summary;
            return View(vm);
        }
    }
}
