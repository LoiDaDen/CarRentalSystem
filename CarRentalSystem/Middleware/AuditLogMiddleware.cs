using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CarRentalSystem.Middleware
{
    /// <summary>
    /// Middleware ghi AuditLog cho MOI request HTTP.
    /// </summary>
    public class AuditLogMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditLogMiddleware> _logger;
        private readonly string _connectionString;
        private readonly bool _trustForwardedHeaders;

        private static readonly Regex StaticFilePattern = new Regex(
            @"\.(css|js|map|ico|png|jpg|jpeg|gif|svg|woff2?|ttf|eot|webp|avif)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] ExcludedPaths = new[]
        {
            "/health", "/hangfire", "/docs/", "/images/", "/css/", "/js/",
            "/_content/", "/favicon", "/signalr", "/swagger", "/api/chat",
            "/SecureFile/GetSecureImage", "/Cart/GetCartCount",
            "/Admin/Booking/GetRecentNotifications"
        };

        public AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger, IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
            _trustForwardedHeaders = configuration.GetValue<bool>("Security:TrustForwardedHeaders");

            // =========================================
            // SECURITY: Mask connection string in logs
            // =========================================
            var maskedConn = Regex.Replace(_connectionString,
                @"(Password|Pwd|User Id|Server)=([^;]*)",
                "$1=*****",
                RegexOptions.IgnoreCase);
            _logger.LogInformation("[AUDIT-DBG] Connection: {Conn}", maskedConn);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (ShouldLog(context))
            {
                await WriteAuditLogAsync(context);
            }
        }

        private static bool ShouldLog(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";

            if (StaticFilePattern.IsMatch(path))
                return false;

            if (ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (path.Contains("__browserLink", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private async Task WriteAuditLogAsync(HttpContext context)
        {
            try
            {
                var path = context.Request.Path.Value ?? "";
                var method = context.Request.Method;
                var userId = GetUserId(context);
                var userName = context.User.FindFirst(ClaimTypes.Name)?.Value;
                var (action, entityType) = ParseActionFromPath(method, path);
                var description = BuildDescription(method, path, context.Response.StatusCode);
                var ip = GetClientIp(context);
                var ua = context.Request.Headers["User-Agent"].ToString();
                var timestamp = DateTime.UtcNow;

                // 1. Ghi file (luôn chạy)
                // 2. Ghi DB bằng raw SQL
                const string sql = @"
                    INSERT INTO AuditLogs (Action, EntityType, EntityId, UserId, UserName,
                        Description, IpAddress, UserAgent, RequestUrl, Timestamp, OldValues, NewValues)
                    VALUES (@Action, @EntityType, NULL, @UserId, @UserName,
                        @Description, @IpAddress, @UserAgent, @RequestUrl, @Timestamp, NULL, NULL);
                    SELECT SCOPE_IDENTITY();";

                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Action", action);
                cmd.Parameters.AddWithValue("@EntityType", entityType);
                cmd.Parameters.AddWithValue("@UserId", userId.HasValue ? userId.Value : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@UserName", userName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Description", description);
                cmd.Parameters.AddWithValue("@IpAddress", ip);
                cmd.Parameters.AddWithValue("@UserAgent", ua.Length > 500 ? ua[..500] : ua);
                cmd.Parameters.AddWithValue("@RequestUrl", $"{method} {path}");
                cmd.Parameters.AddWithValue("@Timestamp", timestamp);

                var result = await cmd.ExecuteScalarAsync();
                _logger.LogDebug("[AUDIT-DB] INSERT success. NewID={NewId} Action={Action} Path={Path}", result, action, path);

                // 3. Verify: đọc lại xem có data không
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUDIT-DB] FAILED to insert: {Msg}", ex.Message);
            }
        }

        private static int? GetUserId(HttpContext context)
        {
            var claim = context.User.FindFirst("UserID") ?? context.User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : null;
        }

        private string GetClientIp(HttpContext context)
        {
            if (_trustForwardedHeaders)
            {
                var headerNames = new[]
                {
                    "CF-Connecting-IP",
                    "X-Real-IP",
                    "X-Forwarded-For"
                };

                foreach (var headerName in headerNames)
                {
                    var value = context.Request.Headers[headerName].ToString();
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    var ip = value.Split(',')[0].Trim();
                    if (!string.IsNullOrWhiteSpace(ip))
                        return NormalizeIp(ip);
                }
            }

            return NormalizeIp(context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }

        private static (string action, string entityType) ParseActionFromPath(string method, string path)
        {
            var segs = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            string controller = "System";
            string actionName = "";

            if (segs.Length >= 2)
            {
                if (segs[0].Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                    segs[0].Equals("Partner", StringComparison.OrdinalIgnoreCase) ||
                    segs[0].Equals("Driver", StringComparison.OrdinalIgnoreCase))
                {
                    controller = segs[1];
                    if (segs.Length >= 3) actionName = segs[2];
                }
                else
                {
                    controller = segs[0];
                    actionName = segs[1];
                }
            }
            else if (segs.Length == 1)
            {
                controller = segs[0];
            }

            return (ToFriendlyAction(method, controller, actionName), ToFriendlyModule(controller));
        }

        private static string BuildDescription(string method, string path, int statusCode)
        {
            var (action, module) = ParseActionFromPath(method, path);
            var target = TryGetTargetId(path);
            var result = statusCode >= 400 ? "khong thanh cong" : "thanh cong";
            var targetText = target == null ? "" : $" #{target}";

            return $"{action} trong module {module}{targetText}. Ket qua: {statusCode} ({result}).";
        }

        private static string? TryGetTargetId(string path)
        {
            var segs = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var last = segs.LastOrDefault();
            return int.TryParse(last, out _) ? last : null;
        }

        private static string NormalizeIp(string ip)
        {
            if (string.Equals(ip, "::1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ip, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
                return $"Localhost ({ip})";

            if (ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
                return ip["::ffff:".Length..];

            return ip;
        }

        private static string ToFriendlyModule(string controller)
        {
            return controller.ToLowerInvariant() switch
            {
                "auth" => "Tai khoan",
                "booking" => "Don thue xe",
                "payment" => "Thanh toan",
                "car" => "Xe",
                "cart" => "Gio xe",
                "ekyc" => "eKYC",
                "user" => "Nguoi dung",
                "driver" => "Tai xe",
                "handover" => "Ban giao xe",
                "digitalcontract" => "Hop dong so",
                "contract" => "Hop dong",
                "auditlog" => "Audit log",
                "area" or "areabooking" or "arealocation" => "Khu vuc",
                "dashboard" or "admin" => "Quan tri",
                _ => string.IsNullOrWhiteSpace(controller) ? "He thong" : controller
            };
        }

        private static string ToFriendlyAction(string method, string controller, string actionName)
        {
            var action = actionName.ToLowerInvariant();

            if (controller.Equals("Payment", StringComparison.OrdinalIgnoreCase) && action == "createpayment")
                return "Mo cong PayOS";
            if (controller.Equals("Payment", StringComparison.OrdinalIgnoreCase) && action == "webhook")
                return "PayOS xac nhan";
            if (controller.Equals("Booking", StringComparison.OrdinalIgnoreCase) && action == "book")
                return "Gui yeu cau dat xe";
            if (controller.Equals("Booking", StringComparison.OrdinalIgnoreCase) && action == "deposit")
                return "Mo trang dat coc";
            if (controller.Equals("Booking", StringComparison.OrdinalIgnoreCase) && action == "confirmmanualpayment")
                return "Bao da chuyen khoan";
            if (action is "login")
                return "Dang nhap";
            if (action is "logout")
                return "Dang xuat";
            if (action is "edit" or "update" or "updatestatus")
                return "Cap nhat";
            if (action is "delete" or "remove")
                return "Xoa";
            if (action is "cancel")
                return "Huy";
            if (action is "confirm" or "confirmdeposit" or "confirmpickup")
                return "Xac nhan";
            if (action is "details" or "detail")
                return "Xem chi tiet";
            if (action is "checkout")
                return "Xem xac nhan";
            if (action is "create" or "add")
                return "Tao moi";

            return method.ToUpperInvariant() switch
            {
                "GET" => "Xem",
                "POST" => "Thao tac",
                "PUT" or "PATCH" => "Cap nhat",
                "DELETE" => "Xoa",
                _ => method
            };
        }
    }

    public static class AuditLogMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuditLog(this IApplicationBuilder app)
        {
            return app.UseMiddleware<AuditLogMiddleware>();
        }
    }
}
