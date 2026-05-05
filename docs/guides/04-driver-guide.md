# Hướng Dẫn Sử Dụng — Tài xế (Driver)

> **Vai trò:** Driver là tài xế được thuê cùng với xe trên nền tảng CarRental. Tài xế có thể xem các chuyến xe được phân công, cập nhật trạng thái và nhận thanh toán.

---

## 1. Đăng ký & Kích hoạt tài khoản Driver

### 1.1. Đăng ký
1. Truy cập `/Auth/Register` — đăng ký tài khoản
2. Sau đó liên hệ **Admin** hoặc đăng ký dịch vụ tài xế tại trang **Thuê tài xế** (`/Driver/Index`)

### 1.2. Thông tin tài xế cần cung cấp
- Họ tên đầy đủ
- Số điện thoại
- Số bằng lái xe
- Kinh nghiệm lái xe
- Ảnh CCCD (mặt trước + mặt sau)
- Ảnh bằng lái
- Ảnh chân dung

### 1.3. Truy cập Driver Dashboard
- Menu người dùng > **Cổng Tài Xế (Driver)**
- Hoặc truy cập trực tiếp: `/Driver/DriverDashboard`

---

## 2. Driver Dashboard (Tổng quan)

Truy cập: `/Driver/DriverDashboard`

- **Chuyến xe hôm nay** — Số chuyến được phân công trong ngày
- **Tổng thu nhập** — Tổng thu từ các chuyến đã hoàn thành
- **Đánh giá** — Điểm trung bình từ khách hàng
- **Lịch sử chuyến** — Danh sách các chuyến đã hoàn thành

---

## 3. Quản lý Chuyến xe

### 3.1. Xem danh sách chuyến
Truy cập: `/Driver/DriverDashboard/Index`

- Xem tất cả chuyến xe được phân công
- Trạng thái chuyến:
  - **Pending** — Chờ nhận chuyến
  - **Confirmed** — Đã xác nhận
  - **Active** — Đang thực hiện chuyến
  - **Completed** — Đã hoàn thành
  - **Cancelled** — Đã hủy

### 3.2. Nhận chuyến
- Xem thông tin chi tiết chuyến xe:
  - Thông tin khách hàng
  - Địa điểm đón
  - Địa điểm trả
  - Thời gian dự kiến
  - Ghi chú đặc biệt

### 3.3. Cập nhật trạng thái chuyến
- **Bắt đầu chuyến** — Khi bắt đầu di chuyển đón khách
- **Đã đón khách** — Khi khách lên xe
- **Hoàn thành chuyến** — Khi đã trả khách tại điểm đến

---

## 4. Chi tiết chuyến xe

Truy cập: `/Driver/DriverDashboard/Details/{id}`

- Thông tin chi tiết từng chuyến
- Xem lộ trình (điểm đón → điểm trả)
- Xem phí chuyến xe
- Xem đánh giá từ khách hàng

---

## 5. Đánh giá khách hàng

- Sau khi hoàn thành chuyến, tài xế có thể đánh giá khách hàng
- Góp phần xây dựng hệ thống đánh giá minh bạch

---

## 6. Thông tin cá nhân

Truy cập: `/Auth/Profile`

- Cập nhật thông tin cá nhân
- Cập nhật số điện thoại, địa chỉ
- Thay đổi mật khẩu

---

## 7. Đăng xuất

Truy cập: `/Auth/Logout`

---

## Tóm tắt luồng Driver

```
Đăng ký → Admin phê duyệt Driver
                    ↓
         Role: Driver + Truy cập Driver Dashboard
                    ↓
    Khách đặt xe kèm tài xế → Admin/Staff phân công
                    ↓
         Nhận thông báo chuyến mới
                    ↓
         Xác nhận nhận chuyến → Bắt đầu
                    ↓
         Đón khách → Di chuyển → Trả khách
                    ↓
         Hoàn thành chuyến → Nhận thanh toán
                    ↓
         Đánh giá khách hàng
```
