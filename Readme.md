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

---
```
---

## 2.5. Thanh toán và đặt cọc

Customer có thể:

- Chọn đặt cọc (mặc định 30%) hoặc thanh toán toàn bộ khi đặt lịch.
- Thực hiện thanh toán qua cổng thanh toán (hiện là **sandbox** thông qua `IPaymentGateway`).
- Được hoàn tiền tự động khi lịch hẹn bị hủy.

---

## 2.6. Hủy lịch hẹn

Customer có thể:

- Tự hủy lịch hẹn nếu thời điểm hủy cách giờ hẹn tối thiểu `CancelBeforeHours`.
- Nhận hoàn tiền khi hủy thành công.

Customer sẽ bị chặn hủy nếu còn ít hơn `CancelBeforeHours` đến giờ hẹn.

---

## 2.7. Đánh giá dịch vụ

Customer có thể:

- Chấm điểm từ 1 đến 5 sao sau khi lịch hẹn hoàn thành.
- Viết nhận xét về dịch vụ đã sử dụng.
- Xem đánh giá của khách hàng khác tại danh sách và trang chi tiết dịch vụ.

---

## 2.8. Quên / đặt lại mật khẩu

Customer có thể:

- Nhập email tại trang `ForgotPassword` để yêu cầu đặt lại mật khẩu.
- Nhận email chứa link đặt lại mật khẩu.
- Mở link và nhập mật khẩu mới tại trang `ResetPassword`.

Link có hiệu lực **30 phút**, chỉ dùng **1 lần**, và token được lưu dạng băm trong cơ sở dữ liệu.

---

## Customer KHÔNG được phép

- Hủy lịch sát giờ hẹn (dưới `CancelBeforeHours`).
- Đánh giá khi lịch hẹn chưa hoàn thành.
- Cập nhật trạng thái lịch hẹn.
- Truy cập trang Provider.

---

# 3. 🧑‍⚕️ Provider – Nhà cung cấp dịch vụ

Provider là người cung cấp dịch vụ và xử lý các lịch hẹn của khách hàng.

## 3.1. Cấu hình giờ làm việc theo tuần

Provider có thể truy cập `/Provider/WorkingHours` để:

- Bật/tắt từng ngày làm việc trong tuần.
- Chỉnh giờ bắt đầu và kết thúc cho từng ngày.

Lịch trống và việc đặt lịch của khách hàng sẽ tuân theo cấu hình này.

---

## 3.2. Cập nhật trạng thái lịch hẹn

Provider có thể:

- Xác nhận lịch hẹn (`Pending → Confirmed`).
- Đánh dấu hoàn thành (`Confirmed → Completed`).
- Hủy lịch hẹn ở các trạng thái cho phép.

Hệ thống kiểm tra luồng trạng thái bằng `AppointmentRules.TryValidateTransition` và từ chối các chuyển đổi không hợp lệ, ví dụ từ `Pending` sang `Completed` trực tiếp.

---

## 3.3. Xem đánh giá

Provider có thể:

- Xem các đánh giá và nhận xét của khách hàng tại `/Provider/Reviews`.

---

## Provider KHÔNG được phép

- Chuyển trạng thái lịch hẹn sai luồng.
- Xem hoặc chỉnh sửa dữ liệu của Provider khác.

---

# ⚙️ Chức năng hệ thống tự động

## Tự hủy lịch quá hạn

Một tác vụ nền (`AppointmentAutoCancelService`) tự động:

- Hủy các lịch `Pending` đã quá giờ hẹn.
- Hủy các lịch `Pending` mà Provider không xác nhận trong `ConfirmationTimeoutHours`.
- Hoàn tiền cho khách hàng.
- Gửi email thông báo khi hủy.

---

# 🔄 Trạng thái lịch hẹn

| Trạng thái | Ý nghĩa | Chuyển được sang |
|---|---|---|
| Pending | Khách vừa đặt, chờ xác nhận | Confirmed, Cancelled |
| Confirmed | Provider đã xác nhận | Completed, Cancelled |
| Completed | Dịch vụ đã thực hiện xong | Không |
| Cancelled | Lịch đã bị hủy | Không |



