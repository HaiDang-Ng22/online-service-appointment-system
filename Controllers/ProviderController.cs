using System.Security.Claims;
using Appointment.Data;
using Appointment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Controllers
{
    [Authorize(Roles = "Provider,Admin")]
    public class ProviderController : Controller
    {
        private readonly AppDbContext _context;

        public ProviderController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Provider
        public async Task<IActionResult> Index(string? status)
        {
            string providerId = GetCurrentUserId();

            var query = _context.Appointments
                .Include(a => a.Service)
                .Include(a => a.Customer)
                .Where(a => a.ProviderId == providerId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            var appointments = await query.ToListAsync();

            var sortedAppointments = appointments
                .OrderByDescending(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .ToList();

            ViewBag.CurrentStatusFilter = status;
            ViewBag.PendingCount = await _context.Appointments.CountAsync(a => a.ProviderId == providerId && a.Status == "Pending");
            ViewBag.ConfirmedCount = await _context.Appointments.CountAsync(a => a.ProviderId == providerId && a.Status == "Confirmed");
            ViewBag.CompletedCount = await _context.Appointments.CountAsync(a => a.ProviderId == providerId && a.Status == "Completed");
            ViewBag.CancelledCount = await _context.Appointments.CountAsync(a => a.ProviderId == providerId && a.Status == "Cancelled");

            return View(sortedAppointments);
        }

        // GET: /Provider/Services
        public async Task<IActionResult> Services()
        {
            string providerId = GetCurrentUserId();
            var services = await _context.Services
                .Include(s => s.Category)
                .Where(s => s.ProviderId == providerId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return View(services);
        }

        // GET: /Provider/CreateService
        public async Task<IActionResult> CreateService()
        {
            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View();
        }

        // POST: /Provider/CreateService
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateService(Service model)
        {
            string providerId = GetCurrentUserId();
            model.Id = Guid.NewGuid().ToString("N");
            model.ProviderId = providerId;
            model.CreatedAt = DateTime.UtcNow;

            ModelState.Remove("Provider");
            ModelState.Remove("Category");

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _context.Categories.ToListAsync();
                return View(model);
            }

            _context.Services.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Tạo dịch vụ mới thành công!";
            return RedirectToAction(nameof(Services));
        }

        // GET: /Provider/EditService/abc
        public async Task<IActionResult> EditService(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            string providerId = GetCurrentUserId();
            var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == id && s.ProviderId == providerId);

            if (service == null) return NotFound();

            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View(service);
        }

        // POST: /Provider/EditService/abc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditService(string id, Service model)
        {
            string providerId = GetCurrentUserId();
            var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == id && s.ProviderId == providerId);

            if (service == null) return NotFound();

            service.Name = model.Name;
            service.CategoryId = model.CategoryId;
            service.Description = model.Description;
            service.Price = model.Price;
            service.DurationMinutes = model.DurationMinutes;
            service.Location = model.Location;
            service.IsActive = model.IsActive;

            _context.Services.Update(service);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật dịch vụ thành công!";
            return RedirectToAction(nameof(Services));
        }

        // POST: /Provider/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string appointmentId, string newStatus)
        {
            string providerId = GetCurrentUserId();
            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId && a.ProviderId == providerId);

            if (appointment == null) return NotFound();

            appointment.Status = newStatus;
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái cuộc hẹn thành '{newStatus}'.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Provider/OffDays
        public async Task<IActionResult> OffDays()
        {
            string providerId = GetCurrentUserId();
            var offDays = await _context.ProviderOffDays
                .Where(p => p.ProviderId == providerId)
                .OrderByDescending(p => p.OffDate)
                .ToListAsync();

            return View(offDays);
        }

        // POST: /Provider/AddOffDay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOffDay(DateTime offDate, string? reason)
        {
            string providerId = GetCurrentUserId();

            if (offDate < DateTime.Today)
            {
                TempData["ErrorMessage"] = "Ngày nghỉ không thể là một ngày trong quá khứ.";
                return RedirectToAction(nameof(OffDays));
            }

            bool exists = await _context.ProviderOffDays.AnyAsync(p => p.ProviderId == providerId && p.OffDate.Date == offDate.Date);
            if (exists)
            {
                TempData["ErrorMessage"] = "Ngày này đã được đăng ký làm ngày nghỉ trước đó.";
                return RedirectToAction(nameof(OffDays));
            }

            var item = new ProviderOffDay
            {
                Id = Guid.NewGuid().ToString("N"),
                ProviderId = providerId,
                OffDate = offDate.Date,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProviderOffDays.Add(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã đăng ký ngày nghỉ {offDate:dd/MM/yyyy} thành công!";
            return RedirectToAction(nameof(OffDays));
        }

        // POST: /Provider/DeleteOffDay/abc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOffDay(string id)
        {
            string providerId = GetCurrentUserId();
            var item = await _context.ProviderOffDays.FirstOrDefaultAsync(p => p.Id == id && p.ProviderId == providerId);

            if (item == null) return NotFound();

            _context.ProviderOffDays.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa lịch nghỉ thành công.";
            return RedirectToAction(nameof(OffDays));
        }

        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }
    }
}
