namespace Appointment.Infrastructure
{
    /// <summary>Cấu hình gửi email (appsettings.json -> "Email"). US-26.</summary>
    public class EmailOptions
    {
        public bool Enabled { get; set; } = false;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool EnableSsl { get; set; } = true;
        public string From { get; set; } = "no-reply@appointx.com";
        public string FromName { get; set; } = "AppointX";
    }

    /// <summary>Quy tắc đặt / hủy lịch (appsettings.json -> "BookingRules"). US-19, US-37.</summary>
    public class BookingRulesOptions
    {
        /// <summary>Khách chỉ được hủy lịch trước giờ phục vụ tối thiểu N giờ (0 = chỉ cần chưa tới giờ hẹn).</summary>
        public int CancelBeforeHours { get; set; } = 2;

        /// <summary>Bật/tắt tác vụ tự động hủy lịch chưa xác nhận quá hạn.</summary>
        public bool AutoCancelEnabled { get; set; } = true;

        /// <summary>Lịch ở trạng thái Pending quá N giờ kể từ lúc đặt sẽ bị tự hủy (0 = tắt, chỉ hủy khi quá giờ hẹn).</summary>
        public int ConfirmationTimeoutHours { get; set; } = 24;

        /// <summary>Chu kỳ quét của tác vụ nền (phút).</summary>
        public int AutoCancelIntervalMinutes { get; set; } = 5;
    }

    /// <summary>Cấu hình thanh toán (appsettings.json -> "Payment"). US-28.</summary>
    public class PaymentOptions
    {
        /// <summary>Tỷ lệ đặt cọc trên giá dịch vụ (%).</summary>
        public int DepositPercent { get; set; } = 30;
    }
}
