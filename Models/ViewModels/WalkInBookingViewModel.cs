using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Appointment.Models.ViewModels
{
    public class WalkInBookingViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên khách hàng")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
        [Display(Name = "Họ và tên khách hàng")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (gồm 10 số bắt đầu bằng 0)")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email (tùy chọn)")]
        public string? Email { get; set; }

        [Display(Name = "Địa chỉ liên hệ")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn dịch vụ")]
        [Display(Name = "Dịch vụ phục vụ")]
        public string ServiceId { get; set; } = string.Empty;

        [Display(Name = "Bác sĩ / Chuyên gia phụ trách")]
        public string? ProviderId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày hẹn")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày tiếp nhận")]
        public DateTime AppointmentDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Vui lòng chọn giờ phục vụ")]
        [Display(Name = "Khung giờ bắt đầu")]
        public string SelectedTimeSlot { get; set; } = DateTime.Now.ToString("HH:mm");

        [StringLength(500)]
        [Display(Name = "Yêu cầu / Ghi chú của khách")]
        public string? Notes { get; set; }

        [StringLength(1000)]
        [Display(Name = "Ghi chú nội bộ (Staff & Provider)")]
        public string? InternalNote { get; set; }

        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Confirmed";

        // Lists for UI select
        public List<SelectListItem> ServicesList { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> ProvidersList { get; set; } = new List<SelectListItem>();
    }
}
