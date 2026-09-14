using Appointment.Data;
using Appointment.Models;
using Appointment.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Controllers
{
    public class ServicesController : Controller
    {
        private readonly AppDbContext _context;

        public ServicesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Services
        public async Task<IActionResult> Index(string? search, string? categoryId, decimal? minPrice, decimal? maxPrice, string? location)
        {
            var query = _context.Services
                .Include(s => s.Category)
                .Include(s => s.Provider)
                .Where(s => s.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(term)
                                      || (s.Description != null && s.Description.ToLower().Contains(term))
                                      || (s.Provider != null && s.Provider.FullName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                query = query.Where(s => s.CategoryId == categoryId);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(s => s.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(s => s.Price <= maxPrice.Value);
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                var loc = location.Trim().ToLower();
                query = query.Where(s => s.Location != null && s.Location.ToLower().Contains(loc));
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.SearchTerm = search;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Location = location;

            var services = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
            return View(services);
        }

        // GET: /Services/Details/abc
        public async Task<IActionResult> Details(string id, DateTime? selectedDate)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var service = await _context.Services
                .Include(s => s.Category)
                .Include(s => s.Provider)
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);

            if (service == null) return NotFound();

            DateTime date = selectedDate ?? DateTime.Today;
            if (date < DateTime.Today) date = DateTime.Today;

            // Check if provider has set an OffDay on this date
            var offDay = await _context.ProviderOffDays
                .FirstOrDefaultAsync(p => p.ProviderId == service.ProviderId && p.OffDate.Date == date.Date);

            bool isOffDay = (offDay != null);

            // Fetch working hours
            var workingHoursList = await _context.WorkingHours
                .Where(w => w.ProviderId == service.ProviderId && w.IsAvailable)
                .ToListAsync();

            var workingHours = workingHoursList.FirstOrDefault(w => w.DayOfWeek == date.DayOfWeek);

            TimeSpan startTime = workingHours?.StartTime ?? new TimeSpan(8, 0, 0);
            TimeSpan endTime = workingHours?.EndTime ?? new TimeSpan(17, 0, 0);

            // Fetch existing booked appointments
            var allAppointments = await _context.Appointments
                .Where(a => a.ProviderId == service.ProviderId && a.Status != "Cancelled")
                .ToListAsync();

            var bookedAppointments = allAppointments
                .Where(a => a.AppointmentDate.Date == date.Date)
                .ToList();

            var slots = new List<TimeSlotStatus>();
            TimeSpan current = startTime;
            int stepMinutes = service.DurationMinutes > 0 ? service.DurationMinutes : 30;

            while (current.Add(TimeSpan.FromMinutes(stepMinutes)) <= endTime)
            {
                TimeSpan slotEnd = current.Add(TimeSpan.FromMinutes(stepMinutes));
                bool isBooked = bookedAppointments.Any(a => a.StartTime < slotEnd && a.EndTime > current);
                bool isPast = (date.Date == DateTime.Today && current < DateTime.Now.TimeOfDay);

                // If store is off on this date, no slots are available!
                bool available = !isOffDay && !isBooked && !isPast;

                slots.Add(new TimeSlotStatus
                {
                    Time = current,
                    IsAvailable = available
                });

                current = slotEnd;
            }

            var model = new BookingViewModel
            {
                ServiceId = service.Id,
                ServiceName = service.Name,
                ProviderName = service.Provider?.FullName ?? "N/A",
                Price = service.Price,
                DurationMinutes = service.DurationMinutes,
                CategoryName = service.Category?.Name ?? "Chưa phân loại",
                SelectedDate = date,
                IsProviderOffDay = isOffDay,
                OffDayReason = offDay?.Reason,
                AvailableSlots = slots
            };

            ViewBag.Service = service;
            return View(model);
        }
    }
}
