using CarRentalSystem.Data;
using CarRentalSystem.Repositories;
using CarRentalSystem.Services;
using CarRentalSystem.BackgroundJobs;
using CarRentalSystem.Middleware;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // ═══════════════════════════════════════════════════
    // 🛡️ GLOBAL CSRF PROTECTION — Tự động validate AntiForgeryToken
    // cho mọi POST/PUT/DELETE action (trừ [IgnoreAntiforgeryToken])
    // ═══════════════════════════════════════════════════
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ICarService, CarService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ITrackingService, TrackingService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddHttpClient<IDeliveryService, DeliveryService>();

// ═══════════════════════════════════════════════════
// 🔴 REDIS DISTRIBUTED CACHE (Fallback: MemoryCache)
// ═══════════════════════════════════════════════════
var redisConnection = builder.Configuration.GetValue<string>("Redis:ConnectionString");
if (!string.IsNullOrEmpty(redisConnection))
{
    // Nếu có cấu hình Redis → dùng Redis thật
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "CarRental:";
    });
}
else
{
    // Fallback: dùng MemoryCache (cache trong RAM .NET, mất khi restart)
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddScoped<ICacheService, CacheService>();

// ═══════════════════════════════════════════════════
// 🟡 HANGFIRE — Background Job Processing
// ═══════════════════════════════════════════════════
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"), new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));
builder.Services.AddHangfireServer();

// Đăng ký Background Job classes vào DI
builder.Services.AddScoped<EmailBackgroundJob>();
builder.Services.AddScoped<ContractExpirationJob>();
builder.Services.AddScoped<MaintenanceAlertJob>();
builder.Services.AddScoped<DepositRefundJob>();

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";

        // ═══════════════════════════════════════════════════
        // 🔒 SESSION SECURITY — Cookie timeout & hardening
        // ═══════════════════════════════════════════════════
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Name = "CarRental.Auth";
    });

// ═══════════════════════════════════════════════════
// 🚦 REQUEST SIZE LIMIT — Giới hạn upload 10MB max
// ═══════════════════════════════════════════════════
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

var app = builder.Build();

// Chỉ seed data khi KHÔNG phải môi trường Testing (Smoke Tests dùng InMemory DB)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        DbSeeder.Seed(context);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ═══════════════════════════════════════════════════
// 🔒 SECURITY HEADERS — Chống clickjacking, XSS, MIME sniffing
// ═══════════════════════════════════════════════════
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ═══════════════════════════════════════════════
// 🚦 RATE LIMITING — Chống brute force & API abuse
// ═══════════════════════════════════════════════
app.UseRateLimiting();

// ═══════════════════════════════════════════════
// 🛡️ GLOBAL ERROR HANDLING — Bắt mọi exception
// ═══════════════════════════════════════════════
app.UseGlobalExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

// ═══════════════════════════════════════════════
// 📝 AUDIT LOG — Ghi lại mọi thao tác POST/PUT/DELETE
// ═══════════════════════════════════════════════
app.UseAuditLog();

// ═══════════════════════════════════════════════════
// 🟡 HANGFIRE DASHBOARD — chỉ Admin mới truy cập được
// ═══════════════════════════════════════════════════
app.MapHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    DashboardTitle = "Car Rental — Background Jobs"
});

// ═══════════════════════════════════════════════════
// 🟡 HANGFIRE RECURRING JOBS — Lên lịch tự động
// ═══════════════════════════════════════════════════
RecurringJob.AddOrUpdate<ContractExpirationJob>(
    "contract-expiration-check",
    job => job.Execute(),
    Cron.Hourly); // Chạy mỗi giờ

RecurringJob.AddOrUpdate<MaintenanceAlertJob>(
    "maintenance-alert-check",
    job => job.Execute(),
    Cron.Daily(8)); // Mỗi ngày lúc 8:00 AM

RecurringJob.AddOrUpdate<DepositRefundJob>(
    "deposit-refund-process",
    job => job.Execute(),
    Cron.Daily(9)); // Mỗi ngày lúc 9:00 AM

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

// ═══════════════════════════════════════════════════
// 🔒 Hangfire Dashboard Authorization Filter
// Chỉ cho phép user đã đăng nhập với quyền Admin
// ═══════════════════════════════════════════════════
public class HangfireAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext dashboardContext)
    {
        // Lấy HttpContext từ OwinContext (Hangfire 1.8.x cách chuẩn)
        var httpContext = dashboardContext.GetHttpContext();
        if (httpContext == null) return false;

        // Cho phép truy cập tự do (ai cũng vào được) trong môi trường Development
        var env = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (env.IsDevelopment())
            return true;

        // Production: Yêu cầu phải đăng nhập VÀ là Admin
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("Admin");
    }
}

// Extension method cho DashboardContext → HttpContext
public static class HangfireDashboardExtensions
{
    public static HttpContext GetHttpContext(this Hangfire.Dashboard.DashboardContext context)
    {
        // Sử dụng reflection để lấy HttpContext từ DashboardContext
        var httpContextProperty = context.GetType().GetProperty("HttpContext");
        return httpContextProperty?.GetValue(context) as HttpContext;
    }
}
