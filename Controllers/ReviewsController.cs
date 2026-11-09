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
    /// <summary>US-30: Khách hàng đánh giá & chấm điểm chất lượng dịch vụ sau khi lịch hẹn hoàn thành.</summary>
    [Authorize(Roles = "Customer")]
    public class ReviewsController : Controller
    {
        private readonly AppDbContext _context;

        public ReviewsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Reviews/Create?appointmentId=abc
        [HttpGet]
        public async Task<IActionResult> Create(string appointmentId)
        {
            var (appointment, error) = await LoadReviewableAppointmentAsync(appointmentId);
            if (appointment == null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction("Index", "Appointments");
            }

            var existing = await _context.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.AppointmentId == appointment.Id);

            return View(new ReviewFormViewModel
            {
                AppointmentId = appointment.Id,
                ServiceName = appointment.Service?.Name ?? string.Empty,
                ProviderName = appointment.Provider?.FullName ?? string.Empty,
                AppointmentDate = appointment.AppointmentDate,
                IsEdit = existing != null,
                Rating = existing?.Rating ?? 0,
                Comment = existing?.Comment
            });
        }

        // POST: /Reviews/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReviewFormViewModel model)
        {
            var (appointment, error) = await LoadReviewableAppointmentAsync(model.AppointmentId);
            if (appointment == null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction("Index", "Appointments");
            }

            model.ServiceName = appointment.Service?.Name ?? string.Empty;
            model.ProviderName = appointment.Provider?.FullName ?? string.Empty;
            model.AppointmentDate = appointment.AppointmentDate;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.AppointmentId == appointment.Id);
            if (review == null)
            {
                review = new Review
                {
                    Id = Guid.NewGuid().ToString("N"),
                    AppointmentId = appointment.Id,
                    ServiceId = appointment.ServiceId,
                    CustomerId = appointment.CustomerId,
                    Rating = model.Rating,
                    Comment = model.Comment?.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                _context.Reviews.Add(review);
                TempData["SuccessMessage"] = "Cảm ơn bạn đã đánh giá dịch vụ!";
            }
            else
            {
                review.Rating = model.Rating;
                review.Comment = model.Comment?.Trim();
                review.UpdatedAt = DateTime.UtcNow;
                TempData["SuccessMessage"] = "Đã cập nhật đánh giá của bạn.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Details", "Services", new { id = appointment.ServiceId });
        }

        // POST: /Reviews/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string appointmentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.AppointmentId == appointmentId && r.CustomerId == userId);
            if (review == null) return NotFound();

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa đánh giá của bạn.";
            return RedirectToAction("Index", "Appointments");
        }

        private async Task<(AppointmentModel? Appointment, string Error)> LoadReviewableAppointmentAsync(string? appointmentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(appointmentId) || string.IsNullOrEmpty(userId))
            {
                return (null, "Không tìm thấy lịch hẹn cần đánh giá.");
            }

            var appointment = await _context.Appointments
                .Include(a => a.Service)
                .Include(a => a.Provider)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.CustomerId == userId);

            if (appointment == null) return (null, "Không tìm thấy lịch hẹn cần đánh giá.");

            if (appointment.Status != AppointmentRules.Completed)
            {
                return (null, "Bạn chỉ có thể đánh giá sau khi lịch hẹn đã hoàn thành.");
            }

            return (appointment, string.Empty);
        }
    }
}
