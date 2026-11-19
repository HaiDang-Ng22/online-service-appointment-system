using Appointment.Data;
using System.Globalization;
using System.Text;
using Appointment.Helpers;
using Appointment.Infrastructure;
using Appointment.Models;
using Appointment.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _audit;

        public AdminController(AppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        // GET: /Admin
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsers = await _context.Users.CountAsync(u => u.Role != "Admin");
            ViewBag.TotalCustomers = await _context.Users.CountAsync(u => u.Role == "Customer");
            ViewBag.TotalProviders = await _context.Users.CountAsync(u => u.Role == "Provider");
            ViewBag.TotalStaff = await _context.Users.CountAsync(u => u.Role == "Staff");

            ViewBag.TotalServices = await _context.Services.CountAsync();
            ViewBag.TotalAppointments = await _context.Appointments.CountAsync();
            ViewBag.PendingAppointments = await _context.Appointments.CountAsync(a => a.Status == "Pending");
            ViewBag.CompletedAppointments = await _context.Appointments.CountAsync(a => a.Status == "Completed");

            var recentAppointments = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Service)
                .Include(a => a.Provider)
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .ToListAsync();

            return View(recentAppointments);
        }

        // GET: /Admin/Users - Chia tab Khách hàng & Nhà cung cấp/Nhân viên
        public async Task<IActionResult> Users(string? tab, string? search)
        {
            var query = _context.Users.Where(u => u.Role != "Admin").AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(term)
                                      || u.Email.ToLower().Contains(term)
                                      || (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
            }

            var users = await query
                .Include(u => u.ProvidedServices)
                .Include(u => u.ProviderAppointments)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            ViewBag.ActiveTab = string.IsNullOrWhiteSpace(tab) ? "customers" : tab.ToLower();
            ViewBag.SearchTerm = search;
            ViewBag.CustomerCount = users.Count(u => u.Role == "Customer");
            ViewBag.PartnerCount = users.Count(u => u.Role == "Provider" || u.Role == "Staff");

            return View(users);
        }

        // GET: /Admin/CreateUser
        public IActionResult CreateUser()
        {
            return View();
        }

        // POST: /Admin/CreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserByAdminViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var existing = await _context.Users.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower());
            if (existing)
            {
                ModelState.AddModelError("Email", "Email này đã được đăng ký.");
                return View(model);
            }

            var validRoles = new[] { "Admin", "Provider" };
            if (!validRoles.Contains(model.Role))
            {
                ModelState.AddModelError("Role", "Admin chỉ có quyền tạo tài khoản Nhà cung cấp (Provider) hoặc Quản trị viên (Admin).");
                return View(model);
            }

            var newUser = new User
            {
                Id = Guid.NewGuid().ToString("N"),
                FullName = model.FullName,
                Email = model.Email.Trim(),
                PasswordHash = PasswordHasher.HashPassword(model.Password),
                Role = model.Role,
                PhoneNumber = model.PhoneNumber,
                Address = model.Address,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);

            // Tự động khởi tạo giờ làm việc mặc định cho Nhà cung cấp mới (T2 - T7: 8h - 17h)
            if (newUser.Role == "Provider")
            {
                foreach (var day in Enum.GetValues<DayOfWeek>())
                {
                    if (day != DayOfWeek.Sunday)
                    {
                        _context.WorkingHours.Add(new WorkingHour
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            ProviderId = newUser.Id,
                            DayOfWeek = day,
                            StartTime = new TimeSpan(8, 0, 0),
                            EndTime = new TimeSpan(17, 0, 0),
                            IsAvailable = true
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(AuditActions.UserCreated,
                $"Admin tạo tài khoản {newUser.Email} với vai trò {newUser.Role}.", "User", newUser.Id,
                severity: newUser.Role == "Admin" ? AuditLog.SeverityCritical : AuditLog.SeverityWarning);

            TempData["SuccessMessage"] = $"Tạo tài khoản {newUser.FullName} ({newUser.Role}) thành công!";
            return RedirectToAction(nameof(Users));
        }

        // POST: /Admin/ToggleUserStatus/abc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null || user.Role == "Admin") return NotFound();

            user.IsActive = !user.IsActive;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(user.IsActive ? AuditActions.UserUnlocked : AuditActions.UserLocked,
                $"Admin {(user.IsActive ? "mở khóa" : "khóa")} tài khoản {user.Email} ({user.Role}).", "User", user.Id,
                severity: AuditLog.SeverityWarning);

            TempData["SuccessMessage"] = $"Đã {(user.IsActive ? "kích hoạt" : "khóa")} tài khoản {user.Email}.";
            return RedirectToAction(nameof(Users));
        }

        // GET: /Admin/Categories
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .Include(c => c.Services)
                .ToListAsync();
            return View(categories);
        }

        // POST: /Admin/CreateCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(Category model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["ErrorMessage"] = "Tên danh mục không được để trống.";
                return RedirectToAction(nameof(Categories));
            }

            model.Id = Guid.NewGuid().ToString("N");
            _context.Categories.Add(model);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(AuditActions.CategoryCreated, $"Thêm danh mục '{model.Name}'.", "Category", model.Id);

            TempData["SuccessMessage"] = "Đã thêm danh mục mới!";
            return RedirectToAction(nameof(Categories));
        }

        // GET: /Admin/EditCategory/abc
        public async Task<IActionResult> EditCategory(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            return View(category);
        }

        // POST: /Admin/EditCategory/abc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(string id, Category model)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError("Name", "Tên danh mục không được để trống.");
                return View(model);
            }

            category.Name = model.Name;
            category.Description = model.Description;

            _context.Categories.Update(category);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(AuditActions.CategoryUpdated, $"Sửa danh mục '{category.Name}'.", "Category", category.Id);

            TempData["SuccessMessage"] = "Cập nhật danh mục thành công!";
            return RedirectToAction(nameof(Categories));
        }

        // POST: /Admin/DeleteCategory/abc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(string id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            string deletedName = category.Name;
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(AuditActions.CategoryDeleted, $"Xóa danh mục '{deletedName}'.", "Category", id,
                severity: AuditLog.SeverityWarning);

            TempData["SuccessMessage"] = "Đã xóa danh mục thành công.";
            return RedirectToAction(nameof(Categories));
        }

        // =====================================================================
        // US-43: Nhật ký kiểm toán & bảo mật (chỉ Admin, chỉ đọc – không có chức năng sửa / xóa)
        // =====================================================================

        private const int AuditPageSize = 25;

        // GET: /Admin/AuditLogs
        [HttpGet]
        public async Task<IActionResult> AuditLogs(AuditLogFilter filter)
        {
            filter ??= new AuditLogFilter();
            if (filter.Page < 1) filter.Page = 1;

            var query = ApplyAuditFilter(_context.AuditLogs.AsNoTracking(), filter);

            int total = await query.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AuditPageSize));
            if (filter.Page > totalPages) filter.Page = totalPages;

            var items = await query
                .OrderByDescending(l => l.Id)
                .Skip((filter.Page - 1) * AuditPageSize)
                .Take(AuditPageSize)
                .ToListAsync();

            var since = DateTime.UtcNow.AddHours(-24);
            var model = new AuditLogListViewModel
            {
                Filter = filter,
                Items = items,
                Total = total,
                Page = filter.Page,
                PageSize = AuditPageSize,
                TotalAllTime = await _context.AuditLogs.CountAsync(),
                EventsLast24h = await _context.AuditLogs.CountAsync(l => l.CreatedAt >= since),
                FailedLoginsLast24h = await _context.AuditLogs.CountAsync(l => l.Action == AuditActions.LoginFailed && l.CreatedAt >= since),
                LockoutsLast24h = await _context.AuditLogs.CountAsync(l => l.Action == AuditActions.LoginLocked && l.CreatedAt >= since),
                AccessDeniedLast24h = await _context.AuditLogs.CountAsync(l => l.Action == AuditActions.AccessDenied && l.CreatedAt >= since),
                CriticalLast24h = await _context.AuditLogs.CountAsync(l => l.Severity == AuditLog.SeverityCritical && l.CreatedAt >= since)
            };

            return View(model);
        }

        // GET: /Admin/ExportAuditLogs  (CSV – mở được bằng Excel)
        [HttpGet]
        public async Task<IActionResult> ExportAuditLogs(AuditLogFilter filter)
        {
            filter ??= new AuditLogFilter();

            var rows = await ApplyAuditFilter(_context.AuditLogs.AsNoTracking(), filter)
                .OrderByDescending(l => l.Id)
                .Take(50000)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Id,Thời gian (UTC+7),Người dùng,Email,Vai trò,Hành động,Nhóm,Đối tượng,Mã đối tượng,Mô tả,Mức độ,Kết quả,IP,Yêu cầu,Hash");
            foreach (var l in rows)
            {
                var localTime = DateTime.SpecifyKind(l.CreatedAt, DateTimeKind.Utc).AddHours(7);
                sb.AppendLine(string.Join(",",
                    l.Id.ToString(CultureInfo.InvariantCulture),
                    Csv(localTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                    Csv(l.UserName),
                    Csv(l.UserEmail),
                    Csv(l.UserRole),
                    Csv(AuditActions.LabelOf(l.Action)),
                    Csv(l.Category),
                    Csv(l.EntityType),
                    Csv(l.EntityId),
                    Csv(l.Description),
                    Csv(l.Severity),
                    Csv(l.Success ? "Thành công" : "Thất bại"),
                    Csv(l.IpAddress),
                    Csv(l.RequestPath),
                    Csv(l.Hash)));
            }

            await _audit.LogAsync(AuditActions.AuditExported, $"Xuất {rows.Count:N0} dòng nhật ký kiểm toán ra CSV.", "AuditLog", null,
                severity: AuditLog.SeverityWarning);

            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(sb.ToString());
            return File(bytes, "text/csv; charset=utf-8", $"audit-log-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        // POST: /Admin/VerifyAuditChain
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyAuditChain()
        {
            var result = await _audit.VerifyChainAsync();

            await _audit.LogAsync(AuditActions.AuditVerified,
                $"Kiểm tra toàn vẹn nhật ký: {(result.IsValid ? "HỢP LỆ" : "PHÁT HIỆN BẤT THƯỜNG")} - {result.Message}",
                "AuditLog", result.FirstBrokenId?.ToString(CultureInfo.InvariantCulture),
                success: result.IsValid,
                severity: result.IsValid ? AuditLog.SeverityInfo : AuditLog.SeverityCritical);

            if (result.IsValid)
                TempData["SuccessMessage"] = result.Message;
            else
                TempData["ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(AuditLogs));
        }

        private static IQueryable<AuditLog> ApplyAuditFilter(IQueryable<AuditLog> query, AuditLogFilter f)
        {
            if (!string.IsNullOrWhiteSpace(f.Category))
                query = query.Where(l => l.Category == f.Category);

            if (!string.IsNullOrWhiteSpace(f.EventCode))
                query = query.Where(l => l.Action == f.EventCode);

            if (!string.IsNullOrWhiteSpace(f.Severity))
                query = query.Where(l => l.Severity == f.Severity);

            if (!string.IsNullOrWhiteSpace(f.Role))
                query = query.Where(l => l.UserRole == f.Role);

            if (f.Result == "success")
                query = query.Where(l => l.Success);
            else if (f.Result == "failed")
                query = query.Where(l => !l.Success);

            if (f.From.HasValue)
            {
                var fromUtc = DateTime.SpecifyKind(f.From.Value.Date, DateTimeKind.Local).ToUniversalTime();
                query = query.Where(l => l.CreatedAt >= fromUtc);
            }

            if (f.To.HasValue)
            {
                var toUtc = DateTime.SpecifyKind(f.To.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
                query = query.Where(l => l.CreatedAt < toUtc);
            }

            if (!string.IsNullOrWhiteSpace(f.Q))
            {
                var term = f.Q.Trim().ToLower();
                query = query.Where(l => l.Description.ToLower().Contains(term)
                                      || (l.UserName != null && l.UserName.ToLower().Contains(term))
                                      || (l.UserEmail != null && l.UserEmail.ToLower().Contains(term))
                                      || (l.EntityId != null && l.EntityId.ToLower().Contains(term))
                                      || (l.IpAddress != null && l.IpAddress.Contains(term)));
            }

            return query;
        }

        // =====================================================================
        // THỐNG KÊ DOANH THU & HIỆU SUẤT VẬN HÀNH TOÀN HỆ THỐNG (ADMIN)
        // =====================================================================

        // GET: /Admin/Statistics
        public async Task<IActionResult> Statistics(string? period, string? providerId, DateTime? fromDate, DateTime? toDate)
        {
            var now = DateTime.Now;
            DateTime start, end;
            period = string.IsNullOrWhiteSpace(period) ? "month" : period.ToLower();

            switch (period)
            {
                case "today":
                    start = now.Date;
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "week":
                    start = now.Date.AddDays(-7);
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "year":
                    start = new DateTime(now.Year, 1, 1);
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "custom":
                    start = fromDate?.Date ?? now.Date.AddMonths(-1);
                    end = (toDate?.Date ?? now.Date).AddDays(1).AddTicks(-1);
                    break;
                case "month":
                default:
                    period = "month";
                    start = new DateTime(now.Year, now.Month, 1);
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
            }

            ViewBag.Period = period;
            ViewBag.FromDate = start.ToString("yyyy-MM-dd");
            ViewBag.ToDate = end.ToString("yyyy-MM-dd");
            ViewBag.SelectedProviderId = providerId;

            // 1. Chỉ số tổng thể
            ViewBag.TotalCustomers = await _context.Users.CountAsync(u => u.Role == "Customer");
            ViewBag.TotalProviders = await _context.Users.CountAsync(u => u.Role == "Provider");
            ViewBag.TotalServices = await _context.Services.CountAsync();

            // 2. Lọc lịch hẹn theo khoảng thời gian và nhà cung cấp
            var aptQuery = _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Service).ThenInclude(s => s!.Category)
                .Include(a => a.Provider)
                .Where(a => a.AppointmentDate >= start && a.AppointmentDate <= end);

            if (!string.IsNullOrWhiteSpace(providerId))
            {
                aptQuery = aptQuery.Where(a => a.ProviderId == providerId);
            }

            var appointments = await aptQuery.OrderByDescending(a => a.AppointmentDate).ToListAsync();
            ViewBag.TotalAppointments = appointments.Count;

            var completedCount = appointments.Count(a => a.Status == "Completed");
            var cancelledCount = appointments.Count(a => a.Status == "Cancelled");
            var confirmedCount = appointments.Count(a => a.Status == "Confirmed");
            var pendingCount = appointments.Count(a => a.Status == "Pending");

            ViewBag.CompletedCount = completedCount;
            ViewBag.CancelledCount = cancelledCount;
            ViewBag.CompletionRate = appointments.Count > 0 ? Math.Round((double)completedCount / appointments.Count * 100, 1) : 0;
            ViewBag.CancelRate = appointments.Count > 0 ? Math.Round((double)cancelledCount / appointments.Count * 100, 1) : 0;

            // 3. Thanh toán & Doanh thu
            var payQuery = _context.Payments
                .Include(p => p.Appointment).ThenInclude(a => a!.Provider)
                .Where(p => p.Status == "Paid" && p.PaidAt != null && p.PaidAt >= start && p.PaidAt <= end);

            if (!string.IsNullOrWhiteSpace(providerId))
            {
                payQuery = payQuery.Where(p => p.Appointment != null && p.Appointment.ProviderId == providerId);
            }

            var payments = await payQuery.ToListAsync();
            decimal totalRevenue = payments.Sum(p => p.Amount);
            ViewBag.TotalRevenue = totalRevenue;

            // 4. Biểu đồ doanh thu theo thời gian
            if (period == "year")
            {
                // Nhóm theo từng tháng trong năm
                var revByMonth = payments
                    .GroupBy(p => p.PaidAt!.Value.ToString("yyyy-MM"))
                    .Select(g => new { Label = "Tháng " + g.Key.Substring(5), Total = g.Sum(x => x.Amount) })
                    .OrderBy(x => x.Label)
                    .ToList();
                ViewBag.RevenueTimelineJson = System.Text.Json.JsonSerializer.Serialize(revByMonth);
            }
            else
            {
                // Nhóm theo từng ngày
                var revByDay = payments
                    .GroupBy(p => p.PaidAt!.Value.ToString("dd/MM"))
                    .Select(g => new { Label = g.Key, Total = g.Sum(x => x.Amount) })
                    .ToList();
                ViewBag.RevenueTimelineJson = System.Text.Json.JsonSerializer.Serialize(revByDay);
            }

            // 5. Biểu đồ doanh thu theo Nhà Cung Cấp
            var allProviders = await _context.Users.Where(u => u.Role == "Provider").OrderBy(u => u.FullName).ToListAsync();
            ViewBag.ProvidersList = allProviders;

            var provRevenueData = allProviders.Select(p =>
            {
                var pPayments = payments.Where(pay => pay.Appointment != null && pay.Appointment.ProviderId == p.Id).ToList();
                var pApts = appointments.Where(a => a.ProviderId == p.Id).ToList();
                var pCompleted = pApts.Count(a => a.Status == "Completed");
                var pCancelled = pApts.Count(a => a.Status == "Cancelled");
                var rev = pPayments.Sum(pay => pay.Amount);

                return new
                {
                    ProviderId = p.Id,
                    ProviderName = p.FullName,
                    Email = p.Email,
                    Phone = p.PhoneNumber ?? "-",
                    Address = p.Address ?? "-",
                    TotalApts = pApts.Count,
                    CompletedApts = pCompleted,
                    CancelledApts = pCancelled,
                    Revenue = rev,
                    CompletionRate = pApts.Count > 0 ? Math.Round((double)pCompleted / pApts.Count * 100, 1) : 0
                };
            }).OrderByDescending(x => x.Revenue).ToList();

            ViewBag.ProviderStatsTable = provRevenueData;
            ViewBag.ProviderRevenueChartJson = System.Text.Json.JsonSerializer.Serialize(provRevenueData.Select(x => new { Label = x.ProviderName, Total = x.Revenue }));

            // 6. Biểu đồ trạng thái lịch hẹn
            var statusDist = new[]
            {
                new { Label = "Hoàn thành", Count = completedCount },
                new { Label = "Đã xác nhận", Count = confirmedCount },
                new { Label = "Chờ duyệt", Count = pendingCount },
                new { Label = "Đã hủy", Count = cancelledCount }
            };
            ViewBag.StatusDistJson = System.Text.Json.JsonSerializer.Serialize(statusDist);

            // 7. Biểu đồ phương thức thanh toán
            var methodDist = payments.GroupBy(p => p.Method)
                .Select(g => new { Label = g.Key == "BankTransfer" ? "Chuyển khoản" : g.Key == "Card" ? "Thẻ ngân hàng" : "Ví điện tử", Total = g.Sum(x => x.Amount) })
                .ToList();
            ViewBag.MethodDistJson = System.Text.Json.JsonSerializer.Serialize(methodDist);

            return View(appointments);
        }

        // GET: /Admin/ExportAppointmentsExcel
        [HttpGet]
        public async Task<IActionResult> ExportAppointmentsExcel(DateTime? fromDate, DateTime? toDate, string? status, string? providerId)
        {
            var query = _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Service).ThenInclude(s => s!.Category)
                .Include(a => a.Provider)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(a => a.AppointmentDate >= fromDate.Value.Date);
            }
            if (toDate.HasValue)
            {
                query = query.Where(a => a.AppointmentDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }
            if (!string.IsNullOrWhiteSpace(providerId))
            {
                query = query.Where(a => a.ProviderId == providerId);
            }

            var list = await query.OrderByDescending(a => a.AppointmentDate).ToListAsync();
            var title = "Báo Cáo Thống Kê Doanh Thu & Lịch Hẹn Hệ Thống AppointX";
            var bytes = ExcelExportHelper.ExportAppointmentsToExcel(list, title);

            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BaoCao_LichHen_AppointX_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        // GET: /Admin/GetProviderDetail?id=xxx
        [HttpGet]
        public async Task<IActionResult> GetProviderDetail(string id)
        {
            try
            {
                var provider = await _context.Users.FindAsync(id);
                if (provider == null) return NotFound(new { success = false, message = "Không tìm thấy cơ sở đối tác." });

                var services = await _context.Services
                    .Where(s => s.ProviderId == id)
                    .Select(s => new { s.Name, s.Price, s.DurationMinutes })
                    .ToListAsync();

                var rawAppointments = await _context.Appointments
                    .Include(a => a.Customer)
                    .Include(a => a.Service)
                    .Where(a => a.ProviderId == id)
                    .OrderByDescending(a => a.AppointmentDate)
                    .Take(10)
                    .ToListAsync();

                var appointments = rawAppointments
                    .Select(a => new
                    {
                        CustomerName = a.Customer != null ? a.Customer.FullName : "Khách vãng lai",
                        ServiceName = a.Service != null ? a.Service.Name : "-",
                        Date = a.AppointmentDate.ToString("dd/MM/yyyy"),
                        Time = a.StartTime.ToString(@"hh\:mm"),
                        a.Status,
                        Price = a.Service != null ? a.Service.Price : 0
                    })
                    .ToList();

                var payments = await _context.Payments
                    .Include(p => p.Appointment)
                    .Where(p => p.Appointment != null && p.Appointment.ProviderId == id && p.Status == "Paid")
                    .ToListAsync();

                decimal totalRevenue = payments.Sum(p => p.Amount);

                return Json(new
                {
                    success = true,
                    provider = new
                    {
                        provider.FullName,
                        provider.Email,
                        provider.PhoneNumber,
                        provider.Address,
                        TotalRevenue = totalRevenue,
                        ServicesCount = services.Count,
                        Services = services,
                        RecentAppointments = appointments
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi truy vấn: {ex.Message}" });
            }
        }

        // =====================================================================
        // US-REVIEW: QUẢN LÝ ĐÁNH GIÁ TOÀN HỆ THỐNG CHO ADMIN
        // =====================================================================

        // GET: /Admin/Reviews
        [HttpGet]
        public async Task<IActionResult> Reviews(string? providerId, int? rating)
        {
            var query = _context.Reviews
                .Include(r => r.Service).ThenInclude(s => s!.Provider)
                .Include(r => r.Customer)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(providerId))
            {
                query = query.Where(r => r.Service != null && r.Service.ProviderId == providerId);
            }

            if (rating.HasValue && rating.Value > 0)
            {
                query = query.Where(r => r.Rating == rating.Value);
            }

            var reviews = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            // Thống kê điểm số theo từng nhà cung cấp
            var providers = await _context.Users.Where(u => u.Role == "Provider").OrderBy(u => u.FullName).ToListAsync();
            ViewBag.ProvidersList = providers;
            ViewBag.SelectedProviderId = providerId;
            ViewBag.SelectedRating = rating;

            var allReviews = await _context.Reviews.Include(r => r.Service).ToListAsync();
            var providerRatings = providers.Select(p =>
            {
                var pRevs = allReviews.Where(r => r.Service != null && r.Service.ProviderId == p.Id).ToList();
                var count = pRevs.Count;
                var avg = count > 0 ? Math.Round(pRevs.Average(r => r.Rating), 1) : 0;
                var satisfiedCount = pRevs.Count(r => r.Rating >= 4);

                return new
                {
                    ProviderId = p.Id,
                    ProviderName = p.FullName,
                    ReviewCount = count,
                    AverageRating = avg,
                    SatisfiedRate = count > 0 ? Math.Round((double)satisfiedCount / count * 100, 1) : 0
                };
            }).OrderByDescending(x => x.ReviewCount).ThenByDescending(x => x.AverageRating).ToList();

            ViewBag.ProviderRatingStats = providerRatings;

            return View(reviews);
        }

        // POST: /Admin/QuickCreateCategory
        [HttpPost]
        public async Task<IActionResult> QuickCreateCategory([FromBody] Category model)
        {
            if (string.IsNullOrWhiteSpace(model?.Name))
            {
                return Json(new { success = false, message = "Tên danh mục không được để trống." });
            }

            var exists = await _context.Categories.AnyAsync(c => c.Name.ToLower() == model.Name.Trim().ToLower());
            if (exists)
            {
                return Json(new { success = false, message = $"Danh mục '{model.Name}' đã tồn tại trong hệ thống." });
            }

            var newCategory = new Category
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = model.Name.Trim(),
                Description = model.Description?.Trim() ?? string.Empty
            };

            _context.Categories.Add(newCategory);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(AuditActions.CategoryCreated,
                $"Admin thêm danh mục mới: {newCategory.Name}.", "Category", newCategory.Id);

            return Json(new { success = true, category = newCategory });
        }

        /// <summary>Escape một ô CSV; chặn CSV-injection khi mở bằng Excel (ô bắt đầu bằng = + - @).</summary>
        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            var v = value;
            if (v[0] is '=' or '+' or '-' or '@') v = "'" + v;
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }
    }
}
