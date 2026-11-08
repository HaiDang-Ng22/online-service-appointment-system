using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Appointment.Infrastructure
{
    public interface IEmailSender
    {
        /// <summary>Gửi email HTML. Trả về true nếu đã gửi qua SMTP, false nếu chưa cấu hình hoặc gửi lỗi.</summary>
        Task<bool> SendAsync(string toEmail, string subject, string htmlBody);

        /// <summary>True khi SMTP đã được bật & cấu hình đầy đủ.</summary>
        bool IsConfigured { get; }
    }

    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.Host);

        public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("SMTP chưa được cấu hình (Email:Enabled / Email:Host). Bỏ qua gửi email '{Subject}' tới {To}.", subject, toEmail);
                return false;
            }

            try
            {
                using var client = new SmtpClient(_options.Host, _options.Port)
                {
                    EnableSsl = _options.EnableSsl
                };

                if (!string.IsNullOrWhiteSpace(_options.Username))
                {
                    client.Credentials = new NetworkCredential(_options.Username, _options.Password);
                }

                using var message = new MailMessage
                {
                    From = new MailAddress(_options.From, _options.FromName, System.Text.Encoding.UTF8),
                    Subject = subject,
                    SubjectEncoding = System.Text.Encoding.UTF8,
                    Body = htmlBody,
                    BodyEncoding = System.Text.Encoding.UTF8,
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);

                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gửi email tới {To} thất bại.", toEmail);
                return false;
            }
        }
    }
}
