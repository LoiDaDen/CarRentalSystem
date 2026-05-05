# Hướng Dẫn Sử Dụng — Đối tác (Partner)

> **Vai trò:** Partner là cá nhân/tổ chức sở hữu xe cho thuê trên nền tảng CarRental. Có thể đăng xe, quản lý đội xe, theo dõi đơn thuê và xem doanh thu.

---

## 1. Đăng ký & Kích hoạt tài khoản Partner

### 1.1. Đăng ký tài khoản
1. Truy cập `/Auth/Register` — đăng ký tài khoản Customer thông thường

### 1.2. Nâng cấp lên Partner
1. Sau khi đăng nhập, truy cập **Cổng Đối Tác (Partner)** từ menu người dùng
2. Hoặc đăng ký Partner tại `/Partner/Dashboard/Register`
3. Cung cấp thông tin bổ sung:
   - Địa chỉ kinh doanh
   - Số điện thoại liên hệ
   - Thông tin xe dự kiến cho thuê

### 1.3. Chờ duyệt
- Yêu cầu Partner sẽ được **Admin duyệt** tại `/Admin/User/PartnerRequests`
- Sau khi duyệt, tài khoản sẽ có role **Partner**

### 1.4. Truy cập Partner Portal
- Menu người dùng > **Cổng Đối Tác (Partner)**
- Hoặc truy cập trực tiếp: `/Partner/Dashboard`

---

## 2. Partner Dashboard (Tổng quan)

Truy cập: `/Partner/Dashboard`

- **Tổng doanh thu** — Tổng thu từ các xe đã cho thuê
- **Số xe** — Tổng số xe đang quản lý
- **Đơn hàng** — Số đơn đặt xe đang chờ / đã hoàn thành
- **Xe khả dụng** — Số xe đang sẵn sàng cho thuê

---

## 3. Quản lý Đội xe

### 3.1. Xem danh sách xe
Truy cập: `/Partner/Dashboard/MyCars`

- Xem tất cả xe thuộc quyền quản lý
- Trạng thái xe:
  - **Available** — Sẵn sàng cho thuê
  - **Rented** — Đang được thuê
  - **Maintenance** — Đang bảo trì

### 3.2. Đăng xe mới
Truy cập: `/Partner/Dashboard/AddCar`

**Thông tin xe cần cung cấp:**
- Tên xe (VD: Toyota Camry 2024)
- Biển số đăng ký
- Số khung (VIN)
- Hãng xe (Toyota, Honda,...)
- Loại xe (Sedan, SUV,...)
- Số chỗ ngồi
- Giá thuê/ngày (PricePerDay)
- Giá thuê/giờ (PricePerHour)
- Mô tả xe
- Tính năng (Bluetooth, GPS, Camera lùi,...)
- Nhiên liệu (Xăng, Dầu, Điện)
- Hộp số (Tự động, Số sàn)
- Hình ảnh xe (upload nhiều ảnh)
- Địa chỉ nhận xe
- Bảo hiểm (Basic, Premium, Full Cover)

**Sau khi đăng:**
- Xe sẽ chuyển sang trạng thái **Chờ duyệt**
- Admin sẽ xem xét và duyệt tại `/Admin/CarApproval`
- Xe chỉ hiển thị công khai sau khi được duyệt

### 3.3. Chỉnh sửa xe
Truy cập: `/Partner/Dashboard/EditCar/{id}`

- Cập nhật thông tin xe
- Cập nhật giá thuê
- Upload thêm hình ảnh

### 3.4. Xóa xe
- Xóa xe khỏi danh sách (xe sẽ bị ẩn khỏi thị trường)

---

## 4. Quản lý Đơn thuê xe

Truy cập: `/Partner/Dashboard/Bookings`

- Xem danh sách đơn thuê xe của bạn
- Trạng thái đơn:
  - **Pending** — Chờ xác nhận
  - **Confirmed** — Đã xác nhận
  - **Active** — Đang thuê
  - **Completed** — Đã hoàn thành
  - **Cancelled** — Đã hủy
- Nhận thông báo khi có đơn đặt xe mới

---

## 5. Hợp đồng

Truy cập: `/Partner/Dashboard/Contract`

- Xem hợp đồng với CarRental
- Tải hợp đồng (PDF)
- Theo dõi trạng thái hợp đồng

---

## 6. Thông tin cá nhân & Tài khoản

### 6.1. Cập nhật hồ sơ
Truy cập: `/Auth/Profile`

- Cập nhật thông tin cá nhân
- Thay đổi mật khẩu
- Cập nhật địa chỉ kinh doanh

### 6.2. Xem website
- Truy cập trang chủ để xem xe đã đăng trên thị trường

---

## 7. Đăng xuất

Truy cập: `/Auth/Logout` hoặc click **Đăng xuất** ở sidebar

---

## Tóm tắt luồng Partner

```
Đăng ký Customer → Đăng ký Partner → Admin duyệt
                                           ↓
                            Role: Partner + Truy cập Partner Portal
                                           ↓
              Đăng xe mới → Chờ duyệt (Admin) → Xe hiển thị công khai
                                           ↓
                        Khách đặt xe → Đơn Pending
                                           ↓
                        Xác nhận → Đơn Confirmed/Active
                                           ↓
                        Khách trả xe → Đơn Completed
                                           ↓
                        Nhận thanh toán + Cập nhật doanh thu
```
