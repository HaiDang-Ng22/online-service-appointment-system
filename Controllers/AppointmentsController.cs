using System.Security.Claims;
using Appointment.Data;
using Appointment.Infrastructure;
using Appointment.Models;
using Appointment.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Appointment.Controllers
{
    [Authorize]
    public class AppointmentsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PaymentService _paymentService;
        private readonly BookingRulesOptions _rules;
        private readonly IAuditService _audit;
        private readonly CheckInCodeService _checkInCodes;
        private readonly QrCodeService _qr;
        private readonly AppointmentSlipPdfService _slipPdf;

        public AppointmentsController(AppDbContext context, PaymentService paymentService, IOptions<BookingRulesOptions> rules,
            IAuditService audit, CheckInCodeService checkInCodes, QrCodeService qr, AppointmentSlipPdfService slipPdf)
        {
            _context = context;
            _paymentService = paymentService;
            _rules = rules.Value;
            _audit = audit;
            _checkInCodes = checkInCodes;
            _qr = qr;
            _slipPdf = slipPdf;
        }

        // GET: /Appointments
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var appointments = await _context.Appointments
                .Include(a => a.Service)
                .Include(a => a.Provider)
                .Include(a => a.Staff)
                .Where(a => a.CustomerId == userId)
                .ToListAsync();

            var sortedAppointments = appointments
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToList();

            // US-28 / US-30 / US-19: thông tin thanh toán, đánh giá & quyền hủy lịch cho từng dòng
            ViewBag.PaymentSummaries = await _paymentService.GetSummariesAsync(sortedAppointments);

            var appointmentIds = sortedAppointments.Select(a => a.Id).ToList();
            ViewBag.ReviewedIds = (await _context.Reviews.AsNoTracking()
                .Where(r => appointmentIds.Contains(r.AppointmentId))
                .Select(r => r.AppointmentId)
                .ToListAsync()).ToHashSet();

            ViewBag.CancelableIds = sortedAppointments
                .Where(a => AppointmentRules.CanCustomerCancel(a, _rules.CancelBeforeHours, out _))
                .Select(a => a.Id)
                .ToHashSet();
            ViewBag.CancelBeforeHours = _rules.CancelBeforeHours;

            return View(sortedAppointments);
        }

        // POST: /Appointments/Book
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(BookingViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var service = await _context.Services.FindAsync(model.ServiceId);
            if (service == null || !service.IsActive)
            {
                TempData["ErrorMessage"] = "Dịch vụ không tồn tại hoặc đã ngừng hoạt động.";
                return RedirectToAction("Index", "Services");
            }

            // Check if provider is off on selected date
            bool isOffDay = await _context.ProviderOffDays.AnyAsync(p => p.ProviderId == service.ProviderId && p.OffDate.Date == model.SelectedDate.Date);
            if (isOffDay)
            {
                TempData["ErrorMessage"] = "Nhà cung cấp / Cửa hàng nghỉ vào ngày này. Không thể đặt lịch!";
                return RedirectToAction("Details", "Services", new { id = model.ServiceId, selectedDate = model.SelectedDate.ToString("yyyy-MM-dd") });
            }

            if (!TimeSpan.TryParse(model.SelectedTimeSlot, out TimeSpan startTime))
            {
                TempData["ErrorMessage"] = "Vui lòng chọn khung giờ hợp lệ.";
                return RedirectToAction("Details", "Services", new { id = model.ServiceId, selectedDate = model.SelectedDate.ToString("yyyy-MM-dd") });
            }

            TimeSpan endTime = startTime.Add(TimeSpan.FromMinutes(service.DurationMinutes));

            // Không cho đặt vào thời điểm đã qua
            if (model.SelectedDate.Date + startTime <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "Khung giờ này đã qua. Vui lòng chọn khung giờ khác.";
                return RedirectToAction("Details", "Services", new { id = model.ServiceId, selectedDate = model.SelectedDate.ToString("yyyy-MM-dd") });
            }

            // US-14: chỉ cho đặt trong khung giờ làm việc theo tuần của nhà cung cấp
            var providerWorkingHours = await _context.WorkingHours
                .Where(w => w.ProviderId == service.ProviderId)
                .ToListAsync();

            if (providerWorkingHours.Any())
            {
                var dayConfig = providerWorkingHours.FirstOrDefault(w => w.DayOfWeek == model.SelectedDate.DayOfWeek && w.IsAvailable);
                if (dayConfig == null || startTime < dayConfig.StartTime || endTime > dayConfig.EndTime)
                {
                    TempData["ErrorMessage"] = "Khung giờ này nằm ngoài giờ làm việc của nhà cung cấp. Vui lòng chọn khung giờ khác.";
                    return RedirectToAction("Details", "Services", new { id = model.ServiceId, selectedDate = model.SelectedDate.ToString("yyyy-MM-dd") });
                }
            }

            var existingAppointments = await _context.Appointments
                .Where(a => a.ProviderId == service.ProviderId && a.Status != "Cancelled")
                .ToListAsync();

            bool isOverlap = existingAppointments.Any(a =>
                a.AppointmentDate.Date == model.SelectedDate.Date
                && a.StartTime < endTime
                && a.EndTime > startTime);

            if (isOverlap)
            {
                TempData["ErrorMessage"] = "Khung giờ này vừa được người khác đặt trước. Vui lòng chọn khung giờ khác.";
                return RedirectToAction("Details", "Services", new { id = model.ServiceId, selectedDate = model.SelectedDate.ToString("yyyy-MM-dd") });
            }

            var appointment = new AppointmentModel
            {
                Id = Guid.NewGuid().ToString("N"),
                CustomerId = userId,
                ServiceId = service.Id,
                ProviderId = service.ProviderId,
                AppointmentDate = model.SelectedDate.Date,
                StartTime = startTime,
                EndTime = endTime,
                Status = "Pending",
                Notes = model.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(AuditActions.AppointmentCreated,
                $"Đặt lịch '{service.Name}' ngày {appointment.AppointmentDate:dd/MM/yyyy} lúc {startTime:hh\\:mm}.",
                "Appointment", appointment.Id);

            // US-28: chuyển sang bước thanh toán / đặt cọc (khách có thể chọn "thanh toán sau")
            if (service.Price > 0)
            {
                TempData["SuccessMessage"] = $"Đặt lịch thành công! Hãy đặt cọc {_paymentService.DepositPercent}% để giữ chỗ (hoặc thanh toán sau). Lịch sẽ chờ nhà cung cấp xác nhận.";
                return RedirectToAction("Checkout", "Payments", new { id = appointment.Id });
            }

            TempData["SuccessMessage"] = "Đặt lịch thành công! Vui lòng chờ nhà cung cấp hoặc nhân viên xác nhận.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Appointments/Details/abc
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var appointment = await _context.Appointments
                .Include(a => a.Service)
                    .ThenInclude(s => s!.Category)
                .Include(a => a.Provider)
                .Include(a => a.Customer)
                .Include(a => a.Staff)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            string role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (role == "Customer" && appointment.CustomerId != userId)
            {
                return Forbid();
            }

            // US-28: thanh toán; US-30: đánh giá; US-19: quyền hủy lịch
            ViewBag.PaymentSummary = await _paymentService.GetSummaryAsync(appointment);
            ViewBag.Payments = await _context.Payments.AsNoTracking()
                .Where(p => p.AppointmentId == appointment.Id)
                .ToListAsync();
            ViewBag.Review = await _context.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.AppointmentId == appointment.Id);
            ViewBag.CanCancel = AppointmentRules.CanCustomerCancel(appointment, _rules.CancelBeforeHours, out string cancelError);
            ViewBag.CancelError = cancelError;
            ViewBag.CancelBeforeHours = _rules.CancelBeforeHours;

            return View(appointment);
        }

        // GET: /Appointments/PrintInvoice/abc
        public async Task<IActionResult> PrintInvoice(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var appointment = await _context.Appointments
                .Include(a => a.Service)
                    .ThenInclude(s => s!.Category)
                .Include(a => a.Provider)
                .Include(a => a.Customer)
                .Include(a => a.Staff)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            string role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            // Customer can only view their own invoice; Provider can only view their own; Staff and Admin can view any
            if (role == "Customer" && appointment.CustomerId != userId) return Forbid();
            if (role == "Provider" && appointment.ProviderId != userId) return Forbid();

            return View(appointment);
        }

        // =====================================================================
        // US-32: Mã QR check-in & xuất phiếu hẹn PDF
        // =====================================================================

        // GET: /Appointments/QrCode/abc  -> ảnh PNG mã QR check-in có chữ ký
        [HttpGet]
        public async Task<IActionResult> QrCode(string id)
        {
            var (appointment, error) = await LoadForSlipAsync(id);
            if (appointment == null) return error!;

            if (appointment.Status == AppointmentRules.Cancelled)
            {
                return NotFound(); // lịch đã hủy không còn mã check-in
            }

            var png = _qr.GeneratePng(_checkInCodes.BuildPayload(appointment.Id), 10);

            // Mã QR chứa thông tin của lịch hẹn nên không cho trình duyệt / proxy lưu cache
            Response.Headers["Cache-Control"] = "private, no-store";
            return File(png, "image/png");
        }

        // GET: /Appointments/DownloadSlip/abc  -> tải phiếu hẹn PDF
        [HttpGet]
        public async Task<IActionResult> DownloadSlip(string id)
        {
            var (appointment, error) = await LoadForSlipAsync(id);
            if (appointment == null) return error!;

            var summary = await _paymentService.GetSummaryAsync(appointment);
            byte[]? qr = appointment.Status == AppointmentRules.Cancelled
                ? null
                : _qr.GeneratePng(_checkInCodes.BuildPayload(appointment.Id), 8);

            string shortCode = CheckInCodeService.ShortCode(appointment.Id);
            byte[] pdf;
            try
            {
                pdf = _slipPdf.Generate(appointment, summary, qr, shortCode);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Không thể tạo phiếu hẹn PDF: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }

            await _audit.LogAsync(AuditActions.AppointmentSlipDownloaded,
                $"Tải phiếu hẹn PDF {shortCode}.", "Appointment", appointment.Id);

            Response.Headers["Cache-Control"] = "private, no-store";
            return File(pdf, "application/pdf", $"PhieuHen-{shortCode}.pdf");
        }

        /// <summary>Nạp lịch hẹn kèm phân quyền: Customer chỉ xem của mình, Provider chỉ xem của mình, Staff/Admin xem tất cả.</summary>
        private async Task<(AppointmentModel? Appointment, IActionResult? Error)> LoadForSlipAsync(string? id)
        {
            if (string.IsNullOrEmpty(id)) return (null, NotFound());

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return (null, Unauthorized());

            var appointment = await _context.Appointments
                .Include(a => a.Service)
                .Include(a => a.Provider)
                .Include(a => a.Customer)
                .Include(a => a.Staff)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return (null, NotFound());

            string role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (role == "Customer" && appointment.CustomerId != userId) return (null, Forbid());
            if (role == "Provider" && appointment.ProviderId != userId) return (null, Forbid());

            return (appointment, null);
        }

        // POST: /Appointments/Cancel/abc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound();

            string role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (role == "Customer" && appointment.CustomerId != userId) return Forbid();
            if (role == "Provider" && appointment.ProviderId != userId) return Forbid();

            if (appointment.Status == "Completed")
            {
                TempData["ErrorMessage"] = "Không thể hủy lịch hẹn đã hoàn thành.";
                return RedirectToAction(nameof(Index));
            }

            // US-19: khách hàng chỉ được hủy trước giờ phục vụ (theo BookingRules:CancelBeforeHours)
            if (role == "Customer")
            {
                if (!AppointmentRules.CanCustomerCancel(appointment, _rules.CancelBeforeHours, out string cancelError))
                {
                    TempData["ErrorMessage"] = cancelError;
                    return RedirectToAction(nameof(Index));
                }
            }
            else if (appointment.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = "Lịch hẹn này đã được hủy trước đó.";
                return RedirectToAction(nameof(Index));
            }

            appointment.Status = "Cancelled";
            appointment.CancelReason = role == "Customer" ? "Khách hàng chủ động hủy lịch" : $"Hủy bởi {role}";

            // US-28: hoàn lại các khoản đã thanh toán / đặt cọc
            decimal refunded = await _paymentService.RefundAllAsync(appointment.Id, "Hoàn tiền do lịch hẹn bị hủy");

            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(AuditActions.AppointmentCancelled,
                $"{(role == "Customer" ? "Khách hàng" : role)} hủy lịch hẹn #{appointment.Id[..8].ToUpper()}." + (refunded > 0 ? $" Hoàn {refunded:N0} VNĐ." : string.Empty),
                "Appointment", appointment.Id, severity: AuditLog.SeverityWarning);
            if (refunded > 0)
            {
                await _audit.LogAsync(AuditActions.PaymentRefunded,
                    $"Hoàn {refunded:N0} VNĐ do hủy lịch hẹn #{appointment.Id[..8].ToUpper()}.", "Appointment", appointment.Id);
            }

            TempData["SuccessMessage"] = refunded > 0
                ? $"Đã hủy lịch hẹn thành công. Số tiền {refunded:N0} VNĐ đã thanh toán sẽ được hoàn lại."
                : "Đã hủy lịch hẹn thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}
