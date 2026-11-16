namespace Appointment.Models.ViewModels
{
    /// <summary>US-43: bộ lọc nhật ký kiểm toán.</summary>
    public class AuditLogFilter
    {
        public string? Q { get; set; }
        public string? Category { get; set; }
        /// <summary>Mã hành động (không đặt tên là Action vì trùng route value "action" của MVC).</summary>
        public string? EventCode { get; set; }
        public string? Severity { get; set; }

        /// <summary>all | success | failed</summary>
        public string? Result { get; set; }
        public string? Role { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
    }

    public class AuditLogListViewModel
    {
        public AuditLogFilter Filter { get; set; } = new AuditLogFilter();
        public List<AuditLog> Items { get; set; } = new List<AuditLog>();
        public int Total { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

        // Thống kê nhanh 24 giờ gần nhất
        public int EventsLast24h { get; set; }
        public int FailedLoginsLast24h { get; set; }
        public int LockoutsLast24h { get; set; }
        public int AccessDeniedLast24h { get; set; }
        public int CriticalLast24h { get; set; }
        public int TotalAllTime { get; set; }
    }
}
