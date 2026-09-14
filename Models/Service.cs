using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Appointment.Models
{
    public class Service
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        public string ProviderId { get; set; } = string.Empty;
        [ForeignKey(nameof(ProviderId))]
        public User? Provider { get; set; }

        [Required]
        public string CategoryId { get; set; } = string.Empty;
        [ForeignKey(nameof(CategoryId))]
        public Category? Category { get; set; }

        [Required(ErrorMessage = "Tên dịch vụ là bắt buộc")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int DurationMinutes { get; set; } = 30;

        [StringLength(250)]
        public string? Location { get; set; }

        public string? ImageUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<AppointmentModel>? Appointments { get; set; }
    }
}
