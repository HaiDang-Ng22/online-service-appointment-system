using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Appointment.Data;
using Appointment.Models;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Infrastructure
{
    /// <summary>
    /// US-43: danh mục mã hành động ghi vào nhật ký kiểm toán. Phần trước dấu chấm chính là nhóm (Category).
    /// </summary>
    public static class AuditActions
    {
        // ----- Xác thực -----
        public const string LoginSuccess = "Auth.LoginSuccess";
        public const string LoginFailed = "Auth.LoginFailed";
        public const string LoginLocked = "Auth.LoginLocked";
        public const string LoginInactive = "Auth.LoginInactive";
        public const string Logout = "Auth.Logout";

        // ----- Tài khoản -----
        public const string Register = "Account.Register";
        public const string ProfileUpdated = "Account.ProfileUpdated";
        public const string PasswordChanged = "Account.PasswordChanged";
        public const string PasswordResetRequested = "Account.PasswordResetRequested";
        public const string PasswordResetCompleted = "Account.PasswordResetCompleted";
        public const string PasswordResetFailed = "Account.PasswordResetFailed";

        // ----- Lịch hẹn -----
        public const string AppointmentCreated = "Appointment.Created";
        public const string AppointmentWalkIn = "Appointment.WalkIn";
        public const string AppointmentCancelled = "Appointment.Cancelled";
        public const string AppointmentAutoCancelled = "Appointment.AutoCancelled";
        public const string AppointmentStatusChanged = "Appointment.StatusChanged";
        public const string AppointmentAssigned = "Appointment.Assigned";
        public const string AppointmentRecordUpdated = "Appointment.RecordUpdated";
        public const string AppointmentRescheduled = "Appointment.Rescheduled";
        public const string AppointmentCheckIn = "Appointment.CheckIn";
        public const string AppointmentCheckInFailed = "Appointment.CheckInFailed";
        public const string AppointmentSlipDownloaded = "Appointment.SlipDownloaded";

        // ----- Thanh toán -----
        public const string PaymentSucceeded = "Payment.Succeeded";
        public const string PaymentFailed = "Payment.Failed";
        public const string PaymentRefunded = "Payment.Refunded";

        // ----- Đánh giá -----
        public const string ReviewSaved = "Review.Saved";
        public const string ReviewDeleted = "Review.Deleted";

        // ----- Dịch vụ & cấu hình của Provider -----
        public const string ServiceCreated = "Service.Created";
        public const string ServiceUpdated = "Service.Updated";
        public const string WorkingHoursUpdated = "Service.WorkingHoursUpdated";
        public const string OffDayAdded = "Service.OffDayAdded";
        public const string OffDayDeleted = "Service.OffDayDeleted";

        // ----- Quản trị -----
        public const string UserCreated = "Admin.UserCreated";
        public const string UserLocked = "Admin.UserLocked";
        public const string UserUnlocked = "Admin.UserUnlocked";
        public const string CategoryCreated = "Admin.CategoryCreated";
        public const string CategoryUpdated = "Admin.CategoryUpdated";
        public const string CategoryDeleted = "Admin.CategoryDeleted";

        // ----- Bảo mật -----
        public const string AccessDenied = "Security.AccessDenied";

        // ----- Chính nhật ký / báo cáo -----
        public const string AuditExported = "Audit.Exported";
        public const string AuditVerified = "Audit.Verified";
        public const string StatsExported = "Report.StatsExported";

        /// <summary>Nhãn tiếng Việt hiển thị trên giao diện (đồng thời là danh sách để lọc).</summary>
        public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
        {
            [LoginSuccess] = "Đăng nhập thành công",
            [LoginFailed] = "Đăng nhập thất bại",
            [LoginLocked] = "Đăng nhập bị chặn (khóa tạm thời)",
            [LoginInactive] = "Đăng nhập vào tài khoản bị khóa",
            [Logout] = "Đăng xuất",
            [Register] = "Đăng ký tài khoản",
            [ProfileUpdated] = "Cập nhật hồ sơ",
            [PasswordChanged] = "Đổi mật khẩu",
            [PasswordResetRequested] = "Yêu cầu đặt lại mật khẩu",
            [PasswordResetCompleted] = "Đặt lại mật khẩu thành công",
            [PasswordResetFailed] = "Đặt lại mật khẩu thất bại",
            [AppointmentCreated] = "Đặt lịch hẹn",
            [AppointmentWalkIn] = "Đặt lịch tại quầy",
            [AppointmentCancelled] = "Hủy lịch hẹn",
            [AppointmentAutoCancelled] = "Hệ thống tự động hủy lịch",
            [AppointmentStatusChanged] = "Đổi trạng thái lịch hẹn",
            [AppointmentAssigned] = "Tiếp nhận lịch hẹn",
            [AppointmentRecordUpdated] = "Cập nhật hồ sơ / ghi chú lịch hẹn",
            [AppointmentRescheduled] = "Dời lịch khẩn cấp",
            [AppointmentCheckIn] = "Check-in QR",
            [AppointmentCheckInFailed] = "Check-in thất bại",
            [AppointmentSlipDownloaded] = "Tải phiếu hẹn PDF",
            [PaymentSucceeded] = "Thanh toán / đặt cọc thành công",
            [PaymentFailed] = "Thanh toán thất bại",
            [PaymentRefunded] = "Hoàn tiền",
            [ReviewSaved] = "Gửi / sửa đánh giá",
            [ReviewDeleted] = "Xóa đánh giá",
            [ServiceCreated] = "Tạo dịch vụ",
            [ServiceUpdated] = "Sửa dịch vụ",
            [WorkingHoursUpdated] = "Đổi giờ làm việc",
            [OffDayAdded] = "Thêm ngày nghỉ",
            [OffDayDeleted] = "Xóa ngày nghỉ",
            [UserCreated] = "Admin tạo tài khoản",
            [UserLocked] = "Admin khóa tài khoản",
            [UserUnlocked] = "Admin mở khóa tài khoản",
            [CategoryCreated] = "Thêm danh mục",
            [CategoryUpdated] = "Sửa danh mục",
            [CategoryDeleted] = "Xóa danh mục",
            [AccessDenied] = "Truy cập bị từ chối",
            [AuditExported] = "Xuất nhật ký kiểm toán",
            [AuditVerified] = "Kiểm tra toàn vẹn nhật ký",
            [StatsExported] = "Xuất báo cáo thống kê"
        };

        public static readonly IReadOnlyDictionary<string, string> CategoryLabels = new Dictionary<string, string>
        {
            ["Auth"] = "Xác thực",
            ["Account"] = "Tài khoản",
            ["Appointment"] = "Lịch hẹn",
            ["Payment"] = "Thanh toán",
            ["Review"] = "Đánh giá",
            ["Service"] = "Dịch vụ & cấu hình",
            ["Admin"] = "Quản trị",
            ["Security"] = "Bảo mật",
            ["Audit"] = "Nhật ký",
            ["Report"] = "Báo cáo",
            ["System"] = "Hệ thống"
        };

        public static string CategoryOf(string action)
        {
            int i = action.IndexOf('.');
            return i > 0 ? action.Substring(0, i) : "System";
        }

        public static string LabelOf(string action) => Labels.TryGetValue(action, out var l) ? l : action;
    }

    /// <summary>Người thực hiện hành động (dùng khi chưa có phiên đăng nhập, ví dụ lúc đăng nhập / tác vụ nền).</summary>
    public record AuditActor(string? UserId, string? Name, string? Email, string Role)
    {
        public static AuditActor Anonymous(string? email = null) => new AuditActor(null, null, email, "Anonymous");
        public static AuditActor SystemActor => new AuditActor(null, "Hệ thống", null, "System");
        public static AuditActor FromUser(User u) => new AuditActor(u.Id, u.FullName, u.Email, u.Role);
    }

    public class AuditVerifyResult
    {
        public bool IsValid { get; set; } = true;
        public long TotalChecked { get; set; }
        public long? FirstBrokenId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class AuditFailureInfo
    {
        public int Count { get; set; }
        public DateTime? LastFailureUtc { get; set; }
    }

    public interface IAuditService
    {
        /// <summary>
        /// Ghi một dòng nhật ký. KHÔNG BAO GIỜ ném lỗi ra ngoài (lỗi ghi log không được làm hỏng nghiệp vụ chính).
        /// Nếu không truyền <paramref name="actor"/> sẽ tự lấy người dùng, IP, trình duyệt từ HTTP request hiện tại.
        /// </summary>
        Task LogAsync(string action, string description, string? entityType = null, string? entityId = null,
            bool success = true, string? severity = null, AuditActor? actor = null);

        /// <summary>Số lần đăng nhập sai liên tiếp (kể từ lần đăng nhập thành công gần nhất) trong khoảng <paramref name="window"/>.</summary>
        Task<AuditFailureInfo> GetRecentLoginFailuresAsync(string email, TimeSpan window);

        /// <summary>Số lần đăng nhập sai từ một địa chỉ IP trong khoảng <paramref name="window"/>.</summary>
        Task<int> CountRecentLoginFailuresByIpAsync(string? ip, TimeSpan window);

        /// <summary>Kiểm tra toàn bộ chuỗi mã băm để phát hiện dòng nhật ký bị sửa / xóa trực tiếp trong cơ sở dữ liệu.</summary>
        Task<AuditVerifyResult> VerifyChainAsync();
    }

    public class AuditService : IAuditService
    {
        public const string Genesis = "GENESIS";

        // Khóa tuần tự hóa việc ghi để chuỗi mã băm không bị rẽ nhánh khi có nhiều request đồng thời.
        private static readonly SemaphoreSlim ChainLock = new SemaphoreSlim(1, 1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<AuditService> _logger;

        public AuditService(IServiceScopeFactory scopeFactory, IHttpContextAccessor http, ILogger<AuditService> logger)
        {
            _scopeFactory = scopeFactory;
            _http = http;
            _logger = logger;
        }

        public async Task LogAsync(string action, string description, string? entityType = null, string? entityId = null,
            bool success = true, string? severity = null, AuditActor? actor = null)
        {
            try
            {
                var http = _http.HttpContext;
                actor ??= ResolveActor(http);

                var nowUtc = DateTime.UtcNow;
                var entry = new AuditLog
                {
                    // Làm tròn mili-giây để mã băm khớp sau khi đi qua SQLite / PostgreSQL
                    CreatedAt = new DateTime(nowUtc.Ticks - (nowUtc.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc),
                    UserId = Clean(actor.UserId, 64),
                    UserName = Clean(actor.Name, 150),
                    UserEmail = Clean(actor.Email?.ToLowerInvariant(), 150),
                    UserRole = Clean(actor.Role, 20) ?? "Anonymous",
                    Action = Clean(action, 60) ?? "Unknown",
                    Category = AuditActions.CategoryOf(action),
                    EntityType = Clean(entityType, 40),
                    EntityId = Clean(entityId, 64),
                    Description = Clean(description, 1000) ?? string.Empty,
                    Success = success,
                    Severity = severity ?? (success ? AuditLog.SeverityInfo : AuditLog.SeverityWarning),
                    IpAddress = Clean(http?.Connection.RemoteIpAddress?.ToString(), 64),
                    UserAgent = Clean(http?.Request.Headers["User-Agent"].ToString(), 300),
                    RequestPath = http == null ? null : Clean($"{http.Request.Method} {http.Request.Path}", 300)
                };

                await ChainLock.WaitAsync();
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var prev = await db.AuditLogs.AsNoTracking()
                        .OrderByDescending(l => l.Id)
                        .Select(l => l.Hash)
                        .FirstOrDefaultAsync();

                    entry.PrevHash = string.IsNullOrEmpty(prev) ? Genesis : prev;
                    entry.Hash = ComputeHash(entry);

                    db.AuditLogs.Add(entry);
                    await db.SaveChangesAsync();
                }
                finally
                {
                    ChainLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không ghi được nhật ký kiểm toán cho hành động {Action}.", action);
            }
        }

        public async Task<AuditFailureInfo> GetRecentLoginFailuresAsync(string email, TimeSpan window)
        {
            var info = new AuditFailureInfo();
            try
            {
                email = (email ?? string.Empty).Trim().ToLowerInvariant();
                var since = DateTime.UtcNow - window;

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Chỉ tính các lần sai SAU lần đăng nhập thành công gần nhất
                var lastSuccess = await db.AuditLogs.AsNoTracking()
                    .Where(l => l.Action == AuditActions.LoginSuccess && l.UserEmail == email && l.CreatedAt >= since)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => (DateTime?)l.CreatedAt)
                    .FirstOrDefaultAsync();

                var from = lastSuccess.HasValue && lastSuccess.Value > since ? lastSuccess.Value : since;

                var failures = await db.AuditLogs.AsNoTracking()
                    .Where(l => l.Action == AuditActions.LoginFailed && l.UserEmail == email && l.CreatedAt > from)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => l.CreatedAt)
                    .ToListAsync();

                info.Count = failures.Count;
                info.LastFailureUtc = failures.Count > 0 ? DateTime.SpecifyKind(failures[0], DateTimeKind.Utc) : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không đọc được số lần đăng nhập sai.");
            }
            return info;
        }

        public async Task<int> CountRecentLoginFailuresByIpAsync(string? ip, TimeSpan window)
        {
            if (string.IsNullOrWhiteSpace(ip)) return 0;
            try
            {
                var since = DateTime.UtcNow - window;
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                return await db.AuditLogs.AsNoTracking()
                    .CountAsync(l => l.Action == AuditActions.LoginFailed && l.IpAddress == ip && l.CreatedAt >= since);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không đọc được số lần đăng nhập sai theo IP.");
                return 0;
            }
        }

        public async Task<AuditVerifyResult> VerifyChainAsync()
        {
            var result = new AuditVerifyResult();

            // Giữ khóa để không có dòng mới chen vào lúc đang kiểm tra
            await ChainLock.WaitAsync();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                string expectedPrev = Genesis;
                long lastId = 0;

                while (true)
                {
                    var batch = await db.AuditLogs.AsNoTracking()
                        .Where(l => l.Id > lastId)
                        .OrderBy(l => l.Id)
                        .Take(500)
                        .ToListAsync();

                    if (batch.Count == 0) break;

                    foreach (var row in batch)
                    {
                        result.TotalChecked++;
                        lastId = row.Id;

                        if (!string.Equals(row.PrevHash, expectedPrev, StringComparison.Ordinal))
                        {
                            result.IsValid = false;
                            result.FirstBrokenId = row.Id;
                            result.Message = $"Chuỗi bị đứt tại dòng #{row.Id}: có dòng nhật ký phía trước đã bị xóa hoặc thay thế.";
                            return result;
                        }

                        if (!string.Equals(row.Hash, ComputeHash(row), StringComparison.Ordinal))
                        {
                            result.IsValid = false;
                            result.FirstBrokenId = row.Id;
                            result.Message = $"Nội dung dòng #{row.Id} không khớp mã băm: dòng này đã bị chỉnh sửa trực tiếp trong cơ sở dữ liệu.";
                            return result;
                        }

                        expectedPrev = row.Hash;
                    }
                }

                result.Message = result.TotalChecked == 0
                    ? "Chưa có dòng nhật ký nào để kiểm tra."
                    : $"Toàn vẹn: đã kiểm tra {result.TotalChecked:N0} dòng, không phát hiện chỉnh sửa hay xóa.";
                return result;
            }
            finally
            {
                ChainLock.Release();
            }
        }

        public static string ComputeHash(AuditLog e)
        {
            var created = DateTime.SpecifyKind(e.CreatedAt, DateTimeKind.Utc)
                .ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

            var raw = string.Join("|",
                e.PrevHash ?? string.Empty,
                created,
                e.UserId ?? string.Empty,
                e.UserName ?? string.Empty,
                e.UserEmail ?? string.Empty,
                e.UserRole ?? string.Empty,
                e.Action ?? string.Empty,
                e.Category ?? string.Empty,
                e.EntityType ?? string.Empty,
                e.EntityId ?? string.Empty,
                e.Description ?? string.Empty,
                e.Severity ?? string.Empty,
                e.Success ? "1" : "0",
                e.IpAddress ?? string.Empty,
                e.UserAgent ?? string.Empty,
                e.RequestPath ?? string.Empty);

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static AuditActor ResolveActor(HttpContext? http)
        {
            var user = http?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                return new AuditActor(
                    user.FindFirstValue(ClaimTypes.NameIdentifier),
                    user.FindFirstValue(ClaimTypes.Name),
                    user.FindFirstValue(ClaimTypes.Email),
                    user.FindFirstValue(ClaimTypes.Role) ?? "Customer");
            }

            return http == null ? AuditActor.SystemActor : AuditActor.Anonymous();
        }

        /// <summary>Làm sạch chuỗi: bỏ ký tự điều khiển (chống giả mạo dòng log) và cắt theo độ dài tối đa.</summary>
        private static string? Clean(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return null;

            var sb = new StringBuilder(Math.Min(value.Length, maxLength));
            foreach (var ch in value)
            {
                if (sb.Length >= maxLength) break;
                sb.Append(char.IsControl(ch) ? ' ' : ch);
            }
            return sb.ToString().Trim();
        }
    }
}
