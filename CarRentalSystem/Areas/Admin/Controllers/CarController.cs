using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CarController : Controller
    {
        private readonly ICarService _carService;
        private readonly CarRentalSystem.Data.ApplicationDbContext _context;

        public CarController(ICarService carService, CarRentalSystem.Data.ApplicationDbContext context)
        {
            _carService = carService;
            _context = context;
        }

        public async Task<IActionResult> Manage(int? brandId, int? locationId)
        {
            var query = _context.Cars
                .Include(c => c.CarBrand)
                .Include(c => c.Location)
                .Include(c => c.CarType)
                .Include(c => c.CarStatus)
                .Include(c => c.CarImages)
                .AsQueryable();

            if (brandId.HasValue)
                query = query.Where(c => c.BrandID == brandId.Value);

            if (locationId.HasValue)
                query = query.Where(c => c.LocationID == locationId.Value);

            var cars = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();

            ViewBag.Brands = await _context.CarBrands.OrderBy(b => b.Name).ToListAsync();
            ViewBag.Locations = await _context.Locations.OrderBy(l => l.Name).ToListAsync();
            
            ViewBag.CurrentBrandId = brandId;
            ViewBag.CurrentLocationId = locationId;

            return View(cars);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Brands = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CarBrands, "BrandID", "Name");
            ViewBag.CarTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CarTypes, "CarTypeID", "Name");
            ViewBag.Locations = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Locations, "LocationID", "Name");
            ViewBag.CarStatuses = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CarStatuses, "CarStatusID", "Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Car car, string? CarAddress, string? Latitude, string? Longitude, List<IFormFile> imageFiles)
        {
            if (!string.IsNullOrEmpty(CarAddress)) car.CarAddress = CarAddress;
            if (double.TryParse(Latitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat)) car.Latitude = lat;
            if (double.TryParse(Longitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng)) car.Longitude = lng;

            ModelState.Remove("CarImages");
            ModelState.Remove("Bookings");
            ModelState.Remove("Trackings");
            ModelState.Remove("Alerts");
            ModelState.Remove("Reviews");
            ModelState.Remove("MaintenanceLogs");
            ModelState.Remove("CarBrand");
            ModelState.Remove("CarStatus");
            ModelState.Remove("CarType");
            ModelState.Remove("Location");
            ModelState.Remove("InsuranceTier");

            // Unique Check
            if (await _context.Cars.AnyAsync(c => c.LicensePlate == car.LicensePlate))
                ModelState.AddModelError("LicensePlate", "Biển số xe này đã tồn tại.");
            if (await _context.Cars.AnyAsync(c => c.VIN == car.VIN))
                ModelState.AddModelError("VIN", "Số VIN này đã tồn tại.");

            if (ModelState.IsValid)
            {
                if (car.PricePerHour == 0)
                {
                    car.PricePerHour = Math.Round(car.PricePerDay / 8m);
                }
                await _carService.AddCarAsync(car);

                if (imageFiles != null && imageFiles.Count > 0)
                {
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "cars");
                    Directory.CreateDirectory(uploadsFolder);

                    foreach (var imageFile in imageFiles)
                    {
                        if (imageFile.Length > 0)
                        {
                            string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await imageFile.CopyToAsync(fileStream);
                            }
                            // Save image entry
                            var carImage = new CarImage { CarID = car.CarID, ImageUrl = "/images/cars/" + uniqueFileName };
                            await _carService.AddCarImageAsync(carImage);
                        }
                    }
                }

                return RedirectToAction(nameof(Manage));
            }
            
            ViewBag.Brands = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CarBrands, "BrandID", "Name", car.BrandID);
            ViewBag.CarTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CarTypes, "CarTypeID", "Name", car.CarTypeID);
            ViewBag.Locations = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Locations, "LocationID", "Name", car.LocationID);
            ViewBag.CarStatuses = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CarStatuses, "CarStatusID", "Name", car.CarStatusID);
            return View(car);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var car = await _carService.GetCarByIdAsync(id);
            if (car == null) return NotFound();
            
            ViewBag.Brands = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarBrands.ToListAsync(), "BrandID", "Name", car.BrandID);
            ViewBag.CarTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarTypes.ToListAsync(), "CarTypeID", "Name", car.CarTypeID);
            ViewBag.Locations = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.Locations.ToListAsync(), "LocationID", "Name", car.LocationID);
            ViewBag.CarStatuses = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarStatuses.ToListAsync(), "CarStatusID", "Name", car.CarStatusID);
            return View(car);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Car car, string? CarAddress, string? Latitude, string? Longitude, List<IFormFile> imageFiles)
        {
            if (!string.IsNullOrEmpty(CarAddress)) car.CarAddress = CarAddress;
            if (double.TryParse(Latitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat)) car.Latitude = lat;
            if (double.TryParse(Longitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng)) car.Longitude = lng;

            ModelState.Remove("CarImages");
            ModelState.Remove("Bookings");
            ModelState.Remove("Trackings");
            ModelState.Remove("Alerts");
            ModelState.Remove("Reviews");
            ModelState.Remove("MaintenanceLogs");
            ModelState.Remove("CarBrand");
            ModelState.Remove("CarStatus");
            ModelState.Remove("CarType");
            ModelState.Remove("Location");

            // Unique Check
            if (await _context.Cars.AnyAsync(c => c.LicensePlate == car.LicensePlate && c.CarID != car.CarID))
                ModelState.AddModelError("LicensePlate", "Biển số xe này đã tồn tại.");
            if (await _context.Cars.AnyAsync(c => c.VIN == car.VIN && c.CarID != car.CarID))
                ModelState.AddModelError("VIN", "Số VIN này đã tồn tại.");

            if (ModelState.IsValid)
            {
                if (car.PricePerHour == 0)
                {
                    car.PricePerHour = Math.Round(car.PricePerDay / 8m);
                }
                if (imageFiles != null && imageFiles.Count > 0)
                {
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "cars");
                    Directory.CreateDirectory(uploadsFolder);

                    foreach (var imageFile in imageFiles)
                    {
                        if (imageFile.Length > 0)
                        {
                            string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await imageFile.CopyToAsync(fileStream);
                            }
                            var carImage = new CarImage { CarID = car.CarID, ImageUrl = "/images/cars/" + uniqueFileName };
                            await _carService.AddCarImageAsync(carImage);
                        }
                    }
                }

                await _carService.UpdateCarAsync(car);
                TempData["Success"] = "Cập nhật xe thành công!";
                return RedirectToAction(nameof(Manage));
            }
            
            ViewBag.Brands = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarBrands.ToListAsync(), "BrandID", "Name", car.BrandID);
            ViewBag.CarTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarTypes.ToListAsync(), "CarTypeID", "Name", car.CarTypeID);
            ViewBag.Locations = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.Locations.ToListAsync(), "LocationID", "Name", car.LocationID);
            ViewBag.CarStatuses = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarStatuses.ToListAsync(), "CarStatusID", "Name", car.CarStatusID);
            return View(car);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var car = await _context.Cars.Include(c => c.CarImages).FirstOrDefaultAsync(c => c.CarID == id);
            if (car != null)
            {
                // Delete physical files
                if (car.CarImages != null)
                {
                    foreach (var img in car.CarImages)
                    {
                        DeletePhysicalFile(img.ImageUrl);
                    }
                }
                
                await _carService.DeleteCarAsync(id);
                TempData["Success"] = "Đã xóa xe thành công!";
            }
            return RedirectToAction(nameof(Manage));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteImage(int id)
        {
            var image = await _context.CarImages.FindAsync(id);
            if (image != null)
            {
                DeletePhysicalFile(image.ImageUrl);
                _context.CarImages.Remove(image);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        private void DeletePhysicalFile(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;
            try
            {
                string fileName = Path.GetFileName(imageUrl);
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "cars", fileName);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                // Minimal logging or just ignore for this context
                Console.WriteLine($"Error deleting file: {ex.Message}");
            }
        }
    }
}
