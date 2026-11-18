using System.Security.Claims;
using System.Security.Cryptography;
using Appointment.Data;
using Appointment.Helpers;
using Appointment.Infrastructure;
using Appointment.Models;
using Appointment.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Appointment.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _env;
        private readonly IAuditService _audit;
        private readonly SecurityOptions _security;

        public AccountController(AppDbContext context, IEmailSender emailSender, IWebHostEnvironment env,
            IAuditService audit, IOptions<SecurityOptions> security)
        {
            _context = context;
            _emailSender = emailSender;
            _env = env;
            _audit = audit;
            _security = security.Value;
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var existingUser = await _context.Users.AnyAsync(u => u.Email.ToLower() == model.Email.Trim().ToLower());
                if (existingUser)
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng trong hệ thống.");
                    return View(model);
                }

                var assignedRole = (model.Role == "Provider") ? "Provider" 
                                 : (model.Role == "Staff") ? "Staff" 
                                 : "Customer";

                var newUser = new User
                {
                    Id = Guid.NewGuid().ToString("N"),
                    FullName = model.FullName.Trim(),
                    Email = model.Email.Trim().ToLower(),
                    PasswordHash = PasswordHasher.HashPassword(model.Password),
                    PhoneNumber = model.PhoneNumber?.Trim(),
                    Address = model.Address?.Trim(),
                    Role = assignedRole,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);

                // If user registered as Provider, initialize default Working Hours (Mon - Sat, 8:00 - 17:00)
                if (assignedRole == "Provider")
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

                await _audit.LogAsync(AuditActions.Register,
                    $"Đăng ký tài khoản mới ({assignedRole}) - {newUser.Email}.",
                    "User", newUser.Id, actor: AuditActor.FromUser(newUser));

                TempData["SuccessMessage"] = $"Đăng ký tài khoản {(assignedRole == "Provider" ? "Đối tác / Bác sĩ" : "Khách hàng")} thành công! Vui lòng đăng nhập.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Lỗi khi lưu tài khoản: {ex.Message}");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            string attemptedEmail = model.Email.Trim().ToLowerInvariant();
            var lockWindow = TimeSpan.FromMinutes(Math.Max(1, _security.LockoutMinutes));

            // US-43: khóa tạm thời khi đăng nhập sai liên tiếp (theo email) hoặc quá nhiều lần sai từ cùng một IP
            var failures = await _audit.GetRecentLoginFailuresAsync(attemptedEmail, lockWindow);
            string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            int ipFailures = await _audit.CountRecentLoginFailuresByIpAsync(clientIp, lockWindow);

            bool lockedByEmail = _security.MaxFailedLogins > 0 && failures.Count >= _security.MaxFailedLogins;
            bool lockedByIp = _security.MaxFailedLoginsPerIp > 0 && ipFailures >= _security.MaxFailedLoginsPerIp;

            if (lockedByEmail || lockedByIp)
            {
                int minutesLeft = lockWindow.Minutes + lockWindow.Hours * 60;
                if (failures.LastFailureUtc.HasValue)
                {
                    var remaining = lockWindow - (DateTime.UtcNow - failures.LastFailureUtc.Value);
                    minutesLeft = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
                }

                await _audit.LogAsync(AuditActions.LoginLocked,
                    lockedByEmail
                        ? $"Chặn đăng nhập vào {attemptedEmail}: đã sai mật khẩu {failures.Count} lần liên tiếp."
                        : $"Chặn đăng nhập từ IP {clientIp}: quá nhiều lần đăng nhập sai ({ipFailures} lần).",
                    "User", null, success: false, severity: AuditLog.SeverityCritical,
                    actor: AuditActor.Anonymous(attemptedEmail));

                ModelState.AddModelError(string.Empty,
                    $"Bạn đã nhập sai quá nhiều lần. Vui lòng thử lại sau khoảng {minutesLeft} phút hoặc dùng chức năng \"Quên mật khẩu\".");
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == attemptedEmail);
            if (user == null || !PasswordHasher.VerifyPassword(model.Password, user.PasswordHash))
            {
                await _audit.LogAsync(AuditActions.LoginFailed,
                    $"Đăng nhập sai email hoặc mật khẩu ({attemptedEmail}).",
                    "User", user?.Id, success: false,
                    severity: failures.Count + 1 >= _security.MaxFailedLogins ? AuditLog.SeverityCritical : AuditLog.SeverityWarning,
                    actor: AuditActor.Anonymous(attemptedEmail));

                int remainingTries = _security.MaxFailedLogins - (failures.Count + 1);
                string hint = _security.MaxFailedLogins > 0 && remainingTries > 0 && remainingTries <= 2
                    ? $" Bạn còn {remainingTries} lần thử trước khi bị khóa tạm thời."
                    : string.Empty;

                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác." + hint);
                return View(model);
            }

            if (!user.IsActive)
            {
                await _audit.LogAsync(AuditActions.LoginInactive,
                    $"Tài khoản bị khóa cố đăng nhập ({user.Email}).",
                    "User", user.Id, success: false, actor: AuditActor.FromUser(user));

                ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị khóa hoặc ngừng hoạt động.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            });

            await _audit.LogAsync(AuditActions.LoginSuccess,
                $"Đăng nhập thành công ({user.Role}).", "User", user.Id, actor: AuditActor.FromUser(user));

            TempData["SuccessMessage"] = $"Xin chào {user.FullName}!";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (user.Role == "Admin") return RedirectToAction("Index", "Admin");
            if (user.Role == "Provider") return RedirectToAction("Index", "Provider");
            if (user.Role == "Staff") return RedirectToAction("Index", "Staff");

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _audit.LogAsync(AuditActions.Logout, "Đăng xuất.", "User", User.FindFirstValue(ClaimTypes.NameIdentifier));

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["SuccessMessage"] = "Đã đăng xuất thành công.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction(nameof(Login));

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var model = new ProfileViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                Role = user.Role,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || userId != model.Id)
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.Email = user.Email;
                model.Role = user.Role;
                model.AvatarUrl = user.AvatarUrl;
                model.CreatedAt = user.CreatedAt;
                return View(model);
            }
            // Handle avatar upload if provided
            if (model.AvatarFile != null && model.AvatarFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                var ext = Path.GetExtension(model.AvatarFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                {
                    ModelState.AddModelError("AvatarFile", "Định dạng ảnh không hợp lệ. Vui lòng chọn file .jpg, .jpeg, .png, .webp hoặc .gif.");
                    model.Email = user.Email;
                    model.Role = user.Role;
                    model.AvatarUrl = user.AvatarUrl;
                    model.CreatedAt = user.CreatedAt;
                    return View(model);
                }

                if (model.AvatarFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("AvatarFile", "Dung lượng ảnh tối đa là 5MB.");
                    model.Email = user.Email;
                    model.Role = user.Role;
                    model.AvatarUrl = user.AvatarUrl;
                    model.CreatedAt = user.CreatedAt;
                    return View(model);
                }

                var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "avatars");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var safeFileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsFolder, safeFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.AvatarFile.CopyToAsync(stream);
                }

                user.AvatarUrl = $"/uploads/avatars/{safeFileName}";
            }

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();
            user.Address = model.Address?.Trim();

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(AuditActions.ProfileUpdated, "Cập nhật thông tin cá nhân.", "User", user.Id);

            TempData["SuccessMessage"] = "Cập nhật thông tin cá nhân thành công!";
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            if (!PasswordHasher.VerifyPassword(model.CurrentPassword, user.PasswordHash))
            {
                ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không chính xác.");
                return View(model);
            }

            if (model.NewPassword == model.CurrentPassword)
            {
                ModelState.AddModelError("NewPassword", "Mật khẩu mới không được trùng với mật khẩu hiện tại.");
                return View(model);
            }

            user.PasswordHash = PasswordHasher.HashPassword(model.NewPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction(nameof(ChangePassword));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // =====================================================================
        // US-26: Khôi phục & đặt lại mật khẩu qua email
        // =====================================================================

        private const int ResetTokenMinutes = 30;

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(nameof(Profile));
            }
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var email = model.Email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            // Luôn trả về cùng một thông báo để không lộ việc email có tồn tại hay không.
            if (user != null && user.IsActive)
            {
                var activeTokens = await _context.PasswordResetTokens
                    .Where(t => t.UserId == user.Id && t.UsedAt == null)
                    .ToListAsync();

                // Chống spam: tối đa 1 email / 60 giây cho mỗi tài khoản
                bool tooSoon = activeTokens.Any(t => (DateTime.UtcNow - DateTime.SpecifyKind(t.CreatedAt, DateTimeKind.Utc)).TotalSeconds < 60);

                if (!tooSoon)
                {
                    foreach (var old in activeTokens)
                    {
                        old.UsedAt = DateTime.UtcNow; // vô hiệu hóa token cũ
                    }

                    var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

                    _context.PasswordResetTokens.Add(new PasswordResetToken
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        UserId = user.Id,
                        TokenHash = PasswordHasher.HashPassword(rawToken),
                        ExpiresAt = DateTime.UtcNow.AddMinutes(ResetTokenMinutes),
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();

                    var link = Url.Action(nameof(ResetPassword), "Account", new { email = user.Email, token = rawToken }, Request.Scheme)!;

                    var body = $@"
<div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto'>
  <h2 style='color:#0f172a'>Đặt lại mật khẩu AppointX</h2>
  <p>Xin chào <strong>{System.Net.WebUtility.HtmlEncode(user.FullName)}</strong>,</p>
  <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Nhấn vào nút bên dưới để tạo mật khẩu mới (liên kết có hiệu lực trong {ResetTokenMinutes} phút và chỉ dùng được một lần):</p>
  <p style='text-align:center;margin:28px 0'>
    <a href='{link}' style='background:#2563eb;color:#fff;padding:12px 28px;border-radius:8px;text-decoration:none;font-weight:600'>Đặt lại mật khẩu</a>
  </p>
  <p style='color:#64748b;font-size:13px'>Nếu nút không hoạt động, hãy sao chép liên kết sau vào trình duyệt:<br/>{link}</p>
  <p style='color:#64748b;font-size:13px'>Nếu bạn không yêu cầu việc này, hãy bỏ qua email — mật khẩu của bạn vẫn được giữ nguyên.</p>
</div>";

                    bool sent = await _emailSender.SendAsync(user.Email, "[AppointX] Đặt lại mật khẩu", body);

                    await _audit.LogAsync(AuditActions.PasswordResetRequested,
                        $"Yêu cầu đặt lại mật khẩu cho {user.Email} (email {(sent ? "đã gửi" : "chưa gửi được")}).",
                        "User", user.Id, actor: AuditActor.FromUser(user));

                    // Chỉ ở môi trường Development và khi chưa cấu hình SMTP: hiển thị link để dev thử nghiệm.
                    if (!sent && _env.IsDevelopment())
                    {
                        TempData["DevResetLink"] = link;
                    }
                }
            }

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string? email, string? token)
        {
            var model = new ResetPasswordViewModel
            {
                Email = email ?? string.Empty,
                Token = token ?? string.Empty
            };

            var (user, _) = await FindValidResetTokenAsync(email, token);
            model.IsTokenValid = user != null;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var (user, record) = await FindValidResetTokenAsync(model.Email, model.Token);
            if (user == null || record == null)
            {
                await _audit.LogAsync(AuditActions.PasswordResetFailed,
                    $"Đặt lại mật khẩu thất bại: liên kết không hợp lệ hoặc đã hết hạn ({model.Email}).",
                    "User", null, success: false, severity: AuditLog.SeverityWarning,
                    actor: AuditActor.Anonymous(model.Email));

                model.IsTokenValid = false;
                return View(model);
            }

            user.PasswordHash = PasswordHasher.HashPassword(model.NewPassword);
            record.UsedAt = DateTime.UtcNow;

            // Vô hiệu hóa mọi token còn lại của tài khoản
            var others = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && t.UsedAt == null && t.Id != record.Id)
                .ToListAsync();
            foreach (var t in others) t.UsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(AuditActions.PasswordResetCompleted,
                $"Đặt lại mật khẩu thành công cho {user.Email}.", "User", user.Id,
                severity: AuditLog.SeverityWarning, actor: AuditActor.FromUser(user));

            TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.";
            return RedirectToAction(nameof(Login));
        }

        private async Task<(User? User, PasswordResetToken? Token)> FindValidResetTokenAsync(string? email, string? token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token)) return (null, null);

            var normalized = email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
            if (user == null) return (null, null);

            var hash = PasswordHasher.HashPassword(token);
            var record = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.UserId == user.Id && t.TokenHash == hash && t.UsedAt == null);

            if (record == null || record.ExpiresAt < DateTime.UtcNow) return (null, null);

            return (user, record);
        }
    }
}
