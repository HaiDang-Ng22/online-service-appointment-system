using System.ComponentModel.DataAnnotations;

namespace Appointment.Models.ViewModels
{
    // ---------- US-26: Quên / đặt lại mật khẩu ----------
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email đã đăng ký")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        [Display(Name = "Xác nhận mật khẩu mới")]
        public string ConfirmNewPassword { get; set; } = string.Empty;

        public bool IsTokenValid { get; set; } = true;
    }

    // ---------- US-14: Khung giờ làm việc theo tuần ----------
    public class WorkingHourDayInput
    {
        public DayOfWeek Day { get; set; }
        public bool IsAvailable { get; set; }
        public TimeSpan StartTime { get; set; } = new TimeSpan(8, 0, 0);
        public TimeSpan EndTime { get; set; } = new TimeSpan(17, 0, 0);
    }

    public class WorkingHoursViewModel
    {
        public List<WorkingHourDayInput> Days { get; set; } = new List<WorkingHourDayInput>();
    }

    // ---------- US-30: Đánh giá dịch vụ ----------
    public class RatingInfo
    {
        public double Average { get; set; }
        public int Count { get; set; }
    }

    public class ReviewFormViewModel
    {
        [Required]
        public string AppointmentId { get; set; } = string.Empty;

        public string? ServiceName { get; set; }
        public string? ProviderName { get; set; }
        public DateTime AppointmentDate { get; set; }
        public bool IsEdit { get; set; }

        [Range(1, 5, ErrorMessage = "Vui lòng chọn số sao từ 1 đến 5")]
        [Display(Name = "Điểm đánh giá")]
        public int Rating { get; set; }

        [StringLength(1000, ErrorMessage = "Nhận xét tối đa 1000 ký tự")]
        [Display(Name = "Nhận xét")]
        public string? Comment { get; set; }
    }

    // ---------- US-28: Thanh toán & đặt cọc ----------
    public class CheckoutViewModel
    {
        public AppointmentModel Appointment { get; set; } = null!;
        public Infrastructure.PaymentSummary Summary { get; set; } = null!;
        public int DepositPercent { get; set; }
        public bool IsSandbox { get; set; }
        public List<Payment> History { get; set; } = new List<Payment>();
    }
}
