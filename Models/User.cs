using System.ComponentModel.DataAnnotations;

namespace Appointment.Models
{
    public class User
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required(ErrorMessage = "Họ tên là bắt buộc")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }

        public string? AvatarUrl { get; set; }

        /// <summary>
        /// Roles: Admin, Customer, Provider, Staff
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Customer";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<Service>? ProvidedServices { get; set; }
        public ICollection<AppointmentModel>? CustomerAppointments { get; set; }
        public ICollection<AppointmentModel>? ProviderAppointments { get; set; }
        public ICollection<ProviderOffDay>? OffDays { get; set; }
    }
}
