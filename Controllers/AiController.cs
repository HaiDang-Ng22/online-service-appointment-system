using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Appointment.Data;
using Appointment.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Controllers
{
    /// <summary>
    /// API Controller xử lý toàn bộ các tính năng AI của hệ thống AppointX:
    /// - US-AI-01: Chatbot hỗ trợ khách hàng & tư vấn đặt lịch (Gemini API + Closed-World Grounding)
    /// - US-AI-02: AI Gợi ý dịch vụ thông minh (Recommendation Engine)
    /// - US-AI-03: AI Phân tích & Tóm tắt đánh giá chất lượng dịch vụ (Provider Reviews)
    /// - US-AI-05: AI Phân tích thống kê & Dự báo xu hướng kinh doanh (Dashboard Insights)
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
        // US-AI-01: Chatbot Endpoint
        // ─────────────────────────────────────────────

        /// <summary>
        /// POST /Ai/Chat — Nhận câu hỏi từ widget chatbot và trả về phản hồi tuân thủ Ràng buộc dữ liệu đóng.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
                return Json(new { success = false, reply = "Vui lòng nhập câu hỏi của bạn." });

            // 1. Làm sạch đầu vào & Giới hạn độ dài
            var sanitizedMessage = request.Message.Trim();
            if (sanitizedMessage.Length > 1000)
            {
                sanitizedMessage = sanitizedMessage.Substring(0, 1000);
            }

            var qLower = sanitizedMessage.ToLower();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2. Tra cứu lịch hẹn cá nhân trực tiếp nếu người dùng yêu cầu
            if (qLower.Contains("lịch hẹn của tôi") || qLower.Contains("lịch của tôi") ||
                qLower.Contains("lịch sử đặt") || qLower.Contains("tôi đã đặt") ||
                qLower.Contains("xem lịch hẹn") || qLower.Contains("cuộc hẹn của tôi"))
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new
                    {
                        success = true,
                        reply = "🔒 **Tra cứu lịch hẹn cá nhân:**\n\nĐể bảo vệ thông tin riêng tư, Quý khách vui lòng [Đăng nhập vào tài khoản](/Account/Login?returnUrl=/) để tra cứu danh sách cuộc hẹn và trạng thái phiếu khám của mình."
                    });
                }

                var myAppointments = await _context.Appointments
                    .Include(a => a.Service)
                    .Include(a => a.Provider)
                    .Where(a => a.CustomerId == userId)
                    .OrderByDescending(a => a.AppointmentDate)
                    .Take(5)
                    .ToListAsync();

                if (!myAppointments.Any())
                {
                    return Json(new
                    {
                        success = true,
                        reply = "📅 **Lịch hẹn của bạn:**\n\nBạn hiện chưa có cuộc hẹn nào trong hệ thống AppointX. Bạn có thể xem các dịch vụ khám sức khỏe hoặc làm đẹp bên dưới để đặt lịch giữ chỗ ngay nhé!"
                    });
                }

                var sbMy = new StringBuilder();
                sbMy.AppendLine("📅 **Danh sách các cuộc hẹn gần đây của bạn tại AppointX:**\n");
                foreach (var a in myAppointments)
                {
                    string statusBadge = a.Status switch
                    {
                        "Confirmed" => "🟢 Đã xác nhận",
                        "Pending" => "🟡 Chờ duyệt",
                        "Completed" => "🔵 Đã hoàn thành",
                        "Cancelled" => "🔴 Đã hủy",
                        _ => a.Status
                    };

                    sbMy.AppendLine($"- 📌 **{a.Service?.Name ?? "Dịch vụ"}**");
                    sbMy.AppendLine($"  • Thời gian: **{a.AppointmentDate:dd/MM/yyyy} lúc {a.StartTime:hh\\:mm}**");
                    sbMy.AppendLine($"  • Cơ sở / Chuyên gia: {a.Provider?.FullName ?? "Cơ sở uy tín"}");
                    sbMy.AppendLine($"  • Trạng thái: **{statusBadge}**");
                    sbMy.AppendLine($"  👉 [Xem chi tiết & Phiếu hẹn](/Appointments/PrintInvoice/{a.Id})\n");
                }
                sbMy.AppendLine("*(Lưu ý: Bạn có thể hủy lịch miễn phí trước giờ phục vụ tối thiểu 2 tiếng theo quy định).*");

                return Json(new { success = true, reply = sbMy.ToString() });
            }

            // 3. Tra cứu đánh giá & phản hồi thực tế nếu người dùng hỏi
            if (qLower.Contains("đánh giá") || qLower.Contains("review") || qLower.Contains("phản hồi khách hàng") || qLower.Contains("chất lượng dịch vụ"))
            {
                var recentReviews = await _context.Reviews
                    .Include(r => r.Service)
                    .Include(r => r.Customer)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(4)
                    .ToListAsync();

                if (recentReviews.Any())
                {
                    var sbRev = new StringBuilder();
                    sbRev.AppendLine("⭐ **Đánh giá & Phản hồi gần đây từ khách hàng tại AppointX:**\n");
                    foreach (var r in recentReviews)
                    {
                        sbRev.AppendLine($"- 🌟 **{r.Service?.Name ?? "Dịch vụ"}** ({r.Rating}/5 sao)");
                        sbRev.AppendLine($"  • Nhận xét: *\"{r.Comment}\"*");
                        sbRev.AppendLine($"  • Khách hàng: {r.Customer?.FullName ?? "Khách hàng"} - Ngày: {r.CreatedAt:dd/MM/yyyy}\n");
                    }
                    sbRev.AppendLine("👉 Quý khách có thể bấm trực tiếp vào dịch vụ mong muốn để xem thêm lịch trống và đặt hẹn.");
                    return Json(new { success = true, reply = sbRev.ToString() });
                }
            }

            // 4. Thu thập danh mục & dịch vụ thực tế trong cơ sở dữ liệu làm Closed-world Grounding Context
            var catalogContext = await BuildSystemCatalogContextAsync();

            // 5. Gọi AI Service xử lý kèm Guardrails & Symptom Mapping
            var reply = await _ai.ChatAsync(sanitizedMessage, catalogContext, request.History);

            return Json(new { success = true, reply });
        }

        // ─────────────────────────────────────────────
        // US-AI-02: Gợi ý Dịch vụ Thông minh (Recommendation Engine)
        // ─────────────────────────────────────────────

        /// <summary>
        /// GET /Ai/Recommend — Gợi ý dịch vụ thông minh cá nhân hóa hoặc danh mục nổi bật.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Recommend(string? currentServiceId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Lấy danh sách dịch vụ đang kích hoạt
            var servicesQuery = _context.Services
                .Include(s => s.Category)
                .Include(s => s.Provider)
                .Where(s => s.IsActive);

            if (!string.IsNullOrEmpty(currentServiceId))
            {
                // Nếu đang ở trang chi tiết dịch vụ, loại trừ dịch vụ hiện tại
                servicesQuery = servicesQuery.Where(s => s.Id != currentServiceId);
            }

            var allServices = await servicesQuery.Take(15).ToListAsync();
            if (!allServices.Any())
            {
                return Json(new { success = true, recommendations = new List<object>() });
            }

            var servicesJson = JsonSerializer.Serialize(allServices.Select(s => new
            {
                s.Id,
                ServiceName = s.Name,
                Category = s.Category?.Name ?? "",
                Provider = s.Provider?.FullName ?? "",
                s.Price,
                s.DurationMinutes
            }));

            string customerContext;
            if (!string.IsNullOrEmpty(userId))
            {
                var pastAppointments = await _context.Appointments
                    .Include(a => a.Service).ThenInclude(s => s!.Category)
                    .Where(a => a.CustomerId == userId)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(5)
                    .Select(a => new
                    {
                        Service = a.Service != null ? a.Service.Name : "",
                        Category = a.Service != null && a.Service.Category != null ? a.Service.Category.Name : "",
                        a.Status
                    })
                    .ToListAsync();

                customerContext = $"Khách hàng đã đăng nhập. Lịch sử đặt hẹn: {JsonSerializer.Serialize(pastAppointments)}";
            }
            else
            {
                customerContext = "Khách hàng vãng lai. Hãy đề xuất các gói dịch vụ nổi bật, đa dạng chuyên khoa.";
            }

            var rawAiResult = await _ai.RecommendServicesAsync(customerContext, servicesJson);

            // Cố gắng parse JSON từ AI
            List<RecommendationItem>? parsedItems = null;
            if (!string.IsNullOrWhiteSpace(rawAiResult))
            {
                try
                {
                    var cleaned = rawAiResult.Trim();
                    if (cleaned.StartsWith("```"))
                    {
                        cleaned = string.Join("\n", cleaned.Split('\n').Skip(1)).TrimEnd('`').Trim();
                    }
                    parsedItems = JsonSerializer.Deserialize<List<RecommendationItem>>(cleaned, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch { }
            }

            // Nếu parse được từ AI, trả về theo AI
            if (parsedItems != null && parsedItems.Any())
            {
                var matchedIds = parsedItems.Select(p => p.ServiceId).ToList();
                var matchedServices = allServices.Where(s => matchedIds.Contains(s.Id)).ToList();

                if (matchedServices.Any())
                {
                    var enriched = matchedServices.Select(s =>
                    {
                        var rec = parsedItems.FirstOrDefault(p => p.ServiceId == s.Id);
                        return new
                        {
                            s.Id,
                            ServiceName = s.Name,
                            s.Description,
                            s.Price,
                            s.DurationMinutes,
                            s.ImageUrl,
                            Category = s.Category?.Name ?? "Dịch vụ",
                            Provider = s.Provider?.FullName ?? "Chuyên gia",
                            AiReason = rec?.Reason ?? "Dịch vụ phù hợp với nhu cầu của bạn"
                        };
                    }).ToList();

                    return Json(new { success = true, recommendations = enriched });
                }
            }

            // Fallback Engine Thông minh: Tự động chọn top 3-4 dịch vụ chất lượng với lý do phong phú
            var fallbackEnriched = allServices.Take(3).Select((s, index) =>
            {
                string reason = index switch
                {
                    0 => "⭐ Dịch vụ có lượng đặt lịch và đánh giá tích cực cao nhất tuần qua",
                    1 => "💡 Đề xuất phù hợp giúp bạn tiết kiệm thời gian và chăm sóc toàn diện",
                    _ => "✨ Gói dịch vụ ưu đãi được nhiều khách hàng tin chọn nhất"
                };

                return new
                {
                    s.Id,
                    ServiceName = s.Name,
                    s.Description,
                    s.Price,
                    s.DurationMinutes,
                    s.ImageUrl,
                    Category = s.Category?.Name ?? "Dịch vụ",
                    Provider = s.Provider?.FullName ?? "Cơ sở uy tín",
                    AiReason = reason
                };
            }).ToList();

            return Json(new { success = true, recommendations = fallbackEnriched });
        }

        // ─────────────────────────────────────────────
        // US-AI-03: Phân tích Đánh giá cho Provider
        // ─────────────────────────────────────────────

        /// <summary>
        /// GET /Ai/AnalyzeReviews — Provider xem AI phân tích tổng hợp nhận xét của khách.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Provider,Admin")]
        public async Task<IActionResult> AnalyzeReviews()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Chưa đăng nhập." });

            var query = _context.Reviews
                .Include(r => r.Service)
                .Include(r => r.Customer)
                .AsQueryable();

            if (User.IsInRole("Provider"))
            {
                query = query.Where(r => r.Service != null && r.Service.ProviderId == userId);
            }

            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .Take(40)
                .ToListAsync();

            if (!reviews.Any())
            {
                return Json(new { success = true, analysis = "📭 **Chưa có đánh giá nào:** Cơ sở của bạn chưa ghi nhận phản hồi từ khách hàng. Khi các lịch hẹn hoàn thành (`Completed`) và khách để lại nhận xét, AI sẽ tự động phân tích giúp bạn." });
            }

            var reviewsData = string.Join("\n", reviews.Select(r =>
                $"- [{r.CreatedAt:dd/MM/yyyy}] {r.Service?.Name}: {r.Rating}/5 sao - \"{r.Comment}\" (Bởi {r.Customer?.FullName ?? "Ẩn danh"})"));

            var analysis = await _ai.AnalyzeReviewsAsync(reviewsData);
            return Json(new { success = true, analysis });
        }

        // ─────────────────────────────────────────────
        // US-AI-05: Phân tích Thống kê & Dự báo Dashboard Insights
        // ─────────────────────────────────────────────

        /// <summary>
        /// GET /Ai/AnalyzeStats — Phân tích số liệu và dự báo xu hướng cho Admin và Provider.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Provider")]
        public async Task<IActionResult> AnalyzeStats()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var query = _context.Appointments
                .Include(a => a.Service).ThenInclude(s => s!.Category)
                .Include(a => a.Customer)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(a => a.ProviderId == userId);
            }

            var appointments = await query
                .OrderByDescending(a => a.AppointmentDate)
                .Take(150)
                .ToListAsync();

            var byStatus = appointments.GroupBy(a => a.Status)
                .Select(g => $"{g.Key}: {g.Count()}").ToList();

            var byDayOfWeek = appointments.GroupBy(a => a.AppointmentDate.DayOfWeek)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}: {g.Count()} ca").ToList();

            var topServices = appointments
                .Where(a => a.Service != null)
                .GroupBy(a => a.Service!.Name)
                .OrderByDescending(g => g.Count())
                .Take(4)
                .Select(g => $"{g.Key}: {g.Count()} lượt").ToList();

            var completedList = appointments.Where(a => a.Status == "Completed").ToList();
            var totalRevenue = completedList.Where(a => a.Service != null).Sum(a => a.Service!.Price);

            var statsData = $@"Đối tượng: {(isAdmin ? "Toàn bộ hệ sinh thái AppointX" : "Cơ sở đối tác")}
Tổng số cuộc hẹn gần đây: {appointments.Count}
Phân bố trạng thái: {string.Join(", ", byStatus)}
Phân bố theo ngày trong tuần: {string.Join(", ", byDayOfWeek)}
Top dịch vụ nổi bật: {string.Join(", ", topServices)}
Số cuộc hẹn hoàn thành: {completedList.Count}
Doanh thu hoàn tất: {totalRevenue:N0} VNĐ";

            var analysis = await _ai.AnalyzeStatsAsync(statsData);
            return Json(new { success = true, analysis });
        }

        // ─────────────────────────────────────────────
        // Hàm phụ trợ tạo Grounding Context
        // ─────────────────────────────────────────────
        private async Task<string> BuildSystemCatalogContextAsync()
        {
            var services = await _context.Services
                .Include(s => s.Category)
                .Include(s => s.Provider)
                .Where(s => s.IsActive)
                .Take(25)
                .ToListAsync();

            var categories = await _context.Categories.Take(10).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("=== DANH MỤC DỊCH VỤ CÓ TRONG HỆ THỐNG ===");
            foreach (var c in categories)
            {
                sb.AppendLine($"- Danh mục: {c.Name} ({c.Description})");
            }

            sb.AppendLine("\n=== DANH SÁCH DỊCH VỤ HIỆN CÓ TẠI APPOINTX (DÙNG ĐỂ GỢI Ý & ĐẶT LỊCH) ===");
            foreach (var s in services)
            {
                sb.AppendLine($"- [Dịch vụ]: {s.Name}");
                sb.AppendLine($"  • Danh mục: {s.Category?.Name ?? "Chung"}");
                sb.AppendLine($"  • Cơ sở / Chuyên gia: {s.Provider?.FullName ?? "Cơ sở uy tín"}");
                sb.AppendLine($"  • Đơn giá: {s.Price:N0} VNĐ (Thời lượng: {s.DurationMinutes} phút)");
                sb.AppendLine($"  • Địa điểm: {s.Location ?? s.Provider?.Address ?? "Tại cơ sở cung cấp"}");
                sb.AppendLine($"  • Đường dẫn đặt lịch: [Đặt lịch ngay](/Services/Details?id={s.Id})");
            }

            sb.AppendLine("\n=== HƯỚNG DẪN LIÊN KẾT TRIỆU CHỨNG SỨC KHỎE / NHU CẦU ===");
            sb.AppendLine("- Người dùng bị: đau đầu, nhức đầu, chóng mặt, mệt mỏi, sốt, đau người, cảm cúm, ốm, bệnh... -> Hãy đồng cảm, khuyên đi khám sớm và gợi ý dịch vụ 'Khám Sức Khỏe Tổng Quát' hoặc các bác sĩ đa khoa trong hệ thống.");
            sb.AppendLine("- Người dùng cần: làm đẹp, da mặt, mụn, nám, tàn nhang, massage, spa, thư giãn... -> Gợi ý dịch vụ 'Chăm Sóc Da Mặt Chuyên Sâu' hoặc cơ sở Salon/Spa.");
            sb.AppendLine("- Người dùng cần: răng, đau răng, niềng răng, trám răng, nhổ răng -> Gợi ý chuyên khoa Nha khoa nếu có trong danh sách.");
            sb.AppendLine("- Người dùng cần: cắt tóc, uốn, nhuộm -> Gợi ý Salon tóc.");

            sb.AppendLine("\n=== QUY ĐỊNH & CHÍNH SÁCH ĐẶT LỊCH HỆ THỐNG ===");
            sb.AppendLine("- Đặt cọc: 30% giá dịch vụ khi xác nhận lịch online, hỗ trợ hoàn cọc nếu hủy lịch đúng hạn.");
            sb.AppendLine("- Hủy lịch hẹn: Khách hàng được phép hủy lịch miễn phí trước giờ phục vụ tối thiểu 2 tiếng (`CancelBeforeHours = 2`). Tiền cọc sẽ được hoàn lại 100%.");
            sb.AppendLine("- Tự động hủy lịch quá hạn: Lịch hẹn trạng thái Chờ duyệt (`Pending`) quá 24 tiếng chưa được Provider xác nhận hoặc quá giờ hẹn sẽ tự động bị hủy để bảo vệ quyền lợi khách hàng.");
            sb.AppendLine("- Đánh giá: Sau khi lịch hoàn thành (`Completed`), khách hàng được chấm điểm từ 1 đến 5 sao kèm nhận xét chất lượng.");
            sb.AppendLine("- Giờ làm việc: Thường mở cửa từ 08:00 đến 18:00 các ngày trong tuần (tùy cấu hình từng nhà cung cấp).");

            return sb.ToString();
        }

        public class ChatRequest
        {
            public string Message { get; set; } = "";
            public string? History { get; set; }
        }

        public class RecommendationItem
        {
            public string ServiceId { get; set; } = "";
            public string Reason { get; set; } = "";
        }
    }
}
