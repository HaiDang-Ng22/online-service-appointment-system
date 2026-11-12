using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Appointment.Infrastructure
{
    // Cấu hình Gemini từ appsettings.json
    public class GeminiOptions
    {
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "gemini-2.0-flash";
    }

    /// <summary>
    /// Triển khai IAiService sử dụng Google Gemini API (REST).
    /// Không cần thêm NuGet package — chỉ dùng HttpClient gọi REST trực tiếp.
    /// </summary>
    public class GeminiAiService : IAiService
    {
        private readonly HttpClient _http;
        private readonly GeminiOptions _options;
        private readonly ILogger<GeminiAiService> _logger;

        private const string GEMINI_BASE = "https://generativelanguage.googleapis.com/v1beta/models";

        // System context về AppointX để chatbot hiểu ngữ cảnh
        private const string SYSTEM_CONTEXT = @"
Bạn là trợ lý AI của hệ thống AppointX — nền tảng đặt lịch dịch vụ trực tuyến.
AppointX hỗ trợ nhiều loại hình: Bác sĩ/Phòng khám, Salon/Spa, Tư vấn viên, Thể thao, Giáo dục, v.v.

Quy trình đặt lịch:
1. Khách tìm kiếm dịch vụ theo tên, danh mục, hoặc nhà cung cấp.
2. Chọn dịch vụ → xem chi tiết, giá, thời lượng, đánh giá.
3. Chọn ngày → xem khung giờ còn trống → đặt lịch.
4. Hệ thống xác nhận → thanh toán/đặt cọc (nếu có).
5. Nhà cung cấp xác nhận → khách đến đúng giờ → check-in → hoàn thành.
6. Sau khi hoàn thành, khách có thể đánh giá dịch vụ (1-5 sao).

Vai trò hệ thống: Guest (khách vãng lai), Customer (khách hàng), Provider (nhà cung cấp), Staff (nhân viên), Admin (quản trị).

Hãy trả lời bằng tiếng Việt, thân thiện, ngắn gọn và chính xác. 
Nếu khách hỏi ngoài phạm vi hệ thống, hãy lịch sự hướng dẫn lại.
Sử dụng emoji phù hợp để tăng tính thân thiện.";

        public GeminiAiService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<GeminiAiService> logger)
        {
            _http = httpFactory.CreateClient("GeminiApi");
            _options = new GeminiOptions();
            config.GetSection("Gemini").Bind(_options);
            _logger = logger;
        }

        /// <summary>
        /// Gọi Gemini API generateContent endpoint.
        /// </summary>
        private async Task<string> CallGeminiAsync(string systemPrompt, string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _logger.LogWarning("Gemini API Key chưa được cấu hình trong appsettings.json");
                return "⚠️ Chức năng AI chưa được kích hoạt. Vui lòng liên hệ quản trị viên để cấu hình Gemini API Key.";
            }

            try
            {
                var url = $"{GEMINI_BASE}/{_options.Model}:generateContent?key={_options.ApiKey}";

                var requestBody = new
                {
                    system_instruction = new
                    {
                        parts = new[] { new { text = systemPrompt } }
                    },
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[] { new { text = userPrompt } }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 2048,
                        topP = 0.95
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Gemini API error {StatusCode}: {Body}", response.StatusCode, responseBody);
                    return "⚠️ Xin lỗi, AI tạm thời không khả dụng. Vui lòng thử lại sau.";
                }

                // Parse response JSON
                using var doc = JsonDocument.Parse(responseBody);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var parts = candidates[0].GetProperty("content").GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        return parts[0].GetProperty("text").GetString() ?? "Không có phản hồi từ AI.";
                    }
                }

                return "Không có phản hồi từ AI.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi Gemini API");
                return "⚠️ Đã xảy ra lỗi khi kết nối với AI. Vui lòng thử lại sau.";
            }
        }

        public async Task<string> ChatAsync(string userMessage, string? conversationHistory = null)
        {
            var prompt = userMessage;
            if (!string.IsNullOrWhiteSpace(conversationHistory))
            {
                prompt = $"Lịch sử hội thoại trước đó:\n{conversationHistory}\n\nCâu hỏi mới của khách: {userMessage}";
            }

            return await CallGeminiAsync(SYSTEM_CONTEXT, prompt);
        }

        public async Task<string> RecommendServicesAsync(string customerContext, string availableServices)
        {
            var systemPrompt = @"Bạn là AI gợi ý dịch vụ của AppointX. 
Dựa trên thông tin khách hàng và danh sách dịch vụ hiện có, hãy gợi ý 3-5 dịch vụ phù hợp nhất.
Trả về dạng JSON array với format: [{""serviceId"": number, ""reason"": ""lý do gợi ý ngắn gọn""}]
CHỈ trả về JSON, không giải thích thêm.";

            var userPrompt = $"Thông tin khách hàng:\n{customerContext}\n\nDanh sách dịch vụ:\n{availableServices}";
            return await CallGeminiAsync(systemPrompt, userPrompt);
        }

        public async Task<string> AnalyzeReviewsAsync(string reviewsData)
        {
            var systemPrompt = @"Bạn là AI phân tích đánh giá của AppointX.
Hãy phân tích tổng hợp các đánh giá từ khách hàng và trả về báo cáo bằng tiếng Việt gồm:
1. 📊 Tóm tắt tổng quan (sentiment chung: tích cực/tiêu cực/trung lập, điểm trung bình)
2. ✅ Điểm mạnh nổi bật (top 3 điểm khách khen)
3. ⚠️ Điểm cần cải thiện (top 3 điểm khách chê)
4. 🔑 Từ khóa nổi bật
5. 💡 Gợi ý cải thiện cụ thể
Dùng markdown formatting, ngắn gọn nhưng có chiều sâu.";

            return await CallGeminiAsync(systemPrompt, $"Dữ liệu đánh giá:\n{reviewsData}");
        }

        public async Task<string> GenerateReminderAsync(string appointmentDetails)
        {
            var systemPrompt = @"Bạn là AI soạn email nhắc nhở lịch hẹn cho AppointX.
Hãy soạn nội dung email nhắc nhở ngắn gọn, thân thiện, chuyên nghiệp bằng tiếng Việt.
Bao gồm: lời chào cá nhân, thông tin lịch hẹn (dịch vụ, ngày giờ, địa điểm), lưu ý chuẩn bị, và lời chúc.
Chỉ trả về nội dung email (plain text), không có subject line hay metadata.";

            return await CallGeminiAsync(systemPrompt, $"Thông tin lịch hẹn:\n{appointmentDetails}");
        }

        public async Task<string> AnalyzeStatsAsync(string statsData)
        {
            var systemPrompt = @"Bạn là AI phân tích kinh doanh của AppointX.
Dựa trên dữ liệu thống kê được cung cấp, hãy phân tích và trả về báo cáo bằng tiếng Việt gồm:
1. 📈 Tổng quan hiệu suất
2. ⏰ Giờ cao điểm & ngày bận nhất
3. 🏆 Dịch vụ/Danh mục phổ biến nhất
4. 📉 Xu hướng tăng/giảm so với trước
5. 🔮 Dự báo tuần tới (dựa trên pattern hiện tại)
6. 💡 Khuyến nghị hành động cụ thể
Dùng markdown formatting, có số liệu cụ thể. Trả lời bằng tiếng Việt.";

            return await CallGeminiAsync(systemPrompt, $"Dữ liệu thống kê:\n{statsData}");
        }
    }
}
