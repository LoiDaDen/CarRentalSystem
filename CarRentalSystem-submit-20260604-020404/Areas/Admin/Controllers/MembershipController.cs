using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MembershipController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MembershipController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Membership
        public async Task<IActionResult> Index()
        {
            var tiers = await _context.MembershipTiers.OrderBy(t => t.SortOrder).ToListAsync();
            var contractTypes = await _context.ContractTypes.OrderBy(c => c.Name).ToListAsync();
            
            ViewBag.ContractTypes = contractTypes;
            return View(tiers);
        }

        // POST: Admin/Membership/UpdateTier
        [HttpPost]
        public async Task<IActionResult> UpdateTier(MembershipTier model)
        {
            var tier = await _context.MembershipTiers.FindAsync(model.MembershipTierID);
            if (tier == null) return NotFound();

            tier.Name = model.Name;
            tier.MinSpending = model.MinSpending;
            tier.DiscountPercent = model.DiscountPercent;
            tier.FreeDeliveryKm = model.FreeDeliveryKm;
            tier.PrioritySupport = model.PrioritySupport;
            tier.ExtraFreeHours = model.ExtraFreeHours;
            tier.Color = model.Color;
            tier.Icon = model.Icon;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật hạng {tier.Name}!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Membership/UpdateContract
        [HttpPost]
        public async Task<IActionResult> UpdateContract(ContractType model)
        {
            var ct = await _context.ContractTypes.FindAsync(model.ContractTypeID);
            if (ct == null) return NotFound();

            ct.Name = model.Name;
            ct.DiscountPercent = model.DiscountPercent;
            ct.Description = model.Description;
            ct.MinDurationDays = model.MinDurationDays;
            ct.MaxDurationDays = model.MaxDurationDays;
            ct.IsActive = model.IsActive;
            ct.FreeMaintenanceCount = model.FreeMaintenanceCount;
            ct.FreeInsurance = model.FreeInsurance;
            ct.AfterSalesDescription = model.AfterSalesDescription;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật loại hợp đồng {ct.Name}!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Membership/ToggleContract
        [HttpPost]
        public async Task<IActionResult> ToggleContract(int id)
        {
            var ct = await _context.ContractTypes.FindAsync(id);
            if (ct == null) return NotFound();

            ct.IsActive = !ct.IsActive;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Loại hợp đồng {ct.Name} đã {(ct.IsActive ? "bật" : "tắt")}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
