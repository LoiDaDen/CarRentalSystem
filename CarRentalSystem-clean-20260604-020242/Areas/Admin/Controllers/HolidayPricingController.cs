using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
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
    public class HolidayPricingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHolidayPricingService _holidayService;

        public HolidayPricingController(ApplicationDbContext context, IHolidayPricingService holidayService)
        {
            _context = context;
            _holidayService = holidayService;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy danh sách custom holiday pricing từ DB
            var dbPricings = await _context.HolidayPricings
                .Include(h => h.Car)
                .OrderByDescending(h => h.StartDate)
                .ToListAsync();

            // Lấy danh sách ngày lễ tự động từ service
            var currentYear = DateTime.Now.Year;
            var holidays = _holidayService.GetHolidays(currentYear);
            
            ViewBag.Holidays = holidays;
            ViewBag.CurrentYear = currentYear;
            ViewBag.WeekendMultiplier = _holidayService.WeekendMultiplier;
            ViewBag.MajorHolidayMultiplier = _holidayService.MajorHolidayMultiplier;
            ViewBag.TetHolidayMultiplier = _holidayService.TetHolidayMultiplier;
            
            return View(dbPricings);
        }

        // API: Tính giá cho một khoảng thời gian (tính theo ngày)
        [HttpGet]
        public IActionResult CalculatePrice(DateTime startDate, DateTime endDate, decimal pricePerDay)
        {
            var (totalPrice, totalDays, avgMultiplier) = _holidayService.CalculatePrice(pricePerDay, startDate, endDate);
            var maxMultiplier = _holidayService.GetMultiplier(startDate, endDate);
            
            // Lấy thông tin tất cả các ngày trong khoảng
            var dateInfos = new System.Collections.Generic.List<object>();
            decimal normalDays = 0, weekendDays = 0, holidayDays = 0;

            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var (isHoliday, isWeekend, holidayName) = _holidayService.GetDateInfo(date);
                var multiplier = _holidayService.GetMultiplier(date);
                var dayPrice = pricePerDay * multiplier;

                if (isHoliday) holidayDays++;
                else if (isWeekend) weekendDays++;
                else normalDays++;

                dateInfos.Add(new {
                    date = date.ToString("dd/MM/yyyy"),
                    dayOfWeek = GetDayOfWeekVN(date.DayOfWeek),
                    isHoliday,
                    isWeekend,
                    holidayName,
                    multiplier,
                    dayPrice,
                    priceIncrease = multiplier > 1 ? (multiplier - 1) * 100 : 0
                });
            }

            // Xác định loại ngày chiếm nhiều nhất để hiển thị
            string summary;
            if (holidayDays > 0 && holidayDays >= weekendDays)
                summary = $"Có {holidayDays} ngày lễ/Tết";
            else if (weekendDays > 0)
                summary = $"Có {weekendDays} ngày cuối tuần";
            else
                summary = "Toàn ngày thường";

            return Json(new {
                startDate = startDate.ToString("dd/MM/yyyy"),
                endDate = endDate.ToString("dd/MM/yyyy"),
                pricePerDay,
                totalDays,
                totalPrice,
                avgMultiplier,
                maxMultiplier,
                normalDays,
                weekendDays,
                holidayDays,
                summary,
                dateInfos
            });
        }

        private string GetDayOfWeekVN(DayOfWeek dow)
        {
            return dow switch
            {
                DayOfWeek.Monday => "Thứ 2",
                DayOfWeek.Tuesday => "Thứ 3",
                DayOfWeek.Wednesday => "Thứ 4",
                DayOfWeek.Thursday => "Thứ 5",
                DayOfWeek.Friday => "Thứ 6",
                DayOfWeek.Saturday => "Thứ 7",
                DayOfWeek.Sunday => "Chủ nhật",
                _ => dow.ToString()
            };
        }

        // API: Lấy thông tin ngày
        [HttpGet]
        public IActionResult GetDateInfo(DateTime date)
        {
            var (isHoliday, isWeekend, holidayName) = _holidayService.GetDateInfo(date);
            var multiplier = _holidayService.GetMultiplier(date);
            
            return Json(new {
                date = date.ToString("dd/MM/yyyy"),
                dayOfWeek = date.DayOfWeek.ToString(),
                isHoliday,
                isWeekend,
                holidayName,
                multiplier,
                priceIncrease = (multiplier - 1) * 100
            });
        }

        // API: Lấy lịch nghỉ lễ năm
        [HttpGet]
        public IActionResult GetYearHolidays(int? year)
        {
            var targetYear = year ?? DateTime.Now.Year;
            var holidays = _holidayService.GetHolidays(targetYear);
            
            var holidayInfos = holidays.Select(h => {
                var (isHoliday, isWeekend, holidayName) = _holidayService.GetDateInfo(h);
                return new {
                    date = h.ToString("dd/MM/yyyy"),
                    dayOfWeek = h.DayOfWeek.ToString(),
                    holidayName,
                    multiplier = _holidayService.GetMultiplier(h),
                    type = holidayName?.StartsWith("Tết") == true ? "Tết" : 
                           isHoliday ? "Ngày lễ" : "Cuối tuần"
                };
            }).ToList();
            
            return Json(new {
                year = targetYear,
                holidays = holidayInfos,
                weekendMultiplier = _holidayService.WeekendMultiplier,
                majorHolidayMultiplier = _holidayService.MajorHolidayMultiplier,
                tetHolidayMultiplier = _holidayService.TetHolidayMultiplier
            });
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Cars = _context.Cars.Where(c => !c.IsDeleted).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HolidayPricing pricing)
        {
            if (ModelState.IsValid)
            {
                _context.Add(pricing);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã thêm cấu hình giá ngày lễ!";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Cars = _context.Cars.Where(c => !c.IsDeleted).ToList();
            return View(pricing);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var pricing = await _context.HolidayPricings.FindAsync(id);
            if (pricing == null) return NotFound();
            ViewBag.Cars = _context.Cars.Where(c => !c.IsDeleted).ToList();
            return View(pricing);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HolidayPricing pricing)
        {
            if (id != pricing.HolidayPricingID) return NotFound();
            
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(pricing);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã cập nhật cấu hình giá!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!HolidayPricingExists(pricing.HolidayPricingID))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Cars = _context.Cars.Where(c => !c.IsDeleted).ToList();
            return View(pricing);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var pricing = await _context.HolidayPricings.FindAsync(id);
            if (pricing != null)
            {
                _context.HolidayPricings.Remove(pricing);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa cấu hình giá.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var pricing = await _context.HolidayPricings.FindAsync(id);
            if (pricing != null)
            {
                pricing.IsActive = !pricing.IsActive;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã {(pricing.IsActive ? "kích hoạt" : "vô hiệu hóa")} cấu hình giá.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool HolidayPricingExists(int id)
        {
            return _context.HolidayPricings.Any(h => h.HolidayPricingID == id);
        }
    }
}
