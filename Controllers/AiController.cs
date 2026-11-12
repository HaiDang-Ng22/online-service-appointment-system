using System.Security.Claims;
using System.Text.Json;
using Appointment.Data;
using Appointment.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Controllers
{
    /// <summary>
    /// API Controller xử lý các tác vụ AI: Chatbot, Gợi ý, Phân tích đánh giá, Thống kê.
    /// </summary>
    public class AiController : Controller
    {
        private readonly IAiService _ai;
        private readonly AppDbContext _context;
        private readonly ILogger<AiController> _logger;

        public AiController(IAiService ai, AppDbContext context, ILogger<AiController> logger)
        {
            _ai = ai;
            _context = context;
            _logger = logger;
        }

        // ─────────────────────────────────────────────
        // AI-01: Chatbot endpoint
        // ─────────────────────────────────────────────

        /// <summary>
        /// POST /Ai/Chat — Nhận câu hỏi từ widget chatbot, trả lời bằng Gemini.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
                return Json(new { success = false, reply = "Vui lòng nhập câu hỏi." });

            var reply = await _ai.ChatAsync(request.Message, request.History);
            return Json(new { success = true, reply });
        }

        // ─────────────────────────────────────────────
        // AI-02: Gợi ý dịch vụ thông minh
        // ─────────────────────────────────────────────

        /// <summary>
        /// GET /Ai/Recommend — Trả về danh sách gợi ý dịch vụ cho khách hàng hiện tại.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Recommend()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Lấy danh sách dịch vụ hiện có
            var allServices = await _context.Services
                .Include(s => s.Category)
                .Include(s => s.Provider)
                .Where(s => s.IsActive)
                .Select(s => new
                {
                    s.Id,
                    ServiceName = s.Name,
                    Category = s.Category != null ? s.Category.Name : "",
                    Provider = s.Provider != null ? s.Provider.FullName : "",
                    s.Price,
                    s.DurationMinutes,
                    s.Description
                })
                .ToListAsync();

            var servicesJson = JsonSerializer.Serialize(allServices);
            string customerContext;

            if (!string.IsNullOrEmpty(userId))
            {
                // Customer đã đăng nhập: lấy lịch sử đặt lịch & đánh giá
                var recentAppointments = await _context.Appointments
                    .Include(a => a.Service)
                    .Where(a => a.CustomerId == userId)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(10)
                    .Select(a => new
                    {
                        ServiceName = a.Service != null ? a.Service.Name : "",
                        Category = a.Service != null && a.Service.Category != null ? a.Service.Category.Name : "",
                        a.Status,
                        Date = a.AppointmentDate.ToString("dd/MM/yyyy")
                    })
                    .ToListAsync();

                var reviews = await _context.Reviews
                    .Include(r => r.Service)
                    .Where(r => r.CustomerId == userId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(5)
                    .Select(r => new
                    {
                        ServiceName = r.Service != null ? r.Service.Name : "",
                        r.Rating,
                        r.Comment
                    })
                    .ToListAsync();

                customerContext = $"Lịch sử đặt lịch gần đây: {JsonSerializer.Serialize(recentAppointments)}\n" +
                                  $"Đánh giá gần đây: {JsonSerializer.Serialize(reviews)}";
            }
            else
            {
                customerContext = "Khách vãng lai chưa đăng nhập. Gợi ý các dịch vụ phổ biến, được đánh giá cao nhất.";
            }

            var result = await _ai.RecommendServicesAsync(customerContext, servicesJson);

            // Cố gắng parse JSON array từ AI
            try
            {
                // Loại bỏ markdown code block nếu AI bọc trong ```json
                var cleaned = result.Trim();
                if (cleaned.StartsWith("```"))
                {
                    cleaned = cleaned.Split('\n').Skip(1).ToArray()
                        .Aggregate("", (a, b) => a + b + "\n").TrimEnd('`').Trim();
                }

                var recommendations = JsonSerializer.Deserialize<List<RecommendationItem>>(cleaned);
                if (recommendations != null && recommendations.Count > 0)
                {
                    // Lấy thông tin đầy đủ của các dịch vụ được gợi ý
                    var serviceIds = recommendations.Select(r => r.serviceId).ToList();
                    var services = await _context.Services
                        .Include(s => s.Category)
                        .Include(s => s.Provider)
                        .Where(s => serviceIds.Contains(s.Id) && s.IsActive)
                        .ToListAsync();

                    var enriched = services.Select(s =>
                    {
                        var rec = recommendations.FirstOrDefault(r => r.serviceId == s.Id);
                        return new
                        {
                            s.Id,
                            ServiceName = s.Name,
                            s.Description,
                            s.Price,
                            s.DurationMinutes,
                            s.ImageUrl,
                            Category = s.Category?.Name ?? "",
                            Provider = s.Provider?.FullName ?? "",
                            AiReason = rec?.reason ?? ""
                        };
                    }).ToList();

                    return Json(new { success = true, recommendations = enriched });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không parse được JSON gợi ý từ AI, trả về raw text");
            }

            return Json(new { success = true, rawText = result });
        }

        // ─────────────────────────────────────────────
        // AI-03: Phân tích đánh giá cho Provider
        // ─────────────────────────────────────────────

        /// <summary>
        /// GET /Ai/AnalyzeReviews — Provider xem AI phân tích tổng hợp đánh giá.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AnalyzeReviews()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Chưa đăng nhập." });

            var reviews = await _context.Reviews
                .Include(r => r.Service)
                .Include(r => r.Customer)
                .Where(r => r.Service != null && r.Service.ProviderId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .Select(r => new
                {
                    Service = r.Service != null ? r.Service.Name : "",
                    r.Rating,
                    r.Comment,
                    Customer = r.Customer != null ? r.Customer.FullName : "Ẩn danh",
                    Date = r.CreatedAt.ToString("dd/MM/yyyy")
                })
                .ToListAsync();

            if (reviews.Count == 0)
                return Json(new { success = true, analysis = "📭 Chưa có đánh giá nào để phân tích. Hãy hoàn thành thêm lịch hẹn để nhận phản hồi từ khách hàng!" });

            var reviewsData = string.Join("\n", reviews.Select(r =>
                $"- [{r.Date}] {r.Service}: {r.Rating}⭐ - \"{r.Comment}\" (bởi {r.Customer})"));

            var analysis = await _ai.AnalyzeReviewsAsync(reviewsData);
            return Json(new { success = true, analysis });
        }

        // ─────────────────────────────────────────────
        // AI-05: Phân tích thống kê & Dự báo
        // ─────────────────────────────────────────────

        /// <summary>
        /// GET /Ai/AnalyzeStats — Admin/Provider xem AI phân tích thống kê.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AnalyzeStats()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var isProvider = User.IsInRole("Provider");

            if (!isAdmin && !isProvider)
                return Json(new { success = false, message = "Không có quyền truy cập." });

            var query = _context.Appointments
                .Include(a => a.Service).ThenInclude(s => s!.Category)
                .Include(a => a.Customer)
                .AsQueryable();

            if (isProvider)
                query = query.Where(a => a.ProviderId == userId);

            var appointments = await query
                .OrderByDescending(a => a.AppointmentDate)
                .Take(200)
                .ToListAsync();

            // Thống kê theo trạng thái
            var byStatus = appointments.GroupBy(a => a.Status)
                .Select(g => $"{g.Key}: {g.Count()}").ToList();

            // Thống kê theo ngày trong tuần
            var byDayOfWeek = appointments.GroupBy(a => a.AppointmentDate.DayOfWeek)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}: {g.Count()} lịch hẹn").ToList();

            // Thống kê theo giờ
            var byHour = appointments
                .Where(a => a.StartTime != default)
                .GroupBy(a => a.StartTime.Hours)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}:00 - {g.Count()} lịch hẹn").ToList();

            // Top dịch vụ
            var topServices = appointments
                .Where(a => a.Service != null)
                .GroupBy(a => a.Service!.Name)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => $"{g.Key}: {g.Count()} lượt đặt").ToList();

            // Tổng doanh thu (từ Completed)
            var completedAppointments = appointments.Where(a => a.Status == "Completed").ToList();
            var totalRevenue = completedAppointments
                .Where(a => a.Service != null)
                .Sum(a => a.Service!.Price);

            var statsData = $@"Phạm vi: {(isAdmin ? "Toàn hệ thống" : "Cá nhân Provider")}
Tổng lịch hẹn (gần nhất): {appointments.Count}
Phân bố trạng thái: {string.Join(", ", byStatus)}
Phân bố theo thứ: {string.Join(", ", byDayOfWeek)}
Phân bố theo giờ: {string.Join(", ", byHour)}
Top dịch vụ: {string.Join(", ", topServices)}
Số lịch hoàn thành: {completedAppointments.Count}
Doanh thu hoàn thành: {totalRevenue:N0} VND
Ngày hiện tại: {DateTime.Now:dd/MM/yyyy}";

            var analysis = await _ai.AnalyzeStatsAsync(statsData);
            return Json(new { success = true, analysis });
        }

        // DTO classes
        public class ChatRequest
        {
            public string Message { get; set; } = "";
            public string? History { get; set; }
        }

        public class RecommendationItem
        {
            public string serviceId { get; set; } = "";
            public string reason { get; set; } = "";
        }
    }
}
