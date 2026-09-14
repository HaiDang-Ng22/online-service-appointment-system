using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Appointment.Models
{
    public class WorkingHour
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        public string ProviderId { get; set; } = string.Empty;
        [ForeignKey(nameof(ProviderId))]
        public User? Provider { get; set; }

        public DayOfWeek DayOfWeek { get; set; }

        public TimeSpan StartTime { get; set; } = new TimeSpan(8, 0, 0);

        public TimeSpan EndTime { get; set; } = new TimeSpan(17, 0, 0);

        public bool IsAvailable { get; set; } = true;
    }
}
