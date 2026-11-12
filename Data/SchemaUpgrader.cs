using Microsoft.EntityFrameworkCore;

namespace Appointment.Data
{
    /// <summary>
    /// Bổ sung các bảng / cột mới (US-26, US-28, US-30, US-37, US-42) cho cơ sở dữ liệu ĐÃ TỒN TẠI từ trước.
    /// Với cơ sở dữ liệu mới, EF Core tự tạo đầy đủ từ model nên các lệnh ở đây sẽ không có tác dụng (IF NOT EXISTS).
    /// Chỉ thêm mới, không sửa / xóa dữ liệu hiện có.
    /// </summary>
    public static class SchemaUpgrader
    {
        public static void Apply(AppDbContext context)
        {
            bool isPostgres = context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

            // Kiểu dữ liệu theo từng provider
            string text = isPostgres ? "text" : "TEXT";
            string ts = isPostgres ? "timestamp with time zone" : "TEXT";
            string integer = isPostgres ? "integer" : "INTEGER";
            string dec = isPostgres ? "numeric(18,2)" : "TEXT";

            var statements = new List<string>
            {
                // US-26: Quên / đặt lại mật khẩu
                $@"CREATE TABLE IF NOT EXISTS ""PasswordResetTokens"" (
                    ""Id"" {text} NOT NULL CONSTRAINT ""PK_PasswordResetTokens"" PRIMARY KEY,
                    ""UserId"" {text} NOT NULL,
                    ""TokenHash"" {text} NOT NULL,
                    ""ExpiresAt"" {ts} NOT NULL,
                    ""UsedAt"" {ts} NULL,
                    ""CreatedAt"" {ts} NOT NULL,
                    CONSTRAINT ""FK_PasswordResetTokens_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
                )",
                @"CREATE INDEX IF NOT EXISTS ""IX_PasswordResetTokens_TokenHash"" ON ""PasswordResetTokens"" (""TokenHash"")",

                // US-30: Đánh giá dịch vụ
                $@"CREATE TABLE IF NOT EXISTS ""Reviews"" (
                    ""Id"" {text} NOT NULL CONSTRAINT ""PK_Reviews"" PRIMARY KEY,
                    ""AppointmentId"" {text} NOT NULL,
                    ""ServiceId"" {text} NOT NULL,
                    ""CustomerId"" {text} NOT NULL,
                    ""Rating"" {integer} NOT NULL,
                    ""Comment"" {text} NULL,
                    ""CreatedAt"" {ts} NOT NULL,
                    ""UpdatedAt"" {ts} NULL,
                    CONSTRAINT ""FK_Reviews_Appointments_AppointmentId"" FOREIGN KEY (""AppointmentId"") REFERENCES ""Appointments"" (""Id"") ON DELETE RESTRICT,
                    CONSTRAINT ""FK_Reviews_Services_ServiceId"" FOREIGN KEY (""ServiceId"") REFERENCES ""Services"" (""Id"") ON DELETE RESTRICT,
                    CONSTRAINT ""FK_Reviews_Users_CustomerId"" FOREIGN KEY (""CustomerId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
                )",
                @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Reviews_AppointmentId"" ON ""Reviews"" (""AppointmentId"")",

                // US-28: Thanh toán & đặt cọc
                $@"CREATE TABLE IF NOT EXISTS ""Payments"" (
                    ""Id"" {text} NOT NULL CONSTRAINT ""PK_Payments"" PRIMARY KEY,
                    ""AppointmentId"" {text} NOT NULL,
                    ""CustomerId"" {text} NOT NULL,
                    ""Amount"" {dec} NOT NULL,
                    ""Type"" {text} NOT NULL,
                    ""Method"" {text} NOT NULL,
                    ""Status"" {text} NOT NULL,
                    ""TransactionCode"" {text} NULL,
                    ""Note"" {text} NULL,
                    ""CreatedAt"" {ts} NOT NULL,
                    ""PaidAt"" {ts} NULL,
                    ""RefundedAt"" {ts} NULL,
                    CONSTRAINT ""FK_Payments_Appointments_AppointmentId"" FOREIGN KEY (""AppointmentId"") REFERENCES ""Appointments"" (""Id"") ON DELETE RESTRICT,
                    CONSTRAINT ""FK_Payments_Users_CustomerId"" FOREIGN KEY (""CustomerId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
                )",
                @"CREATE INDEX IF NOT EXISTS ""IX_Payments_AppointmentId"" ON ""Payments"" (""AppointmentId"")"
            };

            foreach (var sql in statements)
            {
                try
                {
                    context.Database.ExecuteSqlRaw(sql);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SchemaUpgrader] Bỏ qua lệnh lỗi: {ex.Message}");
                }
            }

            // US-37 / US-19: cột lý do hủy lịch (bảng Appointments đã tồn tại từ trước)
            try
            {
                context.Database.ExecuteSqlRaw(isPostgres
                    ? @"ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""CancelReason"" text NULL"
                    : @"ALTER TABLE ""Appointments"" ADD COLUMN ""CancelReason"" TEXT NULL");
            }
            catch
            {
                // Cột đã tồn tại -> bỏ qua
            }

            // US-42: Ghi chú nghiệp vụ nội bộ (bảng Appointments đã tồn tại từ trước)
            try
            {
                context.Database.ExecuteSqlRaw(isPostgres
                    ? @"ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""InternalNote"" text NULL"
                    : @"ALTER TABLE ""Appointments"" ADD COLUMN ""InternalNote"" TEXT NULL");
            }
            catch
            {
                // Cột đã tồn tại -> bỏ qua
            }

            // US-AI-04: Cột thời điểm gửi email nhắc lịch hẹn AI
            try
            {
                context.Database.ExecuteSqlRaw(isPostgres
                    ? @"ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""ReminderSentAt"" timestamp with time zone NULL"
                    : @"ALTER TABLE ""Appointments"" ADD COLUMN ""ReminderSentAt"" TEXT NULL");
            }
            catch
            {
                // Cột đã tồn tại -> bỏ qua
            }
        }
    }
}
