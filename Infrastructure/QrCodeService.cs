using QRCoder;

namespace Appointment.Infrastructure
{
    /// <summary>US-32: tạo ảnh PNG mã QR ngay trên máy chủ (không gửi mã lịch hẹn cho dịch vụ bên thứ ba).</summary>
    public class QrCodeService
    {
        public byte[] GeneratePng(string content, int pixelsPerModule = 10)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data);
            return png.GetGraphic(pixelsPerModule);
        }
    }
}
