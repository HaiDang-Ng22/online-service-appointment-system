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

                    // Soạn thảo nội dung email bằng AI
                    string reminderContent = await aiService.GenerateReminderAsync(details);

                    string emailBody = $@"
<div style='font-family:""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;max-width:600px;margin:0 auto;padding:24px;background-color:#ffffff;border:1px solid #e2e8f0;border-radius:12px;'>
    <div style='border-bottom:2px solid #2563eb;padding-bottom:12px;margin-bottom:20px;'>
        <h2 style='color:#1e3a8a;margin:0;font-size:22px;'>🔔 Thông Báo Nhắc Lịch Hẹn — AppointX</h2>
        <small style='color:#64748b;'>Hệ thống nhắc nhở thông minh đồng hành cùng quý khách</small>
    </div>

    <div style='line-height:1.6;color:#334155;font-size:15px;white-space:pre-wrap;'>
{reminderContent}
    </div>

    <div style='margin-top:24px;padding:16px;background-color:#f8fafc;border-radius:8px;font-size:13px;color:#64748b;border-left:4px solid #3b82f6;'>
        <strong>Lưu ý:</strong> Vui lòng có mặt trước giờ hẹn từ 5 - 10 phút. Nếu có nhu cầu thay đổi hoặc dời lịch, quý khách vui lòng liên hệ trước ít nhất 2 giờ theo chính sách của hệ thống.
    </div>

    <div style='margin-top:24px;text-align:center;font-size:12px;color:#94a3b8;border-top:1px solid #f1f5f9;padding-top:16px;'>
        Email này được gửi tự động bởi Hệ Thống Quản Lý Đặt Lịch AppointX.<br/>
        Vui lòng không phản hồi trực tiếp vào địa chỉ email này.
    </div>
</div>";

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
    }
}
