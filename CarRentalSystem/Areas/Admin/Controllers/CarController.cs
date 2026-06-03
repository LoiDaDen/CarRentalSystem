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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            ViewBag.ServiceTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.ServiceTypes.Where(s => s.IsActive), "ServiceTypeID", "Name");
            ViewBag.Amenities = await GetActiveAmenitiesAsync();
            ViewBag.SelectedAmenityIds = new HashSet<int>();
            ViewBag.PendingNewAmenityNames = new List<string>();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            Car car,
            string? CarAddress,
            string? Latitude,
            string? Longitude,
            List<IFormFile> imageFiles,
            List<int> AmenityIDs,
            List<string> NewAmenityNames,
            int? areaId)
        {
            var role = GetCurrentRole();

            if (role == "Staff")
                return Forbid();

            if (!string.IsNullOrEmpty(CarAddress)) car.CarAddress = CarAddress;
            if (double.TryParse(Latitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat)) car.Latitude = lat;
            if (double.TryParse(Longitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng)) car.Longitude = lng;
            car.PricePerHour = car.PricePerDay > 0 ? Math.Round(car.PricePerDay / 8m) : 0;

            var selfDriveServiceType = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.Code == ServiceTypeConstants.SelfDrive);
            var withDriverServiceType = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.Code == ServiceTypeConstants.WithDriver);
            car.ServiceTypeID ??= selfDriveServiceType?.ServiceTypeID;
            if (car.ServiceTypeID == withDriverServiceType?.ServiceTypeID)
            {
                car.IsSelfDriveAllowed = false;
                car.IsWithDriverAllowed = true;
                car.TransportPermitStatus = string.IsNullOrWhiteSpace(car.TransportPermitStatus) ? "Pending" : car.TransportPermitStatus;
                car.IsApproved = false;
            }
            else
            {
                car.ServiceTypeID = selfDriveServiceType?.ServiceTypeID;
                car.IsSelfDriveAllowed = true;
                car.IsWithDriverAllowed = false;
                car.TransportPermitStatus = "NotRequired";
                car.BadgeType = null;
                car.BadgeExpiryDate = null;
                car.IsApproved = true;
            }

            ModelState.Remove("CarImages");
            ModelState.Remove("Bookings");
            ModelState.Remove("Trackings");
            ModelState.Remove("Alerts");
            ModelState.Remove("Reviews");
            ModelState.Remove("MaintenanceLogs");
            ModelState.Remove("CarBrand");
            ModelState.Remove("CarStatus");
            ModelState.Remove("CarType");
            ModelState.Remove("CarModel");
            ModelState.Remove("Location");
            ModelState.Remove("ServiceType");
            ModelState.Remove("Owner");
            ModelState.Remove("CarAmenityMappings");
            ModelState.Remove("InsuranceTier");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");
            ModelState.Remove("DeletedAt");
            ModelState.Remove("PricePerHour");

            if (await _context.Cars.AnyAsync(c => c.LicensePlate == car.LicensePlate))
                ModelState.AddModelError("LicensePlate", "Biển số xe này đã tồn tại.");
            if (await _context.Cars.AnyAsync(c => c.VIN == car.VIN))
                ModelState.AddModelError("VIN", "Số VIN này đã tồn tại.");

            if (!car.NextInspectionDate.HasValue)
                ModelState.AddModelError(nameof(Car.NextInspectionDate), "Vui lòng nhập ngày đăng kiểm tiếp theo.");

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

                await SyncCarAmenitiesAsync(car.CarID, AmenityIDs, NewAmenityNames);

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
            ViewBag.ServiceTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.ServiceTypes.Where(s => s.IsActive), "ServiceTypeID", "Name", car.ServiceTypeID);
            ViewBag.Amenities = await GetActiveAmenitiesAsync();
            ViewBag.SelectedAmenityIds = AmenityIDs?.ToHashSet() ?? new HashSet<int>();
            ViewBag.PendingNewAmenityNames = NormalizeNewAmenityNames(NewAmenityNames).ToList();
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
            ViewBag.Amenities = await GetActiveAmenitiesAsync();
            ViewBag.SelectedAmenityIds = car.CarAmenityMappings
                .Where(mapping => !mapping.IsDeleted)
                .Select(mapping => mapping.CarAmenityID)
                .ToHashSet();
            ViewBag.PendingNewAmenityNames = new List<string>();

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
        public async Task<IActionResult> Edit(
            Car car,
            string? CarAddress,
            string? Latitude,
            string? Longitude,
            List<IFormFile> imageFiles,
            List<int> AmenityIDs,
            List<string> NewAmenityNames,
            int? areaId)
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
            ModelState.Remove("CarModel");
            ModelState.Remove("Location");
            ModelState.Remove("ServiceType");
            ModelState.Remove("Owner");
            ModelState.Remove("CarAmenityMappings");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");
            ModelState.Remove("DeletedAt");
            ModelState.Remove("PricePerHour");

            if (await _context.Cars.AnyAsync(c => c.LicensePlate == car.LicensePlate && c.CarID != car.CarID))
                ModelState.AddModelError("LicensePlate", "Biển số xe này đã tồn tại.");
            if (await _context.Cars.AnyAsync(c => c.VIN == car.VIN && c.CarID != car.CarID))
                ModelState.AddModelError("VIN", "Số VIN này đã tồn tại.");

            if (!car.NextInspectionDate.HasValue)
                ModelState.AddModelError(nameof(Car.NextInspectionDate), "Vui lòng nhập ngày đăng kiểm tiếp theo.");

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
                await SyncCarAmenitiesAsync(car.CarID, AmenityIDs, NewAmenityNames);
                TempData["Success"] = "Cập nhật xe thành công!";
                return RedirectToAction(nameof(Manage), new { areaId });
            }

            ViewBag.Brands = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarBrands.ToListAsync(), "BrandID", "Name", car.BrandID);
            ViewBag.CarTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarTypes.ToListAsync(), "CarTypeID", "Name", car.CarTypeID);
            ViewBag.CarStatuses = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.CarStatuses.ToListAsync(), "CarStatusID", "Name", car.CarStatusID);
            if (car.CarID > 0 && ((AmenityIDs?.Any() ?? false) || NormalizeNewAmenityNames(NewAmenityNames).Any()))
            {
                await SyncCarAmenitiesAsync(car.CarID, AmenityIDs, NewAmenityNames);
            }
            ViewBag.Locations = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.Locations.ToListAsync(), "LocationID", "Name", car.LocationID);
            ViewBag.Amenities = await GetActiveAmenitiesAsync();
            ViewBag.SelectedAmenityIds = AmenityIDs?.ToHashSet() ?? new HashSet<int>();
            ViewBag.PendingNewAmenityNames = NormalizeNewAmenityNames(NewAmenityNames).ToList();
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
                .Include(c => c.CarAmenityMappings)
                    .ThenInclude(m => m.CarAmenity)
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

        private async Task<List<CarAmenity>> GetActiveAmenitiesAsync()
        {
            return await _context.CarAmenities
                .AsNoTracking()
                .Where(a => a.IsActive)
                .OrderBy(a => a.DisplayOrder)
                .ThenBy(a => a.Name)
                .ToListAsync();
        }

        private async Task SyncCarAmenitiesAsync(int carId, IEnumerable<int>? selectedIds, IEnumerable<string>? newAmenityNames)
        {
            var resolvedIds = await ResolveAmenityIdsAsync(selectedIds, newAmenityNames);
            var validIds = await _context.CarAmenities
                .Where(a => resolvedIds.Contains(a.CarAmenityID))
                .Select(a => a.CarAmenityID)
                .ToListAsync();
            var selected = validIds.ToHashSet();

            var existingMappings = await _context.CarAmenityMappings
                .IgnoreQueryFilters()
                .Where(mapping => mapping.CarID == carId)
                .ToListAsync();

            foreach (var mapping in existingMappings)
            {
                mapping.IsDeleted = !selected.Contains(mapping.CarAmenityID);
            }

            var existingIds = existingMappings.Select(mapping => mapping.CarAmenityID).ToHashSet();
            foreach (var amenityId in selected.Where(id => !existingIds.Contains(id)))
            {
                _context.CarAmenityMappings.Add(new CarAmenityMapping
                {
                    CarID = carId,
                    CarAmenityID = amenityId
                });
            }

            var featureNames = await _context.CarAmenities
                .Where(a => selected.Contains(a.CarAmenityID))
                .OrderBy(a => a.DisplayOrder)
                .ThenBy(a => a.Name)
                .Select(a => a.Name)
                .ToListAsync();
            var car = await _context.Cars.FindAsync(carId);
            if (car != null)
            {
                car.Features = string.Join(", ", featureNames);
            }

            await _context.SaveChangesAsync();
        }

        private async Task<HashSet<int>> ResolveAmenityIdsAsync(IEnumerable<int>? selectedIds, IEnumerable<string>? newAmenityNames)
        {
            var selected = (selectedIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .ToHashSet();
            var names = NormalizeNewAmenityNames(newAmenityNames).ToList();
            if (!names.Any())
            {
                return selected;
            }

            var amenities = await _context.CarAmenities.ToListAsync();
            var existingCodes = amenities
                .Select(a => a.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var nextOrder = amenities.Any() ? amenities.Max(a => a.DisplayOrder) + 1 : 1;
            var createdAmenities = new List<CarAmenity>();

            foreach (var name in names)
            {
                var compareName = NormalizeAmenityNameForCompare(name);
                var baseCode = BuildAmenityCode(name);
                var existing = amenities.FirstOrDefault(a =>
                    NormalizeAmenityNameForCompare(a.Name) == compareName ||
                    string.Equals(a.Code, baseCode, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.IsActive = true;
                    selected.Add(existing.CarAmenityID);
                    continue;
                }

                var amenity = new CarAmenity
                {
                    Code = EnsureUniqueAmenityCode(baseCode, existingCodes),
                    Name = name.Length > 120 ? name[..120] : name,
                    Icon = "bi-check2-circle",
                    DisplayOrder = nextOrder++,
                    IsActive = true
                };

                existingCodes.Add(amenity.Code);
                amenities.Add(amenity);
                createdAmenities.Add(amenity);
                _context.CarAmenities.Add(amenity);
            }

            if (createdAmenities.Any())
            {
                await _context.SaveChangesAsync();
                foreach (var amenity in createdAmenities)
                {
                    selected.Add(amenity.CarAmenityID);
                }
            }

            return selected;
        }

        private static IEnumerable<string> NormalizeNewAmenityNames(IEnumerable<string>? names)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawName in names ?? Enumerable.Empty<string>())
            {
                var name = Regex.Replace(rawName ?? string.Empty, @"\s+", " ").Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                var key = NormalizeAmenityNameForCompare(name);
                if (seen.Add(key))
                {
                    yield return name;
                }
            }
        }

        private static string NormalizeAmenityNameForCompare(string value)
        {
            return BuildAmenityCode(value);
        }

        private static string BuildAmenityCode(string name)
        {
            var normalized = name.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (ch == 'đ' || ch == 'Đ')
                {
                    builder.Append('D');
                }
                else if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(char.ToUpperInvariant(ch));
                }
                else
                {
                    builder.Append('_');
                }
            }

            var code = Regex.Replace(builder.ToString(), "_+", "_").Trim('_');
            if (string.IsNullOrWhiteSpace(code))
            {
                code = "AMENITY";
            }

            return code.Length > 60 ? code[..60].Trim('_') : code;
        }

        private static string EnsureUniqueAmenityCode(string baseCode, HashSet<string> existingCodes)
        {
            var cleanBase = string.IsNullOrWhiteSpace(baseCode) ? "AMENITY" : baseCode;
            cleanBase = cleanBase.Length > 60 ? cleanBase[..60].Trim('_') : cleanBase;
            var code = cleanBase;
            var suffix = 2;

            while (existingCodes.Contains(code))
            {
                var suffixText = "_" + suffix++;
                var maxBaseLength = Math.Max(1, 60 - suffixText.Length);
                code = cleanBase.Length > maxBaseLength
                    ? cleanBase[..maxBaseLength].Trim('_') + suffixText
                    : cleanBase + suffixText;
            }

            return code;
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
