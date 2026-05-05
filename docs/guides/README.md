# Hướng Dẫn Sử Dụng Hệ Thống CarRental

> Tài liệu hướng dẫn sử dụng chi tiết cho từng vai trò trong hệ thống CarRental.

---

## Danh sách hướng dẫn

| # | Vai trò | File | Mô tả |
|---|---|---|---|
| 1 | **Quản trị viên (Admin)** | [01-admin-guide.md](01-admin-guide.md) | Toàn quyền điều hành hệ thống |
| 2 | **Khách hàng (Customer)** | [02-customer-guide.md](02-customer-guide.md) | Thuê xe, quản lý đơn đặt, hạng thành viên |
| 3 | **Đối tác (Partner)** | [03-partner-guide.md](03-partner-guide.md) | Đăng xe, quản lý đội xe, theo dõi doanh thu |
| 4 | **Tài xế (Driver)** | [04-driver-guide.md](04-driver-guide.md) | Nhận chuyến, cập nhật trạng thái, nhận thanh toán |
| 5 | **Quản lý KV (Manager) & Nhân viên KV (Staff)** | [05-manager-staff-guide.md](05-manager-staff-guide.md) | Quản lý theo khu vực được phân công |

---

## Tổng quan hệ thống

### Các vai trò (Roles)

| Role | Mô tả | Cổng truy cập |
|---|---|---|
| **Admin** | Quản trị viên toàn hệ thống | `/Admin/Dashboard` |
| **Manager** | Quản lý khu vực (Area) | `/Admin/Dashboard` |
| **Staff** | Nhân viên khu vực | `/Admin/Dashboard` |
| **Customer** | Khách hàng thuê xe | Trang chủ `/` |
| **Partner** | Đối tác sở hữu xe | `/Partner/Dashboard` |
| **Driver** | Tài xế thuê theo chuyến | `/Driver/DriverDashboard` |

### Sơ đồ luồng người dùng

```
┌─────────────────────────────────────────────────────────────┐
│                     KHÁCH HÀNG (Customer)                   │
│  Đăng ký → eKYC → Tìm xe → Đặt xe → Thanh toán           │
│             → Chuyến xe → Tích điểm → Đánh giá             │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│   PARTNER    │    │   DRIVER    │    │ ADMIN/MGR/   │
│ Đăng xe     │    │ Nhận chuyến │    │   STAFF      │
│ Quản lý xe  │    │ Cập nhật TX │    │ Quản lý KV  │
│ Doanh thu    │    │ Thanh toán  │    │ Dashboard    │
└──────────────┘    └──────────────┘    └──────────────┘
```

### Các trang chính

| Trang | Đường dẫn | Vai trò |
|---|---|---|
| Trang chủ | `/` | Tất cả |
| Đăng nhập | `/Auth/Login` | Tất cả |
| Đăng ký | `/Auth/Register` | Tất cả |
| Dashboard Admin | `/Admin/Dashboard` | Admin, Manager, Staff |
| Đội xe | `/Car` | Tất cả (xem) / Customer (đặt) |
| Đối tác | `/Partner/Dashboard` | Partner |
| Tài xế | `/Driver/DriverDashboard` | Driver |
| Hạng thành viên | `/Membership/Index` | Customer |
| eKYC | `/Ekyc/Index` | Customer |
| Thuê dài hạn | `/LongTerm/Index` | Customer |
| Thuê tài xế | `/Driver/Index` | Customer |
| Bản đồ xe | `/Car/MapSearch` | Tất cả |
| Giỏ hàng | `/Cart` | Customer |

### Trạng thái đơn đặt xe

| Mã | Trạng thái | Mô tả |
|---|---|---|
| 1 | **Draft** | Đang soạn thảo |
| 2 | **Pending** | Chờ xác nhận |
| 3 | **Confirmed** | Đã xác nhận |
| 4 | **Cancelled** | Đã hủy |
| 5 | **Active** | Đang thuê |
| 6 | **Completed** | Đã hoàn thành |

### Trạng thái xe

| Mã | Trạng thái | Mô tả |
|---|---|---|
| 1 | **Available** | Sẵn sàng cho thuê |
| 2 | **Rented** | Đang được thuê |
| 3 | **Maintenance** | Đang bảo trì |

### Trạng thái eKYC

| Mã | Trạng thái | Mô tả |
|---|---|---|
| 1 | **Pending** | Đang chờ duyệt |
| 2 | **Verified** | Đã xác thực |
| 3 | **Rejected** | Bị từ chối |
