using System.ComponentModel.DataAnnotations;

namespace Appointment.Models.ViewModels
{
    public class CreateUserByAdminViewModel
    {
        [Required(ErrorMessage = "Họ và tên là bắt buộc")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vai trò là bắt buộc")]
        public string Role { get; set; } = "Customer"; // Admin, Provider, Staff, Customer

        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
    }
}
