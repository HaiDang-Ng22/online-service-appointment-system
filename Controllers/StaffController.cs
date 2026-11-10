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

        // GET: /Staff/WalkInBooking (US-40: Đặt lịch hẹn hộ cho khách vãng lai tại quầy)
        [HttpGet]
        public async Task<IActionResult> WalkInBooking()
        {
            var model = new WalkInBookingViewModel
            {
                AppointmentDate = DateTime.Today,
                SelectedTimeSlot = string.Empty
            };

            await PopulateWalkInDropdowns(model);
            return View(model);
        }

        // GET: /Staff/LookupCustomer?phone=... (US-40)
        [HttpGet]
        public async Task<IActionResult> LookupCustomer(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return Json(new { found = false });
            }

            var cleanPhone = phone.Trim();
            var customer = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == cleanPhone);

            if (customer != null)
            {
                return Json(new
                {
                    found = true,
                    fullName = customer.FullName,
                    email = customer.Email.EndsWith("@walkin.appointx.vn") ? "" : customer.Email,
                    address = customer.Address ?? ""
                });
            }

            return Json(new { found = false });
        }

        // GET: /Staff/GetAvailableSlots (US-40: Lấy mốc thời gian theo từng loại dịch vụ và bác sĩ)
        [HttpGet]
        public async Task<IActionResult> GetAvailableSlots(string serviceId, string? providerId, DateTime? date)
        {
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                return Json(new { success = false, message = "Vui lòng chọn dịch vụ." });
            }

            var service = await _context.Services.FindAsync(serviceId);
            if (service == null)
            {
                return Json(new { success = false, message = "Dịch vụ không tồn tại." });
            }

            var targetDate = (date ?? DateTime.Today).Date;
            string actualProviderId = !string.IsNullOrWhiteSpace(providerId) ? providerId : service.ProviderId;

            // Kiểm tra ngày nghỉ của nhà cung cấp / bác sĩ
            var offDay = await _context.ProviderOffDays
                .FirstOrDefaultAsync(p => p.ProviderId == actualProviderId && p.OffDate.Date == targetDate);

            if (offDay != null)
            {
                return Json(new
                {
                    success = true,
                    isOffDay = true,
                    offDayReason = offDay.Reason ?? "Bác sĩ / Nhà cung cấp nghỉ vào ngày này.",
                    durationMinutes = service.DurationMinutes,
                    slots = new List<object>()
                });
            }

            // Lấy cấu hình giờ làm việc theo tuần
            var workingHoursList = await _context.WorkingHours
                .Where(w => w.ProviderId == actualProviderId && w.IsAvailable)
                .ToListAsync();

            var workingHours = workingHoursList.FirstOrDefault(w => w.DayOfWeek == targetDate.DayOfWeek);

            TimeSpan startTime = workingHours?.StartTime ?? new TimeSpan(8, 0, 0);
            TimeSpan endTime = workingHours?.EndTime ?? new TimeSpan(17, 0, 0);

            bool hasWeeklySchedule = await _context.WorkingHours.AnyAsync(w => w.ProviderId == actualProviderId);
            if (hasWeeklySchedule && workingHours == null)
            {
                endTime = startTime; // Ngày nghỉ tuần
            }

            // Lấy danh sách lịch đã đặt của bác sĩ
            var bookedAppointments = await _context.Appointments
                .Where(a => a.ProviderId == actualProviderId && a.Status != "Cancelled" && a.AppointmentDate.Date == targetDate)
                .ToListAsync();

            int stepMinutes = service.DurationMinutes > 0 ? service.DurationMinutes : 30;
            var slots = new List<object>();
            TimeSpan current = startTime;

            while (current.Add(TimeSpan.FromMinutes(stepMinutes)) <= endTime)
            {
                TimeSpan slotEnd = current.Add(TimeSpan.FromMinutes(stepMinutes));
                bool isBooked = bookedAppointments.Any(a => a.StartTime < slotEnd && a.EndTime > current);
                // Đối với khách tại quầy, nếu ca hẹn đã trôi qua (kết thúc trước giờ hiện tại) thì không chọn được
                bool isPast = (targetDate == DateTime.Today && slotEnd <= DateTime.Now.TimeOfDay);

                bool available = !isBooked && !isPast;

                slots.Add(new
                {
                    time = current.ToString(@"hh\:mm"),
                    endTime = slotEnd.ToString(@"hh\:mm"),
                    display = $"{current:hh\\:mm} - {slotEnd:hh\\:mm}",
                    isAvailable = available
                });

                current = slotEnd;
            }

            return Json(new
            {
                success = true,
                isOffDay = false,
                durationMinutes = stepMinutes,
                serviceName = service.Name,
                slots = slots
            });
        }

        // POST: /Staff/WalkInBooking (US-40)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WalkInBooking(WalkInBookingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateWalkInDropdowns(model);
                return View(model);
            }

            var service = await _context.Services
                .Include(s => s.Provider)
                .FirstOrDefaultAsync(s => s.Id == model.ServiceId);

            if (service == null || !service.IsActive)
            {
                ModelState.AddModelError("ServiceId", "Dịch vụ đã chọn không tồn tại hoặc đã ngừng hoạt động.");
                await PopulateWalkInDropdowns(model);
                return View(model);
            }

            string providerId = !string.IsNullOrWhiteSpace(model.ProviderId) ? model.ProviderId : service.ProviderId;

            if (!TimeSpan.TryParse(model.SelectedTimeSlot, out TimeSpan startTime))
            {
                ModelState.AddModelError("SelectedTimeSlot", "Khung giờ bắt đầu không hợp lệ.");
                await PopulateWalkInDropdowns(model);
                return View(model);
            }

            TimeSpan endTime = startTime.Add(TimeSpan.FromMinutes(service.DurationMinutes));

            // Kiểm tra ngày nghỉ của bác sĩ
            bool isOff = await _context.ProviderOffDays
                .AnyAsync(p => p.ProviderId == providerId && p.OffDate.Date == model.AppointmentDate.Date);
            if (isOff)
            {
                ModelState.AddModelError("", "Bác sĩ / Nhà cung cấp được chọn đang trong ngày nghỉ vào ngày này. Vui lòng chọn bác sĩ hoặc ngày khác.");
                await PopulateWalkInDropdowns(model);
                return View(model);
            }

            // Kiểm tra trùng lịch hẹn đang hoạt động
            var conflicts = await _context.Appointments
                .Where(a => a.ProviderId == providerId && a.Status != "Cancelled" && a.AppointmentDate.Date == model.AppointmentDate.Date)
                .ToListAsync();

            bool isOverlap = conflicts.Any(a => a.StartTime < endTime && a.EndTime > startTime);
            if (isOverlap)
            {
                ModelState.AddModelError("", "Bác sĩ đã có lịch hẹn khác trong khung giờ này. Vui lòng chọn khung giờ khác hoặc đổi bác sĩ.");
                await PopulateWalkInDropdowns(model);
                return View(model);
            }

            // 1. Tự động tạo hoặc tìm hồ sơ khách hàng nhanh
            string cleanPhone = model.PhoneNumber.Trim();
            var customer = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == cleanPhone);
            if (customer == null)
            {
                string email = !string.IsNullOrWhiteSpace(model.Email) ? model.Email.Trim() : $"{cleanPhone}@walkin.appointx.vn";
                if (await _context.Users.AnyAsync(u => u.Email == email))
                {
                    email = $"{cleanPhone}_{Guid.NewGuid().ToString("N").Substring(0, 4)}@walkin.appointx.vn";
                }

                customer = new User
                {
                    Id = Guid.NewGuid().ToString("N"),
                    FullName = model.CustomerName.Trim(),
                    PhoneNumber = cleanPhone,
                    Email = email,
                    Address = model.Address?.Trim(),
                    Role = "Customer",
                    PasswordHash = PasswordHasher.HashPassword("123456"),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Users.Add(customer);
                await _context.SaveChangesAsync();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(model.CustomerName)) customer.FullName = model.CustomerName.Trim();
                if (!string.IsNullOrWhiteSpace(model.Address)) customer.Address = model.Address.Trim();
                _context.Users.Update(customer);
                await _context.SaveChangesAsync();
            }

            // 2. Tạo cuộc hẹn tại quầy
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var appointment = new AppointmentModel
            {
                Id = Guid.NewGuid().ToString("N"),
                CustomerId = customer.Id,
                ServiceId = service.Id,
                ProviderId = providerId,
                StaffId = staffId,
                AppointmentDate = model.AppointmentDate.Date,
                StartTime = startTime,
                EndTime = endTime,
                Status = string.IsNullOrWhiteSpace(model.Status) ? "Confirmed" : model.Status,
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? "Khách vãng lai đăng ký tại quầy tiếp tân" : model.Notes.Trim(),
                InternalNote = string.IsNullOrWhiteSpace(model.InternalNote) ? null : model.InternalNote.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đặt lịch tại quầy thành công cho khách hàng {customer.FullName}! Đang mở phiếu hẹn...";
            return RedirectToAction("PrintInvoice", "Appointments", new { id = appointment.Id });
        }

        // GET: /Staff/ScanQR (US-39: Quét mã QR Check-in điểm danh tại quầy)
        [HttpGet]
        public IActionResult ScanQR()
        {
            return View();
        }

        public class CheckInRequest
        {
            public string Code { get; set; } = string.Empty;
        }

        // POST: /Staff/ProcessCheckIn (US-39)
        [HttpPost]
        public async Task<IActionResult> ProcessCheckIn([FromBody] CheckInRequest? jsonReq, [FromForm] string? code)
        {
            string rawCode = (jsonReq?.Code ?? code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawCode))
            {
                return Json(new { success = false, message = "Mã check-in không được để trống." });
            }

            // Xử lý chuỗi mã quét được: có thể là URL chứa id/code, tiền tố #APX-, hoặc ID thô
            string cleanId = rawCode;
            if (cleanId.Contains("id="))
            {
                var match = System.Text.RegularExpressions.Regex.Match(cleanId, @"[?&]id=([a-zA-Z0-9]+)");
                if (match.Success) cleanId = match.Groups[1].Value;
            }
            else if (cleanId.Contains("code="))
            {
                var match = System.Text.RegularExpressions.Regex.Match(cleanId, @"[?&]code=([a-zA-Z0-9]+)");
                if (match.Success) cleanId = match.Groups[1].Value;
            }
            else if (cleanId.StartsWith("#APX-", StringComparison.OrdinalIgnoreCase))
            {
                cleanId = cleanId.Substring(5);
            }
            else if (cleanId.StartsWith("APX-", StringComparison.OrdinalIgnoreCase))
            {
                cleanId = cleanId.Substring(4);
            }

            cleanId = cleanId.Trim().ToLowerInvariant();

            // Tìm lịch hẹn theo Id chính xác hoặc tiền tố
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Service)
                .Include(a => a.Provider)
                .Include(a => a.Staff)
                .FirstOrDefaultAsync(a => a.Id.ToLower() == cleanId || a.Id.ToLower().StartsWith(cleanId));

            if (appointment == null)
            {
                return Json(new
                {
                    success = false,
                    message = $"Không tìm thấy lịch hẹn nào tương ứng với mã '{rawCode}'."
                });
            }

            // Kiểm tra trạng thái hiện tại
            if (appointment.Status == AppointmentRules.Cancelled)
            {
                return Json(new
                {
                    success = false,
                    isCancelled = true,
                    message = $"Lịch hẹn đã bị HỦY trước đó. Lý do: {(string.IsNullOrEmpty(appointment.CancelReason) ? "Không có lý do cụ thể" : appointment.CancelReason)}",
                    appointment = new
                    {
                        id = appointment.Id,
                        shortId = appointment.Id.Substring(0, 8).ToUpper(),
                        customerName = appointment.Customer?.FullName ?? "N/A",
                        customerPhone = appointment.Customer?.PhoneNumber ?? "N/A",
                        serviceName = appointment.Service?.Name ?? "N/A",
                        providerName = appointment.Provider?.FullName ?? "N/A",
                        date = appointment.AppointmentDate.ToString("dd/MM/yyyy"),
                        time = $"{appointment.StartTime:hh\\:mm} - {appointment.EndTime:hh\\:mm}",
                        status = appointment.Status,
                        statusLabel = AppointmentRules.StatusLabel(appointment.Status)
                    }
                });
            }

            if (appointment.Status == AppointmentRules.Completed)
            {
                return Json(new
                {
                    success = false,
                    isCompleted = true,
                    message = "Lịch hẹn này đã HOÀN THÀNH trước đó, không thể check-in lại.",
                    appointment = new
                    {
                        id = appointment.Id,
                        shortId = appointment.Id.Substring(0, 8).ToUpper(),
                        customerName = appointment.Customer?.FullName ?? "N/A",
                        customerPhone = appointment.Customer?.PhoneNumber ?? "N/A",
                        serviceName = appointment.Service?.Name ?? "N/A",
                        providerName = appointment.Provider?.FullName ?? "N/A",
                        date = appointment.AppointmentDate.ToString("dd/MM/yyyy"),
                        time = $"{appointment.StartTime:hh\\:mm} - {appointment.EndTime:hh\\:mm}",
                        status = appointment.Status,
                        statusLabel = AppointmentRules.StatusLabel(appointment.Status)
                    }
                });
            }

            bool wasAlreadyInProgress = (appointment.Status == AppointmentRules.InProgress);

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(appointment.StaffId) && !string.IsNullOrEmpty(staffId))
            {
                appointment.StaffId = staffId;
            }

            // Tự động chuyển trạng thái thành In-Progress ngay khi quét thành công (Acceptance Criteria US-39)
            appointment.Status = AppointmentRules.InProgress;
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            string msg = wasAlreadyInProgress
                ? "Khách hàng đã điểm danh trước đó và đang thực hiện dịch vụ."
                : "Điểm danh Check-in thành công! Cuộc hẹn đã chuyển sang trạng thái Đang thực hiện (In-Progress).";

            return Json(new
            {
                success = true,
                wasAlreadyInProgress = wasAlreadyInProgress,
                message = msg,
                appointment = new
                {
                    id = appointment.Id,
                    shortId = appointment.Id.Substring(0, 8).ToUpper(),
                    customerName = appointment.Customer?.FullName ?? "N/A",
                    customerPhone = appointment.Customer?.PhoneNumber ?? "N/A",
                    customerEmail = appointment.Customer?.Email ?? "N/A",
                    serviceName = appointment.Service?.Name ?? "N/A",
                    serviceDuration = appointment.Service?.DurationMinutes ?? 30,
                    providerName = appointment.Provider?.FullName ?? "N/A",
                    staffName = appointment.Staff?.FullName ?? "Tiếp tân",
                    date = appointment.AppointmentDate.ToString("dd/MM/yyyy"),
                    time = $"{appointment.StartTime:hh\\:mm} - {appointment.EndTime:hh\\:mm}",
                    status = appointment.Status,
                    statusLabel = AppointmentRules.StatusLabel(appointment.Status),
                    internalNote = appointment.InternalNote ?? "",
                    notes = appointment.Notes ?? ""
                }
            });
        }

        private async Task PopulateWalkInDropdowns(WalkInBookingViewModel model)
        {
            var services = await _context.Services
                .Include(s => s.Category)
                .Include(s => s.Provider)
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

            model.ServicesList = services.Select(s => new SelectListItem
            {
                Value = s.Id,
                Text = $"{s.Name} ({s.Price:N0} đ - {s.DurationMinutes} phút/ca) - {s.Provider?.FullName}"
            }).ToList();

            if (string.IsNullOrEmpty(model.ServiceId) && model.ServicesList.Any())
            {
                model.ServiceId = model.ServicesList.First().Value;
            }

            var providers = await _context.Users
                .Where(u => u.Role == "Provider" && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            model.ProvidersList = providers.Select(p => new SelectListItem
            {
                Value = p.Id,
                Text = $"{p.FullName} - {p.PhoneNumber ?? p.Email}"
            }).ToList();
        }
    }
}
