using Appointment.Data;
using Appointment.Infrastructure;
using Appointment.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Connection Strings from appsettings.json
var postgresConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=AppointmentDb;Username=postgres;Password=postgres";

var sqliteConnectionString = builder.Configuration.GetConnectionString("SqliteConnection") 
    ?? "Data Source=Appointment.db";

// Register DbContext (Prefers PostgreSQL, defaults to SQLite if YOUR_PASSWORD placeholder is unchanged)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (postgresConnectionString.Contains("D@ng0799192226", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(sqliteConnectionString);
    }
    else
    {
        options.UseNpgsql(postgresConnectionString);
    }
});

// Authentication & Cookie Configuration
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        // US-43: tăng cường bảo mật cookie đăng nhập
        options.Cookie.Name = "AppointX.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        // US-43: ghi nhật ký mọi lần truy cập bị từ chối (sai vai trò)
        options.Events.OnRedirectToAccessDenied = async context =>
        {
            var audit = context.HttpContext.RequestServices.GetRequiredService<IAuditService>();
            await audit.LogAsync(AuditActions.AccessDenied,
                $"Bị từ chối truy cập {context.Request.Method} {context.Request.Path} (không đủ quyền).",
                success: false, severity: AuditLog.SeverityWarning);

            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
        };
    });

builder.Services.AddAuthorization();

// Cấu hình & dịch vụ bổ sung: email (US-26), quy tắc đặt/hủy lịch (US-19, US-37), thanh toán (US-28)
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<BookingRulesOptions>(builder.Configuration.GetSection("BookingRules"));
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection("Payment"));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<CheckInOptions>(builder.Configuration.GetSection("CheckIn"));

// US-43: nhật ký kiểm toán & bảo mật (ghi tự động người dùng / IP / trình duyệt từ HTTP request)
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuditService, AuditService>();

// US-32: mã QR check-in có chữ ký, tạo ảnh QR phía máy chủ và xuất phiếu hẹn PDF
builder.Services.AddSingleton<CheckInCodeService>();
builder.Services.AddSingleton<QrCodeService>();
builder.Services.AddSingleton<AppointmentSlipPdfService>();

builder.Services.AddHttpClient("GeminiApi");
builder.Services.AddScoped<IAiService, GeminiAiService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddSingleton<IPaymentGateway, SandboxPaymentGateway>();

// US-37: tác vụ nền tự động hủy lịch hẹn chưa xác nhận quá hạn
builder.Services.AddHostedService<AppointmentAutoCancelService>();

// US-AI-04: tác vụ nền tự động soạn thảo & gửi email nhắc lịch hẹn bằng AI
builder.Services.AddHostedService<AppointmentReminderService>();

var app = builder.Build();

// Auto initialize database tables & seed data
try
{
    DbInitializer.Initialize(app.Services);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning($"Lỗi kết nối PostgreSQL ({ex.Message}). Tự động fallback sang cơ sở dữ liệu SQLite...");

    // Fallback to SQLite if PostgreSQL connection fails at runtime
    using (var scope = app.Services.CreateScope())
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite(sqliteConnectionString);
        using var sqliteContext = new AppDbContext(optionsBuilder.Options);
        DbInitializer.EnsureDatabaseSchemaCreated(sqliteContext);
        DbInitializer.SeedData(sqliteContext);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// US-43: các HTTP header bảo mật cơ bản (chống clickjacking, MIME sniffing, lộ referrer)
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=()"; // camera dùng cho quét QR check-in
    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
