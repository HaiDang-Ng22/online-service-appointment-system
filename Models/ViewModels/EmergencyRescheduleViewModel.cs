using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Appointment.Models.ViewModels
{
    public class EmergencyRescheduleViewModel
    {
        // 1. Tiêu chí tìm kiếm lịch bị ảnh hưởng
        [Display(Name = "Bác sĩ / Nhà cung cấp gặp sự cố")]
        public string? SourceProviderId { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Ngày xảy ra sự cố")]
        public DateTime IncidentDate { get; set; } = DateTime.Today;

        [Display(Name = "Ca làm việc bị ảnh hưởng")]
        public string ShiftFilter { get; set; } = "All"; // All, Morning (<12h), Afternoon (12h-18h), Evening (>18h)

        // 2. Phương án điều phối / dời lịch
        [Required(ErrorMessage = "Vui lòng chọn bác sĩ thay thế hoặc bác sĩ phụ trách mới")]
        [Display(Name = "Bác sĩ / Chuyên gia thay thế")]
        public string TargetProviderId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn ngày chuyển đến")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày hẹn mới")]
        public DateTime TargetDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Vui lòng chọn hoặc nhập lý do dời lịch khẩn cấp")]
        [Display(Name = "Lý do sự cố khẩn cấp")]
        [StringLength(250)]
        public string Reason { get; set; } = "Bác sĩ gặp sự cố sức khỏe đột xuất";

        [Display(Name = "Thông báo xin lỗi gửi khách hàng")]
        [StringLength(1000)]
        public string? ApologyMessage { get; set; }

        // 3. Danh sách cuộc hẹn được chọn để dời
        public List<string> SelectedAppointmentIds { get; set; } = new List<string>();

        // 4. Danh sách hiển thị
        public List<SelectListItem> ProvidersList { get; set; } = new List<SelectListItem>();
        public List<AffectedAppointmentItem> AffectedAppointments { get; set; } = new List<AffectedAppointmentItem>();
    }

    public class AffectedAppointmentItem
    {
        public string Id { get; set; } = string.Empty;
        public string ShortId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public int ServiceDuration { get; set; }
        public string TimeSlot { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool HasConflict { get; set; }
        public string? ConflictDetail { get; set; }
    }
}
