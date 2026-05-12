using CarRentalSystem.Authorization.Handlers;
using CarRentalSystem.Authorization.Requirements;
using CarRentalSystem.Data;
using CarRentalSystem.Repositories;
using CarRentalSystem.Services;
using CarRentalSystem.BackgroundJobs;
using CarRentalSystem.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.SqlServer;
using DotNetEnv;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Load .env file (nếu tồn tại) - không crash nếu file không tồn tại
var envFilePath = Path.Combine(builder.Environment.ContentRootPath, ".env");
if (File.Exists(envFilePath))
{
    try
    {
        DotNetEnv.Env.Load(envFilePath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not load .env file: {ex.Message}");
    }
}

ConfigurationGuard.ValidateProductionConfiguration(builder.Configuration, builder.Environment);

// Add services to the container.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews(options =>
{
    // ═══════════════════════════════════════════════════
    // 🛡️ GLOBAL CSRF PROTECTION — Tự động validate AntiForgeryToken
    // cho mọi POST/PUT/DELETE action (trừ [IgnoreAntiforgeryToken])
    // ═══════════════════════════════════════════════════
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
})
.AddViewLocalization()
.AddDataAnnotationsLocalization();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
        // Chỉ enable sensitive data logging trong Development
        .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ICarService, CarService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ITrackingService, TrackingService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPayOSPaymentService, PayOSPaymentService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IBookingQuoteService, BookingQuoteService>();
builder.Services.AddScoped<IReferralService, ReferralService>();
builder.Services.AddScoped<IEligibilityService, EligibilityService>();
builder.Services.AddScoped<IPaymentLedgerService, PaymentLedgerService>();
builder.Services.AddScoped<IBookingTransitionService, BookingTransitionService>();
builder.Services.AddScoped<IFileUploadValidationService, FileUploadValidationService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IAreaService, AreaService>();
builder.Services.AddScoped<ILongTermRentalService, LongTermRentalService>(); // Long-term rental business logic
builder.Services.AddScoped<ISnapshotService, SnapshotService>(); // Database snapshot & backup
builder.Services.AddHttpClient<IDeliveryService, DeliveryService>();
builder.Services.AddSingleton<IDeliveryScopeService, DeliveryScopeService>();

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
// 🤖 reCAPTCHA v2 — Chống bot trên form login/register
// ═══════════════════════════════════════════════════
builder.Services.AddHttpClient<IRecaptchaService, RecaptchaService>();
builder.Services.AddScoped<IRecaptchaService, RecaptchaService>();

// ═══════════════════════════════════════════════════
// 🏥 HEALTH CHECKS — Monitoring endpoint /health
// ═══════════════════════════════════════════════════
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (builder.Configuration.GetValue<bool>("Security:TrustForwardedHeaders"))
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

// ═══════════════════════════════════════════════════
// 🟡 HANGFIRE — Background Job Processing
// ═══════════════════════════════════════════════════
if (!builder.Environment.IsEnvironment("Testing"))
{
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
}

// Đăng ký Background Job classes vào DI
builder.Services.AddScoped<EmailBackgroundJob>();
builder.Services.AddScoped<ContractExpirationJob>();
builder.Services.AddScoped<MaintenanceAlertJob>();
builder.Services.AddScoped<DepositRefundJob>();
builder.Services.AddScoped<ExpireBookingHoldsJob>();
builder.Services.AddScoped<CleanupUnverifiedAccountsJob>();

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
        // Bảo mật: chỉ gửi cookie qua HTTPS trong Production
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
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
    // Bảo mật: chỉ gửi cookie qua HTTPS trong Production
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// ═══════════════════════════════════════════════════
// 🚦 REQUEST SIZE LIMIT — Giới hạn upload 10MB max
// ═══════════════════════════════════════════════════
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();
AuditLogServiceAccessor.ScopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

app.UseForwardedHeaders();

// ═══════════════════════════════════════════════════
// 🗄️ DATABASE MIGRATION & SEED
// Migration: luôn chạy để DB schema up-to-date
// Seed: CHỈ chạy trong Development VÀ khi DB trống (lần đầu tiên)
// Production: KHÔNG seed — dữ liệu phải import thẳng vào DB
// ═══════════════════════════════════════════════════
if (!app.Environment.IsEnvironment("Testing") &&
    app.Configuration.GetValue<bool>("Security:RunMigrationsOnStartup"))
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        context.Database.Migrate();
        
        if (app.Environment.IsDevelopment() &&
            app.Configuration.GetValue<bool>("Security:SeedReferenceDataOnStartup") &&
            !context.Users.Any())
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
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=(self)";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com https://code.jquery.com; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com https://fonts.googleapis.com; " +
        "img-src 'self' data: blob: https:; " +
        "font-src 'self' data: https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.gstatic.com; " +
        "connect-src 'self' https://nominatim.openstreetmap.org; frame-ancestors 'none'";
    await next();
});

// Bật HTTPS redirection trong Production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Tắt ServeUnknownFileTypes để bảo mật
app.UseStaticFiles();
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
            Path.Combine(builder.Environment.ContentRootPath, "..", "docs")),
        RequestPath = "/docs",
        ServeUnknownFileTypes = false,
        DefaultContentType = "text/plain"
    });
}

var supportedCultures = new[] { new CultureInfo("vi-VN"), new CultureInfo("en-US") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("vi-VN"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});
app.UseRouting();
app.UseCors();

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
if (!app.Environment.IsEnvironment("Testing"))
{
    app.MapHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() },
        DashboardTitle = "Car Rental — Background Jobs"
    });
}

// ═══════════════════════════════════════════════════
// 🟡 HANGFIRE RECURRING JOBS — Lên lịch tự động
// ═══════════════════════════════════════════════════
// Chỉ đăng ký recurring jobs khi KHÔNG phải môi trường Testing
// (Testing dùng InMemory DB, không có SQL Server cho Hangfire)
if (!app.Environment.IsEnvironment("Testing") &&
    app.Configuration.GetValue<bool>("Security:SeedReferenceDataOnStartup"))
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

    RecurringJob.AddOrUpdate<ExpireBookingHoldsJob>(
        "expire-booking-holds",
        job => job.Execute(),
        "*/5 * * * *");

    // Xóa tài khoản chưa xác thực email sau 24 giờ
    RecurringJob.AddOrUpdate<CleanupUnverifiedAccountsJob>(
        "cleanup-unverified-accounts",
        job => job.ExecuteAsync(),
        Cron.Hourly); // Chạy mỗi giờ
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

        // --- Seed ContractTypes — Chỉ thêm nếu chưa có ---
        var requiredPackages = new[] { "Theo ngày", "1 Tuần", "1 Tháng", "3 Tháng", "6 Tháng", "1 Năm" };
        var existingPackageNames = db.ContractTypes
            .Where(c => requiredPackages.Contains(c.Name))
            .Select(c => c.Name)
            .ToList();

        // Chỉ seed nếu thiếu gói nào đó, KHÔNG xóa existing
        if (existingPackageNames.Count < 6)
        {
            // Thêm các gói còn thiếu
            var missingPackages = requiredPackages.Where(p => !existingPackageNames.Contains(p)).ToList();

            foreach (var packageName in missingPackages)
            {
                switch (packageName)
                {
                    case "Theo ngày":
                        db.ContractTypes.Add(new CarRentalSystem.Models.ContractType
                        {
                            Name = "Theo ngày",
                            MinDurationDays = 1,
                            MaxDurationDays = 6,
                            DiscountPercent = 0,
                            FreeMaintenanceCount = 0,
                            IsActive = true,
                            Description = "Thuê xe theo ngày, không giảm giá"
                        });
                        break;
                    case "1 Tuần":
                        db.ContractTypes.Add(new CarRentalSystem.Models.ContractType
                        {
                            Name = "1 Tuần",
                            MinDurationDays = 7,
                            MaxDurationDays = 29,
                            DiscountPercent = 5,
                            FreeMaintenanceCount = 0,
                            IsActive = true,
                            Description = "Thuê từ 7 ngày, giảm 5% so với giá lẻ"
                        });
                        break;
                    case "1 Tháng":
                        db.ContractTypes.Add(new CarRentalSystem.Models.ContractType
                        {
                            Name = "1 Tháng",
                            MinDurationDays = 30,
                            MaxDurationDays = 30,
                            DiscountPercent = 10,
                            FreeMaintenanceCount = 1,
                            IsActive = true,
                            Description = "Thuê 30 ngày, giảm 10%, miễn phí 1 lần bảo dưỡng"
                        });
                        break;
                    case "3 Tháng":
                        db.ContractTypes.Add(new CarRentalSystem.Models.ContractType
                        {
                            Name = "3 Tháng",
                            MinDurationDays = 90,
                            MaxDurationDays = 90,
                            DiscountPercent = 15,
                            FreeMaintenanceCount = 2,
                            IsActive = true,
                            Description = "Thuê 3 tháng, giảm 15%, miễn phí 2 lần bảo dưỡng"
                        });
                        break;
                    case "6 Tháng":
                        db.ContractTypes.Add(new CarRentalSystem.Models.ContractType
                        {
                            Name = "6 Tháng",
                            MinDurationDays = 180,
                            MaxDurationDays = 180,
                            DiscountPercent = 20,
                            FreeMaintenanceCount = 4,
                            IsActive = true,
                            Description = "Thuê 6 tháng, giảm 20%, miễn phí 4 lần bảo dưỡng"
                        });
                        break;
                    case "1 Năm":
                        db.ContractTypes.Add(new CarRentalSystem.Models.ContractType
                        {
                            Name = "1 Năm",
                            MinDurationDays = 360,
                            MaxDurationDays = 365,
                            DiscountPercent = 25,
                            FreeMaintenanceCount = 8,
                            IsActive = true,
                            Description = "Thuê 1 năm, giảm 25%, miễn phí 8 lần bảo dưỡng, ưu tiên xe mới"
                        });
                        break;
                }
            }
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
