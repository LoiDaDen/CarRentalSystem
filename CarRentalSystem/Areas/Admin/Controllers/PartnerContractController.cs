using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PartnerContractController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PartnerContractController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/PartnerContract
        public async Task<IActionResult> Index(string? category, string? status)
        {
            ViewBag.Category = category;
            ViewBag.Status = status;

            // ═══ Driver Contracts ═══
            var driverQuery = _context.DriverContracts
                .Include(dc => dc.Driver)
                .OrderByDescending(dc => dc.CreatedAt)
                .AsQueryable();

            if (status == "pending") driverQuery = driverQuery.Where(dc => dc.Status == "Pending");
            else if (status == "active") driverQuery = driverQuery.Where(dc => dc.Status == "Active");
            else if (status == "expired") driverQuery = driverQuery.Where(dc => dc.Status == "Expired");
            else if (status == "cancelled") driverQuery = driverQuery.Where(dc => dc.Status == "Cancelled");

            // ═══ Partner Contracts ═══
            var partnerQuery = _context.PartnerContracts
                .Include(pc => pc.PartnerUser)
                .OrderByDescending(pc => pc.CreatedAt)
                .AsQueryable();

            if (status == "pending") partnerQuery = partnerQuery.Where(pc => pc.Status == "Pending");
            else if (status == "active") partnerQuery = partnerQuery.Where(pc => pc.Status == "Active");
            else if (status == "expired") partnerQuery = partnerQuery.Where(pc => pc.Status == "Expired");
            else if (status == "cancelled") partnerQuery = partnerQuery.Where(pc => pc.Status == "Cancelled");

            var driverContracts = category == "partner" ? new List<DriverContract>() : await driverQuery.ToListAsync();
            var partnerContracts = category == "driver" ? new List<PartnerContract>() : await partnerQuery.ToListAsync();

            // Stats
            ViewBag.TotalDriverContracts = await _context.DriverContracts.CountAsync();
            ViewBag.TotalPartnerContracts = await _context.PartnerContracts.CountAsync();
            ViewBag.PendingCount = await _context.DriverContracts.CountAsync(dc => dc.Status == "Pending")
                                 + await _context.PartnerContracts.CountAsync(pc => pc.Status == "Pending");
            ViewBag.ActiveCount = await _context.DriverContracts.CountAsync(dc => dc.Status == "Active")
                                + await _context.PartnerContracts.CountAsync(pc => pc.Status == "Active");
            ViewBag.ExpiredCount = await _context.DriverContracts.CountAsync(dc => dc.Status == "Expired")
                                 + await _context.PartnerContracts.CountAsync(pc => pc.Status == "Expired");

            ViewBag.DriverContracts = driverContracts;
            ViewBag.PartnerContracts = partnerContracts;

            return View();
        }

        // GET: Admin/PartnerContract/DriverDetails/5
        public async Task<IActionResult> DriverDetails(int id)
        {
            var contract = await _context.DriverContracts
                .Include(dc => dc.Driver)
                .ThenInclude(d => d.User)
                .Include(dc => dc.Driver)
                .ThenInclude(d => d.Location)
                .FirstOrDefaultAsync(dc => dc.DriverContractID == id);

            if (contract == null) return NotFound();
            return View(contract);
        }

        // GET: Admin/PartnerContract/PartnerDetails/5
        public async Task<IActionResult> PartnerDetails(int id)
        {
            var contract = await _context.PartnerContracts
                .Include(pc => pc.PartnerUser)
                .FirstOrDefaultAsync(pc => pc.PartnerContractID == id);

            if (contract == null) return NotFound();
            return View(contract);
        }

        // POST: Admin/PartnerContract/CancelDriver/5
        [HttpPost]
        public async Task<IActionResult> CancelDriver(int id, string? reason)
        {
            var contract = await _context.DriverContracts.FindAsync(id);
            if (contract == null) return NotFound();

            contract.Status = "Cancelled";
            contract.CancelledAt = DateTime.UtcNow;
            contract.CancelReason = reason ?? "Hủy bởi Admin";
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã hủy hợp đồng tài xế #{contract.ContractNo}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/PartnerContract/CancelPartner/5
        [HttpPost]
        public async Task<IActionResult> CancelPartner(int id, string? reason)
        {
            var contract = await _context.PartnerContracts.FindAsync(id);
            if (contract == null) return NotFound();

            contract.Status = "Cancelled";
            contract.CancelledAt = DateTime.UtcNow;
            contract.CancelReason = reason ?? "Hủy bởi Admin";
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã hủy hợp đồng đối tác #{contract.ContractNo}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
