namespace Appointment.Infrastructure
{
    /// <summary>
    /// Interface cho các dịch vụ AI tích hợp Gemini.
    /// </summary>
    public interface IAiService
    {
        /// <summary>
        /// AI Chatbot: Trả lời câu hỏi của khách hàng về hệ thống AppointX.
        /// </summary>
        Task<string> ChatAsync(string userMessage, string? conversationHistory = null);

        /// <summary>
        /// AI Gợi ý dịch vụ thông minh dựa trên lịch sử và ngữ cảnh khách hàng.
        /// </summary>
        Task<string> RecommendServicesAsync(string customerContext, string availableServices);

        /// <summary>
        /// AI Phân tích và tóm tắt đánh giá từ khách hàng cho Provider.
        /// </summary>
        Task<string> AnalyzeReviewsAsync(string reviewsData);

        /// <summary>
        /// AI Soạn nội dung email nhắc nhở lịch hẹn cá nhân hóa.
        /// </summary>
        Task<string> GenerateReminderAsync(string appointmentDetails);

        /// <summary>
        /// AI Phân tích thống kê và dự báo xu hướng.
        /// </summary>
        Task<string> AnalyzeStatsAsync(string statsData);
    }
}
