using System.ComponentModel.DataAnnotations;

namespace Appointment.Models
{
    /// <summary>
    /// US-43: một dòng nhật ký kiểm toán (audit log) – CHỈ GHI THÊM, không có chức năng sửa / xóa.
    /// Mỗi dòng mang mã băm nối chuỗi (PrevHash -> Hash) để phát hiện việc bị chỉnh sửa trực tiếp trong cơ sở dữ liệu.
    /// </summary>
    public class AuditLog
    {
        public const string SeverityInfo = "Info";
        public const string SeverityWarning = "Warning";
        public const string SeverityCritical = "Critical";

        [Key]
        public long Id { get; set; }

        /// <summary>Thời điểm xảy ra (UTC, làm tròn đến mili-giây).</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Id người thực hiện (null = khách chưa đăng nhập / hệ thống).</summary>
        [StringLength(64)]
        public string? UserId { get; set; }

        [StringLength(150)]
        public string? UserName { get; set; }

        /// <summary>Email người thực hiện hoặc email được thử đăng nhập (dùng để đếm đăng nhập sai).</summary>
        [StringLength(150)]
        public string? UserEmail { get; set; }

        /// <summary>Admin / Provider / Staff / Customer / Anonymous / System</summary>
        [StringLength(20)]
        public string UserRole { get; set; } = "Anonymous";

        /// <summary>Mã hành động, xem <see cref="Infrastructure.AuditActions"/>.</summary>
        [Required]
        [StringLength(60)]
        public string Action { get; set; } = string.Empty;

        /// <summary>Nhóm: Auth, Account, Appointment, Payment, Review, Service, Category, Security, Audit, System</summary>
        [Required]
        [StringLength(30)]
        public string Category { get; set; } = "System";

        [StringLength(40)]
        public string? EntityType { get; set; }

        [StringLength(64)]
        public string? EntityId { get; set; }

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>Info | Warning | Critical</summary>
        [Required]
        [StringLength(10)]
        public string Severity { get; set; } = SeverityInfo;

        /// <summary>Hành động thành công hay bị từ chối / thất bại.</summary>
        public bool Success { get; set; } = true;

        [StringLength(64)]
        public string? IpAddress { get; set; }

        [StringLength(300)]
        public string? UserAgent { get; set; }

        [StringLength(300)]
        public string? RequestPath { get; set; }

        /// <summary>Mã băm của dòng liền trước (chuỗi "GENESIS" cho dòng đầu tiên).</summary>
        [StringLength(64)]
        public string PrevHash { get; set; } = string.Empty;

        /// <summary>SHA-256 của nội dung dòng này + PrevHash.</summary>
        [StringLength(64)]
        public string Hash { get; set; } = string.Empty;
    }
}
