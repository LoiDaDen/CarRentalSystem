using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
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
        private readonly IPiiEncryptionService _piiEncryption;

        public PartnerContractController(ApplicationDbContext context, IPiiEncryptionService piiEncryption)
        {
            _context = context;
            _piiEncryption = piiEncryption;
        }

        // GET: Admin/PartnerContract
        public async Task<IActionResult> Index(string? category, string? status)
        {
            if (status == null) status = "active";
            ViewBag.Category = category;
            ViewBag.Status = status;

            // ═══ Driver Contracts ═══ — default to active/pending/expired (NOT cancelled)
            var driverQuery = _context.DriverContracts
                .Include(dc => dc.Driver)
                .OrderByDescending(dc => dc.CreatedAt)
                .AsQueryable();

            if (status == "pending") driverQuery = driverQuery.Where(dc => dc.Status == "Pending");
            else if (status == "active") driverQuery = driverQuery.Where(dc => dc.Status == "Active");
            else if (status == "expired") driverQuery = driverQuery.Where(dc => dc.Status == "Expired");
            else if (status == "cancelled") driverQuery = driverQuery.Where(dc => dc.Status == "Cancelled");
            else driverQuery = driverQuery.Where(dc => dc.Status != "Cancelled"); // default: hide cancelled

            // ═══ Partner Contracts ═══ — default to active/pending/expired (NOT cancelled)
            var partnerQuery = _context.PartnerContracts
                .Include(pc => pc.PartnerUser)
                .OrderByDescending(pc => pc.CreatedAt)
                .AsQueryable();

            if (status == "pending") partnerQuery = partnerQuery.Where(pc => pc.Status == "Pending");
            else if (status == "active") partnerQuery = partnerQuery.Where(pc => pc.Status == "Active");
            else if (status == "expired") partnerQuery = partnerQuery.Where(pc => pc.Status == "Expired");
            else if (status == "cancelled") partnerQuery = partnerQuery.Where(pc => pc.Status == "Cancelled");
            else partnerQuery = partnerQuery.Where(pc => pc.Status != "Cancelled"); // default: hide cancelled

            var driverContracts = category == "partner" ? new List<DriverContract>() : await driverQuery.ToListAsync();
            var partnerContracts = category == "driver" ? new List<PartnerContract>() : await partnerQuery.ToListAsync();

            // ═══ Giải mã PII trước khi hiển thị ═══
            foreach (var contract in driverContracts)
            {
                contract.PartyBCCCD = _piiEncryption.DecryptIfNotEmpty(contract.PartyBCCCD);
                contract.PartyBLicenseNumber = _piiEncryption.DecryptIfNotEmpty(contract.PartyBLicenseNumber);
            }

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

            // ═══ Giải mã PII trước khi hiển thị ═══
            contract.PartyBCCCD = _piiEncryption.DecryptIfNotEmpty(contract.PartyBCCCD);
            contract.PartyBLicenseNumber = _piiEncryption.DecryptIfNotEmpty(contract.PartyBLicenseNumber);

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

            // ═══ Tự động khóa tài xế khi hủy hợp đồng ═══
            var driver = await _context.Drivers.FindAsync(contract.DriverID);
            if (driver != null)
            {
                driver.IsLocked = true;
                driver.LockedAt = DateTime.UtcNow;
                driver.IsAvailable = false;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã hủy hợp đồng tài xế #{contract.ContractNo} và khóa tài khoản tài xế.";
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
