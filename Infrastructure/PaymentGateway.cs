namespace Appointment.Infrastructure
{
    public class GatewayResult
    {
        public bool Success { get; set; }
        public string? TransactionCode { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Cổng thanh toán. Hiện dùng <see cref="SandboxPaymentGateway"/> (mô phỏng, không trừ tiền thật).
    /// Để tích hợp VNPay / MoMo / PayOS... chỉ cần viết thêm một lớp implement interface này
    /// và đổi đăng ký trong Program.cs.
    /// </summary>
    public interface IPaymentGateway
    {
        string Name { get; }
        bool IsSandbox { get; }
        Task<GatewayResult> ChargeAsync(decimal amount, string method, string orderReference);
    }

    public class SandboxPaymentGateway : IPaymentGateway
    {
        public string Name => "Sandbox";
        public bool IsSandbox => true;

        public Task<GatewayResult> ChargeAsync(decimal amount, string method, string orderReference)
        {
            if (amount <= 0)
            {
                return Task.FromResult(new GatewayResult { Success = false, Error = "Số tiền thanh toán không hợp lệ." });
            }

            var code = $"SBX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
            return Task.FromResult(new GatewayResult { Success = true, TransactionCode = code });
        }
    }
}
