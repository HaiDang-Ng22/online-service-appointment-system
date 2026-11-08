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
    [Authorize(Roles = "Provider,Admin")]
    public class ProviderController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PaymentService _paymentService;

        public ProviderController(AppDbContext context, PaymentService paymentService)
        {
            _context = context;
            _paymentService = paymentService;
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
            try
            {
                string providerId = GetCurrentUserId();
                if (string.IsNullOrEmpty(providerId))
                {
                    TempData["ErrorMessage"] = "Phiên làm việc hết hạn hoặc không tìm thấy thông tin đối tác.";
                    return RedirectToAction("Login", "Account");
                }

                model.Id = Guid.NewGuid().ToString("N");
                model.ProviderId = providerId;
                model.CreatedAt = DateTime.UtcNow;

                // Remove navigation and auto-populated properties from validation
                ModelState.Remove(nameof(model.Provider));
                ModelState.Remove(nameof(model.Category));
                ModelState.Remove(nameof(model.ProviderId));
                ModelState.Remove(nameof(model.Id));
                ModelState.Remove(nameof(model.Appointments));

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    ViewBag.Categories = await _context.Categories.ToListAsync();
                    TempData["ErrorMessage"] = "Vui lòng kiểm tra lại: " + string.Join("; ", errors);
                    return View(model);
                }

                _context.Services.Add(model);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Tạo dịch vụ '{model.Name}' thành công!";
                return RedirectToAction(nameof(Services));
            }
            catch (Exception ex)
            {
                ViewBag.Categories = await _context.Categories.ToListAsync();
                TempData["ErrorMessage"] = $"Lỗi khi thêm dịch vụ: {ex.Message}";
                return View(model);
            }
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
            try
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
            catch (Exception ex)
            {
                ViewBag.Categories = await _context.Categories.ToListAsync();
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật dịch vụ: {ex.Message}";
                return View(model);
            }
        }

        // POST: /Provider/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string appointmentId, string newStatus)
        {
            string providerId = GetCurrentUserId();
            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId && a.ProviderId == providerId);

            if (appointment == null) return NotFound();

            // US-21: chỉ cho phép chuyển trạng thái hợp lệ
            if (!AppointmentRules.TryValidateTransition(appointment, newStatus, out string transitionError))
            {
                TempData["ErrorMessage"] = transitionError;
                return RedirectToAction(nameof(Index));
            }

            string? refundNote = null;
            if (newStatus == AppointmentRules.Cancelled && appointment.Status != AppointmentRules.Cancelled)
            {
                appointment.CancelReason = "Nhà cung cấp hủy lịch hẹn";
                decimal refunded = await _paymentService.RefundAllAsync(appointment.Id, "Hoàn tiền do nhà cung cấp hủy lịch");
                if (refunded > 0) refundNote = $" Đã hoàn lại {refunded:N0} VNĐ cho khách hàng.";
            }

            appointment.Status = newStatus;
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái cuộc hẹn thành '{AppointmentRules.StatusLabel(newStatus)}'.{refundNote}";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Provider/UpdateMedicalRecord
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMedicalRecord(string appointmentId, string? diagnosis, string? prescription, string? status)
        {
            try
            {
                string providerId = GetCurrentUserId();
                var appointment = await _context.Appointments
                    .Include(a => a.Customer)
                    .FirstOrDefaultAsync(a => a.Id == appointmentId && a.ProviderId == providerId);

                if (appointment == null) return NotFound();

                // US-21: chỉ cho phép chuyển trạng thái hợp lệ
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
                        appointment.CancelReason = "Nhà cung cấp hủy lịch hẹn";
                        await _paymentService.RefundAllAsync(appointment.Id, "Hoàn tiền do nhà cung cấp hủy lịch");
                    }
                    appointment.Status = status;
                }

                _context.Appointments.Update(appointment);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã cập nhật hồ sơ chẩn đoán/kết luận cho khách hàng {appointment.Customer?.FullName}!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi lưu chẩn đoán: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
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
            try
            {
                string providerId = GetCurrentUserId();
                if (string.IsNullOrEmpty(providerId))
                {
                    TempData["ErrorMessage"] = "Phiên làm việc hết hạn. Vui lòng đăng nhập lại.";
                    return RedirectToAction("Login", "Account");
                }

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
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi đăng ký ngày nghỉ: {ex.Message}";
                return RedirectToAction(nameof(OffDays));
            }
        }

        // POST: /Provider/DeleteOffDay/abc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOffDay(string id)
        {
            try
            {
                string providerId = GetCurrentUserId();
                var item = await _context.ProviderOffDays.FirstOrDefaultAsync(p => p.Id == id && p.ProviderId == providerId);

                if (item == null) return NotFound();

                _context.ProviderOffDays.Remove(item);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã xóa lịch nghỉ thành công.";
                return RedirectToAction(nameof(OffDays));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa ngày nghỉ: {ex.Message}";
                return RedirectToAction(nameof(OffDays));
            }
        }

        // =====================================================================
        // US-14: Cấu hình khung giờ làm việc theo tuần
        // =====================================================================

        // Thứ tự hiển thị: Thứ 2 -> Chủ nhật
        private static readonly DayOfWeek[] WeekOrder =
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };

        // GET: /Provider/WorkingHours
        [HttpGet]
        public async Task<IActionResult> WorkingHours()
        {
            string providerId = GetCurrentUserId();
            var existing = await _context.WorkingHours.Where(w => w.ProviderId == providerId).ToListAsync();

            var model = new WorkingHoursViewModel();
            foreach (var day in WeekOrder)
            {
                var row = existing.FirstOrDefault(w => w.DayOfWeek == day);
                model.Days.Add(new WorkingHourDayInput
                {
                    Day = day,
                    // Chưa cấu hình ngày nào -> gợi ý mặc định Thứ 2 - Thứ 7
                    IsAvailable = row != null ? row.IsAvailable : (!existing.Any() && day != DayOfWeek.Sunday),
                    StartTime = row?.StartTime ?? new TimeSpan(8, 0, 0),
                    EndTime = row?.EndTime ?? new TimeSpan(17, 0, 0)
                });
            }

            return View(model);
        }

        // POST: /Provider/WorkingHours
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WorkingHours(WorkingHoursViewModel model)
        {
            string providerId = GetCurrentUserId();
            if (string.IsNullOrEmpty(providerId)) return RedirectToAction("Login", "Account");

            var days = model.Days ?? new List<WorkingHourDayInput>();

            // Chuẩn hóa: đủ 7 ngày, không trùng
            var normalized = WeekOrder
                .Select(d => days.FirstOrDefault(x => x.Day == d) ?? new WorkingHourDayInput { Day = d, IsAvailable = false })
                .ToList();

            var errors = new List<string>();

            if (!normalized.Any(d => d.IsAvailable))
            {
                errors.Add("Vui lòng bật ít nhất một ngày làm việc trong tuần.");
            }

            foreach (var d in normalized.Where(d => d.IsAvailable))
            {
                string name = DayName(d.Day);
                if (d.StartTime < TimeSpan.Zero || d.StartTime >= TimeSpan.FromHours(24) ||
                    d.EndTime <= TimeSpan.Zero || d.EndTime > TimeSpan.FromHours(24))
                {
                    errors.Add($"{name}: giờ làm việc không hợp lệ.");
                }
                else if (d.EndTime <= d.StartTime)
                {
                    errors.Add($"{name}: giờ kết thúc phải sau giờ bắt đầu.");
                }
                else if ((d.EndTime - d.StartTime) < TimeSpan.FromMinutes(30))
                {
                    errors.Add($"{name}: khoảng thời gian làm việc tối thiểu là 30 phút.");
                }
            }

            if (errors.Any())
            {
                TempData["ErrorMessage"] = string.Join(" ", errors);
                model.Days = normalized;
                return View(model);
            }

            var existing = await _context.WorkingHours.Where(w => w.ProviderId == providerId).ToListAsync();

            foreach (var d in normalized)
            {
                var row = existing.FirstOrDefault(w => w.DayOfWeek == d.Day);
                if (row == null)
                {
                    _context.WorkingHours.Add(new WorkingHour
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        ProviderId = providerId,
                        DayOfWeek = d.Day,
                        StartTime = d.StartTime,
                        EndTime = d.EndTime,
                        IsAvailable = d.IsAvailable
                    });
                }
                else
                {
                    row.IsAvailable = d.IsAvailable;
                    row.StartTime = d.StartTime;
                    row.EndTime = d.EndTime;
                }
            }

            await _context.SaveChangesAsync();

            // Cảnh báo các lịch hẹn tương lai đã đặt nhưng nằm ngoài giờ làm việc mới
            var upcoming = await _context.Appointments
                .Where(a => a.ProviderId == providerId && (a.Status == "Pending" || a.Status == "Confirmed"))
                .ToListAsync();

            int conflicts = upcoming.Count(a =>
            {
                if (a.AppointmentDate.Date < DateTime.Today) return false;
                var cfg = normalized.First(x => x.Day == a.AppointmentDate.DayOfWeek);
                return !cfg.IsAvailable || a.StartTime < cfg.StartTime || a.EndTime > cfg.EndTime;
            });

            TempData["SuccessMessage"] = "Đã lưu khung giờ làm việc theo tuần. Khách hàng sẽ chỉ thấy các khung giờ trống trong thời gian này.";
            if (conflicts > 0)
            {
                TempData["ErrorMessage"] = $"Lưu ý: có {conflicts} lịch hẹn đã đặt nằm ngoài khung giờ mới. Lịch hiện có vẫn được giữ nguyên, bạn hãy liên hệ khách hàng nếu cần đổi.";
            }

            return RedirectToAction(nameof(WorkingHours));
        }

        private static string DayName(DayOfWeek day) => day switch
        {
            DayOfWeek.Monday => "Thứ 2",
            DayOfWeek.Tuesday => "Thứ 3",
            DayOfWeek.Wednesday => "Thứ 4",
            DayOfWeek.Thursday => "Thứ 5",
            DayOfWeek.Friday => "Thứ 6",
            DayOfWeek.Saturday => "Thứ 7",
            _ => "Chủ nhật"
        };

        // =====================================================================
        // US-30: Xem đánh giá của khách hàng về dịch vụ của mình
        // =====================================================================

        // GET: /Provider/Reviews
        [HttpGet]
        public async Task<IActionResult> Reviews()
        {
            string providerId = GetCurrentUserId();

            var reviews = await _context.Reviews
                .AsNoTracking()
                .Include(r => r.Service)
                .Include(r => r.Customer)
                .Where(r => r.Service != null && r.Service.ProviderId == providerId)
                .ToListAsync();

            return View(reviews.OrderByDescending(r => r.CreatedAt).ToList());
        }

        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }
    }
}
