using CarRentalSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VoucherController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public VoucherController(ApplicationDbContext db) { _db = db; }

        [HttpGet("check")]
        public async Task<IActionResult> Check(string code, decimal totalPrice)
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { success = false, message = "Vui lòng nhập mã giảm giá." });

            var promo = await _db.Promotions.FirstOrDefaultAsync(p =>
                p.PromoCode == code.Trim().ToUpper() &&
                p.IsActive &&
                p.ValidFrom <= DateTime.Now &&
                p.ValidUntil >= DateTime.Now &&
                p.UsedCount < p.UsageLimit);

            if (promo == null)
                return Ok(new { success = false, message = "Mã giảm giá không hợp lệ hoặc đã hết hạn." });

            decimal discount = totalPrice * promo.DiscountPercent / 100m;
            if (promo.MaxDiscountAmount.HasValue && discount > promo.MaxDiscountAmount.Value)
                discount = promo.MaxDiscountAmount.Value;

            return Ok(new
            {
                success = true,
                promotionId = promo.PromotionID,
                discountPercent = promo.DiscountPercent,
                discountAmount = discount,
                message = $"Giảm {promo.DiscountPercent:0}% (tối đa {promo.MaxDiscountAmount?.ToString("N0") ?? "∞"}đ)"
            });
        }
    }
}
