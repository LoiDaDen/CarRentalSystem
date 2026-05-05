# Hướng Dẫn Sử Dụng — Quản lý Khu vực (Manager) & Nhân viên Khu vực (Staff)

> **Vai trò:** Manager và Staff là nhân viên quản lý theo khu vực (Area). Được Admin gán vào một hoặc nhiều khu vực. Chỉ thấy và thao tác dữ liệu thuộc khu vực được phân công.

---

## 1. Đăng nhập

1. Truy cập `/Auth/Login`
2. Đăng nhập bằng tài khoản có role **Manager** hoặc **Staff**
3. Sau khi đăng nhập, vào **Trung tâm Điều hành**

---

## 2. Chọn Khu vực làm việc

> **Quan trọng:** Manager/Staff chỉ làm việc với dữ liệu thuộc khu vực được gán. Bạn phải chọn khu vực trước khi thao tác.

### Cách chọn khu vực
- **Dropdown ở Sidebar** — Chọn khu vực từ danh sách dropdown ở sidebar
- Khu vực được chọn sẽ lưu vào cookie `CurrentAreaId`
- Tất cả dữ liệu hiển thị sẽ được lọc theo khu vực đã chọn

---

## 3. Dashboard Khu vực

### 3.1. Tổng quan (Manager + Staff)
Truy cập: `/Admin/Dashboard` hoặc `/Admin/AreaDashboard`

- Xem tổng quan khu vực đang chọn
- Thống kê: số xe, đơn hàng, doanh thu trong khu vực
- Biểu đồ hoạt động

### 3.2. Dashboard Khu vực của tôi (Manager + Staff)
Truy cập: `/Admin/AreaDashboard`

- Xem chi tiết dashboard của khu vực đã chọn
- Hoạt động gần đây trong khu vực

---

## 4. Quản lý Đội xe (Khu vực)

Truy cập: `/Admin/Car/Manage`

**Manager:**
- Xem danh sách xe trong khu vực
- Sửa thông tin xe
- Cập nhật trạng thái xe: Khả dụng / Đang thuê / Bảo trì
- Xem xe trong thùng rác: `/Admin/Car/Trash`

**Staff:**
- Xem danh sách xe trong khu vực (chỉ xem, không sửa)

---

## 5. Quản lý Đơn đặt xe (Khu vực)

Truy cập: `/Admin/Booking`

- Xem danh sách đơn đặt xe trong khu vực
- Lọc theo trạng thái: Pending / Confirmed / Active / Completed / Cancelled
- Cập nhật trạng thái đơn hàng
- Xử lý khiếu nại trong khu vực

---

## 6. Bảo trì (Khu vực)

Truy cập: `/Admin/Maintenance`

- Xem danh sách xe đang bảo trì trong khu vực
- Tạo lịch bảo trì mới
- Ghi nhận chi phí bảo trì
- Cập nhật trạng thái bảo trì: Đang bảo trì → Hoàn thành → Khả dụng

---

## 7. Theo dõi & Định vị

Truy cập: `/Tracking/Map` hoặc `/Tracking/Connect`

- **Bản đồ số** — Xem vị trí xe trong khu vực trên bản đồ
- **Kết nối IoT** — Quản lý thiết bị GPS trên xe

---

## 8. Cài đặt Khu vực (Chỉ Manager)

### 8.1. Danh sách Khu vực
Truy cập: `/Admin/Area`

- Xem danh sách tất cả khu vực (Manager chỉ thấy khu vực được gán)
- Tạo khu vực mới
- Chỉnh sửa khu vực: tên, mã, trạng thái
- Gán địa điểm (Location) vào khu vực

### 8.2. Phân quyền Khu vực (AreaStaff)
Truy cập: `/Admin/AreaStaff`

- Xem danh sách nhân viên trong khu vực
- Thêm Staff vào khu vực
- Gán vai trò Staff/Manager cho nhân viên
- Xóa nhân viên khỏi khu vực

---

## 9. Thông báo

- Chuông thông báo (góc phải) hiển thị các thông báo mới
- Cập nhật mỗi 15 giây tự động
- Thông báo bao gồm: đơn đặt xe mới, cảnh báo bảo trì,...

---

## 10. Hồ sơ cá nhân

Truy cập: `/Auth/Profile`

- Cập nhật thông tin cá nhân
- Thay đổi mật khẩu

---

## 11. Đăng xuất

Truy cập: `/Auth/Logout` hoặc click **Đăng xuất** ở sidebar

---

## So sánh quyền Manager vs Staff

| Chức năng | Manager | Staff |
|---|---|---|
| Dashboard khu vực | Xem | Xem |
| Quản lý đội xe | Sửa | Chỉ xem |
| Quản lý đơn đặt xe | Sửa | Sửa |
| Quản lý bảo trì | Sửa | Sửa |
| Theo dõi GPS/IoT | Xem | Xem |
| Tạo khu vực mới | Yes | No |
| Phân quyền nhân viên | Yes | No |
| Xem thùng rác xe | Yes | No |
