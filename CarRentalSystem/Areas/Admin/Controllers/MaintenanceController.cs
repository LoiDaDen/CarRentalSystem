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
    public class MaintenanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MaintenanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Cars = await _context.Cars.ToListAsync();
            var logs = await _context.MaintenanceLogs
                .Include(m => m.Car)
                .OrderByDescending(m => m.ServiceDate)
                .ToListAsync();
            return View(logs);
        }

        [HttpPost]
        public async Task<IActionResult> Create(MaintenanceLog log)
        {
            if (ModelState.IsValid)
            {
                _context.MaintenanceLogs.Add(log);
                
                // Update car metadata
                var car = await _context.Cars.FindAsync(log.CarID);
                if (car != null)
                {
                    car.CurrentOdometer = log.OdometerAtService;
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View("Index", await _context.MaintenanceLogs.Include(m => m.Car).ToListAsync());
        }
    }
}
