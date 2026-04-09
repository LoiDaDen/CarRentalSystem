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
    [Authorize(Roles = "Admin")]
    public class PromotionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PromotionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var promotions = await _context.Promotions.OrderByDescending(p => p.PromotionID).ToListAsync();
            return View(promotions);
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
                _context.Promotions.Remove(promotion);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa mã khuyến mãi.";
            }
            return RedirectToAction(nameof(Index));
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
