using Appointment.Data;
using Appointment.Helpers;
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

        public AdminController(AppDbContext context)
        {
            _context = context;
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

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa danh mục thành công.";
            return RedirectToAction(nameof(Categories));
        }
    }
}
