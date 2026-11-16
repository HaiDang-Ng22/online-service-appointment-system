# AppointX – Online Service Appointment Management System

## 📌 Giới thiệu

**AppointX** là hệ thống quản lý và đặt lịch dịch vụ trực tuyến, cho phép người dùng tìm kiếm dịch vụ, xem thông tin nhà cung cấp dịch vụ, kiểm tra lịch trống và đặt lịch trực tuyến.

Hệ thống có thể áp dụng cho nhiều loại hình dịch vụ như:

- 🏥 Bác sĩ / Phòng khám
- 💇 Salon / Spa
- 🧑‍💼 Tư vấn viên
- 🏋️ Trung tâm thể thao
- 🎓 Trung tâm giáo dục
- 🔧 Trung tâm dịch vụ
- Và các loại hình dịch vụ khác có nhu cầu đặt lịch

Hệ thống hỗ trợ quản lý toàn bộ quy trình từ khi khách hàng tìm kiếm dịch vụ, đặt lịch, xác nhận, thực hiện dịch vụ cho đến khi hoàn thành lịch hẹn.

---

# 🎯 Mục tiêu hệ thống

Hệ thống được xây dựng nhằm:

- Giúp khách hàng dễ dàng tìm kiếm dịch vụ.
- Cho phép khách hàng xem lịch làm việc và thời gian còn trống.
- Hỗ trợ đặt lịch trực tuyến.
- Giảm tình trạng đặt trùng lịch.
- Giúp nhà cung cấp dịch vụ quản lý lịch hẹn.
- Hỗ trợ nhân viên quản lý và xử lý lịch hẹn.
- Cho phép quản trị viên quản lý toàn bộ hệ thống.
- Theo dõi lịch sử đặt lịch.
- Gửi thông báo và nhắc lịch cho khách hàng.
- Quản lý trạng thái của từng cuộc hẹn.

---

# 👥 Hệ thống phân quyền

Hệ thống gồm 5 Role chính:

| Role | Tên | Mục đích |
|---|---|---|
| Guest | Khách | Người chưa đăng nhập |
| Customer | Khách hàng | Người sử dụng dịch vụ |
| Provider | Nhà cung cấp dịch vụ | Bác sĩ, tư vấn viên, salon, chuyên viên... |
| Staff | Nhân viên | Nhân viên hỗ trợ quản lý |
| Admin | Quản trị viên | Quản lý toàn bộ hệ thống |

---

# 1. 👤 Guest – Khách chưa đăng nhập

Guest là người truy cập hệ thống nhưng chưa đăng nhập.

## Chức năng

### 1.1. Xem trang chủ

Guest có thể:

- Xem giới thiệu hệ thống.
- Xem các dịch vụ nổi bật.
- Xem các nhà cung cấp dịch vụ nổi bật.
- Xem các danh mục dịch vụ.

### 1.2. Tìm kiếm dịch vụ

Guest có thể tìm kiếm:

- Tên dịch vụ.
- Loại dịch vụ.
- Nhà cung cấp.
- Địa điểm.

### 1.3. Xem thông tin dịch vụ

Có thể xem:

- Tên dịch vụ.
- Mô tả.
- Giá dịch vụ.
- Thời lượng.
- Nhà cung cấp.
- Địa điểm.
- Hình ảnh.
- Thông tin liên hệ.

### 1.4. Xem nhà cung cấp dịch vụ

Có thể xem:

- Tên nhà cung cấp.
- Loại hình dịch vụ.
- Địa chỉ.
- Thông tin giới thiệu.
- Danh sách dịch vụ.

### 1.5. Đăng ký tài khoản

Guest có thể tạo tài khoản Customer.

### 1.6. Đăng nhập

Guest có thể đăng nhập vào hệ thống.

## Guest KHÔNG được phép

- Đặt lịch.
- Hủy lịch.
- Quản lý lịch cá nhân.
- Xem lịch sử đặt lịch.
- Quản lý dịch vụ.
- Quản lý lịch làm việc.
- Truy cập trang Admin.

---

# 2. 👨‍💼 Customer – Khách hàng

Customer là người sử dụng hệ thống để tìm kiếm và đặt dịch vụ.

## 2.1. Quản lý tài khoản

Customer có thể:

- Xem thông tin cá nhân.
- Cập nhật họ tên.
- Cập nhật số điện thoại.
- Cập nhật email.
- Cập nhật địa chỉ.
- Thay đổi mật khẩu.
- Cập nhật ảnh đại diện.

---

## 2.2. Tìm kiếm dịch vụ

Customer có thể tìm kiếm theo:

- Tên dịch vụ.
- Danh mục.
- Nhà cung cấp.
- Địa điểm.
- Giá.
- Thời lượng.

---

## 2.3. Xem thông tin dịch vụ

Customer có thể xem:

- Tên dịch vụ.
- Mô tả.
- Giá.
- Thời lượng.
- Nhà cung cấp.
- Địa điểm.
- Thời gian hoạt động.
- Đánh giá.

---

## 2.4. Xem lịch trống

Customer có thể:

- Chọn ngày.
- Xem các khung giờ còn trống.
- Xem khung giờ đã được đặt.
- Chọn người cung cấp dịch vụ.
- Chọn thời gian phù hợp.

Ví dụ:

```text
Ngày: 20/09/2026

08:00  ❌ Đã đặt
09:00  ✅ Còn trống
10:00  ✅ Còn trống
11:00  ❌ Đã đặt
13:00  ✅ Còn trống
14:00  ❌ Đã đặt
```

---

## 2.5. Thanh toán và đặt cọc (US-28)

Customer có thể:

- Chọn **đặt cọc** (mặc định 30%, cấu hình `Payment:DepositPercent`) hoặc **thanh toán toàn bộ** ngay sau khi đặt lịch.
- Chọn phương thức: thẻ ngân hàng, ví điện tử hoặc chuyển khoản.
- Chọn "thanh toán sau" và quay lại thanh toán nốt phần còn lại ở trang *Lịch hẹn của tôi* / *Chi tiết lịch hẹn*.
- Xem lịch sử giao dịch và tình trạng thanh toán (Chưa thanh toán / Đã đặt cọc / Đã thanh toán / Đã hoàn tiền).
- Được **hoàn tiền tự động** khi lịch hẹn bị hủy.

> Cổng thanh toán hiện là **sandbox** (`IPaymentGateway`), chưa trừ tiền thật.

Vị trí: `PaymentsController`, `Infrastructure/PaymentService.cs`, `Views/Payments/Checkout.cshtml`.

---

## 2.6. Hủy lịch hẹn (US-19)

Customer có thể:

- Tự hủy lịch hẹn ở trạng thái Pending / Confirmed nếu thời điểm hủy cách giờ hẹn tối thiểu `CancelBeforeHours` (mặc định 2 giờ).
- Nhận hoàn tiền cho các khoản đã thanh toán / đặt cọc khi hủy thành công.

Customer sẽ **bị chặn hủy** khi: còn ít hơn `CancelBeforeHours` đến giờ hẹn, dịch vụ đang thực hiện, hoặc lịch đã hoàn thành / đã hủy.

Vị trí: `AppointmentRules.CanCustomerCancel`, `AppointmentsController.Cancel`.

---

## 2.7. Đánh giá dịch vụ (US-30)

Customer có thể:

- Chấm điểm từ **1 đến 5 sao** và viết nhận xét sau khi lịch hẹn **hoàn thành** (mỗi lịch hẹn một đánh giá, có thể sửa hoặc xóa).
- Xem đánh giá của khách hàng khác tại danh sách và trang chi tiết dịch vụ.

Vị trí: `ReviewsController`, `Views/Reviews/Create.cshtml`.

---

## 2.8. Quên / đặt lại mật khẩu (US-26)

Customer có thể:

- Nhập email tại trang **ForgotPassword** để yêu cầu đặt lại mật khẩu.
- Nhận email chứa liên kết đặt lại mật khẩu.
- Mở liên kết và nhập mật khẩu mới tại trang **ResetPassword**.

Bảo mật: liên kết có hiệu lực **30 phút**, **chỉ dùng 1 lần**, token lưu dạng băm trong CSDL, giới hạn 1 email / 60 giây cho mỗi tài khoản và luôn trả cùng một thông báo để không lộ email có tồn tại hay không.

Vị trí: `AccountController`, `Views/Account/ForgotPassword*`, `Views/Account/ResetPassword.cshtml`.

---

## 2.9. Mã QR check-in và phiếu hẹn PDF (US-32)

Customer có thể:

- Xem **mã QR check-in** ở trang *Chi tiết lịch hẹn* và trên phiếu in (lịch đã hủy không có mã QR).
- Bấm **Tải PDF** để tải phiếu hẹn khổ A5 gồm thông tin khách, dịch vụ, ngày giờ, tình trạng thanh toán và mã QR.
- Xuất trình mã QR cho nhân viên để check-in nhanh khi đến cơ sở.

Bảo mật: nội dung QR dạng `APX:{id}:{chữ ký}` được ký **HMAC-SHA256** (khóa `CheckIn:SecretKey`), ảnh QR tạo ngay trên máy chủ (không gửi mã lịch hẹn cho dịch vụ bên ngoài); Customer chỉ tải được phiếu của chính mình.

Vị trí: `AppointmentsController.QrCode / DownloadSlip`, `Infrastructure/CheckInCodeService.cs`, `QrCodeService.cs`, `AppointmentSlipPdfService.cs`, thư mục `Fonts/`.

---

Customer KHÔNG được phép

- Hủy lịch sát giờ hẹn (dưới `CancelBeforeHours`).
- Đánh giá khi lịch hẹn chưa hoàn thành.
- Cập nhật trạng thái lịch hẹn.
- Xem / tải phiếu hẹn của khách khác.
- Truy cập trang Provider.

---

# 3. 🧑‍⚕️ Provider – Nhà cung cấp dịch vụ

Provider là người cung cấp dịch vụ và xử lý các lịch hẹn của khách hàng.

## 3.1. Cấu hình giờ làm việc theo tuần (US-14)

Provider truy cập `/Provider/WorkingHours` để:

- Bật / tắt từng ngày làm việc trong tuần.
- Chỉnh giờ bắt đầu và kết thúc cho từng ngày (tối thiểu 30 phút).
- Lịch trống và việc đặt lịch của khách hàng tuân theo cấu hình này.
- Được cảnh báo nếu có lịch đã đặt nằm ngoài khung giờ mới.

---

## 3.2. Cập nhật trạng thái lịch hẹn (US-21, US-24)

Provider có thể:

- Xác nhận lịch hẹn (Pending → Confirmed).
- Chuyển sang đang thực hiện (In-Progress) và đánh dấu hoàn thành (Completed).
- Hủy lịch hẹn ở các trạng thái cho phép (tự động hoàn tiền cho khách).

Hệ thống kiểm tra luồng trạng thái bằng `AppointmentRules.TryValidateTransition` và từ chối chuyển đổi không hợp lệ (ví dụ từ Completed / Cancelled về trạng thái khác, hoặc hoàn thành lịch của ngày chưa tới).

---

## 3.3. Xem đánh giá (US-30)

Provider có thể xem các đánh giá và nhận xét của khách hàng tại `/Provider/Reviews`.

---

## 3.4. Thống kê doanh thu và hiệu suất kinh doanh (US-38)

Tại `/Provider/Statistics`, Provider có thể:

- Lọc theo kỳ (hôm nay, 7 ngày, 30 ngày, tháng này / trước, quý, năm, tùy chọn) và theo dịch vụ.
- Xem **doanh thu ghi nhận** (lịch hoàn thành), **đã thu online** (ròng sau hoàn tiền), **doanh thu dự kiến** và **thất thoát do hủy lịch**, kèm % tăng / giảm so với kỳ trước.
- Xem tỷ lệ hoàn thành / hủy, giá trị trung bình mỗi lịch, khách mới và khách quay lại, đánh giá trung bình.
- Xem top dịch vụ, top khách hàng; biểu đồ doanh thu theo ngày / tháng, lịch theo thứ, theo khung giờ, theo phương thức thanh toán.
- Xuất báo cáo chi tiết ra **CSV** (mở được bằng Excel) hoặc in báo cáo.

Admin xem được số liệu của từng Provider hoặc toàn hệ thống. Vị trí: `ProviderController.Statistics / ExportStatistics`, `Views/Provider/Statistics.cshtml`.

---

Provider KHÔNG được phép

- Chuyển trạng thái lịch hẹn sai luồng.
- Xem hoặc chỉnh sửa dữ liệu (lịch hẹn, phiếu hẹn, thống kê) của Provider khác.

---

# 4. 🧾 Staff – Nhân viên

## 4.1. Quét mã QR check-in (US-32)

Staff truy cập `/Staff/ScanQR` để:

- Quét mã QR bằng camera hoặc nhập tay mã `APX-XXXXXXXX` (tối thiểu 8 ký tự, phải khớp duy nhất một lịch hẹn).
- Xác nhận khách đã đến; lịch chuyển sang **Đang thực hiện (In-Progress)**.
- Mã QR giả mạo / sai chữ ký bị từ chối và được ghi vào nhật ký kiểm toán.

---

# 5. 🛡️ Admin – Quản trị viên

## 5.1. Nhật ký kiểm toán và bảo mật (US-43)

Tại `/Admin/AuditLogs`, Admin có thể:

- Xem nhật ký đăng nhập / đăng xuất, đổi mật khẩu, đặt / hủy / đổi trạng thái lịch, check-in, thanh toán và hoàn tiền, đánh giá, thay đổi dịch vụ, phân quyền, truy cập bị từ chối...
- Lọc theo từ khóa, nhóm, hành động, mức độ, kết quả, vai trò, khoảng ngày; phân trang; xuất CSV.
- Bấm **Kiểm tra toàn vẹn** để phát hiện dòng nhật ký bị sửa / xóa trực tiếp trong CSDL (chuỗi mã băm SHA-256).

Nhật ký **chỉ ghi thêm**, không có chức năng sửa hoặc xóa. Biện pháp bảo mật đi kèm: khóa tạm thời khi đăng nhập sai nhiều lần, giới hạn theo IP, HTTP security headers, cookie HttpOnly / SameSite.

Vị trí: `Infrastructure/AuditService.cs`, `Models/AuditLog.cs`, `AdminController.AuditLogs`, `Views/Admin/AuditLogs.cshtml`.

---

# ⚙️ Chức năng hệ thống tự động

## Tự hủy lịch quá hạn (US-37)

Tác vụ nền `AppointmentAutoCancelService` tự động:

- Hủy các lịch **Pending** đã quá giờ hẹn.
- Hủy các lịch **Pending** mà Provider không xác nhận trong `ConfirmationTimeoutHours` (mặc định 24 giờ).
- Hoàn tiền cho khách hàng.
- Gửi email thông báo khi hủy (nếu SMTP đã cấu hình) và ghi nhật ký kiểm toán.

---

# 🔄 Trạng thái lịch hẹn

| Trạng thái | Ý nghĩa | Chuyển được sang |
|---|---|---|
| Pending | Khách vừa đặt, chờ xác nhận | Confirmed, In-Progress, Completed, Cancelled |
| Confirmed | Provider / Staff đã xác nhận | In-Progress, Completed, Cancelled |
| In-Progress | Khách đã check-in, đang thực hiện dịch vụ | Completed, Cancelled |
| Completed | Dịch vụ đã thực hiện xong | Không |
| Cancelled | Lịch đã bị hủy | Không |

---

Cấu hình trong `appsettings.json`:

```json
"BookingRules": { "CancelBeforeHours": 2, "AutoCancelEnabled": true, "ConfirmationTimeoutHours": 24, "AutoCancelIntervalMinutes": 5 },
"Payment":      { "DepositPercent": 30 },
"Security":     { "MaxFailedLogins": 5, "LockoutMinutes": 15, "MaxFailedLoginsPerIp": 30 },
"CheckIn":      { "SecretKey": "đổi-thành-chuỗi-bí-mật-của-bạn" }
```

> Bảng `AuditLogs` được tạo tự động khi chạy ứng dụng (cả CSDL mới lẫn CSDL cũ). Thư viện mới: `QRCoder`, `QuestPDF` (Community License – miễn phí cho dự án học tập / doanh thu < 1 triệu USD/năm).

---

# 🤖 Bộ Chức Năng AI & Quản Lý API Key (Sprint 5)

| Mã US | Tên Chức Năng | Mô Tả & Điểm Chạm UI |
|---|---|---|
| **US-AI-01** | **Chatbot AI Tư Vấn Đặt Lịch** | Widget nổi `_AiChatWidget.cshtml` (góc phải dưới màn hình) trả lời câu hỏi, tra cứu dịch vụ, tư vấn khung giờ dựa trên Closed-World Grounding. |
| **US-AI-02** | **AI Gợi Ý Dịch Vụ Thông Minh** | Hiển thị tại Trang chủ (`Home/Index`) & Trang chi tiết (`Services/Details`) dựa trên lịch sử đặt hẹn và chuyên mục. |
| **US-AI-03** | **AI Phân Tích & Tóm Tắt Đánh Giá** | Nút bấm tại Kênh Đối Tác (`/Provider/Reviews`) tự động phân tích điểm mạnh, điểm yếu và gợi ý cải thiện. |
| **US-AI-04** | **AI Tự Động Nhắc Lịch Hẹn** | Background Service (`AppointmentReminderService`) định kỳ quét lịch hẹn sắp diễn ra trong 24h, soạn email AI và gửi tự động. |
| **US-AI-05** | **AI Dashboard Insights** | Dashboard Admin (`/Admin`) & Provider (`/Provider`) phân tích xu hướng tải lịch, tỷ lệ hoàn thành/hủy và dự báo vận hành. |

### 🔐 Hướng dẫn cấu hình Gemini API Key an toàn cho thành viên nhóm (Không lộ Key lên GitHub)

Hệ thống hỗ trợ 2 cách thiết lập API Key bảo mật trên máy cá nhân:

#### Cách 1: Sử dụng .NET User Secrets (Khuyến nghị chuẩn ASP.NET Core)
Chạy lệnh sau tại thư mục gốc dự án trên máy của bạn (Key được lưu tại `%APPDATA%\Microsoft\UserSecrets\`, hoàn toàn nằm ngoài Git):
```bash
dotnet user-secrets set "Gemini:ApiKey" "AIzaSy_YOUR_API_KEY_HERE"
```

#### Cách 2: Sử dụng file `appsettings.Development.json`
Tạo file `appsettings.Development.json` tại thư mục gốc (file này đã được cấu hình trong `.gitignore`, không bao giờ bị Git đẩy lên):
```json
{
  "Gemini": {
    "ApiKey": "AIzaSy_YOUR_API_KEY_HERE"
  }
}
```

> **Lưu ý Fallback:** Nếu chưa cấu hình API Key, hệ thống vẫn hoạt động bình thường nhờ **Local In-Domain Fallback Engine**, không bị gián đoạn khi demo hoặc chạy thử nghiệm.
