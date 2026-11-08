using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Appointment.Models
{
    /// <summary>
    /// US-26: token đặt lại mật khẩu. Chỉ lưu mã băm (SHA-256) của token, không lưu token gốc.
    /// </summary>
    public class PasswordResetToken
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        public string UserId { get; set; } = string.Empty;
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [Required]
        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public DateTime? UsedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
