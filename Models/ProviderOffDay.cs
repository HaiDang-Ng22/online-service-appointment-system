using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Appointment.Models
{
    public class ProviderOffDay
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        public string ProviderId { get; set; } = string.Empty;
        [ForeignKey(nameof(ProviderId))]
        public User? Provider { get; set; }

        [DataType(DataType.Date)]
        public DateTime OffDate { get; set; }

        [StringLength(250)]
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
