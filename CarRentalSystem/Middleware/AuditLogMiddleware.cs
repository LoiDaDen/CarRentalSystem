using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CarRentalSystem.Middleware
{
    /// <summary>
    /// Middleware tự động ghi AuditLog cho mọi thao tác POST/PUT/DELETE.
    /// Ghi lại: user, action, URL, IP, timestamp.
    /// </summary>
    public class AuditLogMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditLogMiddleware> _logger;

        public AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Chỉ log cho các request thay đổi dữ liệu
            var method = context.Request.Method;
            bool shouldLog = method == "POST" || method == "PUT" || method == "DELETE" || method == "PATCH";

            await _next(context);

            // Log sau khi request xử lý xong (để biết status code)
            if (shouldLog && context.Response.StatusCode < 500)
            {
                try
                {
                    await LogAuditAsync(context, method);
                }
                catch (Exception ex)
                {
                    // Không để audit log lỗi ảnh hưởng request chính
                    _logger.LogWarning(ex, "Failed to write audit log for {Path}", context.Request.Path);
                }
            }
        }

        private async Task LogAuditAsync(HttpContext context, string method)
        {
            using var scope = context.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var path = context.Request.Path.Value ?? "";
            var userId = GetUserId(context);
            var userName = context.User.FindFirst(ClaimTypes.Name)?.Value;

            // Phân tích action & entity từ URL
            var (action, entityType) = ParseActionFromPath(method, path);

            var log = new AuditLog
            {
                Action = action,
                EntityType = entityType,
                UserId = userId,
                UserName = userName ?? "Anonymous",
                Description = $"{method} {path} → {context.Response.StatusCode}",
                IpAddress = GetClientIp(context),
                UserAgent = context.Request.Headers["User-Agent"].ToString().Length > 500
                    ? context.Request.Headers["User-Agent"].ToString()[..500]
                    : context.Request.Headers["User-Agent"].ToString(),
                RequestUrl = $"{method} {path}",
                Timestamp = DateTime.UtcNow
            };

            db.AuditLogs.Add(log);
            await db.SaveChangesAsync();
        }

        private static int? GetUserId(HttpContext context)
        {
            var userIdClaim = context.User.FindFirst("UserID") ??
                              context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int id))
                return id;
            return null;
        }

        private static string GetClientIp(HttpContext context)
        {
            // Ưu tiên lấy IP thật từ proxy header
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrEmpty(forwardedFor))
                return forwardedFor.Split(',')[0].Trim();

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private static (string action, string entityType) ParseActionFromPath(string method, string path)
        {
            // Phân tích thông minh từ URL patterns
            var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            string entityType = "System";
            string action = method switch
            {
                "POST" => "Create",
                "PUT" or "PATCH" => "Update",
                "DELETE" => "Delete",
                _ => method
            };

            // URL patterns: /Admin/Car/Create, /Booking/Checkout, /Auth/Login
            if (segments.Length >= 2)
            {
                // Check for area pattern: /Admin/Booking/Create
                if (segments[0].Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                    segments[0].Equals("Partner", StringComparison.OrdinalIgnoreCase))
                {
                    entityType = segments.Length >= 2 ? segments[1] : segments[0];
                    if (segments.Length >= 3)
                        action = segments[2];
                }
                else
                {
                    entityType = segments[0];
                    action = segments.Length >= 2 ? segments[1] : method;
                }
            }
            else if (segments.Length == 1)
            {
                entityType = segments[0];
            }

            // Dọn dẹp: capitalize
            entityType = char.ToUpper(entityType[0]) + entityType[1..];
            action = char.ToUpper(action[0]) + action[1..];

            return (action, entityType);
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
