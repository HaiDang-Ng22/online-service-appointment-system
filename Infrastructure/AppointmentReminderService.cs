using Appointment.Data;
using Appointment.Models;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Infrastructure
{
    /// <summary>
    /// US-AI-04: Tác vụ nền tự động soạn thảo và gửi email nhắc lịch hẹn cá nhân hóa thông qua AI.
    /// Quét các lịch hẹn đã xác nhận (Confirmed) sắp diễn ra trong vòng 24 giờ tới và chưa từng được gửi email nhắc nhở.
    /// </summary>
    public class AppointmentReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppointmentReminderService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

        public AppointmentReminderService(
            IServiceScopeFactory scopeFactory,
            ILogger<AppointmentReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Chờ hệ thống khởi tạo cơ sở dữ liệu
                await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        int remindersSent = await RunOnceAsync(stoppingToken);
                        if (remindersSent > 0)
                        {
                            _logger.LogInformation("AI Reminder Service: Đã tạo và gửi {Count} email nhắc nhở lịch hẹn.", remindersSent);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "AI Reminder Service: Lỗi khi xử lý quét gửi nhắc lịch hẹn. Sẽ thử lại ở chu kỳ kế tiếp.");
                    }

                    await Task.Delay(_checkInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Dừng tiến trình khi ứng dụng shutdown
            }
        }

        /// <summary>
        /// Thực thi một lượt quét và gửi email nhắc lịch hẹn AI.
        /// </summary>
        public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var aiService = scope.ServiceProvider.GetRequiredService<IAiService>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var now = DateTime.Now;
            var windowEnd = now.AddHours(24);

            // Tìm các lịch hẹn Confirmed, chưa gửi email nhắc, và nằm trong khoảng (now, now + 24h)
            var candidates = await db.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Service)
                    .ThenInclude(s => s!.Category)
                .Include(a => a.Provider)
                .Where(a => a.Status == AppointmentRules.Confirmed && a.ReminderSentAt == null)
                .ToListAsync(cancellationToken);

            var toRemind = new List<AppointmentModel>();
            foreach (var a in candidates)
            {
                var startAt = AppointmentRules.StartAt(a);
                if (startAt > now && startAt <= windowEnd)
                {
                    toRemind.Add(a);
                }
            }

            if (!toRemind.Any()) return 0;

            int sentCount = 0;
            foreach (var a in toRemind)
            {
                if (a.Customer == null || string.IsNullOrWhiteSpace(a.Customer.Email))
                {
                    a.ReminderSentAt = DateTime.UtcNow;
                    continue;
                }

                try
                {
                    var details = $@"
Khách hàng: {a.Customer.FullName}
Email: {a.Customer.Email}
Số điện thoại: {a.Customer.PhoneNumber}
Dịch vụ: {a.Service?.Name ?? "Dịch vụ"}
Chuyên mục: {a.Service?.Category?.Name ?? "Chung"}
Chuyên gia/Nhà cung cấp: {a.Provider?.FullName ?? "Cơ sở dịch vụ"}
Ngày hẹn: {a.AppointmentDate:dd/MM/yyyy}
Khung giờ: {a.StartTime:hh\:mm} - {a.EndTime:hh\:mm}
Địa điểm: {a.Service?.Location ?? "Tại cơ sở cung cấp"}
Ghi chú khách hàng: {a.Notes ?? "Không có"}
Giá dịch vụ: {a.Service?.Price:N0} VNĐ
";

                    // Soạn thảo lời mở đầu bằng AI (nếu có cấu hình AI)
                    string? aiGreeting = null;
                    try
                    {
                        aiGreeting = await aiService.GenerateReminderAsync(details);
                    }
                    catch
                    {
                        // Fallback sang mẫu mặc định
                    }

                    string emailBody = BuildReminderEmail(a, aiGreeting);

                    await emailSender.SendAsync(
                        a.Customer.Email,
                        $"[AppointX] Nhắc nhở lịch hẹn: {a.Service?.Name ?? "Dịch vụ"} vào {a.AppointmentDate:dd/MM/yyyy}",
                        emailBody);

                    a.ReminderSentAt = DateTime.UtcNow;
                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi khi gửi email nhắc lịch cho cuộc hẹn ID {AppointmentId}", a.Id);
                    // Đánh dấu thời điểm để tránh gửi lặp vô tận khi email lỗi
                    a.ReminderSentAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            return sentCount;
        }

        /// <summary>
        /// Tạo nội dung email template chuẩn cho [US-45] gồm: Tên dịch vụ, Giờ hẹn, Tên bác sĩ, Địa chỉ chỉ đường.
        /// </summary>
        public static string BuildReminderEmail(AppointmentModel a, string? greetingMessage = null)
        {
            var serviceName = a.Service?.Name ?? "Dịch vụ y tế / chăm sóc";
            var doctorName = a.Provider?.FullName ?? "Chuyên gia / Bác sĩ phụ trách";
            var dateStr = a.AppointmentDate.ToString("dd/MM/yyyy");
            var timeStr = $"{a.StartTime:hh\\:mm} - {a.EndTime:hh\\:mm}";
            var location = string.IsNullOrWhiteSpace(a.Service?.Location) ? "Tại cơ sở dịch vụ AppointX" : a.Service.Location;
            var mapsUrl = $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(location)}";

            return $@"
<div style='font-family:""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;max-width:620px;margin:0 auto;padding:24px;background-color:#ffffff;border:1px solid #e2e8f0;border-radius:12px;'>
    <div style='border-bottom:2px solid #2563eb;padding-bottom:12px;margin-bottom:20px;'>
        <h2 style='color:#1e3a8a;margin:0;font-size:22px;'>🔔 Thông Báo Nhắc Lịch Hẹn — AppointX</h2>
        <small style='color:#64748b;'>Hệ thống nhắc nhở tự động giúp quý khách không bỏ lỡ lịch hẹn</small>
    </div>

    {(string.IsNullOrWhiteSpace(greetingMessage) ? "" : $"<div style='background-color:#f1f5f9;padding:12px 16px;border-radius:8px;margin-bottom:16px;font-size:14px;color:#334155;white-space:pre-wrap;'>{greetingMessage}</div>")}

    <p style='color:#334155;font-size:15px;line-height:1.6;'>
        Xin chào <strong>{a.Customer?.FullName ?? "Quý khách"}</strong>,<br/>
        AppointX xin trân trọng nhắc nhở quý khách có lịch hẹn sắp diễn ra với thông tin chi tiết như sau:
    </p>

    <div style='background-color:#f8fafc;border:1px solid #e2e8f0;border-left:4px solid #2563eb;border-radius:8px;padding:16px;margin:18px 0;'>
        <table style='width:100%;border-collapse:collapse;font-size:14px;color:#1e293b;'>
            <tr>
                <td style='padding:8px 0;width:35%;color:#64748b;'><strong>🩺 Tên dịch vụ:</strong></td>
                <td style='padding:8px 0;font-weight:600;color:#1e3a8a;font-size:15px;'>{serviceName}</td>
            </tr>
            <tr>
                <td style='padding:8px 0;color:#64748b;'><strong>⏰ Giờ hẹn:</strong></td>
                <td style='padding:8px 0;font-weight:600;color:#059669;'>{timeStr} | Ngày {dateStr}</td>
            </tr>
            <tr>
                <td style='padding:8px 0;color:#64748b;'><strong>👨‍⚕️ Tên bác sĩ / Chuyên gia:</strong></td>
                <td style='padding:8px 0;font-weight:600;'>{doctorName}</td>
            </tr>
            <tr>
                <td style='padding:8px 0;color:#64748b;vertical-align:top;'><strong>📍 Địa chỉ chỉ đường:</strong></td>
                <td style='padding:8px 0;'>
                    <div style='font-weight:500;'>{location}</div>
                    <div style='margin-top:6px;'>
                        <a href='{mapsUrl}' target='_blank' style='display:inline-block;background-color:#2563eb;color:#ffffff;text-decoration:none;font-weight:600;font-size:12px;padding:5px 12px;border-radius:6px;'>
                            🗺️ Mở chỉ đường trên Google Maps
                        </a>
                    </div>
                </td>
            </tr>
            {(string.IsNullOrWhiteSpace(a.Notes) ? "" : $@"<tr><td style='padding:8px 0;color:#64748b;'><strong>📝 Ghi chú:</strong></td><td style='padding:8px 0;'>{a.Notes}</td></tr>")}
        </table>
    </div>

    <div style='margin-top:20px;padding:14px;background-color:#eff6ff;border-radius:8px;font-size:13px;color:#1e40af;'>
        <strong>💡 Lưu ý:</strong> Quý khách vui lòng có mặt trước giờ hẹn từ 5 - 10 phút. Nếu quý khách có nhu cầu thay đổi hoặc dời lịch, vui lòng thao tác trên hệ thống hoặc liên hệ cơ sở trước ít nhất 2 giờ.
    </div>

    <div style='margin-top:24px;text-align:center;font-size:12px;color:#94a3b8;border-top:1px solid #f1f5f9;padding-top:16px;'>
        Email này được gửi tự động bởi Hệ Thống Quản Lý Đặt Lịch AppointX.<br/>
        Trân trọng cảm ơn Quý khách đã tin tưởng đồng hành!
    </div>
</div>";
        }
    }
}
