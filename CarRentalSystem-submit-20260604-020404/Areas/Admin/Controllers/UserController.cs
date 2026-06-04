using CarRentalSystem.Data;
using CarRentalSystem.BackgroundJobs;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPiiEncryptionService _piiEncryption;
        private readonly IConfiguration _configuration;

        public UserController(
            ApplicationDbContext context,
            IPiiEncryptionService piiEncryption,
            IConfiguration configuration)
        {
            _context = context;
            _piiEncryption = piiEncryption;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index(string? search, string? role)
        {
            ViewBag.Search = search;
            ViewBag.ActiveRole = role;
            ViewBag.PinVerified = HttpContext.Session.GetString("AdminPinVerified") == "true";

            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalAdmins = await _context.Users.CountAsync(u => u.Role == "Admin");
            ViewBag.TotalCustomers = await _context.Users.CountAsync(u => u.Role == "Customer");
            ViewBag.TotalPartners = await _context.Users.CountAsync(u => u.Role == "Partner");
            ViewBag.TotalManagers = await _context.Users.CountAsync(u => u.Role == "Manager");
            ViewBag.TotalStaffs = await _context.Users.CountAsync(u => u.Role == "Staff");

            var users = _context.Users
                .Include(u => u.Bookings)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                users = users.Where(u => u.Name.Contains(search) || u.Email.Contains(search));

            if (!string.IsNullOrEmpty(role))
                users = users.Where(u => u.Role == role);

            var list = await users.OrderByDescending(u => u.CreatedAt).ToListAsync();

            // 🔓 Chỉ decrypt PII khi đã verify PIN
            if (ViewBag.PinVerified == true)
            {
                foreach (var user in list)
                {
                    user.CitizenId = _piiEncryption.DecryptIfNotEmpty(user.CitizenId);
                    user.DrivingLicenseNumber = _piiEncryption.DecryptIfNotEmpty(user.DrivingLicenseNumber);
                }
            }

            return View(list);
        }

        [HttpPost]
        public IActionResult VerifyAdminPin(string pin)
        {
            // Load PIN tu config/environment
            var adminPin = _configuration["Admin:Pin"]
                ?? Environment.GetEnvironmentVariable("ADMIN_PIN");

            // Development fallback - chi trong Dev environment
            if (string.IsNullOrEmpty(adminPin) && HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                adminPin = "123456"; // Dev only fallback
            }

            if (string.IsNullOrEmpty(adminPin))
            {
                TempData["PinError"] = "Hệ thống chưa cấu hình Admin PIN. Vui lòng liên hệ quản trị.";
                return RedirectToAction(nameof(Index));
            }

            if (pin == adminPin)
            {
                HttpContext.Session.SetString("AdminPinVerified", "true");
                return RedirectToAction(nameof(Index));
            }
            TempData["PinError"] = "Mã PIN không chính xác!";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> PartnerRequests()
        {
            var requests = await _context.Users
                .Where(u => u.Role == "Partner" && !u.IsPartnerApproved)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
            return View(requests);
        }

        [HttpPost]
        public async Task<IActionResult> SetRole(int id, string role)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(Index));
            }

            // Không cho phép thay đổi role của Admin
            if (user.Role == "Admin")
            {
                TempData["Error"] = "Không thể thay đổi vai trò của Admin.";
                return RedirectToAction(nameof(Index));
            }

            // Nếu đổi từ Manager xuống Staff - kiểm tra area đó còn Manager khác không
            if (user.Role == "Manager" && role != "Manager")
            {
                // Đếm xem user này là Manager của những area nào
                var managedAreas = await _context.Areas
                    .Where(a => a.ManagerID == user.UserID && a.Status == "active")
                    .ToListAsync();

                foreach (var area in managedAreas)
                {
                    // Đếm số Manager khác trong area (không tính user đang bị hạ role)
                    var otherManagersCount = await _context.Users
                        .CountAsync(u => u.Role == "Manager" && u.UserID != user.UserID &&
                            _context.AreaStaffs.Any(asg => asg.UserID == u.UserID &&
                                asg.AreaID == area.AreaID && asg.RoleInArea == "Manager" && !asg.IsDeleted));

                    // Nếu không có Manager nào khác, kiểm tra ManagerID
                    if (otherManagersCount == 0 && area.ManagerID == user.UserID)
                    {
                        TempData["Error"] = $"Không thể hạ vai trò của {user.Name} vì khu vực '{area.Name}' cần ít nhất 1 Quản lý. Hãy bổ nhiệm Quản lý khác trước.";
                        return RedirectToAction(nameof(Index));
                    }
                }
            }

            // ═══ XỬ LÝ THĂNG PARTNER LÊN STAFF/MANAGER ═══
// Partner có OriginalRole (đã đăng ký làm partner khi là Admin/Staff/Manager)
// Hoặc Partner bình thường được thăng lên Staff/Manager - vẫn giữ quyền Partner gốc
if (user.Role == "Partner" && (role == "Staff" || role == "Manager"))
{
    // Lưu role gốc là Partner để có thể quay lại
    if (string.IsNullOrEmpty(user.OriginalRole))
    {
        user.OriginalRole = user.Role; // Lưu "Partner" làm role gốc
    }
    // Role sẽ được đổi sang Staff/Manager, nhưng vẫn có quyền Partner qua OriginalRole
}

            // Thực hiện đổi role
            var oldRole = user.Role;
            user.Role = role;

            // Nếu đổi lên Manager - cập nhật AreaManager
            if (role == "Manager")
            {
                // Lấy area mà user đang phụ trách
                var userAreaStaff = await _context.AreaStaffs
                    .Where(asg => asg.UserID == user.UserID && !asg.IsDeleted)
                    .ToListAsync();

                if (userAreaStaff.Any())
                {
                    foreach (var asg in userAreaStaff)
                    {
                        asg.RoleInArea = "Manager";
                        // Nếu area chưa có Manager thì set
                        var area = await _context.Areas.FindAsync(asg.AreaID);
                        if (area != null && area.ManagerID == null)
                        {
                            area.ManagerID = user.UserID;
                        }
                    }
                }
                else
                {
                    // Nếu user chưa thuộc area nào, lấy area đầu tiên
                    var firstArea = await _context.Areas.Where(a => a.Status == "active").FirstOrDefaultAsync();
                    if (firstArea != null)
                    {
                        _context.AreaStaffs.Add(new AreaStaff
                        {
                            UserID = user.UserID,
                            AreaID = firstArea.AreaID,
                            RoleInArea = "Manager",
                            AssignedAt = DateTime.UtcNow
                        });
                        if (firstArea.ManagerID == null)
                        {
                            firstArea.ManagerID = user.UserID;
                        }
                    }
                }
            }

            // Nếu đổi từ Manager xuống - cập nhật lại AreaManager
            if (oldRole == "Manager" && role != "Manager")
            {
                var managedAreas = await _context.Areas
                    .Where(a => a.ManagerID == user.UserID && a.Status == "active")
                    .ToListAsync();

                foreach (var area in managedAreas)
                {
                    area.ManagerID = null; // Xóa Manager cũ
                    // Tìm Manager khác trong area
                    var newManager = await _context.AreaStaffs
                        .Where(asg => asg.AreaID == area.AreaID && asg.RoleInArea == "Manager" && !asg.IsDeleted && asg.UserID != user.UserID)
                        .Include(asg => asg.User)
                        .FirstOrDefaultAsync();

                    if (newManager != null)
                    {
                        area.ManagerID = newManager.UserID;
                    }
                    else
                    {
                        // Tìm Staff nào đó trong area để assign làm Manager
                        var staffInArea = await _context.AreaStaffs
                            .Where(asg => asg.AreaID == area.AreaID && !asg.IsDeleted && asg.UserID != user.UserID)
                            .Include(asg => asg.User)
                            .FirstOrDefaultAsync();

                        if (staffInArea != null)
                        {
                            area.ManagerID = staffInArea.UserID;
                            staffInArea.RoleInArea = "Manager";
                            TempData["Info"] = $"Đã tự động bổ nhiệm {staffInArea.User?.Name} làm Quản lý khu vực {area.Name}.";
                        }
                    }
                }

                // Cập nhật RoleInArea trong AreaStaff
                var areaStaffs = await _context.AreaStaffs
                    .Where(asg => asg.UserID == user.UserID && asg.RoleInArea == "Manager" && !asg.IsDeleted)
                    .ToListAsync();
                foreach (var asg in areaStaffs)
                {
                    asg.RoleInArea = "Staff";
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật vai trò của {user.Name} thành {role}.";
            return RedirectToAction(nameof(Index));
        }

        // ═══ DUYỆT ĐỐI TÁC + TẠO HỢP ĐỒNG ═══
        [HttpPost]
        public async Task<IActionResult> ApprovePartner(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Verify user is Partner and not yet approved
            if (user.Role != "Partner")
            {
                TempData["Error"] = "Người dùng này không phải là Đối tác.";
                return RedirectToAction(nameof(PartnerRequests));
            }

            if (user.IsPartnerApproved)
            {
                TempData["Info"] = $"{user.Name} đã được phê duyệt trước đó.";
                return RedirectToAction(nameof(PartnerRequests));
            }

            if (!user.IsVerified || user.VerificationStatus != "Verified")
            {
                TempData["Error"] = $"Đối tác {user.Name} chưa được duyệt eKYC. Vui lòng duyệt eKYC trước khi duyệt đối tác.";
                return RedirectToAction(nameof(PartnerRequests));
            }

            // Bước 1: Duyệt Partner
            user.IsPartnerApproved = true;
            user.PartnerApprovedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Bước 2: Kiểm tra đã có HĐ chưa, nếu chưa thì tạo mới
            var existingContract = await _context.PartnerContracts
                .AnyAsync(pc => pc.PartnerUserID == user.UserID && (pc.Status == "Pending" || pc.Status == "Active"));

            if (!existingContract)
            {
                var contractCount = await _context.PartnerContracts.CountAsync() + 1;
                var contractNo = $"{contractCount:D4}/{DateTime.UtcNow.Year}/HĐHT-XT";
                var now = DateTime.UtcNow;

                var contract = new PartnerContract
                {
                    ContractNo = contractNo,
                    PartnerUserID = user.UserID,
                    PartyAName = "Công ty TNHH Cho Thuê Xe CarRental",
                    PartyAAddress = "TP. Hồ Chí Minh, Việt Nam",
                    PartyBFullName = user.Name,
                    PartyBCCCD = user.CitizenId ?? "",
                    PartyBRevenuePercent = 80.00m,
                    PartyARevenuePercent = 20.00m,
                    PaymentDay = 5,
                    PaymentCycle = "Hàng tháng",
                    ContractDurationMonths = 0, // Vĩnh viễn
                    NoticePeriodDays = 15,
                    Status = "Pending",
                    CreatedAt = now,
                    ExpiresAt = now.AddDays(7),
                    SignedByPartyA = true,
                    PartyASignDate = now,
                    TermsContent = GeneratePartnerContractTerms(user)
                };

                _context.PartnerContracts.Add(contract);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Đã duyệt Đối tác {user.Name} và tạo Hợp đồng #{contractNo}. Đối tác có 7 ngày để ký xác nhận.";
            }
            else
            {
                TempData["Success"] = $"Đã duyệt eKYC của {user.Name}. Hợp đồng đã tồn tại.";
            }

            var activeContract = await _context.PartnerContracts
                .Where(pc => pc.PartnerUserID == user.UserID && (pc.Status == "Pending" || pc.Status == "Active"))
                .OrderByDescending(pc => pc.CreatedAt)
                .FirstOrDefaultAsync();
            if (activeContract != null)
            {
                var contractUrl = Url.Action("ContractDetails", "Dashboard", new { area = "Partner", id = activeContract.PartnerContractID }, Request.Scheme) ?? "";
                BackgroundJob.Enqueue<EmailBackgroundJob>(job => job.SendApprovalEmail(user.Email, user.Name, "partner", activeContract.ContractNo, activeContract.ExpiresAt, contractUrl, "vi"));
                SchedulePartnerContractReminders(activeContract.PartnerContractID, activeContract.ExpiresAt, contractUrl);
            }

            return RedirectToAction(nameof(PartnerRequests));
        }

        private static void SchedulePartnerContractReminders(int contractId, DateTime expiresAt, string contractUrl)
        {
            var now = DateTime.UtcNow;
            foreach (var days in new[] { 3, 1 })
            {
                var runAt = expiresAt.AddDays(-days);
                if (runAt > now)
                {
                    BackgroundJob.Schedule<EmailBackgroundJob>(
                        job => job.SendPartnerContractReminder(contractId, days, contractUrl, "vi"),
                        runAt - now);
                }
            }
        }

        // ═══ BỎ DUYỆT ĐỐI TÁC ═══
        [HttpPost]
        public async Task<IActionResult> RejectPartner(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IsPartnerApproved = false;
            user.PartnerApprovedAt = null;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã bỏ duyệt Đối tác {user.Name}.";
            return RedirectToAction(nameof(PartnerRequests));
        }

        // ToggleVerify - Giữ lại cho Index.cshtml (duyệt eKYC thông thường)
        [HttpPost]
        public async Task<IActionResult> ToggleVerify(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsVerified = !user.IsVerified;
                user.VerificationStatus = user.IsVerified ? "Verified" : "Pending";
                await _context.SaveChangesAsync();

                // ═══ Tự động tạo HĐ Đối tác khi verify Partner ═══
                if (_configuration.GetValue<bool>("Partner:CreateContractOnEkycVerify") && user.IsVerified && user.Role == "Partner")
                {
                    // Kiểm tra đã có HĐ Active chưa
                    var existingContract = await _context.PartnerContracts
                        .AnyAsync(pc => pc.PartnerUserID == user.UserID && (pc.Status == "Pending" || pc.Status == "Active"));

                    if (!existingContract)
                    {
                        var contractCount = await _context.PartnerContracts.CountAsync() + 1;
                        var contractNo = $"{contractCount:D4}/{DateTime.UtcNow.Year}/HĐHT-XT";
                        var now = DateTime.UtcNow;

                        var contract = new PartnerContract
                        {
                            ContractNo = contractNo,
                            PartnerUserID = user.UserID,
                            PartyAName = "Công ty TNHH Cho Thuê Xe CarRental",
                            PartyAAddress = "TP. Hồ Chí Minh, Việt Nam",
                            PartyBFullName = user.Name,
                            PartyBCCCD = user.CitizenId ?? "",
                            PartyBRevenuePercent = 80.00m,
                            PartyARevenuePercent = 20.00m,
                            PaymentDay = 5,
                            PaymentCycle = "Hàng tháng",
                            ContractDurationMonths = 0, // Vĩnh viễn
                            NoticePeriodDays = 15,
                            Status = "Pending",
                            CreatedAt = now,
                            ExpiresAt = now.AddDays(7),
                            SignedByPartyA = true,
                            PartyASignDate = now,
                            TermsContent = GeneratePartnerContractTerms(user)
                        };

                        _context.PartnerContracts.Add(contract);
                        await _context.SaveChangesAsync();

                        TempData["Success"] = $"Đã xác minh {user.Name} và tạo hợp đồng #{contractNo}. Đối tác có 7 ngày để ký xác nhận.";
                    }
                    else
                    {
                        TempData["Success"] = $"Đã cập nhật trạng thái eKYC của {user.Name}. Hợp đồng đã tồn tại.";
                    }
                }
                else
                {
                    TempData["Success"] = $"Đã cập nhật trạng thái eKYC của {user.Name}.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        private string GeneratePartnerContractTerms(User user)
        {
            return @"ĐIỀU 1: NỘI DUNG HỢP TÁC
1.1. Bên B đồng ý ký gửi xe ô tô cho Bên A để thực hiện dịch vụ cho thuê xe tự lái.
1.2. Bên A có trách nhiệm tìm kiếm khách hàng, thực hiện quy trình cho thuê, quản lý tài sản và thu tiền từ khách hàng.

ĐIỀU 2: PHÂN CHIA DOANH THU (80/20)
2.1. Tỷ lệ phân chia: Doanh thu thực tế từ việc cho thuê xe (sau khi trừ các mã giảm giá nếu có) sẽ được chia như sau:
- Bên B (Chủ xe): Nhận 80% doanh thu.
- Bên A (Vận hành): Nhận 20% doanh thu (Phí quản lý, tìm khách và vận hành).
2.2. Kỳ thanh toán: Bên A tổng kết và chuyển thanh toán cho Bên B vào ngày 05 hàng tháng.

ĐIỀU 3: CHI PHÍ VẬN HÀNH
3.1. Bên B chịu trách nhiệm: Phí bảo hiểm vật chất xe, phí đường bộ, đăng kiểm, và các hư hỏng do hao mòn tự nhiên hoặc lỗi kỹ thuật của xe.
3.2. Bên A chịu trách nhiệm: Chi phí marketing, nhân sự giao nhận xe, vệ sinh xe cơ bản sau mỗi lượt thuê.

ĐIỀU 4: TRÁCH NHIỆM BẢO VỆ TÀI SẢN
4.1. Bên A có trách nhiệm kiểm tra kỹ hồ sơ khách thuê (CCCD, GPLX, tài sản thế chấp) để giảm thiểu rủi ro.
4.2. Trong trường hợp khách thuê gây ra va chạm/tai nạn:
- Bên A hỗ trợ làm việc với bảo hiểm và khách thuê để bồi thường cho Bên B.
- Nếu không có bảo hiểm, Bên A có trách nhiệm yêu cầu khách thuê đền bù trực tiếp cho Bên B theo giá trị thiệt hại thực tế.
4.3. Phạt nguội: Bên A có trách nhiệm truy thu tiền phạt từ khách thuê nếu phát sinh vi phạm trong thời gian thuê.

ĐIỀU 5: THỜI HẠN VÀ CHẤM DỨT
5.1. Thời hạn hợp đồng: Vĩnh viễn kể từ ngày ký.
5.2. Nếu một trong hai bên muốn chấm dứt hợp đồng, phải báo trước cho bên kia ít nhất 15 ngày.";
        }

        [HttpPost]
        public async Task<IActionResult> ToggleBlacklist(int id, string? reason)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                // Constraint: Không thể blacklist nếu có booking đang hoạt động (không phải Completed/Cancelled)
                bool hasActiveBookings = await _context.Bookings
                    .AnyAsync(b => b.UserID == id && 
                        b.BookingStatus.Name != "Completed" && 
                        b.BookingStatus.Name != "Cancelled");
                if (hasActiveBookings && !user.IsBlacklisted)
                {
                    TempData["Error"] = "Không thể đưa tài khoản này vào danh sách đen vì người dùng đang có chuyến đi chưa hoàn tất. Vui lòng xử lý chuyến đi trước.";
                    return RedirectToAction(nameof(Index));
                }

                user.IsBlacklisted = !user.IsBlacklisted;
                user.BlacklistReason = user.IsBlacklisted ? (reason ?? "Vi phạm chính sách nền tảng") : null;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã cập nhật trạng thái Blacklist của {user.Name}.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleLock(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                // Constraint: Không thể khóa nếu có booking đang hoạt động (không phải Completed/Cancelled)
                bool hasActiveBookings = await _context.Bookings
                    .AnyAsync(b => b.UserID == id && 
                        b.BookingStatus.Name != "Completed" && 
                        b.BookingStatus.Name != "Cancelled");
                if (hasActiveBookings && !user.IsLocked)
                {
                    TempData["Error"] = "Không thể khóa tài khoản này vì người dùng đang có chuyến đi chưa hoàn tất.";
                    return RedirectToAction(nameof(Index));
                }

                user.IsLocked = !user.IsLocked;
                user.LockedAt = user.IsLocked ? DateTime.UtcNow : null;
                await _context.SaveChangesAsync();
                TempData["Success"] = user.IsLocked ? $"Đã khóa tài khoản {user.Name} tạm thời." : $"Đã mở khóa tài khoản {user.Name}.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
