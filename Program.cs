using Appointment.Data;
using Appointment.Infrastructure;
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
    });

builder.Services.AddAuthorization();

// Cấu hình & dịch vụ bổ sung: email (US-26), quy tắc đặt/hủy lịch (US-19, US-37), thanh toán (US-28)
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<BookingRulesOptions>(builder.Configuration.GetSection("BookingRules"));
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection("Payment"));

builder.Services.AddHttpClient("GeminiApi");
builder.Services.AddScoped<IAiService, GeminiAiService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddSingleton<IPaymentGateway, SandboxPaymentGateway>();

// US-37: tác vụ nền tự động hủy lịch hẹn chưa xác nhận quá hạn
builder.Services.AddHostedService<AppointmentAutoCancelService>();

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
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
