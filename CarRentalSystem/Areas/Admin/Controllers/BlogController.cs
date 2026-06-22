using CarRentalSystem.Authorization;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class BlogController : Controller
    {
        private const long MaxBlogImageBytes = 5 * 1024 * 1024;
        private readonly IBlogService _blogService;
        private readonly IFileUploadValidationService _fileValidation;
        private readonly IWebHostEnvironment _environment;

        public BlogController(
            IBlogService blogService,
            IFileUploadValidationService fileValidation,
            IWebHostEnvironment environment)
        {
            _blogService = blogService;
            _fileValidation = fileValidation;
            _environment = environment;
        }

        public async Task<IActionResult> Index(string? keyword, string? status, int? categoryId)
        {
            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.CategoryId = categoryId;
            ViewBag.Categories = await _blogService.GetAllCategoriesAsync();
            var blogs = await _blogService.GetAdminBlogsAsync(keyword, status, categoryId);
            return View(blogs);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new AdminBlogFormViewModel
            {
                PublishedDate = DateTime.UtcNow,
                Status = BlogStatusConstants.Draft,
                AuthorName = User.Identity?.Name ?? "CarRental Team"
            };
            await PopulateCategoriesAsync(vm.CategoryID);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminBlogFormViewModel vm)
        {
            if (ModelState.IsValid)
            {
                await ApplyUploadsAsync(vm);
                if (ModelState.IsValid)
                {
                    await _blogService.CreateBlogAsync(ToBlog(vm), ParseTags(vm.TagsCsv));
                    TempData["Success"] = "Đã tạo bài viết blog thành công.";
                    return RedirectToAction(nameof(Index));
                }
            }

            await PopulateCategoriesAsync(vm.CategoryID);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var blog = await _blogService.GetByIdAsync(id);
            if (blog == null) return NotFound();

            var vm = ToViewModel(blog);
            await PopulateCategoriesAsync(vm.CategoryID);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AdminBlogFormViewModel vm)
        {
            if (id != vm.BlogID) return NotFound();

            if (ModelState.IsValid)
            {
                await ApplyUploadsAsync(vm);
                if (ModelState.IsValid)
                {
                    var updated = await _blogService.UpdateBlogAsync(id, ToBlog(vm), ParseTags(vm.TagsCsv));
                    if (updated == null) return NotFound();

                    TempData["Success"] = "Đã cập nhật bài viết blog.";
                    return RedirectToAction(nameof(Index));
                }
            }

            await PopulateCategoriesAsync(vm.CategoryID);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            if (!await _blogService.ToggleStatusAsync(id)) return NotFound();
            TempData["Success"] = "Đã cập nhật trạng thái bài viết.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await _blogService.DeleteBlogAsync(id)) return NotFound();
            TempData["Success"] = "Đã xóa bài viết blog.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateCategoriesAsync(int? selectedId)
        {
            var categories = await _blogService.GetAllCategoriesAsync();
            ViewBag.Categories = new SelectList(categories, "BlogCategoryID", "Name", selectedId);
        }

        private async Task ApplyUploadsAsync(AdminBlogFormViewModel vm)
        {
            var thumbnail = await SaveImageAsync(vm.ThumbnailFile, "ảnh thumbnail");
            if (!string.IsNullOrWhiteSpace(thumbnail))
                vm.ThumbnailUrl = thumbnail;

            var cover = await SaveImageAsync(vm.CoverFile, "ảnh cover");
            if (!string.IsNullOrWhiteSpace(cover))
                vm.CoverImageUrl = cover;
        }

        private async Task<string?> SaveImageAsync(IFormFile? file, string label)
        {
            if (file == null || file.Length == 0)
                return null;

            if (!await _fileValidation.IsAllowedImageAsync(file, MaxBlogImageBytes))
            {
                ModelState.AddModelError(string.Empty, $"{label} không hợp lệ. Vui lòng dùng JPG, PNG hoặc WEBP dưới 5MB.");
                return null;
            }

            var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var uploadDir = Path.Combine(webRoot, "images", "blogs");
            Directory.CreateDirectory(uploadDir);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var path = Path.Combine(uploadDir, fileName);
            await using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/images/blogs/{fileName}";
        }

        private static IEnumerable<string> ParseTags(string? csv)
        {
            return (csv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => !string.IsNullOrWhiteSpace(t));
        }

        private static Blog ToBlog(AdminBlogFormViewModel vm)
        {
            return new Blog
            {
                BlogID = vm.BlogID,
                Title = vm.Title,
                Slug = vm.Slug ?? string.Empty,
                Summary = vm.Summary,
                Content = vm.Content,
                ThumbnailUrl = vm.ThumbnailUrl,
                CoverImageUrl = vm.CoverImageUrl,
                ThumbnailAlt = vm.ThumbnailAlt,
                CoverImageAlt = vm.CoverImageAlt,
                CategoryID = vm.CategoryID,
                AuthorName = vm.AuthorName,
                PublishedDate = vm.PublishedDate,
                ReadingTime = vm.ReadingTime,
                IsFeatured = vm.IsFeatured,
                Status = vm.Status,
                MetaTitle = vm.MetaTitle,
                MetaDescription = vm.MetaDescription
            };
        }

        private static AdminBlogFormViewModel ToViewModel(Blog blog)
        {
            return new AdminBlogFormViewModel
            {
                BlogID = blog.BlogID,
                Title = blog.Title,
                Slug = blog.Slug,
                Summary = blog.Summary,
                Content = blog.Content,
                CategoryID = blog.CategoryID,
                AuthorName = blog.AuthorName,
                PublishedDate = blog.PublishedDate,
                ReadingTime = blog.ReadingTime,
                IsFeatured = blog.IsFeatured,
                Status = blog.Status,
                MetaTitle = blog.MetaTitle,
                MetaDescription = blog.MetaDescription,
                ThumbnailUrl = blog.ThumbnailUrl,
                CoverImageUrl = blog.CoverImageUrl,
                ThumbnailAlt = blog.ThumbnailAlt,
                CoverImageAlt = blog.CoverImageAlt,
                TagsCsv = string.Join(", ", blog.BlogTagMappings.Select(m => m.BlogTag?.Name).Where(t => !string.IsNullOrWhiteSpace(t)))
            };
        }
    }
}
