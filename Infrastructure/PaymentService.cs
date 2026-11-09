using Appointment.Data;
using Appointment.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Appointment.Infrastructure
{
    public class PaymentSummary
    {
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
        public decimal Refunded { get; set; }
        public decimal Remaining => Math.Max(0, Total - Paid);
        public decimal DepositAmount { get; set; }

        /// <summary>Free | Unpaid | Deposited | Paid | Refunded</summary>
        public string Status { get; set; } = "Unpaid";

        public string Label => Status switch
        {
            "Free" => "Miễn phí",
            "Paid" => "Đã thanh toán",
            "Deposited" => "Đã đặt cọc",
            "Refunded" => "Đã hoàn tiền",
            _ => "Chưa thanh toán"
        };

        public string BadgeClass => Status switch
        {
            "Paid" => "bg-success-subtle text-success border border-success-subtle",
            "Deposited" => "bg-info-subtle text-info border border-info-subtle",
            "Refunded" => "bg-secondary-subtle text-secondary border",
            "Free" => "bg-light text-secondary border",
            _ => "bg-warning-subtle text-warning-emphasis border border-warning-subtle"
        };
    }

    public class PaymentService
    {
        private readonly AppDbContext _context;
        private readonly PaymentOptions _options;

        public PaymentService(AppDbContext context, IOptions<PaymentOptions> options)
        {
            _context = context;
            _options = options.Value;
        }

        public int DepositPercent => Math.Clamp(_options.DepositPercent, 1, 100);

        public decimal CalculateDeposit(decimal price)
        {
            return Math.Round(price * DepositPercent / 100m, 0, MidpointRounding.AwayFromZero);
        }

        public static PaymentSummary BuildSummary(decimal price, IEnumerable<Payment> payments, decimal depositAmount)
        {
            var list = payments.ToList();
            var summary = new PaymentSummary
            {
                Total = price,
                Paid = list.Where(p => p.Status == Payment.StatusPaid).Sum(p => p.Amount),
                Refunded = list.Where(p => p.Status == Payment.StatusRefunded).Sum(p => p.Amount),
                DepositAmount = depositAmount
            };

            if (price <= 0) summary.Status = "Free";
            else if (summary.Paid >= price) summary.Status = "Paid";
            else if (summary.Paid > 0) summary.Status = "Deposited";
            else if (summary.Refunded > 0) summary.Status = "Refunded";
            else summary.Status = "Unpaid";

            return summary;
        }

        public async Task<PaymentSummary> GetSummaryAsync(AppointmentModel appointment)
        {
            var price = appointment.Service?.Price
                        ?? (await _context.Services.AsNoTracking().Where(s => s.Id == appointment.ServiceId).Select(s => s.Price).FirstOrDefaultAsync());

            var payments = await _context.Payments.AsNoTracking()
                .Where(p => p.AppointmentId == appointment.Id)
                .ToListAsync();

            return BuildSummary(price, payments, CalculateDeposit(price));
        }

        /// <summary>Tóm tắt thanh toán cho nhiều lịch hẹn (dùng cho danh sách).</summary>
        public async Task<Dictionary<string, PaymentSummary>> GetSummariesAsync(IEnumerable<AppointmentModel> appointments)
        {
            var list = appointments.ToList();
            var ids = list.Select(a => a.Id).ToList();

            var payments = await _context.Payments.AsNoTracking()
                .Where(p => ids.Contains(p.AppointmentId))
                .ToListAsync();

            var byAppointment = payments.GroupBy(p => p.AppointmentId).ToDictionary(g => g.Key, g => g.ToList());

            var result = new Dictionary<string, PaymentSummary>();
            foreach (var a in list)
            {
                byAppointment.TryGetValue(a.Id, out var ps);
                var price = a.Service?.Price ?? 0;
                result[a.Id] = BuildSummary(price, ps ?? new List<Payment>(), CalculateDeposit(price));
            }
            return result;
        }

        /// <summary>
        /// Hoàn tiền toàn bộ các giao dịch đã thanh toán của lịch hẹn (khi lịch bị hủy).
        /// Không gọi SaveChanges: caller tự lưu cùng thay đổi trạng thái lịch hẹn.
        /// </summary>
        public async Task<decimal> RefundAllAsync(string appointmentId, string reason)
        {
            var paid = await _context.Payments
                .Where(p => p.AppointmentId == appointmentId && p.Status == Payment.StatusPaid)
                .ToListAsync();

            decimal total = 0;
            foreach (var p in paid)
            {
                p.Status = Payment.StatusRefunded;
                p.RefundedAt = DateTime.UtcNow;
                p.Note = reason;
                total += p.Amount;
            }
            return total;
        }
    }
}
