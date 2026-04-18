using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DriverController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DriverController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Driver
        public async Task<IActionResult> Index(string? status)
        {
            var query = _context.Drivers
                .Include(d => d.Location)
                .Include(d => d.User)
                .OrderByDescending(d => d.CreatedAt)
                .AsQueryable();

            ViewBag.Status = status;

            if (status == "pending")
                query = query.Where(d => !d.IsApproved);
            else if (status == "approved")
                query = query.Where(d => d.IsApproved);

            var drivers = await query.ToListAsync();
            
            ViewBag.TotalCount = await _context.Drivers.CountAsync();
            ViewBag.PendingCount = await _context.Drivers.CountAsync(d => !d.IsApproved);
            ViewBag.ApprovedCount = await _context.Drivers.CountAsync(d => d.IsApproved);
            
            return View(drivers);
        }

        // POST: Admin/Driver/Approve/5
        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var driver = await _context.Drivers
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DriverID == id);
            if (driver == null) return NotFound();

            driver.IsApproved = true;
            driver.IsAvailable = true;

            // ═══ Tự động tạo Hợp đồng Đối tác Tài xế ═══
            var contractCount = await _context.DriverContracts.CountAsync() + 1;
            var contractNo = $"{contractCount:D4}/{DateTime.UtcNow.Year}/HĐ-TXL";
            var now = DateTime.UtcNow;

            var contract = new DriverContract
            {
                ContractNo = contractNo,
                DriverID = driver.DriverID,
                PartyAName = "Công ty TNHH Cho Thuê Xe CarRental",
                PartyARepresentative = "Giám đốc Hệ thống",
                PartyAPosition = "Giám đốc",
                PartyAAddress = "TP. Hồ Chí Minh, Việt Nam",
                PartyBFullName = driver.FullName,
                PartyBCCCD = driver.User?.CitizenId ?? "",
                PartyBLicenseType = driver.LicenseType,
                PartyBLicenseNumber = driver.LicenseNumber ?? "",
                PartyBExperienceYears = driver.ExperienceYears,
                BaseSalary = driver.DailyRate,
                TripFee = driver.HourlyRate,
                BonusPolicy = "Thưởng đánh giá 5 sao từ khách hàng",
                PaymentDay = 5,
                Status = "Pending",
                CreatedAt = now,
                ExpiresAt = now.AddDays(7),
                SignedByPartyA = true,
                PartyASignDate = now,
                TermsContent = GenerateDriverContractTerms(driver)
            };

            _context.DriverContracts.Add(contract);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã duyệt tài xế {driver.FullName} và tạo hợp đồng #{contractNo}. Tài xế có 7 ngày để ký xác nhận.";
            return RedirectToAction(nameof(Index));
        }

        private string GenerateDriverContractTerms(Driver driver)
        {
            return @"ĐIỀU 1: NỘI DUNG HỢP TÁC
1.1. Bên A giao quyền điều khiển xe ô tô cho Bên B để phục vụ khách hàng của hệ thống (đưa đón khách, giao nhận xe cho khách thuê tự lái).
1.2. Bên B cam kết chỉ sử dụng xe vào mục đích công việc được hệ thống chỉ định.

ĐIỀU 2: QUY ĐỊNH VỀ VẬN HÀNH VÀ BẢO QUẢN XE
2.1. Kiểm tra xe: Bên B có trách nhiệm kiểm tra tình trạng xe (thân vỏ, máy móc, xăng/điện) trước và sau khi nhận/bàn giao xe cho khách.
2.2. Vệ sinh: Đảm bảo xe luôn sạch sẽ (nội thất và ngoại thất) theo tiêu chuẩn của hệ thống.
2.3. An toàn: Tuyệt đối không sử dụng rượu bia, chất kích thích khi vận hành xe.

ĐIỀU 3: TRÁCH NHIỆM VẬT CHẤT VÀ PHÁP LÝ
3.1. Vi phạm giao thông: Bên B tự chịu trách nhiệm chi trả các khoản phạt nguội, phạt trực tiếp phát sinh trong thời gian điều khiển xe.
3.2. Va chạm/Tai nạn:
- Nếu lỗi do Bên B: Bên B chịu chi phí sửa chữa và khoản đền bù khấu hao xe (thỏa thuận cụ thể theo từng vụ việc).
- Nếu có bảo hiểm: Bên B thanh toán phần mức miễn thường và chi phí xe nằm xưởng.
3.3. Mất cắp: Nếu để mất xe do lỗi chủ quan (không khóa xe, để xe nơi không người trông coi...), Bên B bồi thường theo giá trị thị trường của xe.

ĐIỀU 4: PHÍ DỊCH VỤ VÀ THƯỞNG
4.1. Thu nhập của Bên B tính theo: Lương cố định + Phí chuyến + Thưởng đánh giá khách hàng.
4.2. Hình thức thanh toán: Chuyển khoản vào ngày 05 hàng tháng.

ĐIỀU 5: CAM KẾT BẢO MẬT
Bên B không được tự ý tiết lộ thông tin khách hàng hoặc quy trình vận hành của hệ thống cho bên thứ ba.";
        }

        // POST: Admin/Driver/Reject/5
        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound();

            driver.IsApproved = false;
            driver.IsAvailable = false;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã từ chối tài xế {driver.FullName}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Driver/ToggleAvailability/5
        [HttpPost]
        public async Task<IActionResult> ToggleAvailability(int id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound();

            driver.IsAvailable = !driver.IsAvailable;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Tài xế {driver.FullName} đã {(driver.IsAvailable ? "bật" : "tắt")} trạng thái sẵn sàng.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Driver/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound();

            _context.Drivers.Remove(driver);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã xóa tài xế {driver.FullName}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Driver/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var driver = await _context.Drivers
                .Include(d => d.Location)
                .Include(d => d.User)
                .Include(d => d.DriverBookings)
                .FirstOrDefaultAsync(d => d.DriverID == id);

            if (driver == null) return NotFound();
            return View(driver);
        }
    }
}
