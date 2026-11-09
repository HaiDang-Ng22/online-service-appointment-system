using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Appointment.Models
{
    /// <summary>
    /// US-28: giao dịch thanh toán trực tuyến / đặt cọc cho một lịch hẹn.
    /// </summary>
    public class Payment
    {
        public const string TypeDeposit = "Deposit";
        public const string TypeFull = "Full";
        public const string TypeRemaining = "Remaining";

        public const string StatusPaid = "Paid";
        public const string StatusFailed = "Failed";
        public const string StatusRefunded = "Refunded";

        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        public string AppointmentId { get; set; } = string.Empty;
        [ForeignKey(nameof(AppointmentId))]
        public AppointmentModel? Appointment { get; set; }

        [Required]
        public string CustomerId { get; set; } = string.Empty;
        [ForeignKey(nameof(CustomerId))]
        public User? Customer { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>Deposit (đặt cọc), Full (thanh toán toàn bộ), Remaining (thanh toán phần còn lại)</summary>
        [Required]
        [StringLength(20)]
        public string Type { get; set; } = TypeDeposit;

        /// <summary>Card, EWallet, BankTransfer</summary>
        [Required]
        [StringLength(30)]
        public string Method { get; set; } = "Card";

        /// <summary>Paid, Failed, Refunded</summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = StatusPaid;

        [StringLength(60)]
        public string? TransactionCode { get; set; }

        [StringLength(250)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PaidAt { get; set; }

        public DateTime? RefundedAt { get; set; }
    }
}
