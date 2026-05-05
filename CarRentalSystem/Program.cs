using CarRentalSystem.Authorization.Handlers;
using CarRentalSystem.Authorization.Requirements;
using CarRentalSystem.Data;
using CarRentalSystem.Repositories;
using CarRentalSystem.Services;
using CarRentalSystem.BackgroundJobs;
using CarRentalSystem.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.SqlServer;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

DotNetEnv.Env.Load(Path.Combine(builder.Environment.ContentRootPath, ".env"));

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
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
        .EnableSensitiveDataLogging());

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ICarService, CarService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ITrackingService, TrackingService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPayOSPaymentService, PayOSPaymentService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IAreaService, AreaService>();
builder.Services.AddScoped<ILongTermRentalService, LongTermRentalService>(); // Long-term rental business logic
builder.Services.AddScoped<ISnapshotService, SnapshotService>(); // Database snapshot & backup
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
// 📅 HOLIDAY PRICING — Tăng giá ngày lễ/cuối tuần
builder.Services.AddSingleton<IHolidayPricingService, HolidayPricingService>();

// ═══════════════════════════════════════════════════
// 🔒 PII ENCRYPTION — Mã hóa CCCD/GPLX at rest
// ═══════════════════════════════════════════════════
builder.Services.AddDataProtection();
builder.Services.AddScoped<IPiiEncryptionService, PiiEncryptionService>();

builder.Services.AddHttpClient<CarRentalSystem.Services.IEkycService, CarRentalSystem.Services.FptEkycService>();
builder.Services.AddScoped<CarRentalSystem.Services.IEkycService, CarRentalSystem.Services.FptEkycService>();

// ═══════════════════════════════════════════════════
// 🤖 GROQ AI CHAT (Miễn phí, không giới hạn)
// ═══════════════════════════════════════════════════
var groqApiKey = builder.Configuration["Groq:ApiKey"]
                  ?? Environment.GetEnvironmentVariable("GROQ_API_KEY")
                  ?? "";
builder.Services.AddHttpClient("GroqChatService", client =>
{
    client.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
    if (!string.IsNullOrWhiteSpace(groqApiKey))
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", groqApiKey);
});
builder.Services.AddSingleton<IChatSessionService, ChatSessionService>();
builder.Services.AddScoped<CarRentalSystem.Services.GroqChatService>();

// ═══════════════════════════════════════════════════
// 🔍 QR CCCD PARSER — Xử lý dữ liệu từ CCCD chip
// ═══════════════════════════════════════════════════
builder.Services.AddScoped<IQrCccdParserService, QrCccdParserService>();

// ═══════════════════════════════════════════════════
// 🏥 HEALTH CHECKS — Monitoring endpoint /health
// ═══════════════════════════════════════════════════
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

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
// 🔐 AREA AUTHORIZATION — Phân quyền theo Area
// Handler xử lý resource-based authorization cho Areas
// ═══════════════════════════════════════════════════
builder.Services.AddScoped<IAuthorizationHandler, AreaAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AreaView", policy =>
        policy.Requirements.Add(new AreaViewRequirement()));
    options.AddPolicy("AreaCreate", policy =>
        policy.Requirements.Add(new AreaCreateRequirement()));
    options.AddPolicy("AreaUpdate", policy =>
        policy.Requirements.Add(new AreaUpdateRequirement()));
    options.AddPolicy("AreaDelete", policy =>
        policy.Requirements.Add(new AreaDeleteRequirement()));
    options.AddPolicy("AreaManageStaff", policy =>
        policy.Requirements.Add(new AreaManageStaffRequirement()));
    options.AddPolicy("AreaViewStats", policy =>
        policy.Requirements.Add(new AreaViewStatsRequirement()));
});
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "CarRental.Session";
});

// ═══════════════════════════════════════════════════
// 🚦 REQUEST SIZE LIMIT — Giới hạn upload 10MB max
// ═══════════════════════════════════════════════════
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

var app = builder.Build();
AuditLogServiceAccessor.ScopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

// ═══════════════════════════════════════════════════
// 🗄️ DATABASE MIGRATION & SEED
// Migration: luôn chạy để DB schema up-to-date
// Seed: CHỈ chạy trong Development VÀ khi DB trống (lần đầu tiên)
// Production: KHÔNG seed — dữ liệu phải import thẳng vào DB
// ═══════════════════════════════════════════════════
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        // Luôn chạy migration để DB schema đồng bộ code
        context.Database.Migrate();
        
        // Chỉ seed dữ liệu mẫu trong Development VÀ khi DB trống
        if (app.Environment.IsDevelopment() && !context.Users.Any())
        {
            DbSeeder.Seed(context);
        }
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

// app.UseHttpsRedirection(); // Tạm thời tắt trong Development
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "..", "docs")),
    RequestPath = "/docs",
    ServeUnknownFileTypes = true,
    DefaultContentType = "text/plain"
});
app.UseRouting();

// ═══════════════════════════════════════════════
// 🚦 RATE LIMITING — Chống brute force & API abuse
// ═══════════════════════════════════════════════
app.UseRateLimiting();

// ═══════════════════════════════════════════════
// 🛡️ GLOBAL ERROR HANDLING — Bắt mọi exception
// ═══════════════════════════════════════════════
app.UseGlobalExceptionHandler();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// ═══════════════════════════════════════════════
// 🔒 ACCOUNT LOCK CHECKER — Kiểm tra IsLocked/IsBlacklisted
// sau mỗi request đã xác thực
// ═══════════════════════════════════════════════
app.UseAccountLockChecker();

// ═══════════════════════════════════════════════
// 📝 AUDIT LOG — Ghi lại MỌI request HTTP (View, Create, Update, Delete)
// Loại trừ: static files, health check, hangfire, signalr
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
// Chỉ đăng ký recurring jobs khi KHÔNG phải môi trường Testing
// (Testing dùng InMemory DB, không có SQL Server cho Hangfire)
if (!app.Environment.IsEnvironment("Testing"))
{
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
}

// ═══════════════════════════════════════════════════
// 📦 DATABASE SEED — Đảm bảo có gói thuê dài hạn
// Chỉ chạy khi KHÔNG phải Testing (Testing dùng InMemory DB đã seed sẵn)
// ═══════════════════════════════════════════════════
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // --- Seed ContractTypes — Always ensure 5 long-term packages exist ---
        var requiredPackages = new[] { "Theo ngày", "1 Tuần", "1 Tháng", "3 Tháng", "6 Tháng", "1 Năm" };
        var existingPackageNames = db.ContractTypes
            .Where(c => requiredPackages.Contains(c.Name))
            .Select(c => c.Name)
            .ToList();

        // If any package is missing, clear all and re-seed
        if (existingPackageNames.Count != 6)
        {
            var allContracts = db.ContractTypes.ToList();
            if (allContracts.Any())
            {
                db.ContractTypes.RemoveRange(allContracts);
                db.SaveChanges();
            }

            db.ContractTypes.AddRange(
                new CarRentalSystem.Models.ContractType
                {
                    Name = "Theo ngày",
                    MinDurationDays = 1,
                    MaxDurationDays = 6,
                    DiscountPercent = 0,
                    FreeMaintenanceCount = 0,
                    IsActive = true,
                    Description = "Thuê xe theo ngày, không giảm giá"
                },
                new CarRentalSystem.Models.ContractType
                {
                    Name = "1 Tuần",
                    MinDurationDays = 7,
                    MaxDurationDays = 29,
                    DiscountPercent = 5,
                    FreeMaintenanceCount = 0,
                    IsActive = true,
                    Description = "Thuê từ 7 ngày, giảm 5% so với giá lẻ"
                },
                new CarRentalSystem.Models.ContractType
                {
                    Name = "1 Tháng",
                    MinDurationDays = 30,
                    MaxDurationDays = 30,
                    DiscountPercent = 10,
                    FreeMaintenanceCount = 1,
                    IsActive = true,
                    Description = "Thuê 30 ngày, giảm 10%, miễn phí 1 lần bảo dưỡng"
                },
                new CarRentalSystem.Models.ContractType
                {
                    Name = "3 Tháng",
                    MinDurationDays = 90,
                    MaxDurationDays = 90,
                    DiscountPercent = 15,
                    FreeMaintenanceCount = 2,
                    IsActive = true,
                    Description = "Thuê 3 tháng, giảm 15%, miễn phí 2 lần bảo dưỡng"
                },
                new CarRentalSystem.Models.ContractType
                {
                    Name = "6 Tháng",
                    MinDurationDays = 180,
                    MaxDurationDays = 180,
                    DiscountPercent = 20,
                    FreeMaintenanceCount = 4,
                    IsActive = true,
                    Description = "Thuê 6 tháng, giảm 20%, miễn phí 4 lần bảo dưỡng"
                },
                new CarRentalSystem.Models.ContractType
                {
                    Name = "1 Năm",
                    MinDurationDays = 360,
                    MaxDurationDays = 365,
                    DiscountPercent = 25,
                    FreeMaintenanceCount = 8,
                    IsActive = true,
                    Description = "Thuê 1 năm, giảm 25%, miễn phí 8 lần bảo dưỡng, ưu tiên xe mới"
                }
            );
            db.SaveChanges();
        }

        // Seed AddOns if none exist
        if (!db.AddOns.Any())
        {
            db.AddOns.AddRange(
                new CarRentalSystem.Models.AddOn
                {
                    Name = "Nước suối",
                    Description = "Chai 500ml mát lạnh",
                    PricePerDay = 10000,
                    IsActive = true
                },
                new CarRentalSystem.Models.AddOn
                {
                    Name = "Khăn lạnh",
                    Description = "Khăn ướt mát lạnh thơm",
                    PricePerDay = 5000,
                    IsActive = true
                },
                new CarRentalSystem.Models.AddOn
                {
                    Name = "Combo Ăn Nhẹ",
                    Description = "Bánh mì + nước + khăn lạnh",
                    PricePerDay = 50000,
                    IsActive = true
                },
                new CarRentalSystem.Models.AddOn
                {
                    Name = "Combo VIP",
                    Description = "Nước + bánh + khăn + hoa xe + sạc dự phòng",
                    PricePerDay = 100000,
                    IsActive = true
                }
            );
            db.SaveChanges();
        }
    }
}

// ═══════════════════════════════════════════════════
// 🏥 HEALTH CHECK ENDPOINT — /health
// ═══════════════════════════════════════════════════
app.MapHealthChecks("/health");

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
// 🌍 SHARED SERVICE PROVIDER — cho background services
// ═══════════════════════════════════════════════════
public static class AppHost
{
    public static IServiceProvider? Services { get; set; }
}

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
