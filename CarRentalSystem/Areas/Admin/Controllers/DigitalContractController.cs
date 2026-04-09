using CarRentalSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DigitalContractController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DigitalContractController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var contracts = await _context.DigitalContracts
                .Include(c => c.Booking)
                .ThenInclude(b => b.Car)
                .Include(c => c.Booking)
                .ThenInclude(b => b.ContractType)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(contracts);
        }

        public async Task<IActionResult> Details(int id)
        {
            var contract = await _context.DigitalContracts
                .Include(c => c.Booking)
                .ThenInclude(b => b.Car)
                .FirstOrDefaultAsync(c => c.DigitalContractID == id);

            if (contract == null) return NotFound();

            return View(contract);
        }
    }
}
