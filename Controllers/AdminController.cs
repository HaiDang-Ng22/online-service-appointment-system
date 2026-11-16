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

        // GET: /Admin/Users - EXPLICITLY HIDE ADMIN ACCOUNTS AS REQUESTED ("ẩn admin đi không được thấy admin")
        public async Task<IActionResult> Users(string? role, string? search)
        {
            var query = _context.Users.Where(u => u.Role != "Admin").AsQueryable();

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role == role);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(term)
                                      || u.Email.ToLower().Contains(term)
                                      || (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
            }

            var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

            ViewBag.SelectedRole = role;
            ViewBag.SearchTerm = search;

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

            var validRoles = new[] { "Admin", "Provider", "Staff", "Customer" };
            if (!validRoles.Contains(model.Role))
            {
                ModelState.AddModelError("Role", "Vai trò không hợp lệ.");
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
