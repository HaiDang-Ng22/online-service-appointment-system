using System.Security.Claims;
using Appointment.Data;
using Appointment.Models;
using Appointment.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Controllers
{
    [Authorize]
    public class AppointmentsController : Controller
    {
        private readonly AppDbContext _context;

        public AppointmentsController(AppDbContext context)
        {
            _context = context;
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

            return View(appointment);
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

            if (appointment.Status == "Completed")
            {
                TempData["ErrorMessage"] = "Không thể hủy lịch hẹn đã hoàn thành.";
                return RedirectToAction(nameof(Index));
            }

            appointment.Status = "Cancelled";
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã hủy lịch hẹn thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}
