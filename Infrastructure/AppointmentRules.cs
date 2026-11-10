using Appointment.Models;

namespace Appointment.Infrastructure
{
    /// <summary>Các quy tắc nghiệp vụ dùng chung cho trạng thái lịch hẹn (US-19, US-21, US-24, US-37, US-39).</summary>
    public static class AppointmentRules
    {
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
        public const string InProgress = "In-Progress";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";

        public static readonly string[] AllStatuses = { Pending, Confirmed, InProgress, Completed, Cancelled };

        /// <summary>Thời điểm bắt đầu phục vụ (giờ máy chủ).</summary>
        public static DateTime StartAt(AppointmentModel a) => a.AppointmentDate.Date + a.StartTime;

        /// <summary>
        /// US-19: Khách chỉ được hủy lịch khi lịch còn Pending/Confirmed và còn cách giờ phục vụ tối thiểu <paramref name="cancelBeforeHours"/> giờ.
        /// </summary>
        public static bool CanCustomerCancel(AppointmentModel appointment, int cancelBeforeHours, out string error)
        {
            error = string.Empty;

            if (appointment.Status == Cancelled)
            {
                error = "Lịch hẹn này đã được hủy trước đó.";
                return false;
            }

            if (appointment.Status == Completed)
            {
                error = "Không thể hủy lịch hẹn đã hoàn thành.";
                return false;
            }

            if (appointment.Status == InProgress)
            {
                error = "Dịch vụ đang trong quá trình thực hiện nên không thể hủy.";
                return false;
            }

            var start = StartAt(appointment);
            var now = DateTime.Now;

            if (start <= now)
            {
                error = "Đã đến hoặc quá giờ phục vụ nên không thể hủy lịch hẹn.";
                return false;
            }

            if (cancelBeforeHours > 0 && start - now < TimeSpan.FromHours(cancelBeforeHours))
            {
                error = $"Chỉ có thể hủy lịch hẹn trước giờ phục vụ ít nhất {cancelBeforeHours} giờ. Vui lòng liên hệ nhà cung cấp để được hỗ trợ.";
                return false;
            }

            return true;
        }

        public static string StatusLabel(string status) => status switch
        {
            Pending => "Chờ xác nhận",
            Confirmed => "Đã xác nhận",
            InProgress => "Đang thực hiện",
            Completed => "Đã hoàn thành",
            Cancelled => "Đã hủy",
            _ => status
        };

        /// <summary>
        /// Kiểm tra việc chuyển trạng thái do Provider/Staff thực hiện.
        /// Pending -> Confirmed | InProgress | Completed | Cancelled; Confirmed -> InProgress | Completed | Cancelled; InProgress -> Completed | Cancelled; Completed/Cancelled là trạng thái cuối.
        /// Cho phép giữ nguyên trạng thái (để cập nhật chẩn đoán).
        /// </summary>
        public static bool TryValidateTransition(AppointmentModel appointment, string? newStatus, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(newStatus) || newStatus == appointment.Status)
            {
                return true;
            }

            if (!AllStatuses.Contains(newStatus))
            {
                error = "Trạng thái không hợp lệ.";
                return false;
            }

            bool allowed = appointment.Status switch
            {
                Pending => newStatus is Confirmed or InProgress or Completed or Cancelled,
                Confirmed => newStatus is InProgress or Completed or Cancelled,
                InProgress => newStatus is Completed or Cancelled,
                _ => false
            };

            if (!allowed)
            {
                error = $"Không thể chuyển lịch hẹn từ '{StatusLabel(appointment.Status)}' sang '{StatusLabel(newStatus)}'.";
                return false;
            }

            if (newStatus == Completed && appointment.AppointmentDate.Date > DateTime.Today)
            {
                error = "Chưa đến ngày hẹn nên chưa thể đánh dấu hoàn thành.";
                return false;
            }

            return true;
        }
    }
}
