# Hướng Dẫn Sử Dụng — Khách hàng (Customer)

> **Vai trò:** Customer là người dùng thuê xe trên nền tảng CarRental. Có thể tìm kiếm xe, đặt xe, xem chuyến xe, đánh giá và quản lý tài khoản.

---

## 1. Đăng ký & Đăng nhập

### 1.1. Đăng ký tài khoản
1. Truy cập `/Auth/Register`
2. Nhập thông tin: Họ tên, Email, Số điện thoại, Mật khẩu
3. Xác thực email (link gửi qua email)
4. Đăng nhập tại `/Auth/Login`

### 1.2. Đăng nhập
- Truy cập `/Auth/Login`
- Nhập email + mật khẩu
- Tài khoản mặc định sau đăng ký sẽ có role **Customer**

---

## 2. Xác thực eKYC (Bắt buộc trước khi thuê xe)

> Để đặt xe, bạn cần xác thực danh tính điện tử (eKYC).

Truy cập: `/Ekyc/Index` (hoặc qua menu người dùng > **Xác thực eKYC**)

**Thông tin cần cung cấp:**
- Ảnh CCCD mặt trước
- Ảnh CCCD mặt sau
- Ảnh bằng lái xe (mặt trước + mặt sau)
- Ảnh chân dung (selfie)

**Trạng thái eKYC:**
- **Pending** — Đang chờ duyệt
- **Verified** — Đã xác thực thành công, có thể đặt xe
- **Rejected** — Bị từ chối, vui lòng nộp lại

> Sau khi nộp, Admin sẽ duyệt trong vòng 24h.

---

## 3. Tìm kiếm & Thuê xe

### 3.1. Xem danh sách xe
Truy cập: `/Car` (từ menu **Đội xe**)

- Xem danh sách xe khả dụng
- Lọc theo:
  - Địa điểm (thành phố)
  - Loại xe (Sedan, SUV,...)
  - Hãng xe (Toyota, Honda,...)
  - Số chỗ ngồi
  - Mức giá
- Sắp xếp theo giá, đánh giá

### 3.2. Tìm kiếm trên bản đồ
Truy cập: `/Car/MapSearch`

- Xem xe trên bản đồ tương tác
- Di chuyển bản đồ để xem xe gần vị trí bạn
- Click vào marker để xem chi tiết xe

### 3.3. Thuê xe dài hạn
Truy cập: `/LongTerm/Index` (menu **Thuê dài hạn**)

- Đăng ký thuê xe từ 1 tháng trở lên
- Xem bảng giá dài hạn ưu đãi
- Tính năng: Thanh toán theo tháng, bảo dưỡng định kỳ

### 3.4. Thuê xe kèm tài xế
Truy cập: `/Driver/Index` (menu **Thuê tài xế**)

- Đặt dịch vụ thuê xe có tài xế
- Chọn tài xế theo đánh giá, kinh nghiệm

---

## 4. Đặt xe (Quy trình chi tiết)

### 4.1. Chọn xe
1. Chọn xe muốn thuê từ danh sách
2. Xem chi tiết: hình ảnh, giá, mô tả, tính năng

### 4.2. Chọn ngày giờ
1. Chọn ngày nhận xe
2. Chọn ngày trả xe
3. Chọn giờ nhận / trả

### 4.3. Chọn địa điểm
- Địa điểm nhận xe (mặc định theo vị trí xe)
- Địa điểm trả xe

### 4.4. Thêm dịch vụ bổ sung (Add-ons)
- Bảo hiểm nâng cao
- GPS
- Ghế trẻ em
- Thuê tài xế thêm

### 4.5. Xem giá & Thanh toán
- Xem chi tiết giá: giá thuê + phí dịch vụ + bảo hiểm + thuế
- Nhập mã khuyến mãi (nếu có)
- Chọn phương thức thanh toán

### 4.6. Xác nhận đặt xe
- Xem lại thông tin
- Nhấn **Xác nhận đặt xe**
- Nhận email xác nhận + mã đặt xe

---

## 5. Giỏ hàng (Cart)

Truy cập: `/Cart`

- Xem danh sách xe đã chọn (nếu thêm nhiều xe cùng lúc)
- Thêm / Bớt xe
- Cập nhật ngày thuê
- Tính tổng giá
- Tiến hành thanh toán giỏ hàng

---

## 6. Chuyến xe của tôi (My Bookings)

Truy cập: `/Booking/MyBookings` (hoặc menu **Chuyến xe của tôi**)

- Xem danh sách tất cả đơn đặt xe của bạn
- Trạng thái đơn:
  - **Draft** — Đang soạn thảo
  - **Pending** — Chờ xác nhận
  - **Confirmed** — Đã xác nhận
  - **Active** — Đang thuê
  - **Completed** — Đã hoàn thành
  - **Cancelled** — Đã hủy
- Xem chi tiết đơn đặt
- Hủy đơn (nếu chưa bắt đầu)

---

## 7. Hạng thành viên & Tích điểm

Truy cập: `/Membership/Index` (menu **Hạng thành viên**)

### 7.1. Hạng thành viên
- **Bronze** — Mặc định khi đăng ký
- **Silver** — Chi tiêu tổng ≥ mốc quy định
- **Gold** — Chi tiêu tổng ≥ mốc cao hơn
- **Platinum** — Chi tiêu tổng ≥ mốc cao nhất

### 7.2. Quyền lợi theo hạng
- Giảm giá khi thuê xe
- Ưu tiên hỗ trợ
- Tích lũy loyalty points

### 7.3. Tích điểm
- Mỗi lần thuê xe sẽ tích điểm loyalty
- Đổi điểm lấy ưu đãi

---

## 8. Hồ sơ cá nhân

Truy cập: `/Auth/Profile`

- Cập nhật thông tin cá nhân
- Đổi mật khẩu
- Xem lịch sử giao dịch
- Xem hạng thành viên hiện tại

---

## 9. Mã khuyến mãi

- Nhập mã khuyến mãi trong bước thanh toán đặt xe
- Xem các mã khuyến mãi đang có tại trang khuyến mãi (nếu có)

---

## 10. Hỗ trợ

Truy cập: `/Contacts` hoặc gửi email về `support@smartrental.vn`

- Gửi tin nhắn hỗ trợ
- FAQ — Câu hỏi thường gặp
- Hotline: 1900 1234 (24/7)

---

## Tóm tắt luồng người dùng Customer

```
Đăng ký → Đăng nhập → Xác thực eKYC
                              ↓
                        Đang chờ duyệt (24h)
                              ↓
                         eKYC Verified
                              ↓
              Tìm xe → Chọn xe → Chọn ngày → Thanh toán
                              ↓
                        Đơn đặt xe Pending
                              ↓
                        Admin xác nhận → Active
                              ↓
                        Nhận xe → Sử dụng
                              ↓
                        Trả xe → Completed
                              ↓
                        Tích điểm + Đánh giá
```
