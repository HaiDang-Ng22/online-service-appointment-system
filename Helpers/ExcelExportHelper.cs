using ClosedXML.Excel;
using Appointment.Models;

namespace Appointment.Helpers
{
    /// <summary>
    /// Helper xuất dữ liệu báo cáo danh sách lịch hẹn và doanh thu ra file Excel (.xlsx) [US-46].
    /// </summary>
    public static class ExcelExportHelper
    {
        public static byte[] ExportAppointmentsToExcel(IEnumerable<AppointmentModel> appointments, string reportTitle)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachLichHen");

            // Cấu hình hiển thị lưới
            worksheet.ShowGridLines = true;

            // 1. Tiêu đề báo cáo
            worksheet.Range("A1:M1").Merge();
            worksheet.Cell("A1").Value = reportTitle.ToUpper();
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 15;
            worksheet.Cell("A1").Style.Font.FontColor = XLColor.White;
            worksheet.Cell("A1").Style.Fill.BackgroundColor = XLColor.FromArgb(30, 58, 138); // #1E3A8A
            worksheet.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell("A1").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            worksheet.Row(1).Height = 35;

            // 2. Dòng thông tin metadata
            worksheet.Range("A2:M2").Merge();
            worksheet.Cell("A2").Value = $"Ngày xuất báo cáo: {DateTime.Now:dd/MM/yyyy HH:mm:ss} | Tổng số bản ghi: {appointments.Count():N0}";
            worksheet.Cell("A2").Style.Font.Italic = true;
            worksheet.Cell("A2").Style.Font.FontSize = 10;
            worksheet.Cell("A2").Style.Font.FontColor = XLColor.FromArgb(100, 116, 139);
            worksheet.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Row(2).Height = 20;

            // Dòng trống
            worksheet.Row(3).Height = 10;

            // 3. Tiêu đề các cột dữ liệu (Row 4)
            string[] headers = new[]
            {
                "STT",
                "Mã Lịch Hẹn",
                "Họ Tên Khách Hàng",
                "Số Điện Thoại",
                "Email",
                "Tên Dịch Vụ",
                "Chuyên Mục",
                "Bác Sĩ / Nhà Cung Cấp",
                "Ngày Hẹn",
                "Khung Giờ Phục Vụ",
                "Trạng Thái",
                "Giá Tiền (VNĐ)",
                "Ghi Chú Khách Hàng"
            };

            int headerRowIndex = 4;
            worksheet.Row(headerRowIndex).Height = 26;

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(headerRowIndex, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 11;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(37, 99, 235); // #2563EB
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromArgb(203, 213, 225);
            }

            // 4. Đổ dữ liệu các dòng
            int currentRow = headerRowIndex + 1;
            int stt = 1;
            decimal totalRevenue = 0;

            foreach (var a in appointments)
            {
                worksheet.Row(currentRow).Height = 22;

                // STT
                worksheet.Cell(currentRow, 1).Value = stt++;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Mã lịch hẹn
                worksheet.Cell(currentRow, 2).Value = a.Id;
                worksheet.Cell(currentRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Khách hàng
                worksheet.Cell(currentRow, 3).Value = a.Customer?.FullName ?? "Khách vãng lai";

                // SĐT
                worksheet.Cell(currentRow, 4).Value = a.Customer?.PhoneNumber ?? "-";
                worksheet.Cell(currentRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Email
                worksheet.Cell(currentRow, 5).Value = a.Customer?.Email ?? "-";

                // Dịch vụ
                worksheet.Cell(currentRow, 6).Value = a.Service?.Name ?? "Dịch vụ";
                worksheet.Cell(currentRow, 6).Style.Font.Bold = true;

                // Danh mục
                worksheet.Cell(currentRow, 7).Value = a.Service?.Category?.Name ?? "-";

                // Bác sĩ / Nhà cung cấp
                worksheet.Cell(currentRow, 8).Value = a.Provider?.FullName ?? "-";

                // Ngày hẹn
                worksheet.Cell(currentRow, 9).Value = a.AppointmentDate.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Khung giờ
                worksheet.Cell(currentRow, 10).Value = $"{a.StartTime:hh\\:mm} - {a.EndTime:hh\\:mm}";
                worksheet.Cell(currentRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Trạng thái
                string statusText = a.Status switch
                {
                    "Pending" => "Chờ xác nhận",
                    "Confirmed" => "Đã xác nhận",
                    "In-Progress" => "Đang thực hiện",
                    "Completed" => "Đã hoàn thành",
                    "Cancelled" => "Đã hủy",
                    _ => a.Status
                };
                worksheet.Cell(currentRow, 11).Value = statusText;
                worksheet.Cell(currentRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                if (a.Status == "Completed")
                {
                    worksheet.Cell(currentRow, 11).Style.Font.FontColor = XLColor.FromArgb(22, 101, 52);
                    worksheet.Cell(currentRow, 11).Style.Font.Bold = true;
                }
                else if (a.Status == "Cancelled")
                {
                    worksheet.Cell(currentRow, 11).Style.Font.FontColor = XLColor.FromArgb(153, 27, 27);
                }

                // Giá tiền
                decimal price = a.Service?.Price ?? 0;
                worksheet.Cell(currentRow, 12).Value = price;
                worksheet.Cell(currentRow, 12).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(currentRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                if (a.Status != "Cancelled")
                {
                    totalRevenue += price;
                }

                // Ghi chú
                worksheet.Cell(currentRow, 13).Value = a.Notes ?? "";

                // Đường viền dòng
                for (int col = 1; col <= headers.Length; col++)
                {
                    var cell = worksheet.Cell(currentRow, col);
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromArgb(226, 232, 240);
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    if (stt % 2 == 0)
                    {
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252);
                    }
                }

                currentRow++;
            }

            // 5. Dòng Tổng Cộng
            worksheet.Row(currentRow).Height = 26;
            worksheet.Range(currentRow, 1, currentRow, 11).Merge();
            var totalLabelCell = worksheet.Cell(currentRow, 1);
            totalLabelCell.Value = "TỔNG CỘNG DOANH THU (KHÔNG TÍNH LỊCH HỦY):";
            totalLabelCell.Style.Font.Bold = true;
            totalLabelCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            totalLabelCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            var totalValCell = worksheet.Cell(currentRow, 12);
            totalValCell.Value = totalRevenue;
            totalValCell.Style.Font.Bold = true;
            totalValCell.Style.NumberFormat.Format = "#,##0 \"VNĐ\"";
            totalValCell.Style.Font.FontColor = XLColor.FromArgb(180, 83, 9);
            totalValCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            totalValCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            for (int col = 1; col <= headers.Length; col++)
            {
                var cell = worksheet.Cell(currentRow, col);
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(254, 243, 199);
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Double;
                cell.Style.Border.OutsideBorderColor = XLColor.FromArgb(217, 119, 6);
            }

            // Tự động căn chỉnh độ rộng cột
            worksheet.Columns().AdjustToContents();
            worksheet.Column(1).Width = 7;
            worksheet.Column(2).Width = 34;
            worksheet.Column(13).Width = 25;

            using var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
