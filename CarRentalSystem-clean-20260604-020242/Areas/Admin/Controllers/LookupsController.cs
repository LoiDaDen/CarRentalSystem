using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class LookupsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LookupsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Locations = await _context.Locations.ToListAsync();
            ViewBag.CarTypes = await _context.CarTypes.ToListAsync();
            ViewBag.CarStatuses = await _context.CarStatuses.ToListAsync();
            ViewBag.BookingStatuses = await _context.BookingStatuses.ToListAsync();
            ViewBag.CarBrands = await _context.CarBrands.ToListAsync();
            
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddLocation(Location model)
        {
            if (await _context.Locations.AnyAsync(l => l.Name == model.Name))
                ModelState.AddModelError("Name", "Tên địa điểm này đã tồn tại.");

            if (ModelState.IsValid)
            {
                _context.Locations.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã thêm khu vực / địa điểm mới.";
            }
            else
            {
                TempData["Error"] = "Dữ liệu không hợp lệ hoặc đã tồn tại.";
            }
            return RedirectToAction(nameof(Index), new { tab = "locations" });
        }

        [HttpGet]
        public async Task<IActionResult> EditLocation(int id)
        {
            var item = await _context.Locations.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        public async Task<IActionResult> EditLocation(int id, Location model)
        {
            if (id != model.LocationID) return NotFound();
            if (await _context.Locations.AnyAsync(l => l.Name == model.Name && l.LocationID != id))
                ModelState.AddModelError("Name", "Tên địa điểm này đã tồn tại.");

            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã cập nhật địa điểm thành công.";
                return RedirectToAction(nameof(Index), new { tab = "locations" });
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            var item = await _context.Locations.FindAsync(id);
            if (item != null)
            {
                try
                {
                    _context.Locations.Remove(item);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã xóa địa điểm.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = "Không thể xóa địa điểm này vì đang có xe hoặc đơn đặt tham chiếu tới.";
                }
            }
            return RedirectToAction(nameof(Index), new { tab = "locations" });
        }

        [HttpPost]
        public async Task<IActionResult> AddCarType(CarType model)
        {
            if (await _context.CarTypes.AnyAsync(t => t.Name == model.Name))
                ModelState.AddModelError("Name", "Loại xe này đã tồn tại.");

            if (ModelState.IsValid)
            {
                _context.CarTypes.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã thêm loại xe mới.";
            }
            else
            {
                TempData["Error"] = "Dữ liệu không hợp lệ hoặc đã tồn tại.";
            }
            return RedirectToAction(nameof(Index), new { tab = "cartypes" });
        }

        [HttpGet]
        public async Task<IActionResult> EditCarType(int id)
        {
            var item = await _context.CarTypes.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        public async Task<IActionResult> EditCarType(int id, CarType model)
        {
            if (id != model.CarTypeID) return NotFound();
            if (await _context.CarTypes.AnyAsync(t => t.Name == model.Name && t.CarTypeID != id))
                ModelState.AddModelError("Name", "Tên loại xe này đã tồn tại.");

            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã cập nhật loại xe thành công.";
                return RedirectToAction(nameof(Index), new { tab = "cartypes" });
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCarType(int id)
        {
            var item = await _context.CarTypes.FindAsync(id);
            if (item != null)
            {
                try
                {
                    _context.CarTypes.Remove(item);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã xóa loại xe.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = "Không thể xóa loại xe này vì đang có xe tham chiếu tới.";
                }
            }
            return RedirectToAction(nameof(Index), new { tab = "cartypes" });
        }

        [HttpPost]
        public async Task<IActionResult> AddCarBrand(CarBrand model)
        {
            if (await _context.CarBrands.AnyAsync(b => b.Name == model.Name))
                ModelState.AddModelError("Name", "Hãng xe này đã tồn tại.");

            if (ModelState.IsValid)
            {
                _context.CarBrands.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã thêm hãng xe mới.";
            }
            else
            {
                TempData["Error"] = "Dữ liệu không hợp lệ hoặc đã tồn tại.";
            }
            return RedirectToAction(nameof(Index), new { tab = "carbrands" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCarBrand(int id)
        {
            var item = await _context.CarBrands.FindAsync(id);
            if (item != null)
            {
                try
                {
                    _context.CarBrands.Remove(item);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã xóa hãng xe.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = "Không thể xóa hãng xe này vì đang có xe tham chiếu tới.";
                }
            }
            return RedirectToAction(nameof(Index), new { tab = "carbrands" });
        }

        [HttpGet]
        public async Task<IActionResult> EditCarBrand(int id)
        {
            var item = await _context.CarBrands.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        public async Task<IActionResult> EditCarBrand(int id, CarBrand model)
        {
            if (id != model.BrandID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã cập nhật hãng xe thành công.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = "Lỗi khi cập nhật hãng xe.";
                    return View(model);
                }
                return RedirectToAction(nameof(Index), new { tab = "carbrands" });
            }
            return View(model);
        }
        
        [HttpPost]
        public async Task<IActionResult> AddCarStatus(CarStatus model)
        {
            if (await _context.CarStatuses.AnyAsync(s => s.Name == model.Name))
                ModelState.AddModelError("Name", "Trạng thái này đã tồn tại.");

            if (ModelState.IsValid)
            {
                _context.CarStatuses.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã thêm trạng thái xe mới.";
            }
            else
            {
                TempData["Error"] = "Dữ liệu không hợp lệ hoặc đã tồn tại.";
            }
            return RedirectToAction(nameof(Index), new { tab = "carstatuses" });
        }

        [HttpGet]
        public async Task<IActionResult> EditCarStatus(int id)
        {
            var item = await _context.CarStatuses.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        public async Task<IActionResult> EditCarStatus(int id, CarStatus model)
        {
            if (id != model.CarStatusID) return NotFound();
            if (await _context.CarStatuses.AnyAsync(s => s.Name == model.Name && s.CarStatusID != id))
                ModelState.AddModelError("Name", "Tên trạng thái đã tồn tại.");

            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã cập nhật trạng thái thành công.";
                return RedirectToAction(nameof(Index), new { tab = "carstatuses" });
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCarStatus(int id)
        {
            var item = await _context.CarStatuses.FindAsync(id);
            if (item != null)
            {
                try
                {
                    _context.CarStatuses.Remove(item);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã xóa trạng thái xe.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = "Không thể xóa trạng thái này vì khối lượng dữ liệu tham chiếu lớn.";
                }
            }
            return RedirectToAction(nameof(Index), new { tab = "carstatuses" });
        }
        
        [HttpPost]
        public async Task<IActionResult> AddBookingStatus(BookingStatus model)
        {
            if (await _context.BookingStatuses.AnyAsync(s => s.Name == model.Name))
                ModelState.AddModelError("Name", "Trạng thái này đã tồn tại.");

            if (ModelState.IsValid)
            {
                _context.BookingStatuses.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã thêm trạng thái đơn mới.";
            }
            else
            {
                TempData["Error"] = "Dữ liệu không hợp lệ hoặc đã tồn tại.";
            }
            return RedirectToAction(nameof(Index), new { tab = "bookingstatuses" });
        }

        [HttpGet]
        public async Task<IActionResult> EditBookingStatus(int id)
        {
            var item = await _context.BookingStatuses.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        public async Task<IActionResult> EditBookingStatus(int id, BookingStatus model)
        {
            if (id != model.BookingStatusID) return NotFound();
            if (await _context.BookingStatuses.AnyAsync(s => s.Name == model.Name && s.BookingStatusID != id))
                ModelState.AddModelError("Name", "Tên trạng thái đã tồn tại.");

            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã cập nhật trạng thái đơn thành công.";
                return RedirectToAction(nameof(Index), new { tab = "bookingstatuses" });
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBookingStatus(int id)
        {
            var item = await _context.BookingStatuses.FindAsync(id);
            if (item != null)
            {
                try
                {
                    _context.BookingStatuses.Remove(item);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã xóa trạng thái đơn.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = "Không thể xóa trạng thái này vì đơn lưu trữ tham chiếu lớn.";
                }
            }
            return RedirectToAction(nameof(Index), new { tab = "bookingstatuses" });
        }
    }
}
