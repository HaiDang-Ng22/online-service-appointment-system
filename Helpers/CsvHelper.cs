namespace Appointment.Helpers
{
    public static class CsvHelper
    {
        /// <summary>Escape một ô CSV; chặn CSV-injection khi mở bằng Excel (ô bắt đầu bằng = + - @).</summary>
        public static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            var v = value;
            if (v[0] is '=' or '+' or '-' or '@') v = "'" + v;
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }
    }
}
