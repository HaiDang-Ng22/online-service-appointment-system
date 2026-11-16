using Microsoft.AspNetCore.Mvc.Rendering;

namespace Appointment.Models.ViewModels
{
    /// <summary>US-38: một điểm trên biểu đồ doanh thu theo thời gian.</summary>
    public class StatsPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int Completed { get; set; }
        public int Total { get; set; }
    }

    public class ServiceStatRow
    {
        public string Name { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public decimal Revenue { get; set; }
        public double SharePercent { get; set; }
        public double? AvgRating { get; set; }
        public int ReviewCount { get; set; }
    }

    public class CustomerStatRow
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public int Count { get; set; }
        public decimal Spent { get; set; }
    }

    public class ProviderStatsViewModel
    {
        // ----- Bộ lọc -----
        /// <summary>today | 7d | 30d | month | lastmonth | quarter | year | custom</summary>
        public string RangeKey { get; set; } = "30d";
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public DateTime PrevFrom { get; set; }
        public DateTime PrevTo { get; set; }
        public string? ServiceId { get; set; }
        public string? ProviderId { get; set; }
        public bool IsAdmin { get; set; }
        public string ScopeName { get; set; } = string.Empty;
        public List<SelectListItem> Services { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Providers { get; set; } = new List<SelectListItem>();

        // ----- Doanh thu -----
        /// <summary>Doanh thu ghi nhận: tổng giá dịch vụ của các lịch ĐÃ HOÀN THÀNH trong kỳ.</summary>
        public decimal RealizedRevenue { get; set; }
        public decimal PrevRealizedRevenue { get; set; }
        public double? RevenueGrowthPct { get; set; }

        /// <summary>Doanh thu dự kiến: lịch còn hiệu lực (chờ xác nhận / đã xác nhận / đang thực hiện) trong kỳ.</summary>
        public decimal ExpectedRevenue { get; set; }

        /// <summary>Doanh thu thất thoát: lịch bị hủy trong kỳ.</summary>
        public decimal LostRevenue { get; set; }

        /// <summary>Tiền khách đã thanh toán / đặt cọc online phát sinh trong kỳ (gộp cả khoản sau đó đã hoàn).</summary>
        public decimal CollectedGross { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal CollectedNet => CollectedGross - RefundedAmount;

        // ----- Lịch hẹn -----
        public int TotalAppointments { get; set; }
        public int PrevTotalAppointments { get; set; }
        public double? AppointmentGrowthPct { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
        public int PendingCount { get; set; }
        public int ConfirmedCount { get; set; }
        public int InProgressCount { get; set; }
        public double CompletionRate { get; set; }
        public double CancelRate { get; set; }
        public decimal AvgRevenuePerCompleted { get; set; }

        // ----- Khách hàng & chất lượng -----
        public int UniqueCustomers { get; set; }
        public int NewCustomers { get; set; }
        public int ReturningCustomers { get; set; }
        public double? AvgRating { get; set; }
        public int ReviewCount { get; set; }

        // ----- Biểu đồ & bảng -----
        /// <summary>day | month</summary>
        public string Granularity { get; set; } = "day";
        public List<StatsPoint> Series { get; set; } = new List<StatsPoint>();
        public List<ServiceStatRow> TopServices { get; set; } = new List<ServiceStatRow>();
        public List<CustomerStatRow> TopCustomers { get; set; } = new List<CustomerStatRow>();

        /// <summary>Số lịch (không tính đã hủy) theo thứ, bắt đầu từ Thứ 2.</summary>
        public int[] ByWeekday { get; set; } = new int[7];
        public List<string> HourLabels { get; set; } = new List<string>();
        public List<int> HourCounts { get; set; } = new List<int>();
        public Dictionary<string, decimal> PaymentByMethod { get; set; } = new Dictionary<string, decimal>();
        public string? BusiestDay { get; set; }
        public string? PeakHour { get; set; }
    }
}
