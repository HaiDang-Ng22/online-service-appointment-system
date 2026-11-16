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

    /// <summary>Cấu hình bảo mật & nhật ký kiểm toán (appsettings.json -> "Security"). US-43.</summary>
    public class SecurityOptions
    {
        /// <summary>Số lần đăng nhập sai liên tiếp tối đa trước khi khóa tạm thời.</summary>
        public int MaxFailedLogins { get; set; } = 5;

        /// <summary>Thời gian khóa tạm thời (phút) tính từ lần đăng nhập sai gần nhất.</summary>
        public int LockoutMinutes { get; set; } = 15;

        /// <summary>Số lần đăng nhập sai tối đa từ một địa chỉ IP trong thời gian khóa (chống dò mật khẩu hàng loạt).</summary>
        public int MaxFailedLoginsPerIp { get; set; } = 30;
    }

    /// <summary>Cấu hình mã QR check-in (appsettings.json -> "CheckIn"). US-32.</summary>
    public class CheckInOptions
    {
        /// <summary>
        /// Khóa bí mật dùng để ký mã QR (HMAC-SHA256) nhằm chống làm giả mã check-in.
        /// Hãy đổi giá trị này trong môi trường thật (hoặc dùng User Secrets).
        /// </summary>
        public string SecretKey { get; set; } = "CHANGE-ME-appointx-checkin-secret";
    }
}
