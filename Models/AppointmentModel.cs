using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Appointment.Models
{
    public class AppointmentModel
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        public string CustomerId { get; set; } = string.Empty;
        [ForeignKey(nameof(CustomerId))]
        public User? Customer { get; set; }

        [Required]
        public string ServiceId { get; set; } = string.Empty;
        [ForeignKey(nameof(ServiceId))]
        public Service? Service { get; set; }

        [Required]
        public string ProviderId { get; set; } = string.Empty;
        [ForeignKey(nameof(ProviderId))]
        public User? Provider { get; set; }

        public string? StaffId { get; set; }
        [ForeignKey(nameof(StaffId))]
        public User? Staff { get; set; }

        [DataType(DataType.Date)]
        public DateTime AppointmentDate { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(1000)]
        [Display(Name = "Chẩn đoán / Kết luận")]
        public string? Diagnosis { get; set; }

        [StringLength(1000)]
        [Display(Name = "Đơn thuốc / Lời dặn")]
        public string? Prescription { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
