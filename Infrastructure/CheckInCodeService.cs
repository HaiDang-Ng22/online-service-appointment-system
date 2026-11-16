using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Appointment.Infrastructure
{
    public enum CheckInCodeKind
    {
        Invalid,
        /// <summary>Mã QR có chữ ký hợp lệ (APX:id:chữ ký).</summary>
        Signed,
        /// <summary>Mã nhập tay / định dạng cũ (id đầy đủ hoặc mã rút gọn APX-XXXXXXXX).</summary>
        Manual
    }

    public class CheckInCodeResult
    {
        public CheckInCodeKind Kind { get; set; } = CheckInCodeKind.Invalid;
        /// <summary>Id lịch hẹn (Signed) hoặc id / tiền tố id viết thường (Manual).</summary>
        public string Id { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    /// <summary>
    /// US-32: tạo & kiểm tra mã QR check-in. Nội dung QR có dạng <c>APX:{idLịchHẹn}:{chữ ký}</c>,
    /// chữ ký = 12 ký tự đầu của HMAC-SHA256(id) nên không thể tự bịa mã cho lịch hẹn của người khác.
    /// </summary>
    public class CheckInCodeService
    {
        private const int MinManualLength = 8;
        private readonly byte[] _key;

        public CheckInCodeService(IOptions<CheckInOptions> options)
        {
            var secret = string.IsNullOrWhiteSpace(options.Value.SecretKey)
                ? new CheckInOptions().SecretKey
                : options.Value.SecretKey;
            _key = Encoding.UTF8.GetBytes(secret);
        }

        public string Sign(string appointmentId)
        {
            var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(appointmentId.ToLowerInvariant()));
            return Convert.ToHexString(hash).Substring(0, 12);
        }

        public string BuildPayload(string appointmentId) => $"APX:{appointmentId.ToLowerInvariant()}:{Sign(appointmentId)}";

        /// <summary>Mã hiển thị cho người đọc, ví dụ APX-1A2B3C4D.</summary>
        public static string ShortCode(string appointmentId) =>
            "APX-" + (appointmentId.Length >= 8 ? appointmentId.Substring(0, 8) : appointmentId).ToUpperInvariant();

        public CheckInCodeResult Parse(string? raw)
        {
            var result = new CheckInCodeResult();
            if (string.IsNullOrWhiteSpace(raw))
            {
                result.Error = "Mã check-in không được để trống.";
                return result;
            }

            var code = raw.Trim();

            // 1) Mã QR có chữ ký: APX:{id}:{sig}
            var signed = Regex.Match(code, @"^APX:([0-9a-fA-F]{32}):([0-9a-fA-F]{12})$");
            if (signed.Success)
            {
                string id = signed.Groups[1].Value.ToLowerInvariant();
                string sig = signed.Groups[2].Value.ToUpperInvariant();
                var expected = Encoding.UTF8.GetBytes(Sign(id));
                var actual = Encoding.UTF8.GetBytes(sig);

                if (!CryptographicOperations.FixedTimeEquals(expected, actual))
                {
                    result.Error = "Mã QR không hợp lệ hoặc đã bị chỉnh sửa (sai chữ ký).";
                    return result;
                }

                result.Kind = CheckInCodeKind.Signed;
                result.Id = id;
                return result;
            }

            if (code.StartsWith("APX:", StringComparison.OrdinalIgnoreCase))
            {
                result.Error = "Mã QR không đúng định dạng.";
                return result;
            }

            // 2) Mã nhập tay / định dạng cũ: URL có id=/code=, #APX-xxxx, APX-xxxx, hoặc id thô
            string clean = code;
            var query = Regex.Match(clean, @"[?&](?:id|code)=([a-zA-Z0-9]+)");
            if (query.Success) clean = query.Groups[1].Value;
            else if (clean.StartsWith("#APX-", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(5);
            else if (clean.StartsWith("APX-", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(4);

            clean = clean.Trim().ToLowerInvariant();

            if (!Regex.IsMatch(clean, @"^[0-9a-f]+$") || clean.Length < MinManualLength)
            {
                result.Error = $"Mã không hợp lệ. Vui lòng nhập tối thiểu {MinManualLength} ký tự của mã lịch hẹn (ví dụ APX-1A2B3C4D).";
                return result;
            }

            result.Kind = CheckInCodeKind.Manual;
            result.Id = clean;
            return result;
        }
    }
}
