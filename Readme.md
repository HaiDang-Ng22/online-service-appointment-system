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