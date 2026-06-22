using CarRentalSystem.Authorization;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class PromotionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PromotionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var promotions = await _context.Promotions
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.PromotionID)
                .ToListAsync();
            return View(promotions);
        }

        public async Task<IActionResult> TrashBin()
        {
            var deletedPromotions = await _context.Promotions
                .Where(p => p.IsDeleted)
                .OrderByDescending(p => p.DeletedAt)
                .ToListAsync();
            return View(deletedPromotions);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Promotion promotion)
        {
            if (ModelState.IsValid)
            {
                promotion.PromoCode = promotion.PromoCode.ToUpper();
                
                // Check if code exists
                if (await _context.Promotions.AnyAsync(p => p.PromoCode == promotion.PromoCode))
                {
                    ModelState.AddModelError("PromoCode", "Mã khuyến mãi này đã tồn tại.");
                    return View(promotion);
                }

                _context.Add(promotion);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm mã khuyến mãi thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(promotion);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null) return NotFound();

            return View(promotion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Promotion promotion)
        {
            if (id != promotion.PromotionID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    promotion.PromoCode = promotion.PromoCode.ToUpper();
                    _context.Update(promotion);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật mã khuyến mãi thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PromotionExists(promotion.PromotionID))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(promotion);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion != null)
            {
                promotion.IsDeleted = true;
                promotion.DeletedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã chuyển vào thùng rác.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Restore(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion != null)
            {
                promotion.IsDeleted = false;
                promotion.DeletedAt = null;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã khôi phục mã khuyến mãi.";
            }
            return RedirectToAction(nameof(TrashBin));
        }

        [HttpPost]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            var promotion = await _context.Promotions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.PromotionID == id && p.IsDeleted);

            if (promotion != null)
            {
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM Promotions WHERE PromotionID = {0}", id);
                TempData["Success"] = "Đã xóa vĩnh viễn.";
            }
            return RedirectToAction(nameof(TrashBin));
        }

        [HttpPost]
        public async Task<IActionResult> EmptyTrash()
        {
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Promotions WHERE IsDeleted = 1");
            TempData["Success"] = "Đã xóa vĩnh viễn tất cả mã khuyến mãi trong thùng rác.";
            return RedirectToAction(nameof(TrashBin));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var promo = await _context.Promotions.FindAsync(id);
            if (promo != null)
            {
                promo.IsActive = !promo.IsActive;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã {(promo.IsActive ? "kích hoạt" : "vô hiệu hóa")} mã khuyến mãi.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool PromotionExists(int id)
        {
            return _context.Promotions.Any(e => e.PromotionID == id);
        }
    }
}
