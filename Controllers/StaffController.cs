using System.Security.Claims;
using Appointment.Data;
using Appointment.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Controllers
{
    [Authorize(Roles = "Staff,Admin")]
    public class StaffController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PaymentService _paymentService;

        public StaffController(AppDbContext context, PaymentService paymentService)
        {
            _context = context;
            _paymentService = paymentService;
        }

        // GET: /Staff
        public async Task<IActionResult> Index(string? status, DateTime? date)
        {
            var query = _context.Appointments
                .Include(a => a.Service)
                .Include(a => a.Customer)
                .Include(a => a.Provider)
                .Include(a => a.Staff)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            var list = await query.ToListAsync();

            if (date.HasValue)
            {
                list = list.Where(a => a.AppointmentDate.Date == date.Value.Date).ToList();
            }

            var sortedList = list
                .OrderByDescending(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .ToList();

            ViewBag.StatusFilter = status;
            ViewBag.DateFilter = date?.ToString("yyyy-MM-dd");

            return View(sortedList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignStaff(string appointmentId)
        {
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(staffId)) return Unauthorized();

            var appointment = await _context.Appointments.FindAsync(appointmentId);
            if (appointment == null) return NotFound();

            appointment.StaffId = staffId;
            if (appointment.Status == "Pending")
            {
                appointment.Status = "Confirmed";
            }

            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã tiếp nhận xử lý cuộc hẹn này!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string appointmentId, string status)
        {
            var appointment = await _context.Appointments.FindAsync(appointmentId);
            if (appointment == null) return NotFound();

            // US-24: chỉ cho phép chuyển trạng thái hợp lệ
            if (!AppointmentRules.TryValidateTransition(appointment, status, out string transitionError))
            {
                TempData["ErrorMessage"] = transitionError;
                return RedirectToAction(nameof(Index));
            }

            if (status == AppointmentRules.Cancelled && appointment.Status != AppointmentRules.Cancelled)
            {
                appointment.CancelReason = "Nhân viên hủy lịch hẹn";
                await _paymentService.RefundAllAsync(appointment.Id, "Hoàn tiền do nhân viên hủy lịch");
            }

            appointment.Status = status;
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã chuyển trạng thái cuộc hẹn thành '{AppointmentRules.StatusLabel(status)}'.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMedicalRecord(string appointmentId, string? diagnosis, string? prescription, string? status)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null) return NotFound();

            // US-24: chỉ cho phép chuyển trạng thái hợp lệ
            if (!AppointmentRules.TryValidateTransition(appointment, status, out string transitionError))
            {
                TempData["ErrorMessage"] = transitionError;
                return RedirectToAction(nameof(Index));
            }

            appointment.Diagnosis = diagnosis?.Trim();
            appointment.Prescription = prescription?.Trim();
            if (!string.IsNullOrEmpty(status))
            {
                if (status == AppointmentRules.Cancelled && appointment.Status != AppointmentRules.Cancelled)
                {
                    appointment.CancelReason = "Nhân viên hủy lịch hẹn";
                    await _paymentService.RefundAllAsync(appointment.Id, "Hoàn tiền do nhân viên hủy lịch");
                }
                appointment.Status = status;
            }

            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật chẩn đoán/kết luận dịch vụ cho khách hàng {appointment.Customer?.FullName}!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Staff/UpdateInternalNote (US-42: Ghi chú nghiệp vụ chăm sóc khách hàng nội bộ)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInternalNote(string appointmentId, string? internalNote)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null) return NotFound();

            appointment.InternalNote = string.IsNullOrWhiteSpace(internalNote) ? null : internalNote.Trim();

            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật ghi chú nội bộ cho lịch hẹn #{appointment.Id.Substring(0, 8).ToUpper()} ({appointment.Customer?.FullName})!";
            return RedirectToAction(nameof(Index));
        }
    }
}
