// CarRentalSystem/Areas/Admin/Controllers/CarController.cs
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin", "Manager", "Staff")]
    public class CarController : AdminAreaControllerBase
    {
        private readonly ICarService _carService;
        private readonly ApplicationDbContext _context;

        public CarController(ICarService carService, ApplicationDbContext context, IAreaService areaService)
            : base(areaService)
        {
            _carService = carService;
            _context = context;
        }

        public async Task<IActionResult> Manage(int? brandId, int? locationId, int? statusId, int? areaId)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            // Lay danh sach area cua user hien tai (cho dropdown)
            var userAreas = await GetUserAreasAsync(userId);
            var userAreaIds = userAreas.Select(a => a.AreaID).ToList();

            // Admin must not be scoped by the working-area cookie on fleet pages.
            // Manager/Staff still use query -> cookie -> first assigned area.
            var currentAreaId = role == "Admin"
                ? areaId
                : await ResolveAreaIdAsync(userId, areaId);

            // Staff/Manager: chi thay xe trong 1 area cu the (khong thay tat ca area)
            if (role == "Staff" || role == "Manager")
            {
                if (currentAreaId.HasValue && currentAreaId.Value > 0)
                {
                    // Kiem tra user co quyen xem area nay khong
                    if (!userAreaIds.Contains(currentAreaId.Value))
                    {
                        // Neu area khong thuoc ve minh -> chon area dau tien
                        currentAreaId = userAreaIds.Count > 0 ? userAreaIds[0] : null;
                    }
                }
                else if (userAreaIds.Count > 0)
                {
                    currentAreaId = userAreaIds[0];
                }

                if (!currentAreaId.HasValue || currentAreaId.Value <= 0)
                {
                    ViewBag.Brands = await _context.CarBrands.OrderBy(b => b.Name).ToListAsync();
                    ViewBag.Locations = new List<Location>();
                    ViewBag.Areas = userAreas;
                    ViewBag.CarStatuses = await _context.CarStatuses.OrderBy(s => s.Name).ToListAsync();
                    ViewBag.CurrentBrandId = brandId;
                    ViewBag.CurrentLocationId = locationId;
                    ViewBag.CurrentStatusId = statusId;
                    ViewBag.CurrentAreaId = currentAreaId;
                    ViewBag.UserAreaIds = userAreaIds;
                    return View(new List<Car>());
                }

                // Lay locations CHI trong area dang chon
                var areaLocationIds = await _areaService.GetLocationIdsForAreaAsync(currentAreaId.Value);
                if (locationId.HasValue && !areaLocationIds.Contains(locationId.Value))
                {
                    locationId = null;
                }

                var query = _context.Cars
                    .Include(c => c.CarBrand)
                    .Include(c => c.Location)
                    .Include(c => c.CarType)
                    .Include(c => c.CarStatus)
                    .Include(c => c.CarImages)
                    .Where(c => areaLocationIds.Contains(c.LocationID))
                    .AsQueryable();

                if (brandId.HasValue)
                    query = query.Where(c => c.BrandID == brandId.Value);

                if (locationId.HasValue && areaLocationIds.Contains(locationId.Value))
                    query = query.Where(c => c.LocationID == locationId.Value);

                if (statusId.HasValue)
                    query = query.Where(c => c.CarStatusID == statusId.Value);

                var cars = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();

                var allLocations = await _context.Locations
                    .Where(l => areaLocationIds.Contains(l.LocationID))
                    .OrderBy(l => l.Name)
                    .ToListAsync();

                ViewBag.Brands = await _context.CarBrands.OrderBy(b => b.Name).ToListAsync();
                ViewBag.Locations = allLocations;
                ViewBag.Areas = userAreas;
                ViewBag.CarStatuses = await _context.CarStatuses.OrderBy(s => s.Name).ToListAsync();
                ViewBag.CurrentBrandId = brandId;
                ViewBag.CurrentLocationId = locationId;
                ViewBag.CurrentStatusId = statusId;
                ViewBag.CurrentAreaId = currentAreaId;
                ViewBag.UserAreaIds = userAreaIds;
                return View(cars);
            }

            // Admin: thay tat ca, co the loc theo area
            if (currentAreaId.HasValue && currentAreaId.Value > 0)
            {
                var areaLocationIds = await _areaService.GetLocationIdsForAreaAsync(currentAreaId.Value);
                if (locationId.HasValue && !areaLocationIds.Contains(locationId.Value))
                {
                    locationId = null;
                }

                var query = _context.Cars
                    .Include(c => c.CarBrand)
                    .Include(c => c.Location)
                    .Include(c => c.CarType)
                    .Include(c => c.CarStatus)
                    .Include(c => c.CarImages)
                    .Where(c => areaLocationIds.Contains(c.LocationID))
                    .AsQueryable();

                if (brandId.HasValue)
                    query = query.Where(c => c.BrandID == brandId.Value);

                if (locationId.HasValue && areaLocationIds.Contains(locationId.Value))
                    query = query.Where(c => c.LocationID == locationId.Value);

                if (statusId.HasValue)
                    query = query.Where(c => c.CarStatusID == statusId.Value);

                var cars = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();

                var locs = await _context.Locations
                    .Where(l => areaLocationIds.Contains(l.LocationID))
                    .OrderBy(l => l.Name).ToListAsync();

                ViewBag.Brands = await _context.CarBrands.OrderBy(b => b.Name).ToListAsync();
                ViewBag.Locations = locs;
                ViewBag.Areas = await _context.Areas.Where(a => !a.IsDeleted).OrderBy(a => a.Name).ToListAsync();
                ViewBag.CarStatuses = await _context.CarStatuses.OrderBy(s => s.Name).ToListAsync();
                ViewBag.CurrentBrandId = brandId;
                ViewBag.CurrentLocationId = locationId;
                ViewBag.CurrentStatusId = statusId;
                ViewBag.CurrentAreaId = currentAreaId;
                return View(cars);
            }

            var allQuery = _context.Cars
                .Include(c => c.CarBrand)
                .Include(c => c.Location)
                .Include(c => c.CarType)
                .Include(c => c.CarStatus)
                .Include(c => c.CarImages)
                .AsQueryable();

            if (brandId.HasValue)
                allQuery = allQuery.Where(c => c.BrandID == brandId.Value);

            if (locationId.HasValue)
                allQuery = allQuery.Where(c => c.LocationID == locationId.Value);

            if (statusId.HasValue)
                allQuery = allQuery.Where(c => c.CarStatusID == statusId.Value);

            var allCars = await allQuery.OrderByDescending(c => c.CreatedAt).ToListAsync();

            ViewBag.Brands = await _context.CarBrands.OrderBy(b => b.Name).ToListAsync();
            ViewBag.Locations = await _context.Locations.OrderBy(l => l.Name).ToListAsync();
            ViewBag.Areas = await _context.Areas.Where(a => !a.IsDeleted).OrderBy(a => a.Name).ToListAsync();
            ViewBag.CarStatuses = await _context.CarStatuses.OrderBy(s => s.Name).ToListAsync();
            ViewBag.CurrentBrandId = brandId;
            ViewBag.CurrentLocationId = locationId;
            ViewBag.CurrentStatusId = statusId;
            ViewBag.CurrentAreaId = areaId;
            return View(allCars);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? areaId)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role == "Staff")
            {
                return Forbid();
            }

            if (role == "Manager")
            {
                var locs = await _areaService.GetLocationIdsForUserAsync(userId);
                var locations = await _context.Locations
                    .Where(l => locs.Contains(l.LocationID))
                    .OrderBy(l => l.Name)
                    .ToListAsync();
                ViewBag.Locations = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(locations, "LocationID", "Name");
            }
            else
            {
                ViewBag.Locations = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Locations, "LocationID", "Name");
            }

            ViewBag.Brands = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CarBrands, "BrandID", "Name");
            ViewBag.CarTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CarTypes, "CarTypeID", "Name");
            ViewBag.CarStatuses = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CarStatuses, "CarStatusID", "Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Car car, string? CarAddress, string? Latitude, string? Longitude, List<IFormFile> imageFiles, int? areaId)
        {
            var role = GetCurrentRole();

            if (role == "Staff")
                return Forbid();

            if (!string.IsNullOrEmpty(CarAddress)) car.CarAddress = CarAddress;
            if (double.TryParse(Latitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat)) car.Latitude = lat;
            if (double.TryParse(Longitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng)) car.Longitude = lng;
            car.PricePerHour = car.PricePerDay > 0 ? Math.Round(car.PricePerDay / 8m) : 0;

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
            ModelState.Remove("PricePerHour");

            if (await _context.Cars.AnyAsync(c => c.LicensePlate == car.LicensePlate))
                ModelState.AddModelError("LicensePlate", "Biển số xe này đã tồn tại.");
            if (await _context.Cars.AnyAsync(c => c.VIN == car.VIN))
                ModelState.AddModelError("VIN", "Số VIN này đã tồn tại.");

            if (ModelState.IsValid)
            {
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
                            var carImage = new CarImage { CarID = car.CarID, ImageUrl = "/images/cars/" + uniqueFileName };
                            await _carService.AddCarImageAsync(carImage);
                        }
                    }
                }

                TempData["Success"] = "Thêm xe thành công!";
                return RedirectToAction(nameof(Manage), new { areaId });
            }

            if (role == "Manager")
            {
                var userId = GetCurrentUserId();
                var locs = await _areaService.GetLocationIdsForUserAsync(userId);
                var locations = await _context.Locations.Where(l => locs.Contains(l.LocationID)).OrderBy(l => l.Name).ToListAsync();
                ViewBag.Locations = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(locations, "LocationID", "Name", car.LocationID);
            }
            else
            {
                ViewBag.Locations = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Locations, "LocationID", "Name", car.LocationID);
            }
            ViewBag.Brands = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CarBrands, "BrandID", "Name", car.BrandID);
            ViewBag.CarTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CarTypes, "CarTypeID", "Name", car.CarTypeID);
            ViewBag.CarStatuses = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CarStatuses, "CarStatusID", "Name", car.CarStatusID);
            return View(car);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, int? areaId)
        {
            var car = await _carService.GetCarByIdAsync(id);
            if (car == null) return NotFound();

            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            // Staff/Manager: chỉ sửa xe trong area của mình
            if (role == "Staff" || role == "Manager")
            {
                var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId, role);
                if (!allowedLocs.Contains(car.LocationID))
                    return Forbid();
            }

            ViewBag.Brands = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarBrands.ToListAsync(), "BrandID", "Name", car.BrandID);
            ViewBag.CarTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarTypes.ToListAsync(), "CarTypeID", "Name", car.CarTypeID);
            ViewBag.CarStatuses = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarStatuses.ToListAsync(), "CarStatusID", "Name", car.CarStatusID);

            if (role == "Manager")
            {
                var locs = await _areaService.GetLocationIdsForUserAsync(userId);
                var locations = await _context.Locations.Where(l => locs.Contains(l.LocationID)).OrderBy(l => l.Name).ToListAsync();
                ViewBag.Locations = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(locations, "LocationID", "Name", car.LocationID);
            }
            else
            {
                ViewBag.Locations = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.Locations.ToListAsync(), "LocationID", "Name", car.LocationID);
            }

            return View(car);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Car car, string? CarAddress, string? Latitude, string? Longitude, List<IFormFile> imageFiles, int? areaId)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role == "Staff")
                return Forbid();

            if (role == "Manager")
            {
                var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId);
                if (!allowedLocs.Contains(car.LocationID))
                    return Forbid();
            }

            if (!string.IsNullOrEmpty(CarAddress)) car.CarAddress = CarAddress;
            if (double.TryParse(Latitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat)) car.Latitude = lat;
            if (double.TryParse(Longitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng)) car.Longitude = lng;
            car.PricePerHour = car.PricePerDay > 0 ? Math.Round(car.PricePerDay / 8m) : 0;

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
            ModelState.Remove("PricePerHour");

            if (await _context.Cars.AnyAsync(c => c.LicensePlate == car.LicensePlate && c.CarID != car.CarID))
                ModelState.AddModelError("LicensePlate", "Biển số xe này đã tồn tại.");
            if (await _context.Cars.AnyAsync(c => c.VIN == car.VIN && c.CarID != car.CarID))
                ModelState.AddModelError("VIN", "Số VIN này đã tồn tại.");

            if (ModelState.IsValid)
            {
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
                return RedirectToAction(nameof(Manage), new { areaId });
            }

            ViewBag.Brands = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarBrands.ToListAsync(), "BrandID", "Name", car.BrandID);
            ViewBag.CarTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarTypes.ToListAsync(), "CarTypeID", "Name", car.CarTypeID);
            ViewBag.CarStatuses = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarStatuses.ToListAsync(), "CarStatusID", "Name", car.CarStatusID);
            ViewBag.Locations = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.Locations.ToListAsync(), "LocationID", "Name", car.LocationID);
            return View(car);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id, int? areaId)
        {
            var car = await _context.Cars
                .FirstOrDefaultAsync(c => c.CarID == id);

            if (car != null)
            {
                var role = GetCurrentRole();
                var userId = GetCurrentUserId();

                if (role == "Manager")
                {
                    var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId);
                    if (!allowedLocs.Contains(car.LocationID))
                        return Forbid();
                }
                else if (role == "Staff")
                {
                    return Forbid();
                }

                var activeBookingStatuses = new[] { "Confirmed", "Active" };
                bool hasActiveBookings = await _context.Bookings
                    .Include(b => b.BookingStatus)
                    .AnyAsync(b => b.CarID == id &&
                        b.BookingStatus != null &&
                        activeBookingStatuses.Contains(b.BookingStatus.Name));
                if (hasActiveBookings)
                {
                    TempData["Error"] = "Không thể xóa xe này vì đang có chuyến đi chưa hoàn tất.";
                    return RedirectToAction(nameof(Manage), new { areaId });
                }

                bool isAssignedToDriver = await _context.DriverBookings
                    .Include(db => db.Booking)
                    .AnyAsync(db => db.Booking != null && db.Booking.CarID == id && (db.Status == "Active" || db.Status == "Confirmed"));

                if (isAssignedToDriver)
                {
                    TempData["Error"] = "Không thể xóa xe này vì đang được gán với tài xế.";
                    return RedirectToAction(nameof(Manage), new { areaId });
                }

                car.IsDeleted = true;
                car.DeletedAt = DateTime.UtcNow;
                _context.Cars.Update(car);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã chuyển xe vào thùng rác!";
            }
            return RedirectToAction(nameof(Manage), new { areaId });
        }

        [HttpGet]
        public async Task<IActionResult> Trash()
        {
            var role = GetCurrentRole();
            if (role == "Staff")
                return Forbid();

            var query = _context.Cars.IgnoreQueryFilters()
                .Include(c => c.CarBrand)
                .Include(c => c.Location)
                .Include(c => c.CarType)
                .Include(c => c.CarStatus)
                .Include(c => c.CarImages)
                .Where(c => c.IsDeleted);

            if (role == "Manager")
            {
                var userId = GetCurrentUserId();
                var locs = await _areaService.GetLocationIdsForUserAsync(userId);
                query = query.Where(c => locs.Contains(c.LocationID));
            }

            var cars = await query.OrderByDescending(c => c.DeletedAt).ToListAsync();
            return View(cars);
        }

        [HttpPost]
        public async Task<IActionResult> Restore(int id, int? areaId)
        {
            var car = await _context.Cars.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.CarID == id && c.IsDeleted);
            if (car != null)
            {
                var role = GetCurrentRole();
                if (role == "Staff")
                    return Forbid();

                if (role == "Manager")
                {
                    var userId = GetCurrentUserId();
                    var locs = await _areaService.GetLocationIdsForUserAsync(userId);
                    if (!locs.Contains(car.LocationID))
                        return Forbid();
                }

                car.IsDeleted = false;
                car.DeletedAt = null;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Khôi phục xe thành công!";
            }
            return RedirectToAction(nameof(Trash), new { areaId });
        }

        [HttpPost]
        public async Task<IActionResult> PermanentDelete(int id, int? areaId)
        {
            var role = GetCurrentRole();
            if (role != "Admin")
                return Forbid();

            var car = await _context.Cars.IgnoreQueryFilters()
                .Include(c => c.CarImages)
                .FirstOrDefaultAsync(c => c.CarID == id && c.IsDeleted);

            if (car != null)
            {
                if (car.CarImages != null)
                {
                    foreach (var img in car.CarImages)
                        DeletePhysicalFile(img.ImageUrl);
                }
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM Cars WHERE CarID = {0}", id);
                TempData["Success"] = "Đã xóa vĩnh viễn xe khỏi hệ thống!";
            }
            return RedirectToAction(nameof(Trash), new { areaId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteImage(int id)
        {
            var image = await _context.CarImages.Include(ci => ci.Car).FirstOrDefaultAsync(ci => ci.ImageID == id);
            if (image == null)
                return Json(new { success = false });

            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role == "Staff")
                return Json(new { success = false, message = "Không có quyền." });

            if (role == "Manager")
            {
                var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId);
                if (!allowedLocs.Contains(image.Car?.LocationID ?? 0))
                    return Json(new { success = false, message = "Xe không thuộc khu vực của bạn." });
            }

            DeletePhysicalFile(image.ImageUrl);
            _context.CarImages.Remove(image);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, int? areaId)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            var car = await _context.Cars
                .Include(c => c.CarBrand)
                .Include(c => c.Location)
                .Include(c => c.CarType)
                .Include(c => c.CarStatus)
                .Include(c => c.CarImages)
                .Include(c => c.Reviews)
                .Include(c => c.Bookings)
                .FirstOrDefaultAsync(c => c.CarID == id);

            if (car == null)
                return NotFound();

            // Staff/Manager: chỉ xem được xe trong khu vực của mình
            if (role == "Staff" || role == "Manager")
            {
                var allowedLocationIds = await _areaService.GetLocationIdsForUserAsync(userId, role);
                if (!allowedLocationIds.Contains(car.LocationID))
                    return Forbid();
            }

            ViewBag.CurrentAreaId = areaId;
            ViewBag.TotalBookings = car.Bookings?.Count ?? 0;
            ViewBag.ActiveBookings = car.Bookings?.Count(b => b.BookingStatusID == 1) ?? 0;

            return View(car);
        }

        private void DeletePhysicalFile(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;
            try
            {
                string fileName = Path.GetFileName(imageUrl);
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "cars", fileName);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting file: {ex.Message}");
            }
        }
    }
}
