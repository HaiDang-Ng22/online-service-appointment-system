using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Appointment.Models
{
    /// <summary>
    /// US-30: đánh giá & chấm điểm chất lượng dịch vụ (mỗi lịch hẹn hoàn thành được đánh giá 1 lần).
    /// </summary>
    public class Review
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        public string AppointmentId { get; set; } = string.Empty;
        [ForeignKey(nameof(AppointmentId))]
        public AppointmentModel? Appointment { get; set; }

        [Required]
        public string ServiceId { get; set; } = string.Empty;
        [ForeignKey(nameof(ServiceId))]
        public Service? Service { get; set; }

        [Required]
        public string CustomerId { get; set; } = string.Empty;
        [ForeignKey(nameof(CustomerId))]
        public User? Customer { get; set; }

        [Range(1, 5, ErrorMessage = "Điểm đánh giá phải từ 1 đến 5 sao")]
        public int Rating { get; set; }

        [StringLength(1000, ErrorMessage = "Nhận xét tối đa 1000 ký tự")]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
