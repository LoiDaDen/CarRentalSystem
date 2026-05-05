# Hướng Dẫn Sử Dụng — Quản trị viên (Admin)

> **Vai trò:** Admin là người có toàn quyền điều hành hệ thống CarRental. Có thể quản lý mọi khía cạnh từ xe, người dùng, tài chính đến cấu hình hệ thống.

---

## 1. Đăng nhập & Truy cập

1. Truy cập trang đăng nhập: `/Auth/Login`
2. Đăng nhập bằng tài khoản có role **Admin**
3. Sau khi đăng nhập, vào **Trung tâm Điều hành** bằng:
   - Menu người dùng (dropdown) chọn **Quản trị**
   - Hoặc truy cập trực tiếp `/Admin/Dashboard`

---

## 2. Tổng quan Dashboard

Truy cập: `/Admin/Dashboard`

- Hiển thị tổng quan toàn hệ thống: tổng xe, đơn hàng, doanh thu, người dùng
- Biểu đồ thống kê hoạt động
- Thông báo gần đây (chuông thông báo góc phải)

---

## 3. Quản lý Đội xe

### 3.1. Danh sách xe
Truy cập: `/Admin/Car/Manage`

- Xem toàn bộ xe trong hệ thống
- Tìm kiếm theo tên, biển số, VIN
- Lọc theo trạng thái: Khả dụng / Đang thuê / Bảo trì
- Thao tác: Sửa, Xóa (xóa mềm), Khôi phục từ thùng rác

### 3.2. Duyệt xe mới
Truy cập: `/Admin/CarApproval`

- Duyệt xe mới đăng ký từ đối tác (Partner)
- Duyệt / Từ chối xe trước khi hiển thị công khai

### 3.3. Thùng rác xe
Truy cập: `/Admin/Car/Trash`

- Xem danh sách xe đã xóa mềm
- Khôi phục hoặc xóa vĩnh viễn

---

## 4. Quản lý Đơn đặt xe

Truy cập: `/Admin/Booking`

- Xem danh sách tất cả đơn đặt xe
- Lọc theo trạng thái: Draft / Pending / Confirmed / Active / Completed / Cancelled
- Xem chi tiết từng đơn
- Cập nhật trạng thái đơn hàng
- Xử lý khiếu nại, hoàn tiền

---

## 5. Quản lý Người dùng

### 5.1. Danh sách người dùng
Truy cập: `/Admin/User`

- Xem toàn bộ tài khoản (Admin, Customer, Staff, Partner, Driver)
- Khóa / Mở khóa tài khoản (`IsLocked`)
- Xem chi tiết eKYC

### 5.2. Duyệt đối tác
Truy cập: `/Admin/User/PartnerRequests`

- Duyệt yêu cầu đăng ký Partner
- Phê duyệt / Từ chối đối tác mới

### 5.3. Duyệt eKYC
Truy cập: `/Admin/User/EkycPending`

- Duyệt xác thực danh tính điện tử của người dùng
- Trạng thái: Pending / Verified / Rejected
- Xem ảnh CCCD mặt trước/sau, bằng lái, ảnh chân dung

---

## 6. Quản lý Tài xế

Truy cập: `/Admin/Driver`

- Xem danh sách tài xế trong hệ thống
- Xem chi tiết hồ sơ tài xế
- Quản lý hợp đồng tài xế

---

## 7. Hợp đồng số & Hợp đồng đối tác

### 7.1. Hợp đồng số
Truy cập: `/Admin/DigitalContract`

- Xem danh sách hợp đồng số
- Chi tiết hợp đồng với thông tin 2 bên

### 7.2. HĐ Đối tác & Tài xế
Truy cập: `/Admin/PartnerContract`

- Quản lý hợp đồng với đối tác và tài xế
- Xem chi tiết, tải hợp đồng

---

## 8. Quản lý Thanh toán

Truy cập: `/Admin/Payment`

- Xem lịch sử giao dịch
- Kiểm tra trạng thái thanh toán
- Xử lý hoàn tiền

---

## 9. CRM & Marketing

### 9.1. Mã khuyến mãi
Truy cập: `/Admin/Promotions`

- Tạo mã giảm giá: giảm % hoặc giảm số tiền cố định
- Thiết lập ngày hết hạn, số lượng sử dụng

### 9.2. Hộp thư hỗ trợ
Truy cập: `/Admin/Contacts`

- Xem và trả lời tin nhắn hỗ trợ từ khách hàng

### 9.3. Kiểm duyệt đánh giá
Truy cập: `/Admin/Reviews`

- Duyệt / Xóa đánh giá xe của khách hàng

---

## 10. Bảo trì

Truy cập: `/Admin/Maintenance`

- Theo dõi lịch bảo trì xe
- Ghi nhận chi phí bảo trì
- Cập nhật lịch bảo dưỡng tiếp theo

---

## 11. Hạng thành viên

Truy cập: `/Admin/Membership`

- Quản lý các gói hạng thành viên (Bronze, Silver, Gold, Platinum)
- Thiết lập mức chi tiêu tối thiểu, quyền lợi

---

## 12. Danh mục hệ thống

Truy cập: `/Admin/Lookups`

- Quản lý danh mục: Hãng xe, Loại xe, Trạng thái xe, Trạng thái đặt xe, Địa điểm
- Thêm / Sửa / Xóa các giá trị danh mục

---

## 13. Giá theo ngày lễ

Truy cập: `/Admin/HolidayPricing`

- Thiết lập hệ số giá (multiplier) cho ngày lễ, Tết
- Ví dụ: Ngày lễ 1/1 — giá x 1.5 lần

---

## 14. Khu vực

Truy cập: `/Admin/Area` hoặc `/Admin/AreaDashboard`

- **Area Dashboard:** Xem thống kê theo khu vực
- **Danh sách Khu vực:** Tạo, sửa khu vực hoạt động
- **Phân quyền Khu vực:** Gán Manager và Staff vào khu vực
- Chỉ Admin mới có quyền tạo/sửa khu vực

---

## 15. Bảo mật & Cảnh báo

Truy cập: `/Admin/Alert`

- Giám sát các cảnh báo bảo mật hệ thống
- Theo dõi tài khoản bị khóa, blacklist

---

## 16. Sao lưu & Phục hồi

Truy cập: `/Admin/Snapshot`

- Tạo snapshot (sao lưu) trạng thái database
- Phục hồi dữ liệu từ snapshot
- Xem danh sách snapshot đã tạo

---

## 17. Theo dõi & Định vị

Truy cập: `/Tracking/Map` hoặc `/Tracking/Connect`

- **Bản đồ số:** Xem vị trí xe trên bản đồ theo thời gian thực
- **Kết nối IoT:** Quản lý kết nối thiết bị GPS/IoT trên xe

---

## 18. Báo cáo

Truy cập: `/Admin/Reports`

- Xem báo cáo tổng hợp hoạt động hệ thống
- Xuất dữ liệu thống kê

---

## 19. Đăng xuất

Truy cập: `/Auth/Logout` hoặc click **Đăng xuất** ở sidebar

---

## Tóm tắt quyền Admin

| Chức năng | Admin | Manager | Staff |
|---|---|---|---|
| Dashboard toàn hệ thống | Yes | Yes | Yes |
| Quản lý đội xe | Full | Khu vực | Xem |
| Duyệt xe mới | Yes | No | No |
| Duyệt Partner/eKYC | Yes | No | No |
| Quản lý người dùng | Full | Khu vực | Xem |
| CRM & Marketing | Yes | No | No |
| Danh mục hệ thống | Yes | No | No |
| Sao lưu & Phục hồi | Yes | No | No |
| Cấu hình Khu vực | Yes | Limited | No |
