using Appointment.Data;
using Appointment.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Appointment.Infrastructure
{
    /// <summary>
    /// US-37: tác vụ nền tự động hủy các lịch hẹn Pending (chưa xác nhận) đã quá hạn:
    ///  - đã qua giờ bắt đầu mà vẫn chưa được xác nhận; hoặc
    ///  - chờ xác nhận quá BookingRules:ConfirmationTimeoutHours giờ kể từ lúc đặt.
    /// Các khoản đã thanh toán / đặt cọc được hoàn lại và khách hàng nhận email thông báo (nếu SMTP đã cấu hình).
    /// </summary>
    public class AppointmentAutoCancelService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly BookingRulesOptions _options;
        private readonly ILogger<AppointmentAutoCancelService> _logger;

        public AppointmentAutoCancelService(
            IServiceScopeFactory scopeFactory,
            IOptions<BookingRulesOptions> options,
            ILogger<AppointmentAutoCancelService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.AutoCancelEnabled)
            {
                _logger.LogInformation("Tự động hủy lịch chưa xác nhận đang TẮT (BookingRules:AutoCancelEnabled=false).");
                return;
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, _options.AutoCancelIntervalMinutes));

            try
            {
                // Chờ ứng dụng khởi tạo xong cơ sở dữ liệu
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        int count = await RunOnceAsync(stoppingToken);
                        if (count > 0)
                        {
                            _logger.LogInformation("Đã tự động hủy {Count} lịch hẹn chưa xác nhận quá hạn.", count);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "Lỗi khi quét lịch hẹn quá hạn. Sẽ thử lại ở chu kỳ sau.");
                    }

                    await Task.Delay(interval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Ứng dụng đang dừng
            }
        }

        /// <summary>Quét và hủy các lịch quá hạn. Trả về số lịch đã hủy.</summary>
        public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var payments = scope.ServiceProvider.GetRequiredService<PaymentService>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var pending = await db.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Service)
                .Where(a => a.Status == AppointmentRules.Pending)
                .ToListAsync(cancellationToken);

            var now = DateTime.Now;
            var timeout = _options.ConfirmationTimeoutHours > 0
                ? TimeSpan.FromHours(_options.ConfirmationTimeoutHours)
                : (TimeSpan?)null;

            var cancelled = new List<(AppointmentModel Appointment, string Reason, decimal Refund)>();

            foreach (var a in pending)
            {
                string? reason = null;

                if (AppointmentRules.StartAt(a) <= now)
                {
                    reason = "Quá giờ hẹn mà nhà cung cấp chưa xác nhận";
                }
                else if (timeout.HasValue)
                {
                    var created = DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc);
                    if (DateTime.UtcNow - created >= timeout.Value)
                    {
                        reason = $"Quá {_options.ConfirmationTimeoutHours} giờ chờ xác nhận";
                    }
                }

                if (reason == null) continue;

                a.Status = AppointmentRules.Cancelled;
                a.CancelReason = $"Hệ thống tự động hủy: {reason}";
                var refund = await payments.RefundAllAsync(a.Id, "Hoàn tiền do hệ thống tự động hủy lịch");
                cancelled.Add((a, reason, refund));
            }

            if (cancelled.Count == 0) return 0;

            await db.SaveChangesAsync(cancellationToken);

            foreach (var (a, reason, refund) in cancelled)
            {
                if (a.Customer == null || string.IsNullOrWhiteSpace(a.Customer.Email)) continue;

                var refundLine = refund > 0
                    ? $"<p>Khoản đã thanh toán <strong>{refund:N0} VNĐ</strong> sẽ được hoàn lại cho bạn.</p>"
                    : string.Empty;

                var body = $@"
<div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto'>
  <h2 style='color:#0f172a'>Lịch hẹn của bạn đã bị hủy tự động</h2>
  <p>Xin chào <strong>{System.Net.WebUtility.HtmlEncode(a.Customer.FullName)}</strong>,</p>
  <p>Lịch hẹn <strong>{System.Net.WebUtility.HtmlEncode(a.Service?.Name ?? "")}</strong> vào <strong>{a.AppointmentDate:dd/MM/yyyy} {a.StartTime:hh\:mm}</strong> đã bị hệ thống hủy vì: <em>{System.Net.WebUtility.HtmlEncode(reason)}</em>.</p>
  {refundLine}
  <p>Bạn có thể đặt lại khung giờ khác trên AppointX bất cứ lúc nào.</p>
</div>";

                await email.SendAsync(a.Customer.Email, "[AppointX] Lịch hẹn của bạn đã bị hủy tự động", body);
            }

            return cancelled.Count;
        }
    }
}
