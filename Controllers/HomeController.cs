using System.Diagnostics;
using Appointment.Data;
using Appointment.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(AppDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .Include(c => c.Services)
                .Take(6)
                .ToListAsync();

            var featuredServices = await _context.Services
                .Include(s => s.Category)
                .Include(s => s.Provider)
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .Take(6)
                .ToListAsync();

            var topProviders = await _context.Users
                .Where(u => u.Role == "Provider" && u.IsActive)
                .Take(4)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.FeaturedServices = featuredServices;
            ViewBag.TopProviders = topProviders;

            ViewBag.TotalServices = await _context.Services.CountAsync(s => s.IsActive);
            ViewBag.TotalProviders = await _context.Users.CountAsync(u => u.Role == "Provider");
            ViewBag.TotalAppointments = await _context.Appointments.CountAsync();

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
