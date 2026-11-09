using System.Security.Claims;
using Appointment.Data;
using Appointment.Infrastructure;
using Appointment.Models;
using Appointment.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Controllers
{
    /// <summary>US-28: Thanh toán trực tuyến & đặt cọc cho lịch hẹn.</summary>
    [Authorize(Roles = "Customer")]
    public class PaymentsController : Controller
    {
        private static readonly string[] AllowedMethods = { "Card", "EWallet", "BankTransfer" };

        private readonly AppDbContext _context;
        private readonly PaymentService _paymentService;
        private readonly IPaymentGateway _gateway;

        public PaymentsController(AppDbContext context, PaymentService paymentService, IPaymentGateway gateway)
        {
            _context = context;
            _paymentService = paymentService;
            _gateway = gateway;
        }

        // GET: /Payments/Checkout/abc
        [HttpGet]
        public async Task<IActionResult> Checkout(string id)
        {
            var appointment = await LoadOwnAppointmentAsync(id);
            if (appointment == null) return NotFound();

            if (appointment.Status == AppointmentRules.Cancelled)
            {
                TempData["ErrorMessage"] = "Lịch hẹn đã bị hủy nên không thể thanh toán.";
                return RedirectToAction("Index", "Appointments");
            }

            return View(await BuildViewModelAsync(appointment));
        }

        // POST: /Payments/Pay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(string id, string paymentType, string method)
        {
            var appointment = await LoadOwnAppointmentAsync(id);
            if (appointment == null) return NotFound();

            if (appointment.Status == AppointmentRules.Cancelled)
            {
                TempData["ErrorMessage"] = "Lịch hẹn đã bị hủy nên không thể thanh toán.";
                return RedirectToAction("Index", "Appointments");
            }

            if (!AllowedMethods.Contains(method))
            {
                TempData["ErrorMessage"] = "Vui lòng chọn phương thức thanh toán hợp lệ.";
                return RedirectToAction(nameof(Checkout), new { id });
            }

            var summary = await _paymentService.GetSummaryAsync(appointment);
            if (summary.Remaining <= 0)
            {
                TempData["SuccessMessage"] = "Lịch hẹn này đã được thanh toán đầy đủ.";
                return RedirectToAction("Details", "Appointments", new { id });
            }

            decimal amount;
            string type;
            if (paymentType == Payment.TypeDeposit && summary.Paid <= 0 && summary.DepositAmount < summary.Remaining)
            {
                amount = summary.DepositAmount;
                type = Payment.TypeDeposit;
            }
            else
            {
                amount = summary.Remaining;
                type = summary.Paid > 0 ? Payment.TypeRemaining : Payment.TypeFull;
            }

            string orderRef = $"APX-{appointment.Id[..8].ToUpper()}";
            var result = await _gateway.ChargeAsync(amount, method, orderRef);

            var payment = new Payment
            {
                Id = Guid.NewGuid().ToString("N"),
                AppointmentId = appointment.Id,
                CustomerId = appointment.CustomerId,
                Amount = amount,
                Type = type,
                Method = method,
                Status = result.Success ? Payment.StatusPaid : Payment.StatusFailed,
                TransactionCode = result.TransactionCode,
                Note = result.Success ? (_gateway.IsSandbox ? "Giao dịch mô phỏng (sandbox)" : null) : result.Error,
                CreatedAt = DateTime.UtcNow,
                PaidAt = result.Success ? DateTime.UtcNow : null
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            if (!result.Success)
            {
                TempData["ErrorMessage"] = $"Thanh toán thất bại: {result.Error ?? "không xác định"}. Vui lòng thử lại.";
                return RedirectToAction(nameof(Checkout), new { id });
            }

            TempData["SuccessMessage"] = type == Payment.TypeDeposit
                ? $"Đặt cọc {amount:N0} VNĐ thành công! Mã giao dịch: {payment.TransactionCode}"
                : $"Thanh toán {amount:N0} VNĐ thành công! Mã giao dịch: {payment.TransactionCode}";
            return RedirectToAction("Details", "Appointments", new { id });
        }

        private async Task<AppointmentModel?> LoadOwnAppointmentAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return null;

            return await _context.Appointments
                .Include(a => a.Service)
                .Include(a => a.Provider)
                .FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == userId);
        }

        private async Task<CheckoutViewModel> BuildViewModelAsync(AppointmentModel appointment)
        {
            var history = await _context.Payments.AsNoTracking()
                .Where(p => p.AppointmentId == appointment.Id)
                .ToListAsync();

            return new CheckoutViewModel
            {
                Appointment = appointment,
                Summary = await _paymentService.GetSummaryAsync(appointment),
                DepositPercent = _paymentService.DepositPercent,
                IsSandbox = _gateway.IsSandbox,
                History = history.OrderByDescending(p => p.CreatedAt).ToList()
            };
        }
    }
}
