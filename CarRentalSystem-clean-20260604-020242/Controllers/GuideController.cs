using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers
{
    public class GuideController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public GuideController(IWebHostEnvironment env)
        {
            _env = env;
        }

        public IActionResult Index(string role = "customer")
        {
            var allowedRoles = new[] { "admin", "manager", "staff", "partner", "driver", "customer" };
            if (!allowedRoles.Contains(role.ToLower()))
                role = "customer";

            // Files are at ../docs/guides relative to ContentRootPath (ContentRootPath = CarRentalSystem/)
            var guidePath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "docs", "guides"));
            var fileName = role.ToLower() switch
            {
                "admin" => "01-admin-guide.md",
                "manager" => "05-manager-staff-guide.md",
                "staff" => "05-manager-staff-guide.md",
                "partner" => "03-partner-guide.md",
                "driver" => "04-driver-guide.md",
                _ => "02-customer-guide.md"
            };

            var filePath = Path.Combine(guidePath, fileName);
            var content = "";
            if (System.IO.File.Exists(filePath))
            {
                content = System.IO.File.ReadAllText(filePath);
            }

            var roleInfo = GetRoleInfo(role);
            ViewData["Role"] = role.ToLower();
            ViewData["RoleName"] = roleInfo.name;
            ViewData["RoleIcon"] = roleInfo.icon;
            ViewData["RoleColor"] = roleInfo.color;
            ViewData["Content"] = content;
            ViewData["MarkdownFile"] = $"/docs/guides/{fileName}";

            return View();
        }

        private static (string name, string icon, string color) GetRoleInfo(string role)
        {
            return role.ToLower() switch
            {
                "admin" => ("Quản trị viên", "bi-shield-lock-fill", "#f59e0b"),
                "manager" => ("Quản lý Khu vực", "bi-person-check-fill", "#ef4444"),
                "staff" => ("Nhân viên Khu vực", "bi-person-badge-fill", "#10b981"),
                "partner" => ("Đối tác", "bi-building-fill", "#f59e0b"),
                "driver" => ("Tài xế", "bi-steering-fill", "#3b82f6"),
                _ => ("Khách hàng", "bi-person-fill", "#8b5cf6")
            };
        }
    }
}
