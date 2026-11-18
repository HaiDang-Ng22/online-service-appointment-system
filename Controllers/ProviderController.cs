using System.Security.Claims;
using Appointment.Data;
using Appointment.Helpers;
using Appointment.Infrastructure;
using Appointment.Models;
using Appointment.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace Appointment.Controllers
{
    [Authorize(Roles = "Provider,Admin")]
    public class ProviderController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PaymentService _paymentService;
        private readonly IAuditService _audit;

        public ProviderController(AppDbContext context, PaymentService paymentService, IAuditService audit)
        {
            _context = context;
            _paymentService = paymentService;
            _audit = audit;
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

                await _audit.LogAsync(AuditActions.ServiceCreated,
                    $"Tạo dịch vụ '{model.Name}' (giá {model.Price:N0} VNĐ, {model.DurationMinutes} phút).", "Service", model.Id);

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

                await _audit.LogAsync(AuditActions.ServiceUpdated,
                    $"Sửa dịch vụ '{service.Name}' (giá {service.Price:N0} VNĐ, {(service.IsActive ? "đang hoạt động" : "tạm ngưng")}).", "Service", service.Id);

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
            decimal refundedAmount = 0;
            string oldStatus = appointment.Status;
            if (newStatus == AppointmentRules.Cancelled && appointment.Status != AppointmentRules.Cancelled)
            {
                appointment.CancelReason = "Nhà cung cấp hủy lịch hẹn";
                refundedAmount = await _paymentService.RefundAllAsync(appointment.Id, "Hoàn tiền do nhà cung cấp hủy lịch");
                if (refundedAmount > 0) refundNote = $" Đã hoàn lại {refundedAmount:N0} VNĐ cho khách hàng.";
            }

            appointment.Status = newStatus;
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(AuditActions.AppointmentStatusChanged,
                $"Provider đổi trạng thái lịch hẹn #{appointment.Id[..8].ToUpper()}: {AppointmentRules.StatusLabel(oldStatus)} → {AppointmentRules.StatusLabel(newStatus)}.",
                "Appointment", appointment.Id,
                severity: newStatus == AppointmentRules.Cancelled ? AuditLog.SeverityWarning : null);
            if (refundedAmount > 0)
            {
                await _audit.LogAsync(AuditActions.PaymentRefunded,
                    $"Hoàn {refundedAmount:N0} VNĐ do Provider hủy lịch #{appointment.Id[..8].ToUpper()}.", "Appointment", appointment.Id);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái cuộc hẹn thành '{AppointmentRules.StatusLabel(newStatus)}'.{refundNote}";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Provider/UpdateMedicalRecord
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMedicalRecord(string appointmentId, string? diagnosis, string? prescription, string? status, string? internalNote = null)
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

                string oldStatus = appointment.Status;
                decimal refundedAmount = 0;
                appointment.Diagnosis = diagnosis?.Trim();
                appointment.Prescription = prescription?.Trim();
                if (internalNote != null)
                {
                    appointment.InternalNote = string.IsNullOrWhiteSpace(internalNote) ? null : internalNote.Trim();
                }
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == AppointmentRules.Cancelled && appointment.Status != AppointmentRules.Cancelled)
                    {
                        appointment.CancelReason = "Nhà cung cấp hủy lịch hẹn";
                        refundedAmount = await _paymentService.RefundAllAsync(appointment.Id, "Hoàn tiền do nhà cung cấp hủy lịch");
                    }
                    appointment.Status = status;
                }

                _context.Appointments.Update(appointment);
                await _context.SaveChangesAsync();

                await _audit.LogAsync(AuditActions.AppointmentRecordUpdated,
                    $"Provider cập nhật hồ sơ chẩn đoán / ghi chú lịch hẹn #{appointment.Id[..8].ToUpper()}.", "Appointment", appointment.Id);
                if (appointment.Status != oldStatus)
                {
                    await _audit.LogAsync(AuditActions.AppointmentStatusChanged,
                        $"Provider đổi trạng thái lịch hẹn #{appointment.Id[..8].ToUpper()}: {AppointmentRules.StatusLabel(oldStatus)} → {AppointmentRules.StatusLabel(appointment.Status)}.",
                        "Appointment", appointment.Id,
                        severity: appointment.Status == AppointmentRules.Cancelled ? AuditLog.SeverityWarning : null);
                }
                if (refundedAmount > 0)
                {
                    await _audit.LogAsync(AuditActions.PaymentRefunded,
                        $"Hoàn {refundedAmount:N0} VNĐ do Provider hủy lịch #{appointment.Id[..8].ToUpper()}.", "Appointment", appointment.Id);
                }

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

                await _audit.LogAsync(AuditActions.OffDayAdded, $"Đăng ký ngày nghỉ {offDate:dd/MM/yyyy}.", "ProviderOffDay", item.Id);

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

                var removedDate = item.OffDate;
                _context.ProviderOffDays.Remove(item);
                await _context.SaveChangesAsync();

                await _audit.LogAsync(AuditActions.OffDayDeleted, $"Xóa ngày nghỉ {removedDate:dd/MM/yyyy}.", "ProviderOffDay", id);

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

            await _audit.LogAsync(AuditActions.WorkingHoursUpdated,
                "Cập nhật khung giờ làm việc theo tuần: " + string.Join("; ", normalized.Where(d => d.IsAvailable)
                    .Select(d => $"{DayName(d.Day)} {d.StartTime:hh\\:mm}-{d.EndTime:hh\\:mm}")) + ".",
                "WorkingHour", providerId);

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


        // =====================================================================
        // US-38: Thống kê doanh thu & hiệu suất kinh doanh cho Provider
        // =====================================================================

        private static readonly string[] WeekdayNames = { "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ nhật" };

        // GET: /Provider/Statistics
        [HttpGet]
        public async Task<IActionResult> Statistics(string? range, DateTime? from, DateTime? to, string? serviceId, string? providerId)
        {
            var model = await BuildStatisticsAsync(range, from, to, serviceId, providerId);
            return View(model);
        }

        // GET: /Provider/ExportStatistics  (CSV chi tiết từng lịch hẹn trong kỳ, mở được bằng Excel)
        [HttpGet]
        public async Task<IActionResult> ExportStatistics(string? range, DateTime? from, DateTime? to, string? serviceId, string? providerId)
        {
            string? scope = ResolveScopeProviderId(providerId);
            var (key, f, t) = ResolveRange(range, from, to);
            var toExclusive = t.AddDays(1);

            IQueryable<AppointmentModel> q = _context.Appointments.AsNoTracking();
            if (scope != null) q = q.Where(a => a.ProviderId == scope);
            if (!string.IsNullOrWhiteSpace(serviceId)) q = q.Where(a => a.ServiceId == serviceId);

            var list = await q.Include(a => a.Service).Include(a => a.Customer)
                .Where(a => a.AppointmentDate >= f && a.AppointmentDate < toExclusive)
                .ToListAsync();
            list = list.OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime).ToList();

            var ids = list.Select(a => a.Id).ToList();
            var payments = ids.Count == 0
                ? new List<Payment>()
                : await _context.Payments.AsNoTracking().Where(p => ids.Contains(p.AppointmentId)).ToListAsync();
            var payByAppt = payments.GroupBy(p => p.AppointmentId).ToDictionary(g => g.Key, g => g.ToList());

            decimal realized = list.Where(a => a.Status == AppointmentRules.Completed).Sum(a => a.Service?.Price ?? 0m);
            decimal paidNet = payments.Where(p => p.Status == Payment.StatusPaid).Sum(p => p.Amount);

            string E(string? v) => CsvHelper.Escape(v);
            var sb = new StringBuilder();
            sb.AppendLine(E("BÁO CÁO DOANH THU & HIỆU SUẤT"));
            sb.AppendLine(E("Kỳ báo cáo") + "," + E($"{f:dd/MM/yyyy} - {t:dd/MM/yyyy}"));
            sb.AppendLine(E("Tổng số lịch hẹn") + "," + list.Count);
            sb.AppendLine(E("Doanh thu ghi nhận (lịch hoàn thành, VNĐ)") + "," + realized.ToString("0", CultureInfo.InvariantCulture));
            sb.AppendLine(E("Đã thu online - còn hiệu lực (VNĐ)") + "," + paidNet.ToString("0", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine(string.Join(",", new[]
            {
                "Mã lịch", "Ngày", "Bắt đầu", "Kết thúc", "Dịch vụ", "Khách hàng", "SĐT", "Trạng thái",
                "Giá dịch vụ (VNĐ)", "Doanh thu ghi nhận (VNĐ)", "Đã thanh toán (VNĐ)", "Đã hoàn (VNĐ)"
            }.Select(E)));

            foreach (var a in list)
            {
                decimal price = a.Service?.Price ?? 0m;
                payByAppt.TryGetValue(a.Id, out var ps);
                decimal paid = ps?.Where(p => p.Status == Payment.StatusPaid).Sum(p => p.Amount) ?? 0m;
                decimal refunded = ps?.Where(p => p.Status == Payment.StatusRefunded).Sum(p => p.Amount) ?? 0m;

                sb.AppendLine(string.Join(",", new[]
                {
                    E("APX-" + a.Id[..8].ToUpper()),
                    E(a.AppointmentDate.ToString("dd/MM/yyyy")),
                    E(a.StartTime.ToString(@"hh\:mm")),
                    E(a.EndTime.ToString(@"hh\:mm")),
                    E(a.Service?.Name),
                    E(a.Customer?.FullName),
                    E(a.Customer?.PhoneNumber),
                    E(AppointmentRules.StatusLabel(a.Status)),
                    price.ToString("0", CultureInfo.InvariantCulture),
                    (a.Status == AppointmentRules.Completed ? price : 0m).ToString("0", CultureInfo.InvariantCulture),
                    paid.ToString("0", CultureInfo.InvariantCulture),
                    refunded.ToString("0", CultureInfo.InvariantCulture)
                }));
            }

            await _audit.LogAsync(AuditActions.StatsExported,
                $"Xuất báo cáo doanh thu {f:dd/MM/yyyy} - {t:dd/MM/yyyy} ({list.Count} lịch hẹn).", "Report", null);

            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(sb.ToString());
            return File(bytes, "text/csv; charset=utf-8", $"bao-cao-doanh-thu-{f:yyyyMMdd}-{t:yyyyMMdd}.csv");
        }

        private bool IsAdminUser => User.IsInRole("Admin");

        /// <summary>Provider chỉ xem số liệu của chính mình; Admin xem một Provider bất kỳ hoặc toàn hệ thống (null).</summary>
        private string? ResolveScopeProviderId(string? requested)
        {
            if (IsAdminUser) return string.IsNullOrWhiteSpace(requested) ? null : requested;
            return GetCurrentUserId();
        }

        private static (string Key, DateTime From, DateTime To) ResolveRange(string? range, DateTime? from, DateTime? to)
        {
            var today = DateTime.Today;
            string key = string.IsNullOrWhiteSpace(range) ? "30d" : range.Trim().ToLowerInvariant();
            DateTime f, t;

            switch (key)
            {
                case "today":
                    f = today; t = today; break;
                case "7d":
                    f = today.AddDays(-6); t = today; break;
                case "month":
                    f = new DateTime(today.Year, today.Month, 1); t = f.AddMonths(1).AddDays(-1); break;
                case "lastmonth":
                    f = new DateTime(today.Year, today.Month, 1).AddMonths(-1); t = f.AddMonths(1).AddDays(-1); break;
                case "quarter":
                    f = new DateTime(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1); t = f.AddMonths(3).AddDays(-1); break;
                case "year":
                    f = new DateTime(today.Year, 1, 1); t = new DateTime(today.Year, 12, 31); break;
                case "custom":
                    if (from.HasValue && to.HasValue)
                    {
                        f = from.Value.Date; t = to.Value.Date;
                        if (t < f) (f, t) = (t, f);
                        if ((t - f).TotalDays > 730) f = t.AddDays(-730); // giới hạn tối đa 2 năm
                    }
                    else
                    {
                        key = "30d"; f = today.AddDays(-29); t = today;
                    }
                    break;
                default:
                    key = "30d"; f = today.AddDays(-29); t = today; break;
            }

            return (key, f, t);
        }

        private async Task<ProviderStatsViewModel> BuildStatisticsAsync(string? range, DateTime? from, DateTime? to, string? serviceId, string? providerId)
        {
            string? scope = ResolveScopeProviderId(providerId);
            var (key, f, t) = ResolveRange(range, from, to);
            var toExclusive = t.AddDays(1);

            int days = (int)(t - f).TotalDays + 1;
            var prevTo = f.AddDays(-1);
            var prevFrom = prevTo.AddDays(-(days - 1));

            var fromUtc = DateTime.SpecifyKind(f, DateTimeKind.Local).ToUniversalTime();
            var toUtc = DateTime.SpecifyKind(toExclusive, DateTimeKind.Local).ToUniversalTime();
            bool filterService = !string.IsNullOrWhiteSpace(serviceId);

            var vm = new ProviderStatsViewModel
            {
                RangeKey = key,
                From = f,
                To = t,
                PrevFrom = prevFrom,
                PrevTo = prevTo,
                ServiceId = filterService ? serviceId : null,
                ProviderId = scope,
                IsAdmin = IsAdminUser
            };

            // ----- Phạm vi: Provider / toàn hệ thống (Admin) -----
            IQueryable<AppointmentModel> baseScope = _context.Appointments.AsNoTracking();
            if (scope != null) baseScope = baseScope.Where(a => a.ProviderId == scope);

            IQueryable<AppointmentModel> scoped = baseScope;
            if (filterService) scoped = scoped.Where(a => a.ServiceId == serviceId);

            var current = await scoped.Include(a => a.Service).Include(a => a.Customer)
                .Where(a => a.AppointmentDate >= f && a.AppointmentDate < toExclusive)
                .ToListAsync();

            var previous = await scoped.Include(a => a.Service)
                .Where(a => a.AppointmentDate >= prevFrom && a.AppointmentDate < f)
                .ToListAsync();

            static decimal PriceOf(AppointmentModel a) => a.Service?.Price ?? 0m;

            var completed = current.Where(a => a.Status == AppointmentRules.Completed).ToList();
            var cancelled = current.Where(a => a.Status == AppointmentRules.Cancelled).ToList();
            var active = current.Where(a => a.Status != AppointmentRules.Cancelled).ToList();

            vm.TotalAppointments = current.Count;
            vm.CompletedCount = completed.Count;
            vm.CancelledCount = cancelled.Count;
            vm.PendingCount = current.Count(a => a.Status == AppointmentRules.Pending);
            vm.ConfirmedCount = current.Count(a => a.Status == AppointmentRules.Confirmed);
            vm.InProgressCount = current.Count(a => a.Status == AppointmentRules.InProgress);

            vm.RealizedRevenue = completed.Sum(a => PriceOf(a));
            vm.ExpectedRevenue = current
                .Where(a => a.Status is AppointmentRules.Pending or AppointmentRules.Confirmed or AppointmentRules.InProgress)
                .Sum(a => PriceOf(a));
            vm.LostRevenue = cancelled.Sum(a => PriceOf(a));
            vm.AvgRevenuePerCompleted = completed.Count > 0 ? vm.RealizedRevenue / completed.Count : 0m;

            vm.CompletionRate = current.Count > 0 ? completed.Count * 100.0 / current.Count : 0;
            vm.CancelRate = current.Count > 0 ? cancelled.Count * 100.0 / current.Count : 0;

            vm.PrevTotalAppointments = previous.Count;
            vm.PrevRealizedRevenue = previous.Where(a => a.Status == AppointmentRules.Completed).Sum(a => PriceOf(a));
            vm.RevenueGrowthPct = vm.PrevRealizedRevenue > 0
                ? (double)((vm.RealizedRevenue - vm.PrevRealizedRevenue) / vm.PrevRealizedRevenue * 100m)
                : (double?)null;
            vm.AppointmentGrowthPct = vm.PrevTotalAppointments > 0
                ? (vm.TotalAppointments - vm.PrevTotalAppointments) * 100.0 / vm.PrevTotalAppointments
                : (double?)null;

            // ----- Tiền thực thu online trong kỳ (US-28) -----
            IQueryable<Payment> pq = _context.Payments.AsNoTracking();
            if (scope != null) pq = pq.Where(p => p.Appointment!.ProviderId == scope);
            if (filterService) pq = pq.Where(p => p.Appointment!.ServiceId == serviceId);

            var payments = await pq
                .Where(p => (p.Status == Payment.StatusPaid || p.Status == Payment.StatusRefunded)
                            && p.PaidAt != null && p.PaidAt >= fromUtc && p.PaidAt < toUtc)
                .ToListAsync();

            vm.CollectedGross = payments.Sum(p => p.Amount);
            vm.RefundedAmount = payments.Where(p => p.Status == Payment.StatusRefunded).Sum(p => p.Amount);
            vm.PaymentByMethod = payments
                .Where(p => p.Status == Payment.StatusPaid)
                .GroupBy(p => p.Method)
                .ToDictionary(g => MethodLabel(g.Key), g => g.Sum(p => p.Amount));

            // ----- Khách hàng mới / quay lại -----
            var customerIds = active.Select(a => a.CustomerId).Distinct().ToList();
            vm.UniqueCustomers = customerIds.Count;
            if (customerIds.Count > 0)
            {
                var returning = await baseScope
                    .Where(a => a.AppointmentDate < f && a.Status != AppointmentRules.Cancelled && customerIds.Contains(a.CustomerId))
                    .Select(a => a.CustomerId)
                    .Distinct()
                    .ToListAsync();
                vm.ReturningCustomers = returning.Count;
                vm.NewCustomers = customerIds.Count - returning.Count;
            }

            // ----- Đánh giá -----
            IQueryable<Review> rq = _context.Reviews.AsNoTracking().Include(r => r.Service).Where(r => r.Service != null);
            if (scope != null) rq = rq.Where(r => r.Service!.ProviderId == scope);
            if (filterService) rq = rq.Where(r => r.ServiceId == serviceId);
            var reviews = await rq.Where(r => r.CreatedAt >= fromUtc && r.CreatedAt < toUtc).ToListAsync();
            vm.ReviewCount = reviews.Count;
            vm.AvgRating = reviews.Count > 0 ? reviews.Average(r => (double)r.Rating) : (double?)null;

            // ----- Chuỗi thời gian (theo ngày nếu kỳ ≤ 62 ngày, ngược lại theo tháng) -----
            vm.Granularity = days <= 62 ? "day" : "month";
            if (vm.Granularity == "day")
            {
                var byDay = current.GroupBy(a => a.AppointmentDate.Date).ToDictionary(g => g.Key, g => g.ToList());
                for (var d = f; d <= t; d = d.AddDays(1))
                {
                    byDay.TryGetValue(d, out var items);
                    items ??= new List<AppointmentModel>();
                    vm.Series.Add(new StatsPoint
                    {
                        Label = d.ToString("dd/MM"),
                        Revenue = items.Where(a => a.Status == AppointmentRules.Completed).Sum(a => PriceOf(a)),
                        Completed = items.Count(a => a.Status == AppointmentRules.Completed),
                        Total = items.Count(a => a.Status != AppointmentRules.Cancelled)
                    });
                }
            }
            else
            {
                for (var m = new DateTime(f.Year, f.Month, 1); m <= t; m = m.AddMonths(1))
                {
                    var items = current.Where(a => a.AppointmentDate.Year == m.Year && a.AppointmentDate.Month == m.Month).ToList();
                    vm.Series.Add(new StatsPoint
                    {
                        Label = m.ToString("MM/yyyy"),
                        Revenue = items.Where(a => a.Status == AppointmentRules.Completed).Sum(a => PriceOf(a)),
                        Completed = items.Count(a => a.Status == AppointmentRules.Completed),
                        Total = items.Count(a => a.Status != AppointmentRules.Cancelled)
                    });
                }
            }

            // ----- Phân bố theo thứ & giờ (không tính lịch đã hủy) -----
            foreach (var a in active)
            {
                int idx = ((int)a.AppointmentDate.DayOfWeek + 6) % 7; // Thứ 2 = 0 ... Chủ nhật = 6
                vm.ByWeekday[idx]++;
            }

            var hourGroups = active.GroupBy(a => a.StartTime.Hours).ToDictionary(g => g.Key, g => g.Count());
            int minHour = hourGroups.Count > 0 ? hourGroups.Keys.Min() : 8;
            int maxHour = hourGroups.Count > 0 ? hourGroups.Keys.Max() : 17;
            for (int h = minHour; h <= maxHour; h++)
            {
                vm.HourLabels.Add($"{h:00}:00");
                vm.HourCounts.Add(hourGroups.TryGetValue(h, out var c) ? c : 0);
            }

            if (active.Count > 0)
            {
                int maxDay = vm.ByWeekday.Max();
                vm.BusiestDay = WeekdayNames[Array.IndexOf(vm.ByWeekday, maxDay)];
                int maxHourCount = hourGroups.Values.Max();
                vm.PeakHour = $"{hourGroups.First(kv => kv.Value == maxHourCount).Key:00}:00";
            }

            // ----- Top dịch vụ -----
            var ratingByService = reviews.GroupBy(r => r.ServiceId)
                .ToDictionary(g => g.Key, g => (Avg: g.Average(r => (double)r.Rating), Count: g.Count()));

            vm.TopServices = current
                .GroupBy(a => new { a.ServiceId, Name = a.Service?.Name ?? "(Dịch vụ đã xóa)" })
                .Select(g =>
                {
                    ratingByService.TryGetValue(g.Key.ServiceId, out var rating);
                    var revenue = g.Where(a => a.Status == AppointmentRules.Completed).Sum(a => PriceOf(a));
                    return new ServiceStatRow
                    {
                        Name = g.Key.Name,
                        Total = g.Count(),
                        Completed = g.Count(a => a.Status == AppointmentRules.Completed),
                        Cancelled = g.Count(a => a.Status == AppointmentRules.Cancelled),
                        Revenue = revenue,
                        SharePercent = vm.RealizedRevenue > 0 ? (double)(revenue / vm.RealizedRevenue * 100m) : 0,
                        AvgRating = rating.Count > 0 ? rating.Avg : (double?)null,
                        ReviewCount = rating.Count
                    };
                })
                .OrderByDescending(r => r.Revenue).ThenByDescending(r => r.Total)
                .Take(10)
                .ToList();

            // ----- Top khách hàng theo doanh thu -----
            vm.TopCustomers = completed
                .GroupBy(a => a.CustomerId)
                .Select(g => new CustomerStatRow
                {
                    Name = g.First().Customer?.FullName ?? "(Khách)",
                    Email = g.First().Customer?.Email,
                    Count = g.Count(),
                    Spent = g.Sum(a => PriceOf(a))
                })
                .OrderByDescending(r => r.Spent).ThenByDescending(r => r.Count)
                .Take(5)
                .ToList();

            // ----- Dữ liệu cho bộ lọc -----
            IQueryable<Service> sq = _context.Services.AsNoTracking();
            if (scope != null) sq = sq.Where(s => s.ProviderId == scope);
            vm.Services = (await sq.OrderBy(s => s.Name).ToListAsync())
                .Select(s => new SelectListItem { Value = s.Id, Text = s.Name, Selected = s.Id == vm.ServiceId })
                .ToList();

            if (IsAdminUser)
            {
                var providers = await _context.Users.AsNoTracking().Where(u => u.Role == "Provider").OrderBy(u => u.FullName).ToListAsync();
                vm.Providers = providers
                    .Select(p => new SelectListItem { Value = p.Id, Text = p.FullName, Selected = p.Id == scope })
                    .ToList();
                vm.ScopeName = scope == null ? "Toàn hệ thống" : providers.FirstOrDefault(p => p.Id == scope)?.FullName ?? "Provider";
            }
            else
            {
                vm.ScopeName = User.FindFirstValue(ClaimTypes.Name) ?? "Provider";
            }

            return vm;
        }

        private static string MethodLabel(string method) => method switch
        {
            "Card" => "Thẻ ngân hàng",
            "EWallet" => "Ví điện tử",
            "BankTransfer" => "Chuyển khoản",
            _ => method
        };

        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }
    }
}
