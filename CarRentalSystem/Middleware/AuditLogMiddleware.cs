using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CarRentalSystem.Middleware
{
    /// <summary>
    /// Middleware ghi AuditLog cho MỌI request HTTP.
    /// Dùng file-based logging để debug + raw SQL INSERT để verify DB.
    /// </summary>
    public class AuditLogMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditLogMiddleware> _logger;
        private readonly string _connectionString;

        private static readonly Regex StaticFilePattern = new Regex(
            @"\.(css|js|map|ico|png|jpg|jpeg|gif|svg|woff2?|ttf|eot|webp|avif)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] ExcludedPaths = new[]
        {
            "/health", "/hangfire", "/docs/", "/images/", "/css/", "/js/",
            "/_content/", "/favicon", "/signalr", "/swagger", "/api/chat",
        };

        private static readonly string _debugFile = Path.Combine(AppContext.BaseDirectory, "audit_debug.log");

        public AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger, IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";

            _logger.LogInformation("[AUDIT-DBG] Connection string: {Conn}", _connectionString);
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
            if (context.Response.StatusCode >= 400)
                return false;

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
                var ip = GetClientIp(context);
                var ua = context.Request.Headers["User-Agent"].ToString();
                var timestamp = DateTime.UtcNow;

                // 1. Ghi file (luôn chạy)
                var fileLine = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] {method} {path} | Status:{context.Response.StatusCode} | User:{userName ?? "Anonymous"} (ID:{userId}) | Action:{action} | Entity:{entityType} | IP:{ip}\n";
                File.AppendAllText(_debugFile, fileLine);

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
                cmd.Parameters.AddWithValue("@Description", $"{method} {path} → {context.Response.StatusCode}");
                cmd.Parameters.AddWithValue("@IpAddress", ip);
                cmd.Parameters.AddWithValue("@UserAgent", ua.Length > 500 ? ua[..500] : ua);
                cmd.Parameters.AddWithValue("@RequestUrl", $"{method} {path}");
                cmd.Parameters.AddWithValue("@Timestamp", timestamp);

                var result = await cmd.ExecuteScalarAsync();
                _logger.LogInformation("[AUDIT-DB] INSERT success! NewID={NewId} Action={Action} Path={Path}", result, action, path);

                // 3. Verify: đọc lại xem có data không
                await using var verifyConn = new SqlConnection(_connectionString);
                await verifyConn.OpenAsync();
                await using var verifyCmd = new SqlCommand("SELECT COUNT(*) FROM AuditLogs", verifyConn);
                var count = (int)(await verifyCmd.ExecuteScalarAsync() ?? 0);
                _logger.LogInformation("[AUDIT-DB] Verify: Total AuditLogs in DB = {Count}", count);
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

        private static string GetClientIp(HttpContext context)
        {
            var fwd = context.Request.Headers["X-Forwarded-For"].ToString();
            return !string.IsNullOrEmpty(fwd) ? fwd.Split(',')[0].Trim() : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private static (string action, string entityType) ParseActionFromPath(string method, string path)
        {
            var segs = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            string entityType = "System";
            string action = method switch
            {
                "GET" => "View",
                "POST" => "Create",
                "PUT" or "PATCH" => "Update",
                "DELETE" => "Delete",
                _ => method
            };

            if (segs.Length >= 2)
            {
                if (segs[0].Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                    segs[0].Equals("Partner", StringComparison.OrdinalIgnoreCase) ||
                    segs[0].Equals("Driver", StringComparison.OrdinalIgnoreCase))
                {
                    entityType = segs[1];
                    if (segs.Length >= 3) action = segs[2];
                }
                else
                {
                    entityType = segs[0];
                    if (segs.Length >= 2) action = segs[1];
                }
            }
            else if (segs.Length == 1)
            {
                entityType = segs[0];
            }

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
