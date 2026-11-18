using Appointment.Models;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Appointment.Infrastructure
{
    /// <summary>
    /// US-32: xuất phiếu hẹn dạng PDF (khổ A5) gồm thông tin lịch hẹn, tình trạng thanh toán và mã QR check-in.
    /// Dùng font DejaVu Sans đi kèm trong thư mục /Fonts để hiển thị đúng tiếng Việt trên mọi hệ điều hành.
    /// Lưu ý giấy phép: QuestPDF Community miễn phí cho cá nhân / dự án học tập / doanh nghiệp doanh thu &lt; 1 triệu USD/năm.
    /// </summary>
    public class AppointmentSlipPdfService
    {
        private const string FontFamily = "DejaVu Sans";
        private static readonly object InitLock = new object();
        private static bool _initialized;

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (InitLock)
            {
                if (_initialized) return;

                QuestPDF.Settings.License = LicenseType.Community;

                foreach (var file in new[] { "DejaVuSans.ttf", "DejaVuSans-Bold.ttf" })
                {
                    var path = Path.Combine(AppContext.BaseDirectory, "Fonts", file);
                    if (File.Exists(path))
                    {
                        using var stream = File.OpenRead(path);
                        FontManager.RegisterFont(stream);
                    }
                }

                _initialized = true;
            }
        }

        public byte[] Generate(AppointmentModel a, PaymentSummary pay, byte[]? qrPng, string shortCode)
        {
            EnsureInitialized();
            bool cancelled = a.Status == AppointmentRules.Cancelled;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(24);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(9.5f).FontColor("#1e293b"));

                    page.Header().Element(c => ComposeHeader(c, shortCode));
                    page.Content().PaddingVertical(10).Element(c => ComposeContent(c, a, pay, qrPng, shortCode, cancelled));
                    page.Footer().Element(ComposeFooter);
                });
            });

            document.WithMetadata(new DocumentMetadata
            {
                Title = $"Phiếu hẹn {shortCode}",
                Author = "AppointX",
                Subject = "Phiếu hẹn dịch vụ"
            });

            return document.GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, string shortCode)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("AppointX").FontSize(20).Bold().FontColor("#2563eb");
                        c.Item().Text("Hệ thống đặt lịch dịch vụ trực tuyến").FontSize(8).FontColor("#64748b");
                    });

                    row.ConstantItem(150).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("PHIẾU HẸN DỊCH VỤ").FontSize(11).Bold();
                        c.Item().AlignRight().Text(shortCode).FontSize(11).Bold().FontColor("#2563eb");
                    });
                });

                col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor("#2563eb");
            });
        }

        private static void ComposeContent(IContainer container, AppointmentModel a, PaymentSummary pay,
            byte[]? qrPng, string shortCode, bool cancelled)
        {
            container.Column(col =>
            {
                col.Spacing(8);

                // Dải trạng thái
                col.Item().Background(StatusColor(a.Status)).PaddingVertical(5).AlignCenter()
                    .Text(AppointmentRules.StatusLabel(a.Status).ToUpper()).Bold().FontSize(10).FontColor(Colors.White);

                // Thông tin chính + mã QR
                col.Item().Row(row =>
                {
                    row.RelativeItem().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(72);
                            c.RelativeColumn();
                        });

                        InfoRow(table, "Khách hàng", a.Customer?.FullName);
                        InfoRow(table, "Điện thoại", a.Customer?.PhoneNumber);
                        InfoRow(table, "Dịch vụ", a.Service?.Name);
                        InfoRow(table, "Nhà cung cấp", a.Provider?.FullName);
                        InfoRow(table, "Ngày hẹn", $"{WeekdayName(a.AppointmentDate.DayOfWeek)}, {a.AppointmentDate:dd/MM/yyyy}");
                        InfoRow(table, "Giờ", $"{a.StartTime:hh\\:mm} - {a.EndTime:hh\\:mm}");
                        InfoRow(table, "Địa điểm", a.Service?.Location);
                        InfoRow(table, "Tiếp nhận", a.Staff?.FullName);
                    });

                    row.ConstantItem(128).PaddingLeft(10).Column(q =>
                    {
                        if (!cancelled && qrPng != null)
                        {
                            q.Item().Border(0.75f).BorderColor("#cbd5e1").Padding(4).Width(110).Image(qrPng);
                            q.Item().PaddingTop(3).AlignCenter().Text("MÃ QR CHECK-IN").FontSize(8).Bold().FontColor("#2563eb");
                            q.Item().AlignCenter().Text(shortCode).FontSize(8).FontColor("#475569");
                        }
                        else
                        {
                            q.Item().Border(0.75f).BorderColor("#cbd5e1").Padding(8).AlignCenter()
                                .Text("Phiếu đã hủy\nkhông còn mã check-in").FontSize(8).FontColor("#64748b");
                        }
                    });
                });

                if (cancelled && !string.IsNullOrWhiteSpace(a.CancelReason))
                {
                    col.Item().Background("#f1f5f9").Padding(6)
                        .Text($"Lý do hủy: {a.CancelReason}").FontSize(8.5f).FontColor("#475569");
                }

                // Thanh toán
                col.Item().Column(p =>
                {
                    p.Item().PaddingBottom(2).Text("THANH TOÁN").Bold().FontSize(9).FontColor("#475569");
                    p.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });

                        HeaderCell(table, "Giá dịch vụ");
                        HeaderCell(table, "Đã thanh toán");
                        HeaderCell(table, "Còn lại");
                        HeaderCell(table, "Tình trạng");

                        BodyCell(table, Money(pay.Total));
                        BodyCell(table, Money(pay.Paid));
                        BodyCell(table, Money(pay.Remaining));
                        BodyCell(table, pay.Label);
                    });
                });

                if (!string.IsNullOrWhiteSpace(a.Notes))
                {
                    col.Item().Column(n =>
                    {
                        n.Item().Text("GHI CHÚ CỦA KHÁCH").Bold().FontSize(9).FontColor("#475569");
                        n.Item().Text(a.Notes).FontSize(9);
                    });
                }
            });
        }

        private static void ComposeFooter(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().LineHorizontal(0.5f).LineColor("#cbd5e1");
                col.Item().PaddingTop(4).Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor("#64748b"));
                    t.Span("Vui lòng đến trước giờ hẹn 10 phút và xuất trình mã QR này cho nhân viên tiếp tân để check-in. ");
                    t.Span($"Xuất lúc {DateTime.Now:dd/MM/yyyy HH:mm}.");
                });
            });
        }

        // ---------- Hàm tiện ích dựng bảng ----------

        private static void InfoRow(TableDescriptor table, string label, string? value)
        {
            table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").PaddingVertical(3).PaddingRight(4)
                .Text(label).FontColor("#64748b");
            table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").PaddingVertical(3)
                .Text(string.IsNullOrWhiteSpace(value) ? "—" : value).Bold();
        }

        private static void HeaderCell(TableDescriptor table, string text)
        {
            table.Cell().Background("#f1f5f9").Border(0.5f).BorderColor("#e2e8f0").Padding(4)
                .Text(text).FontSize(8).Bold().FontColor("#475569");
        }

        private static void BodyCell(TableDescriptor table, string text)
        {
            table.Cell().Border(0.5f).BorderColor("#e2e8f0").Padding(4).Text(text).FontSize(9);
        }

        private static string Money(decimal v) => v.ToString("N0") + " ₫";

        private static string StatusColor(string status) => status switch
        {
            AppointmentRules.Confirmed => "#2563eb",
            AppointmentRules.InProgress => "#0ea5e9",
            AppointmentRules.Completed => "#059669",
            AppointmentRules.Cancelled => "#64748b",
            _ => "#d97706"
        };

        private static string WeekdayName(DayOfWeek d) => d switch
        {
            DayOfWeek.Monday => "Thứ 2",
            DayOfWeek.Tuesday => "Thứ 3",
            DayOfWeek.Wednesday => "Thứ 4",
            DayOfWeek.Thursday => "Thứ 5",
            DayOfWeek.Friday => "Thứ 6",
            DayOfWeek.Saturday => "Thứ 7",
            _ => "Chủ nhật"
        };
    }
}
