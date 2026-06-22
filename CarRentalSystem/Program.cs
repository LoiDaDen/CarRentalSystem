using CarRentalSystem.Authorization.Handlers;
using CarRentalSystem.Authorization.Requirements;
using CarRentalSystem.Authorization;
using CarRentalSystem.Data;
using CarRentalSystem.Repositories;
using CarRentalSystem.Services;
using CarRentalSystem.Services.Ai;
using CarRentalSystem.Hubs;
using CarRentalSystem.BackgroundJobs;
using CarRentalSystem.Middleware;
using CarRentalSystem.Models;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Hangfire;
using Hangfire.SqlServer;
using DotNetEnv;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;

var earlyEnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var shouldLoadEnvFile = !string.Equals(earlyEnvironmentName, "Playwright", StringComparison.OrdinalIgnoreCase)
    && !string.Equals(earlyEnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);
var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (shouldLoadEnvFile && File.Exists(envFilePath))
{
    try
    {
        DotNetEnv.Env.NoClobber().Load(envFilePath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not load .env file: {ex.Message}");
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
var isPlaywrightEnvironment = builder.Environment.IsEnvironment("Playwright");
var isAutomatedTestEnvironment = builder.Environment.IsEnvironment("Testing") || isPlaywrightEnvironment;
var allowLocalHttpCookies = builder.Environment.IsDevelopment() || isAutomatedTestEnvironment;

ConfigurationGuard.ValidateProductionConfiguration(builder.Configuration, builder.Environment);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // ═══════════════════════════════════════════════════
    // 🛡️ GLOBAL CSRF PROTECTION — Tự động validate AntiForgeryToken
    // cho mọi POST/PUT/DELETE action (trừ [IgnoreAntiforgeryToken])
    // ═══════════════════════════════════════════════════
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddSignalR();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/problem+json",
        "application/manifest+json",
        "image/svg+xml"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<AiProviderOptions>(options =>
{
    options.Enabled = builder.Configuration.GetValue("Ai:Enabled", false);
    options.Provider = builder.Configuration["Ai:Provider"] ?? "OpenAI";
    options.ApiKey = builder.Configuration["Ai:ApiKey"]
        ?? (string.Equals(options.Provider, "Groq", StringComparison.OrdinalIgnoreCase)
            ? builder.Configuration["Groq:ApiKey"]
            : null)
        ?? Environment.GetEnvironmentVariable("AI_API_KEY")
        ?? (string.Equals(options.Provider, "Groq", StringComparison.OrdinalIgnoreCase)
            ? Environment.GetEnvironmentVariable("GROQ_API_KEY")
            : null)
        ?? "";
    options.BaseUrl = builder.Configuration["Ai:BaseUrl"]
        ?? (string.Equals(options.Provider, "Groq", StringComparison.OrdinalIgnoreCase)
            ? "https://api.groq.com/openai/v1/"
            : "https://api.openai.com/v1/");
    options.Model = builder.Configuration["Ai:Model"]
        ?? (string.Equals(options.Provider, "Groq", StringComparison.OrdinalIgnoreCase)
            ? "llama-3.3-70b-versatile"
            : "gpt-4.1-mini");
});
builder.Services.AddHttpClient("AiProvider");
builder.Services.AddScoped<IAiToolExecutor, AiToolExecutor>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            sqlOptions.CommandTimeout(builder.Configuration.GetValue("Database:CommandTimeoutSeconds", 20));
        });

    options.ConfigureWarnings(warnings =>
    {
        warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning);
        warnings.Ignore(CoreEventId.FirstWithoutOrderByAndFilterWarning);
    });
});

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ICarService, CarService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IIdentityHashService, IdentityHashService>();
builder.Services.AddSingleton<IPasswordHashService, PasswordHashService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IBookingFactory, BookingFactory>();
builder.Services.AddScoped<ITrackingService, TrackingService>();
var useLocalEmailSink = EmailDeliveryConfiguration.ShouldUseLocalSink(
    builder.Configuration,
    builder.Environment,
    isAutomatedTestEnvironment);
if (useLocalEmailSink)
    builder.Services.AddScoped<IEmailService, LocalEmailSinkService>();
else
    builder.Services.AddScoped<IEmailService, EmailService>();
if (isPlaywrightEnvironment && !builder.Configuration.GetValue<bool>("Playwright:UsePayOSSandbox"))
    builder.Services.AddSingleton<IPayOSGateway, PlaywrightPayOSGateway>();
else
    builder.Services.AddSingleton<IPayOSGateway, PayOSGateway>();
builder.Services.AddScoped<IPayOSPaymentService, PayOSPaymentService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IBookingQuoteService, BookingQuoteService>();
builder.Services.AddScoped<ISelfDrivePricingService, SelfDrivePricingService>();
builder.Services.AddScoped<IWithDriverPricingService, WithDriverPricingService>();
builder.Services.AddScoped<IBookingBusinessRuleService, BookingBusinessRuleService>();
builder.Services.AddScoped<IReferralService, ReferralService>();
builder.Services.AddScoped<ICustomerDrivingLicenseSyncService, CustomerDrivingLicenseSyncService>();
builder.Services.AddScoped<IEligibilityService, EligibilityService>();
builder.Services.AddScoped<IPaymentLedgerService, PaymentLedgerService>();
builder.Services.AddScoped<IPaymentTransitionService, PaymentTransitionService>();
builder.Services.AddScoped<IPrivacyRequestService, PrivacyRequestService>();
builder.Services.AddScoped<ILegalComplianceService, LegalComplianceService>();
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IWebhookSigningService, WebhookSigningService>();
builder.Services.AddScoped<ICameraAnprService, CameraAnprService>();
builder.Services.AddScoped<ICustomerSelfServiceService, CustomerSelfServiceService>();
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddScoped<IBookingTransitionService, BookingTransitionService>();
builder.Services.AddScoped<IFileUploadValidationService, FileUploadValidationService>();
builder.Services.AddScoped<IContractClassificationService, ContractClassificationService>();
builder.Services.AddScoped<IDigitalContractService, DigitalContractService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IAreaService, AreaService>();
builder.Services.AddScoped<ICarChangeRequestService, CarChangeRequestService>();
builder.Services.AddScoped<IInternalPortalScopeService, InternalPortalScopeService>();
builder.Services.AddScoped<IBookingAssignmentService, BookingAssignmentService>();
builder.Services.AddScoped<IHandoverWorkflowService, HandoverWorkflowService>();
builder.Services.AddScoped<IManagerReportService, ManagerReportService>();
builder.Services.AddScoped<ILongTermRentalService, LongTermRentalService>(); // Long-term rental business logic
builder.Services.AddScoped<IBlogService, BlogService>();
builder.Services.AddScoped<ISnapshotService, SnapshotService>(); // Database snapshot & backup
builder.Services.AddScoped<IAuditTrailService, AuditTrailService>();
builder.Services.AddScoped<IPiiAccessService, PiiAccessService>();
builder.Services.AddSingleton<IAuditLogQueue, BoundedAuditLogQueue>();
if (!isAutomatedTestEnvironment)
    builder.Services.AddHostedService<AuditLogBackgroundWriter>();
builder.Services.AddScoped<IEvidenceHashService, EvidenceHashService>();
builder.Services.AddScoped<IDisputeExportService, DisputeExportService>();
builder.Services.AddScoped<ISettlementReviewService, SettlementReviewService>();
builder.Services.AddScoped<ISloMetricsService, SloMetricsService>();
builder.Services.AddSingleton<IEnterpriseTelemetryService, EnterpriseTelemetryService>();
builder.Services.AddScoped<IEnterpriseOperationsService, EnterpriseOperationsService>();
builder.Services.AddScoped<IOperationsCopilotService, OperationsCopilotService>();
builder.Services.AddScoped<ICustomerRiskScoringService, CustomerRiskScoringService>();
builder.Services.AddScoped<IRevenueMetricsService, RevenueMetricsService>();
builder.Services.Configure<StartupKpiOptions>(builder.Configuration.GetSection("StartupKpi"));
builder.Services.AddScoped<IStartupKpiService, StartupKpiService>();
builder.Services.AddScoped<IPartnerSettlementService, PartnerSettlementService>();
builder.Services.AddScoped<IProductAnalyticsService, NoopProductAnalyticsService>();
builder.Services.AddScoped<ILocaleFormatService, LocaleFormatService>();
builder.Services.AddScoped<ITutorialService, TutorialService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IDeliveryService, DeliveryService>();
builder.Services.AddHttpClient<IPayOSPayoutService, PayOSPayoutService>();
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
builder.Services.AddScoped<ICatalogCacheService, CatalogCacheService>();
builder.Services.AddSingleton<IDistributedRateLimitStore, DistributedRateLimitStore>();

// ═══════════════════════════════════════════════════
// 📅 HOLIDAY PRICING — Tăng giá ngày lễ/cuối tuần
builder.Services.AddSingleton<IHolidayPricingService, HolidayPricingService>();

// ═══════════════════════════════════════════════════
// 🔒 PII ENCRYPTION — Mã hóa CCCD/GPLX at rest
// ═══════════════════════════════════════════════════
var dataProtectionBuilder = builder.Services.AddDataProtection();
var dataProtectionApplicationName = builder.Configuration["DataProtection:ApplicationName"];
if (!string.IsNullOrWhiteSpace(dataProtectionApplicationName))
{
    dataProtectionBuilder.SetApplicationName(dataProtectionApplicationName);
}

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}
builder.Services.AddScoped<IPiiEncryptionService, PiiEncryptionService>();

if (isPlaywrightEnvironment && !builder.Configuration.GetValue<bool>("Playwright:UseFptSandbox"))
    builder.Services.AddScoped<CarRentalSystem.Services.IEkycService, CarRentalSystem.Services.PlaywrightEkycService>();
else
    builder.Services.AddHttpClient<CarRentalSystem.Services.IEkycService, CarRentalSystem.Services.FptEkycService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

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
builder.Services.AddScoped<CarRentalSystem.Services.GeminiChatService>();
builder.Services.AddScoped<CarRentalSystem.Services.GroqChatService>();

// ═══════════════════════════════════════════════════
// 🔍 QR CCCD PARSER — Xử lý dữ liệu từ CCCD chip
// ═══════════════════════════════════════════════════
builder.Services.AddScoped<IQrCccdParserService, QrCccdParserService>();

// ═══════════════════════════════════════════════════
// 🤖 reCAPTCHA v2 — Chống bot trên form login/register
// ═══════════════════════════════════════════════════
builder.Services.AddHttpClient<IRecaptchaService, RecaptchaService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Recaptcha:VerifyTimeoutSeconds", 6));
});

// ═══════════════════════════════════════════════════
// 🏥 HEALTH CHECKS — Monitoring endpoint /health
// ═══════════════════════════════════════════════════
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

if (builder.Configuration.GetValue("Observability:OpenTelemetry:Enabled", false))
{
    var serviceName = builder.Configuration["Observability:OpenTelemetry:ServiceName"] ?? "CarRentalSystem";
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(serviceName))
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource(EnterpriseTelemetryService.ActivitySourceName);

            var endpoint = builder.Configuration["Observability:OpenTelemetry:OtlpEndpoint"];
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(endpoint);
                    options.Headers = builder.Configuration["Observability:OpenTelemetry:Headers"];
                });
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation();

            var endpoint = builder.Configuration["Observability:OpenTelemetry:OtlpEndpoint"];
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                metrics.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(endpoint);
                    options.Headers = builder.Configuration["Observability:OpenTelemetry:Headers"];
                });
            }
        });
}

var forwardedHeadersSecurity = builder.Configuration
    .GetSection("ForwardedHeaders")
    .Get<ForwardedHeadersSecurityOptions>() ?? new ForwardedHeadersSecurityOptions();
var testHooks = builder.Configuration
    .GetSection("TestHooks")
    .Get<TestHooksOptions>() ?? new TestHooksOptions();
if (forwardedHeadersSecurity.Enabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = Math.Max(1, forwardedHeadersSecurity.ForwardLimit);
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var proxy in forwardedHeadersSecurity.KnownProxies)
        {
            if (IPAddress.TryParse(proxy, out var address))
                options.KnownProxies.Add(address);
        }

        foreach (var network in forwardedHeadersSecurity.KnownNetworks)
        {
            if (System.Net.IPNetwork.TryParse(network, out var parsedNetwork))
                options.KnownIPNetworks.Add(parsedNetwork);
        }
    });
}

// ═══════════════════════════════════════════════════
// 🟡 HANGFIRE — Background Job Processing
// ═══════════════════════════════════════════════════
if (!isAutomatedTestEnvironment)
{
    var enableBackgroundJobs = builder.Configuration.GetValue("Security:EnableRecurringJobs", true);

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

    if (enableBackgroundJobs)
        builder.Services.AddHangfireServer();
}

// Đăng ký Background Job classes vào DI
builder.Services.AddScoped<EmailBackgroundJob>();
builder.Services.AddScoped<ContractExpirationJob>();
builder.Services.AddScoped<MaintenanceAlertJob>();
builder.Services.AddScoped<DepositRefundJob>();
builder.Services.AddScoped<ExpireBookingHoldsJob>();
builder.Services.AddScoped<RefundPayoutPollingJob>();
builder.Services.AddScoped<CleanupUnverifiedAccountsJob>();

var authenticationBuilder = builder.Services.AddAuthentication("Cookies")
    .AddCookie("External", options =>
    {
        options.Cookie.Name = "CarRental.ExternalAuth";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = allowLocalHttpCookies
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    })
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
        options.Cookie.SecurePolicy = allowLocalHttpCookies
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Name = "CarRental.Auth";
    });

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authenticationBuilder.AddGoogle("Google", options =>
    {
        options.SignInScheme = "External";
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
    });
}

var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
if (!string.IsNullOrWhiteSpace(microsoftClientId) && !string.IsNullOrWhiteSpace(microsoftClientSecret))
{
    authenticationBuilder.AddMicrosoftAccount("Microsoft", options =>
    {
        options.SignInScheme = "External";
        options.ClientId = microsoftClientId;
        options.ClientSecret = microsoftClientSecret;
        options.CallbackPath = "/signin-microsoft";
    });
}

// ═══════════════════════════════════════════════════
// 🔐 AREA AUTHORIZATION — Phân quyền theo Area
// Handler xử lý resource-based authorization cho Areas
// ═══════════════════════════════════════════════════
builder.Services.AddScoped<IAuthorizationHandler, AreaAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PortalPolicyNames.RequireAdmin, policy =>
        policy.RequireRole(UserRoleConstants.Admin));
    options.AddPolicy(PortalPolicyNames.RequireManager, policy =>
        policy.RequireRole(UserRoleConstants.Manager));
    options.AddPolicy(PortalPolicyNames.RequireStaff, policy =>
        policy.RequireRole(UserRoleConstants.Staff));
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
    options.Cookie.SecurePolicy = allowLocalHttpCookies
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ═══════════════════════════════════════════════════
// 🚦 REQUEST SIZE LIMIT — Giới hạn upload 10MB max
// ═══════════════════════════════════════════════════
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        if (builder.Environment.IsDevelopment() && allowedCorsOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy.WithOrigins(allowedCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();
AuditLogServiceAccessor.ScopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

var supportedCultureNames = app.Configuration.GetSection("Localization:SupportedCultures").Get<string[]>() ?? new[] { "vi-VN", "en-US" };
var supportedCultures = supportedCultureNames.Select(CultureInfo.GetCultureInfo).ToList();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(app.Configuration["Localization:DefaultCulture"] ?? "vi-VN"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

if (forwardedHeadersSecurity.Enabled)
    app.UseForwardedHeaders();
app.UseResponseCompression();

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
            app.Configuration.GetValue<bool>("Security:SeedReferenceDataOnStartup"))
        {
            if (!context.Users.Any())
                DbSeeder.Seed(context);
            else
                BlogMockDataSeeder.Seed(context);

            await QaAdminSeeder.SeedFromEnvironmentAsync(context, app.Environment);
            await BusinessReferenceDataSeeder.SeedAsync(context);
            await OwnerCarReferenceDataSeeder.SeedAsync(context);
            await InternalPortalDemoSeeder.SeedAsync(
                context,
                services.GetRequiredService<IPasswordHashService>());
        }
    }
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (!context.Database.GetPendingMigrations().Contains("20260514092145_AddBlogModule"))
        BlogMockDataSeeder.Seed(context);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Playwright"))
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
    var scriptSrc = app.Environment.IsProduction()
        ? "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com https://code.jquery.com https://www.google.com https://www.gstatic.com; "
        : "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com https://code.jquery.com https://www.google.com https://www.gstatic.com; ";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        scriptSrc +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com https://fonts.googleapis.com; " +
        "img-src 'self' data: blob: https:; " +
        "font-src 'self' data: https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.gstatic.com; " +
        "connect-src 'self' https://nominatim.openstreetmap.org https://www.google.com https://recaptcha.google.com; " +
        "frame-src https://www.google.com https://recaptcha.google.com; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; upgrade-insecure-requests";
    if (app.Environment.IsProduction())
    {
        context.Response.Headers["Content-Security-Policy-Report-Only"] =
            "default-src 'self'; " +
            "script-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com https://code.jquery.com https://www.google.com https://www.gstatic.com; " +
            "style-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com https://fonts.googleapis.com; " +
            "img-src 'self' data: blob: https:; " +
            "font-src 'self' data: https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.gstatic.com; " +
            "connect-src 'self' https://nominatim.openstreetmap.org https://www.google.com https://recaptcha.google.com; " +
            "frame-src https://www.google.com https://recaptcha.google.com; " +
            "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; upgrade-insecure-requests";
    }
    await next();
});

// Bật HTTPS redirection trong Production
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Playwright"))
{
    app.UseHttpsRedirection();
}

// Tắt ServeUnknownFileTypes để bảo mật
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (!app.Environment.IsProduction())
            return;

        var path = context.Context.Request.Path.Value ?? string.Empty;
        if (path.EndsWith("/sw.js", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/service-worker.js", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".webmanifest", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.Context.Response.Headers["Pragma"] = "no-cache";
            context.Context.Response.Headers["Expires"] = "0";
            return;
        }

        if (path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers["Cache-Control"] = "private, max-age=600";
            return;
        }

        context.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
    }
});
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

app.UseRouting();
app.UseCors();

var playwrightResetCoordinator = new PlaywrightResetCoordinator();
if (app.Environment.IsEnvironment("Playwright"))
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        if (playwrightResetCoordinator.IsActive &&
            !path.StartsWithSegments("/__test/reset") &&
            !path.StartsWithSegments("/live"))
        {
            while (playwrightResetCoordinator.IsActive && !context.RequestAborted.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromMilliseconds(100), context.RequestAborted);
        }

        await next();
    });
}

// ═══════════════════════════════════════════════
// 🚦 RATE LIMITING — Chống brute force & API abuse
// ═══════════════════════════════════════════════
app.UseRateLimiting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else if (app.Configuration.GetValue("ApiDocs:Enabled", false))
{
    if (app.Configuration.GetValue("ApiDocs:RequireAuth", true))
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/swagger") &&
                !(context.User.Identity?.IsAuthenticated == true &&
                  (context.User.IsInRole(UserRoleConstants.Admin) || context.User.IsInRole(UserRoleConstants.Manager))))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next();
        });
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/Partner", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/Driver", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/OwnerCars");
        return;
    }

    if (path.StartsWith("/Admin/Driver", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/Admin/PartnerContract", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/Admin/DigitalContract", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/Admin/Dashboard");
        return;
    }

    await next();
});

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

if (app.Environment.IsEnvironment("Playwright"))
{
    bool HasPlaywrightSecret(HttpContext httpContext, IConfiguration configuration)
    {
        var expectedSecret = configuration["Playwright:ResetSecret"] ?? "local-playwright-reset";
        var providedSecret = httpContext.Request.Headers["X-Playwright-Reset-Secret"].ToString();
        return string.Equals(expectedSecret, providedSecret, StringComparison.Ordinal);
    }

    async Task<(ApplicationDbContext? Db, IResult? Error)> OpenPlaywrightDbAsync(IServiceProvider services)
    {
        var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connectionString = db.Database.GetDbConnection().ConnectionString;
        if (!connectionString.Contains("CarRentalSystem_Playwright", StringComparison.OrdinalIgnoreCase))
        {
            await scope.DisposeAsync();
            return (null, Results.Problem(
                "Refusing to use a non-Playwright database. The connection string must target CarRentalSystem_Playwright.",
                statusCode: StatusCodes.Status409Conflict));
        }

        return (db, null);
    }

    object BuildSandboxStatus(IConfiguration configuration, ApplicationDbContext db)
    {
        var missing = new List<string>();
        var usePayOS = configuration.GetValue<bool>("Playwright:UsePayOSSandbox");
        var useFpt = configuration.GetValue<bool>("Playwright:UseFptSandbox");
        var publicBaseUrl = configuration["App:PublicBaseUrl"] ?? "";
        var connectionString = db.Database.GetDbConnection().ConnectionString;
        var isPlaywrightDatabase = connectionString.Contains("CarRentalSystem_Playwright", StringComparison.OrdinalIgnoreCase);

        if (!isPlaywrightDatabase)
            missing.Add("ConnectionStrings__DefaultConnection must target CarRentalSystem_Playwright");

        if (usePayOS)
        {
            if (string.IsNullOrWhiteSpace(configuration["PayOS:ClientId"])) missing.Add("PayOS__ClientId");
            if (string.IsNullOrWhiteSpace(configuration["PayOS:ApiKey"])) missing.Add("PayOS__ApiKey");
            if (string.IsNullOrWhiteSpace(configuration["PayOS:ChecksumKey"])) missing.Add("PayOS__ChecksumKey");
            if (string.IsNullOrWhiteSpace(publicBaseUrl) || !Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                missing.Add("App__PublicBaseUrl HTTPS tunnel");
        }

        if (useFpt && string.IsNullOrWhiteSpace(configuration["FptAi:ApiKey"]))
            missing.Add("FptAi__ApiKey");

        return new
        {
            ready = missing.Count == 0,
            environment = app.Environment.EnvironmentName,
            isPlaywrightDatabase,
            usePayOSSandbox = usePayOS,
            useFptSandbox = useFpt,
            publicBaseUrl,
            missing,
            payOS = new
            {
                configured = !string.IsNullOrWhiteSpace(configuration["PayOS:ClientId"]) &&
                             !string.IsNullOrWhiteSpace(configuration["PayOS:ApiKey"]) &&
                             !string.IsNullOrWhiteSpace(configuration["PayOS:ChecksumKey"])
            },
            fptAi = new
            {
                configured = !string.IsNullOrWhiteSpace(configuration["FptAi:ApiKey"])
            },
            maps = new
            {
                routing = configuration["Maps:RoutingBaseUrl"] ?? "https://router.project-osrm.org",
                geocoding = configuration["Maps:GeocodingBaseUrl"] ?? "https://nominatim.openstreetmap.org"
            }
        };
    }

    async Task<PlaywrightScenarioSummary> CreatePayOSVendorCheckoutAsync(IServiceProvider services, IConfiguration configuration, PlaywrightScenarioSummary scenario)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var payOSPaymentService = scope.ServiceProvider.GetRequiredService<IPayOSPaymentService>();
        var baseUrl = configuration["App:PublicBaseUrl"] ?? "";
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("App__PublicBaseUrl is required for PayOS vendor sandbox checkout.");

        var car = await db.Cars
            .Include(c => c.CarBrand)
            .Include(c => c.ServiceType)
            .FirstAsync(c => c.CarID == scenario.CarId);
        var customer = await db.Users.FirstAsync(u => u.Email == "customer@playwright.local");
        var location = await db.Locations.FirstAsync();
        var serviceTypeId = car.ServiceTypeID ?? await db.ServiceTypes
            .Where(s => s.Code == ServiceTypeConstants.SelfDrive)
            .Select(s => s.ServiceTypeID)
            .FirstAsync();
        var licenseId = await db.CustomerDrivingLicenses
            .Where(l => l.UserID == customer.UserID && l.IsActive)
            .Select(l => (int?)l.CustomerDrivingLicenseID)
            .FirstOrDefaultAsync();
        var termsId = await db.LegalDocumentVersions
            .Where(d => d.Code == LegalDocumentCodes.TermsOfUse && d.IsActive)
            .Select(d => (int?)d.LegalDocumentVersionID)
            .FirstOrDefaultAsync();
        var privacyId = await db.LegalDocumentVersions
            .Where(d => d.Code == LegalDocumentCodes.PrivacyNotice && d.IsActive)
            .Select(d => (int?)d.LegalDocumentVersionID)
            .FirstOrDefaultAsync();
        var startDate = DateTime.UtcNow.Date.AddDays(180).AddHours(9);
        var endDate = startDate.AddDays(2);

        var quote = new CheckoutQuoteViewModel
        {
            UserID = customer.UserID,
            CarID = car.CarID,
            ServiceTypeID = serviceTypeId,
            ServiceTypeCode = ServiceTypeConstants.SelfDrive,
            CustomerDrivingLicenseID = licenseId,
            CarName = car.CarName,
            CarBrand = car.CarBrand?.Name ?? "Playwright",
            LicensePlate = car.LicensePlate,
            CustomerName = customer.Name,
            CustomerEmail = customer.Email,
            CustomerPhone = customer.Phone,
            StartDate = startDate,
            EndDate = endDate,
            TotalHours = 48,
            RentalDays = 2,
            PickupLocationID = location.LocationID,
            ReturnLocationID = location.LocationID,
            PickupLocationName = location.Name,
            BaseRentalAmount = 1600000m,
            ServiceFee = 0m,
            DeliveryFee = scenario.DeliveryFee,
            DeliveryAddress = "Playwright Customer Dropoff, Nguyen Hue, Quan 1, TP Ho Chi Minh",
            DeliveryMode = DeliveryModeConstants.OwnerDelivery,
            DeliveryLatitude = scenario.DeliveryLatitude,
            DeliveryLongitude = scenario.DeliveryLongitude,
            DeliveryDistanceKm = scenario.DeliveryDistanceKm,
            DeliveryEtaMinutes = 70,
            DeliveryQuotedAt = DateTime.UtcNow,
            InsuranceFee = 0m,
            InsuranceTier = "Basic",
            TotalBeforeDiscount = 1600000m + scenario.DeliveryFee,
            TotalPrice = 1600000m + scenario.DeliveryFee,
            HoldingFee = 300000m,
            RemainingRentalAmount = 1300000m + scenario.DeliveryFee,
            CollateralAmount = 300000m,
            TermsDocumentVersionID = termsId,
            PrivacyNoticeVersionID = privacyId,
            SelfDriveAgreementAcceptedAt = DateTime.UtcNow,
            SelfDriveAgreementIpAddress = "127.0.0.1",
            SelfDriveAgreementUserAgent = "Playwright vendor sandbox"
        };

        var holdResult = await reservationService.CreateHoldAsync(customer.UserID, car.CarID, startDate, endDate, quote);
        if (!holdResult.IsSuccess || holdResult.Hold == null)
            throw new InvalidOperationException(holdResult.ErrorMessage ?? "Could not create Playwright PayOS hold.");

        var payOSResult = await payOSPaymentService.CreateHoldPaymentAsync(holdResult.Hold, quote, baseUrl);
        if (!payOSResult.IsSuccess || payOSResult.Transaction == null)
            throw new InvalidOperationException(payOSResult.ErrorMessage ?? "Could not create PayOS sandbox checkout.");

        scenario.BookingHoldId = holdResult.Hold.BookingHoldID;
        scenario.OrderCode = payOSResult.Transaction.OrderCode;
        scenario.CheckoutUrl = payOSResult.CheckoutUrl;
        return scenario;
    }

    async Task ClearPlaywrightDataAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        const string sql = """
DECLARE @sql nvarchar(max);

SET @sql = N'';
SELECT @sql = @sql +
    N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N' NOCHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables
WHERE is_ms_shipped = 0
  AND name <> N'__EFMigrationsHistory';
IF LEN(@sql) > 0 EXEC sp_executesql @sql;

SET @sql = N'';
SELECT @sql = @sql +
    N'DELETE FROM ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' + CHAR(10)
FROM sys.tables
WHERE is_ms_shipped = 0
  AND name <> N'__EFMigrationsHistory';
IF LEN(@sql) > 0 EXEC sp_executesql @sql;

SET @sql = N'';
SELECT @sql = @sql +
    N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N' WITH CHECK CHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables
WHERE is_ms_shipped = 0
  AND name <> N'__EFMigrationsHistory';
IF LEN(@sql) > 0 EXEC sp_executesql @sql;
""";

        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    async Task<PlaywrightSeedSummary> ResetPlaywrightDatabaseAsync(
        ApplicationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        db.Database.SetCommandTimeout(180);
        var recreate = false;

        try
        {
            recreate = !await db.Database.CanConnectAsync(cancellationToken) ||
                       (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any();
        }
        catch
        {
            recreate = true;
        }

        if (recreate)
        {
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync(cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            try
            {
                await ClearPlaywrightDataAsync(db, cancellationToken);
            }
            catch (Exception clearEx)
            {
                logger.LogWarning(clearEx, "Fast Playwright data clear failed; retrying once before recreating the database.");
                SqlConnection.ClearAllPools();
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

                try
                {
                    await ClearPlaywrightDataAsync(db, cancellationToken);
                }
                catch (Exception retryEx)
                {
                    logger.LogWarning(retryEx, "Fast Playwright data clear retry failed; recreating the database.");
                    SqlConnection.ClearAllPools();
                    await db.Database.EnsureDeletedAsync(cancellationToken);
                    await db.Database.MigrateAsync(cancellationToken);
                }
            }
        }

        return await PlaywrightTestDataSeeder.SeedAsync(db);
    }

    app.MapPost("/__test/reset", async Task<IResult> (
        HttpContext httpContext,
        IServiceProvider services,
        IConfiguration configuration,
        ILoggerFactory loggerFactory) =>
    {
        if (!HasPlaywrightSecret(httpContext, configuration))
            return Results.Unauthorized();

        var logger = loggerFactory.CreateLogger("PlaywrightReset");
        playwrightResetCoordinator.MarkActive();
        await playwrightResetCoordinator.Lock.WaitAsync(httpContext.RequestAborted);
        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var connectionString = db.Database.GetDbConnection().ConnectionString;
            if (!connectionString.Contains("CarRentalSystem_Playwright", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    "Refusing to reset a non-Playwright database. The connection string must target CarRentalSystem_Playwright.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            SqlConnection.ClearAllPools();
            var summary = await ResetPlaywrightDatabaseAsync(db, logger, httpContext.RequestAborted);
            SqlConnection.ClearAllPools();
            return Results.Json(summary);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Playwright database reset failed.");
            return Results.Problem(
                $"Playwright database reset failed: {ex.Message}",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            playwrightResetCoordinator.Lock.Release();
            playwrightResetCoordinator.MarkInactive();
        }
    }).AllowAnonymous();

    app.MapGet("/__test/sandbox/status", async Task<IResult> (
        HttpContext httpContext,
        IServiceProvider services,
        IConfiguration configuration) =>
    {
        if (!HasPlaywrightSecret(httpContext, configuration))
            return Results.Unauthorized();

        var opened = await OpenPlaywrightDbAsync(services);
        if (opened.Error != null) return opened.Error;
        await using var db = opened.Db!;
        return Results.Json(BuildSandboxStatus(configuration, db));
    }).AllowAnonymous();

    app.MapPost("/__test/seed/scenario", async Task<IResult> (
        PlaywrightScenarioRequest request,
        HttpContext httpContext,
        IServiceProvider services,
        IConfiguration configuration) =>
    {
        if (!HasPlaywrightSecret(httpContext, configuration))
            return Results.Unauthorized();

        var opened = await OpenPlaywrightDbAsync(services);
        if (opened.Error != null) return opened.Error;
        await using var db = opened.Db!;

        try
        {
            var scenario = await PlaywrightTestDataSeeder.SeedScenarioAsync(db, request.Name);
            if (string.Equals(request.Name, "payos-vendor-checkout", StringComparison.OrdinalIgnoreCase))
                scenario = await CreatePayOSVendorCheckoutAsync(services, configuration, scenario);

            return Results.Json(scenario);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status424FailedDependency);
        }
    }).AllowAnonymous();

    app.MapGet("/__test/payments/wait-paid", async Task<IResult> (
        long orderCode,
        int? timeoutSeconds,
        HttpContext httpContext,
        IServiceProvider services,
        IConfiguration configuration) =>
    {
        if (!HasPlaywrightSecret(httpContext, configuration))
            return Results.Unauthorized();

        if (orderCode <= 0)
            return Results.BadRequest(new { message = "orderCode is required." });

        var opened = await OpenPlaywrightDbAsync(services);
        if (opened.Error != null) return opened.Error;
        await using var db = opened.Db!;

        var timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds ?? 120, 5, 300));
        var deadline = DateTime.UtcNow.Add(timeout);
        PayOSTransaction? transaction = null;

        while (DateTime.UtcNow <= deadline && !httpContext.RequestAborted.IsCancellationRequested)
        {
            transaction = await db.PayOSTransactions
                .Include(t => t.Payment)
                .Include(t => t.Booking)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode);

            if (transaction == null)
                return Results.NotFound(new { message = "PayOS transaction not found.", orderCode });

            var paid = string.Equals(transaction.Status, PaymentStatusConstants.Paid, StringComparison.OrdinalIgnoreCase) ||
                       transaction.PaidAt.HasValue ||
                       string.Equals(transaction.Payment?.PaymentStatus, PaymentStatusConstants.Paid, StringComparison.OrdinalIgnoreCase);
            if (paid)
            {
                return Results.Json(new
                {
                    paid = true,
                    orderCode,
                    transaction.PayOSTransactionID,
                    transaction.BookingID,
                    transaction.PaymentID,
                    transaction.Status,
                    transaction.PaidAt
                });
            }

            await Task.Delay(TimeSpan.FromSeconds(2), httpContext.RequestAborted);
        }

        return Results.Json(new
        {
            paid = false,
            orderCode,
            status = transaction?.Status,
            message = "Timed out waiting for a real PayOS sandbox webhook."
        }, statusCode: StatusCodes.Status202Accepted);
    }).AllowAnonymous();
}

if ((app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Playwright")) && testHooks.Enabled)
{
    app.MapPost("/__test/otp/email", async Task<IResult> (
        TestOtpRequest request,
        HttpContext httpContext,
        ApplicationDbContext db) =>
    {
        httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        httpContext.Response.Headers.Pragma = "no-cache";

        var providedSecret = httpContext.Request.Headers["X-Test-Hook-Secret"].ToString();
        if (string.IsNullOrWhiteSpace(testHooks.Secret) ||
            !string.Equals(testHooks.Secret, providedSecret, StringComparison.Ordinal))
            return Results.Unauthorized();

        var connectionString = db.Database.GetDbConnection().ConnectionString;
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (!testHooks.AllowedDatabases.Any(database =>
                !string.IsNullOrWhiteSpace(database) &&
                string.Equals(database, databaseName, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.Problem(
                "Refusing to expose test OTP from a database outside TestHooks:AllowedDatabases.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return Results.BadRequest(new { message = "email is required" });

        var otp = await db.Users.AsNoTracking()
            .Where(user => user.Email.ToLower() == email)
            .Select(user => new { user.EmailVerificationToken, user.EmailVerificationTokenExpiry })
            .FirstOrDefaultAsync();
        if (otp == null || string.IsNullOrWhiteSpace(otp.EmailVerificationToken))
            return Results.NotFound(new { message = "OTP not found" });
        if (otp.EmailVerificationTokenExpiry <= DateTime.UtcNow)
            return Results.Json(new { message = "OTP expired" }, statusCode: StatusCodes.Status410Gone);

        return Results.Json(new
        {
            otp = otp.EmailVerificationToken,
            expiresAtUtc = otp.EmailVerificationTokenExpiry
        });
    }).AllowAnonymous();
}

// ═══════════════════════════════════════════════════
// 🟡 HANGFIRE DASHBOARD — chỉ Admin mới truy cập được
// ═══════════════════════════════════════════════════
if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsEnvironment("Playwright"))
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
    !app.Environment.IsEnvironment("Playwright") &&
    app.Configuration.GetValue("Security:EnableRecurringJobs", true))
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

    RecurringJob.AddOrUpdate<RefundPayoutPollingJob>(
        "refund-payout-polling",
        job => job.Execute(),
        "*/10 * * * *");

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
if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue<bool>("Security:SeedReferenceDataOnStartup"))
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

        await CarRentalSystem.Data.BusinessReferenceDataSeeder.SeedAsync(db);
        await CarRentalSystem.Data.OwnerCarReferenceDataSeeder.SeedAsync(db);
    }
}

// ═══════════════════════════════════════════════════
// 🏥 HEALTH CHECK ENDPOINT — /health
// ═══════════════════════════════════════════════════
app.MapHealthChecks("/health");

app.MapGet("/live", () => Results.Json(new
{
    status = "Healthy",
    checks = new[] { new { name = "process", status = "Healthy", description = "Application process is running." } },
    durationMs = 0,
    timestampUtc = DateTime.UtcNow
})).AllowAnonymous();

app.MapGet("/ready", async (IServiceProvider services, IConfiguration configuration, IWebHostEnvironment environment) =>
{
    var started = System.Diagnostics.Stopwatch.StartNew();
    var checks = new List<object>();
    var healthy = true;

    await using (var scope = services.CreateAsyncScope())
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var canConnect = await db.Database.CanConnectAsync();
            healthy &= canConnect;
            checks.Add(new { name = "database", status = canConnect ? "Healthy" : "Unhealthy", description = canConnect ? "Database connection is available." : "Database connection failed." });
        }
        catch (Exception ex)
        {
            healthy = false;
            checks.Add(new { name = "database", status = "Unhealthy", description = ex.GetType().Name });
        }
    }

    var dataProtectionPath = configuration["DataProtection:KeysPath"];
    var dataProtectionReady = string.IsNullOrWhiteSpace(dataProtectionPath) || Directory.Exists(dataProtectionPath);
    healthy &= dataProtectionReady;
    checks.Add(new { name = "data_protection_keys", status = dataProtectionReady ? "Healthy" : "Unhealthy", description = dataProtectionReady ? "Keys path is available or not required." : "Configured keys path does not exist." });

    var useMockPayOS = environment.IsEnvironment("Playwright") &&
                       !configuration.GetValue<bool>("Playwright:UsePayOSSandbox");
    var payosReady = useMockPayOS || (!string.IsNullOrWhiteSpace(configuration["PayOS:ClientId"]) &&
                     !string.IsNullOrWhiteSpace(configuration["PayOS:ApiKey"]) &&
                     !string.IsNullOrWhiteSpace(configuration["PayOS:ChecksumKey"]));
    healthy &= payosReady;
    checks.Add(new
    {
        name = "payos_config",
        status = payosReady ? "Healthy" : "Unhealthy",
        description = useMockPayOS
            ? "PayOS is intentionally replaced by the Playwright mock gateway."
            : payosReady ? "PayOS config is present." : "PayOS config is incomplete."
    });

    var useMockFpt = environment.IsEnvironment("Playwright") &&
                     !configuration.GetValue<bool>("Playwright:UseFptSandbox");
    var fptReady = useMockFpt || !string.IsNullOrWhiteSpace(configuration["FptAi:ApiKey"]);
    healthy &= fptReady;
    checks.Add(new
    {
        name = "fpt_ekyc_config",
        status = fptReady ? "Healthy" : "Unhealthy",
        description = useMockFpt
            ? "FPT eKYC is intentionally replaced by the Playwright mock service."
            : fptReady ? "FPT eKYC config is present." : "FPT eKYC config is missing."
    });

    if (!string.IsNullOrWhiteSpace(configuration["Redis:ConnectionString"]))
        checks.Add(new { name = "redis_config", status = "Healthy", description = "Redis connection string is configured." });

    if (configuration.GetValue("Security:EnableRecurringJobs", true))
        checks.Add(new { name = "hangfire_config", status = "Healthy", description = "Recurring jobs are enabled; storage follows application database configuration." });

    started.Stop();
    var body = new
    {
        status = healthy ? "Healthy" : "Unhealthy",
        checks,
        durationMs = started.ElapsedMilliseconds,
        timestampUtc = DateTime.UtcNow
    };

    return healthy
        ? Results.Json(body)
        : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<TrackingHub>("/hubs/tracking");

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
public sealed class PlaywrightScenarioRequest
{
    public string Name { get; set; } = string.Empty;
}

public sealed class TestOtpRequest
{
    public string? Email { get; set; }
}

public sealed class PlaywrightResetCoordinator
{
    private int _active;

    public SemaphoreSlim Lock { get; } = new(1, 1);

    public bool IsActive => Volatile.Read(ref _active) > 0;

    public void MarkActive() => Interlocked.Increment(ref _active);

    public void MarkInactive() => Interlocked.Decrement(ref _active);
}

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
