# CarRentalSystem - Hệ Thống Cho Thuê Xe Thông Minh

> Hệ thống cho thuê xe đa nền tảng (B2C + P2P Marketplace) với ASP.NET Core MVC, hỗ trợ nhiều vai trò người dùng, thanh toán trực tuyến, eKYC, định vị GPS, và quản lý khu vực.

---

## Mục Lục

- [Tổng Quan](#tổng-quan)
- [Tính Năng Chính](#tính-năng-chính)
- [Kiến Trúc Hệ Thống](#kiến-trúc-hệ-thống)
- [Các Vai Trò Người Dùng](#các-vai-trò-người-dùng)
- [Cấu Trúc Dự Án](#cấu-trúc-dự-án)
- [Công Nghệ Sử Dụng](#công-nghệ-sử-dụng)
- [Cài Đặt](#cài-đặt)
- [Chạy Ứng Dụng](#chạy-ứng-dụng)
- [Docker](#docker)
- [Cơ Sở Dữ Liệu](#cơ-sở-dữ-liệu)
- [API & Các Điểm Truy Cập Chính](#api--các-điểm-truy-cập-chính)
- [Thanh Toán](#thanh-toán)
- [Bảo Mật](#bảo-mật)
- [Background Jobs](#background-jobs)
- [Testing](#testing)
- [SonarCloud Quality Gate](#sonarcloud-quality-gate)
- [Tài Liệu Hướng Dẫn](#tài-liệu-hướng-dẫn)
- [Đóng Góp](#đóng-góp)

---

## Tổng Quan

**CarRentalSystem** là một nền tảng cho thuê xe toàn diện, hỗ trợ:

- **B2C (Doanh nghiệp - Khách hàng):** Khách hàng đặt xe trực tiếp từ đội xe của nền tảng
- **P2P Marketplace (Người - Người):** Đối tác (Partner) đăng xe cho thuê qua nền tảng

Hệ thống được xây dựng trên **ASP.NET Core 10.0** với **Entity Framework Core**, giao diện **Razor Views**, và tích hợp nhiều dịch vụ bên thứ ba.

---

## Tính Năng Chính

### 1. Quản Lý Xe

- Đăng xe mới (Partner tự đăng, Admin duyệt)
- Tìm kiếm và lọc xe theo: hãng, loại, số chỗ, mức giá, địa điểm
- Tìm kiếm xe trên bản đồ tương tác
- Quản lý trạng thái xe: Khả dụng / Đang thuê / Bảo trì
- Hỗ trợ xe điện (Electric Vehicle) với thông số pin, quãng đường
- Xóa mềm và khôi phục xe từ thùng rác
- Quản lý hình ảnh xe (nhiều ảnh)
- Hệ số hoa hồng linh hoạt theo xe

### 2. Hệ Thống Đặt Xe

- Đặt xe theo ngày/giờ với lịch trình cụ thể
- Giỏ hàng (Cart) - đặt nhiều xe cùng lúc
- Quản lý đơn đặt xe: Draft, Pending, Confirmed, Active, Completed, Cancelled
- Tính năng giao xe tận nơi (Delivery)
- Phí giao hàng, phí nhiên liệu tự động
- Hợp đồng số (Digital Contract) với chữ ký
- Quản lý handover (bàn giao xe)
- Tracking lộ trình xe

### 3. Thuê Dài Hạn

- Các gói thuê: 1 Ngày, 1 Tuần, 1 Tháng, 3 Tháng, 6 Tháng, 1 Năm
- Giảm giá theo gói (5% - 25%)
- Bảo dưỡng miễn phí theo gói
- Thanh toán trả góp theo kỳ hạn
- Hồ sơ kiểm định định kỳ

### 4. Thuê Tài Xế

- Đặt xe kèm tài xế
- Phân công tài xế theo chuyến
- Dashboard riêng cho tài xế
- Đánh giá tài xế sau chuyến
- Đánh giá khách hàng từ tài xế

### 5. eKYC - Xác Thực Danh Tính Điện Tử

- OCR đọc CCCD thông minh (FPT AI)
- Xác thực khuôn mặt (Face Matching)
- Quét QR từ chip CCCD (QR CCCD Parser)
- Mã hóa PII (CCCD, GPLX) at-rest
- Admin duyệt eKYC thủ công
- PIN 6 số bảo vệ thông tin nhạy cảm

### 6. Thanh Toán

- Tích hợp **PayOS** - thanh toán qua VietQR, thẻ ATM, Visa/Mastercard
- Ví điện tử (Wallet) với nạp/rút tiền
- Lịch sử giao dịch chi tiết
- Hoàn tiền (Refund) tự động
- Mã khuyến mãi (giảm % hoặc số tiền cố định)
- Deposit (đặt cọc) với các trạng thái: Pending, Holding, Refunded, Deducted

### 7. Hạng Thành Viên & Loyalty

- 4 hạng: Bronze, Silver, Gold, Platinum
- Tích điểm loyalty theo mỗi chuyến
- Quyền lợi theo hạng thành viên
- Nâng hạng tự động theo tổng chi tiêu

### 8. CRM & Marketing

- Mã khuyến mãi: giảm % hoặc giảm tiền cố định
- Hộp thư hỗ trợ (Contact Messages)
- Kiểm duyệt đánh giá (Review Moderation)
- Thông báo theo thời gian thực (SignalR)
- Voucher cho khách hàng

### 9. Bảo Trì Xe

- Lịch bảo trì định kỳ
- Cảnh báo bảo trì tự động (Background Job)
- Ghi nhận chi phí bảo trì
- Quản lý kiểm định xe (Inspection Records)
- Cập nhật số km (Odometer) và lịch thay dầu

### 10. Theo Dõi & Định Vị

- Bản đồ số (Map Search) với vị trí xe real-time
- Kết nối IoT/GPS device
- Tracking lộ trình (Route tracking)
- Cảnh báo khu vực (Geofencing) - Area Management

### 11. Quản Lý Khu Vực (Area Management)

- Tạo và quản lý nhiều khu vực hoạt động
- Phân quyền Manager và Staff theo khu vực
- Dashboard theo khu vực
- Staff chỉ thấy dữ liệu thuộc khu vực được gán

### 12. Sao Lưu & Phục Hồi

- Tạo Database Snapshot (sao lưu trạng thái DB)
- Phục hồi dữ liệu từ snapshot
- Xem danh sách snapshot đã tạo

### 13. AI Chat Assistant

- Tích hợp **Groq AI** (miễn phí, không giới hạn) - chatbot hỗ trợ khách hàng 24/7
- Hỗ trợ **Gemini AI** (Google)
- Quản lý phiên chat (Chat Session)

### 14. Bảo Mật Nâng Cao

- Rate Limiting - chống brute force
- CSRF Protection toàn cục
- Security Headers (X-Frame-Options, X-XSS-Protection, etc.)
- Audit Log - ghi nhận mọi HTTP request
- Blacklist tài khoản
- Khóa/mở khóa tài khoản (IsLocked)
- Hashing PII (CCCD hash, GPLX hash, Phone hash) để check trùng lặp

---

## Kiến Trúc Hệ Thống

```
┌─────────────────────────────────────────────────────────────────┐
│                     KHÁCH HÀNG (Customer)                        │
│  Đăng ký → eKYC → Tìm xe → Đặt xe → Thanh toán (PayOS)       │
│             → Chuyến xe → Tích điểm → Đánh giá                 │
└─────────────────────────────────────────────────────────────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        ▼                      ▼                      ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────────────┐
│   PARTNER    │    │   DRIVER     │    │  ADMIN / MANAGER /   │
│ Đăng xe      │    │ Nhận chuyến  │    │  STAFF               │
│ Quản lý xe   │    │ Cập nhật TX  │    │ Quản lý khu vực      │
│ Doanh thu    │    │ Thanh toán   │    │ Dashboard            │
└──────────────┘    └──────────────┘    └──────────────────────┘
```

### Luồng Nghiệp Vụ

| Luồng | Mô tả |
|-------|-------|
| Đặt xe ngắn hạn | Customer đặt → Pending → Admin duyệt → Confirmed → Active → Completed |
| Đặt xe dài hạn | Customer chọn gói → Đăng ký → Admin duyệt → Thanh toán kỳ → Bảo dưỡng định kỳ |
| Thuê tài xế | Customer đặt kèm tài xế → Admin phân công → Driver nhận chuyến → Hoàn thành |
| Partner đăng xe | Partner đăng xe → Admin duyệt → Xe hiển thị → Khách đặt → Nhận thanh toán |

---

## Các Vai Trò Người Dùng

| Vai trò | Mô tả | Cổng truy cập |
|---------|-------|--------------|
| **Admin** | Quản trị viên toàn hệ thống | `/Admin/Dashboard` |
| **Manager** | Quản lý khu vực (Area) | `/Admin/Dashboard` |
| **Staff** | Nhân viên khu vực | `/Admin/Dashboard` |
| **Customer** | Khách hàng thuê xe | Trang chủ `/` |
| **Partner** | Đối tác sở hữu xe | `/Partner/Dashboard` |
| **Driver** | Tài xế thuê theo chuyến | `/Driver/DriverDashboard` |

### So Sánh Quyền Admin / Manager / Staff

| Chức năng | Admin | Manager | Staff |
|-----------|-------|---------|-------|
| Dashboard toàn hệ thống | Yes | Yes | Yes |
| Quản lý đội xe | Full | Khu vực | Xem |
| Duyệt xe mới | Yes | No | No |
| Duyệt Partner/eKYC | Yes | No | No |
| Quản lý người dùng | Full | Khu vực | Xem |
| CRM & Marketing | Yes | No | No |
| Danh mục hệ thống | Yes | No | No |
| Sao lưu & Phục hồi | Yes | No | No |
| Cấu hình Khu vực | Yes | Limited | No |
| Tạo khu vực mới | Yes | No | No |
| Phân quyền nhân viên | Yes | Limited | No |

---

## Cấu Trúc Dự Án

```
CarRentalSystem/
├── CarRentalSystem/              # Project chính (ASP.NET Core MVC)
│   ├── Areas/
│   │   ├── Admin/                # Trung tâm điều hành (Admin/Manager/Staff)
│   │   │   ├── Controllers/       # Admin controllers
│   │   │   └── Views/            # Admin views (Razor)
│   │   ├── Partner/              # Cổng đối tác
│   │   │   ├── Controllers/
│   │   │   └── Views/
│   │   └── Driver/              # Cổng tài xế
│   │       ├── Controllers/
│   │       └── Views/
│   ├── Authorization/
│   │   ├── Handlers/            # Authorization handlers (Area-based)
│   │   └── Requirements/        # Authorization requirements
│   ├── BackgroundJobs/          # Hangfire background jobs
│   │   ├── EmailBackgroundJob.cs
│   │   ├── ContractExpirationJob.cs
│   │   ├── MaintenanceAlertJob.cs
│   │   └── DepositRefundJob.cs
│   ├── Controllers/             # Public controllers
│   │   ├── AuthController.cs    # Đăng nhập/đăng ký
│   │   ├── CarController.cs     # Đội xe
│   │   ├── BookingController.cs # Đơn đặt xe
│   │   ├── PaymentController.cs # Thanh toán
│   │   ├── CartController.cs    # Giỏ hàng
│   │   ├── DriverController.cs  # Thuê tài xế
│   │   ├── EkycController.cs     # eKYC
│   │   ├── MembershipController.cs
│   │   ├── LongTermController.cs # Thuê dài hạn
│   │   ├── TrackingController.cs # GPS/IoT tracking
│   │   └── ...
│   ├── Data/
│   │   └── ApplicationDbContext.cs  # EF Core DbContext
│   ├── Migrations/              # EF Core migrations
│   ├── Middleware/
│   │   ├── AuditLogMiddleware.cs     # Ghi log mọi request
│   │   ├── AccountLockMiddleware.cs   # Kiểm tra khóa tài khoản
│   │   ├── GlobalExceptionMiddleware.cs
│   │   └── RateLimitingMiddleware.cs  # Chống brute force
│   ├── Models/                   # Entity models (54+ models)
│   │   ├── User.cs               # Người dùng
│   │   ├── Car.cs                # Xe
│   │   ├── Booking.cs            # Đơn đặt xe
│   │   ├── Payment.cs            # Thanh toán
│   │   ├── Wallet.cs             # Ví điện tử
│   │   ├── Driver.cs             # Tài xế
│   │   ├── Area.cs               # Khu vực
│   │   └── ...
│   ├── Repositories/             # Repository pattern
│   ├── Services/                 # Business logic services (23+ services)
│   │   ├── AuthService.cs
│   │   ├── CarService.cs
│   │   ├── BookingService.cs
│   │   ├── PayOSPaymentService.cs
│   │   ├── BillingService.cs
│   │   ├── EkycService.cs
│   │   ├── GroqChatService.cs
│   │   ├── TrackingService.cs
│   │   ├── SnapshotService.cs    # Sao lưu DB
│   │   └── ...
│   ├── Views/                    # Public views
│   │   ├── Shared/              # Layouts, partial views
│   │   ├── Auth/                # Auth views
│   │   ├── Car/                 # Đội xe views
│   │   └── ...
│   ├── ViewComponents/           # Razor ViewComponents
│   └── wwwroot/                 # Static files (CSS, JS, images)
├── CarRentalSystem.Tests/       # Unit & Integration tests
│   ├── UnitTests/
│   │   ├── Controllers/
│   │   ├── Services/
│   │   └── SecurityMonitorServiceTests.cs
│   ├── IntegrationTests/
│   │   ├── BookingIntegrationTests.cs
│   │   ├── CarIntegrationTests.cs
│   │   └── ...
│   └── Helpers/
├── docs/
│   ├── guides/                   # Hướng dẫn sử dụng
│   │   ├── README.md
│   │   ├── 01-admin-guide.md
│   │   ├── 02-customer-guide.md
│   │   ├── 03-partner-guide.md
│   │   ├── 04-driver-guide.md
│   │   └── 05-manager-staff-guide.md
│   ├── product-backlog-full.md
│   ├── release-backlog.md
│   └── CarRentalSystem.vsdx     # Sơ đồ hệ thống (Visio)
├── playwright/                      # E2E tests (Playwright)
│   └── e2e/
├── nginx/                        # Nginx reverse proxy config
│   └── conf.d/
│       └── carrental-proxy.conf
├── Dockerfile                    # Multi-stage Docker build
├── sonar-project.properties      # SonarCloud config
└── CarRentalSystem.sln          # Solution file
```

---

## Công Nghệ Sử Dụng

### Backend

| Công nghệ | Phiên bản | Mục đích |
|-----------|-----------|----------|
| .NET | 10.0 | Runtime |
| ASP.NET Core MVC | 10.0 | Web framework |
| Entity Framework Core | 10.0.3 | ORM |
| SQL Server | - | Database |
| Hangfire | 1.8.20 | Background job processing |
| Hangfire.SqlServer | 1.8.20 | Job storage |

### Frontend & UI

| Công nghệ | Mục đích |
|-----------|----------|
| Bootstrap 5 | CSS framework |
| jQuery + jQuery Validation | Client-side interactivity |
| Razor Views | Server-side rendering |

### Packages NuGet Chính

| Package | Phiên bản | Mục đích |
|---------|-----------|----------|
| BCrypt.Net-Next | 4.1.0 | Password hashing |
| ClosedXML | 0.104.2 | Export Excel |
| DotNetEnv | 2.5.0 | Environment variables |
| Google.GenAI | 0.11.0 | Gemini AI integration |
| payOS | 2.0.1 | Payment gateway |
| Swashbuckle.AspNetCore | 7.2.0 | API documentation |
| AspNetCore.HealthChecks.SqlServer | 9.0.0 | DB health check |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.3 | Testing |

### Testing

| Công nghệ | Mục đích |
|-----------|----------|
| xUnit | Unit test framework |
| Moq | Mocking framework |
| FluentAssertions | Assertion library |
| Microsoft.AspNetCore.Mvc.Testing | Integration testing |
| coverlet.msbuild | Code coverage |

### External Services

| Dịch vụ | Mục đích |
|---------|----------|
| **FPT AI (eKYC)** | OCR CCCD, Face Matching, Liveness Detection |
| **PayOS** | Thanh toán VietQR, thẻ ATM, Visa/Mastercard |
| **Groq AI** | AI Chat Assistant (miễn phí) |
| **Gemini AI** | AI Chat Assistant (Google) |
| **Redis** (tùy chọn) | Distributed cache |
| **SonarCloud** | Code quality analysis |

---

## Cài Đặt

### Yêu Cầu

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [SQL Server](https://www.microsoft.com/sql-server/sql-server-downloads) (LocalDB, Express, hoặc Full)
- [Docker](https://www.docker.com/) (tùy chọn, để chạy với container)
- Visual Studio 2022+ hoặc VS Code

### 1. Clone & Restore

```bash
git clone https://github.com/your-repo/CarRentalSystem.git
cd CarRentalSystem/CarRentalSystem
dotnet restore
```

### 2. Cấu Hình Database

Chỉnh sửa `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\v11.0;Database=CarRentalDB;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

Hoặc sử dụng SQL Server đầy đủ:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=CarRentalDB;User Id=sa;Password=your_password;TrustServerCertificate=True"
  }
}
```

### 3. Cấu Hình Biến Môi Trường (Tùy chọn)

Tạo file `.env` trong thư mục `CarRentalSystem`:

```env
# Database
ConnectionStrings__DefaultConnection=Server=(localdb)\v11.0;Database=CarRentalDB;Trusted_Connection=True

# PayOS (thay bằng credentials thật)
PayOS__ClientId=your_client_id
PayOS__ApiKey=your_api_key
PayOS__ChecksumKey=your_checksum_key

# FPT AI eKYC
FptAi__ApiKey=your_fpt_api_key

# Groq AI
Groq__ApiKey=your_groq_api_key

# Redis (tùy chọn)
Redis__ConnectionString=localhost:6379
```

### 4. Chạy Migration

```bash
cd CarRentalSystem
dotnet ef database update
```

Hệ thống sẽ tự động chạy migration khi khởi động (trong `Program.cs`).

---

## Chạy Ứng Dụng

### Development

```bash
cd CarRentalSystem
dotnet run
```

Ứng dụng sẽ chạy tại `http://localhost:5000` (hoặc cổng được cấu hình trong `Properties/launchSettings.json`).

### Chế Độ Development vs Production

| Chế độ | Migration | Seed dữ liệu | Hangfire Dashboard |
|--------|-----------|---------------|-------------------|
| Development | Auto | Auto (DB trống) | Mở (ai cũng vào) |
| Production | Auto | Không | Chỉ Admin |

### Health Check

```bash
curl http://localhost:5000/health
```

### Hangfire Dashboard

Truy cập `/hangfire` (trong Development: mở cho tất cả; trong Production: chỉ Admin).

---

## Docker

### Build Image

```bash
docker build -t carrentalsystem -f Dockerfile .
```

### Chạy Container

```bash
docker run -d \
  --name carrental \
  -p 5000:5000 \
  -e ConnectionStrings__DefaultConnection="Server=host.docker.internal;Database=CarRentalDB;User Id=sa;Password=your_password;TrustServerCertificate=True" \
  -e PayOS__ClientId="your_client_id" \
  -e PayOS__ApiKey="your_api_key" \
  -e PayOS__ChecksumKey="your_checksum_key" \
  carrentalsystem
```

### Docker Compose (Nginx + App)

```yaml
version: '3.8'
services:
  app:
    build: .
    ports:
      - "5000:5000"
    environment:
      - ConnectionStrings__DefaultConnection=Server=mssql;Database=CarRentalDB;User Id=sa;Password=StrongPassword123!;TrustServerCertificate=True
    depends_on:
      - mssql

  mssql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=StrongPassword123!
      - MSSQL_PID=Express

  nginx:
    image: nginx:latest
    volumes:
      - ./nginx/conf.d:/etc/nginx/conf.d:ro
    ports:
      - "80:80"
    depends_on:
      - app
```

### Nginx Reverse Proxy

Cấu hình proxy trong `nginx/conf.d/carrental-proxy.conf`:

```nginx
server {
    listen 80;
    server_name your-domain.com;

    location / {
        proxy_pass http://app:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

---

## Cơ Sở Dữ Liệu

### Database Schema (Các bảng chính)

```
Users ──────────────────────────── Người dùng
├── Cars ───────────────────────── Xe (thuộc User nếu là Partner)
│   ├── CarImages ──────────────── Ảnh xe
│   ├── Reviews ────────────────── Đánh giá xe
│   ├── MaintenanceLogs ────────── Lịch sử bảo trì
│   ├── VehicleTrackings ────────── Tracking GPS
│   └── Alerts ─────────────────── Cảnh báo xe
├── Bookings ───────────────────── Đơn đặt xe
│   ├── BookingAddOns ───────────── Dịch vụ bổ sung
│   ├── HandoverRecords ────────── Bàn giao xe
│   ├── RentalContract ──────────── Hợp đồng thuê
│   ├── DigitalContract ─────────── Hợp đồng số
│   └── BookingTransaction ─────── Giao dịch thanh toán
│       └── PayOSTransaction ────── PayOS transaction
├── Payments ───────────────────── Thanh toán
├── Wallets ────────────────────── Ví điện tử
│   └── WalletTransactions ─────── Lịch sử ví
├── Promotions ─────────────────── Mã khuyến mãi
├── Locations ──────────────────── Địa điểm
├── Areas ──────────────────────── Khu vực
│   ├── AreaLocations ───────────── Địa điểm thuộc khu vực
│   └── AreaStaffs ──────────────── Nhân viên theo khu vực
├── LongTermRentals ────────────── Thuê dài hạn
│   ├── PaymentSchedules ───────── Lịch thanh toán
│   └── InspectionRecords ──────── Hồ sơ kiểm định
├── Drivers ─────────────────────── Tài xế
│   └── DriverBookings ──────────── Phân công tài xế
│       └── DriverReviews ───────── Đánh giá tài xế
├── MembershipTiers ─────────────── Hạng thành viên
├── AuditLogs ──────────────────── Nhật ký hệ thống
├── Invoices ────────────────────── Hóa đơn
├── DbSnapshots ────────────────── Snapshot DB
└── ContactMessages ─────────────── Tin nhắn hỗ trợ
```

### Các Bảng Tra Cứu (Lookups)

| Bảng | Mô tả |
|------|-------|
| CarBrands | Hãng xe (Toyota, Honda, BMW,...) |
| CarTypes | Loại xe (Sedan, SUV, Hatchback,...) |
| CarStatuses | Trạng thái xe (Available, Rented, Maintenance) |
| BookingStatuses | Trạng thái đơn đặt (Draft, Pending, Confirmed, Active, Completed, Cancelled) |
| ContractTypes | Gói thuê dài hạn (1 Ngày, 1 Tuần, 1 Tháng,...) |
| AddOns | Dịch vụ bổ sung (Nước suối, Khăn lạnh, Combo...) |
| HolidayPricings | Hệ số giá ngày lễ |

### Trạng Thái Đơn Đặt Xe

| Mã | Trạng thái | Mô tả |
|----|------------|-------|
| 1 | Draft | Đang soạn thảo |
| 2 | Pending | Chờ xác nhận |
| 3 | Confirmed | Đã xác nhận |
| 4 | Cancelled | Đã hủy |
| 5 | Active | Đang thuê |
| 6 | Completed | Đã hoàn thành |

### EF Core Migrations

```bash
# Tạo migration mới
dotnet ef migrations add AddNewFeature

# Cập nhật database
dotnet ef database update

# Xóa migration cuối
dotnet ef migrations remove

# Script SQL
dotnet ef migrations script -o migration.sql
```

---

## API & Các Điểm Truy Cập Chính

### Trang Công Khai

| Trang | Đường dẫn | Mô tả |
|-------|-----------|-------|
| Trang chủ | `/` | Trang chủ với xe nổi bật |
| Đăng nhập | `/Auth/Login` | Đăng nhập |
| Đăng ký | `/Auth/Register` | Đăng ký tài khoản |
| Đội xe | `/Car` | Danh sách xe khả dụng |
| Tìm trên bản đồ | `/Car/MapSearch` | Bản đồ xe |
| Thuê dài hạn | `/LongTerm/Index` | Đăng ký thuê dài hạn |
| Thuê tài xế | `/Driver/Index` | Đặt xe kèm tài xế |
| Hạng thành viên | `/Membership/Index` | Quản lý hạng thành viên |
| eKYC | `/Ekyc/Index` | Xác thực danh tính điện tử |

### Trang Khách Hàng (Đã đăng nhập)

| Trang | Đường dẫn |
|-------|-----------|
| Giỏ hàng | `/Cart` |
| Chuyến xe của tôi | `/Booking/MyBookings` |
| Thanh toán | `/Payment/Process` |
| Hồ sơ | `/Auth/Profile` |
| Chat AI | `/Chat` |

### Trang Partner

| Trang | Đường dẫn |
|-------|-----------|
| Partner Dashboard | `/Partner/Dashboard` |
| Đội xe của tôi | `/Partner/Dashboard/MyCars` |
| Thêm xe mới | `/Partner/Dashboard/AddCar` |
| Đơn thuê xe | `/Partner/Dashboard/Bookings` |
| Hợp đồng | `/Partner/Dashboard/Contract` |

### Trang Driver

| Trang | Đường dẫn |
|-------|-----------|
| Driver Dashboard | `/Driver/DriverDashboard` |
| Chi tiết chuyến | `/Driver/DriverDashboard/Details/{id}` |

### Trang Admin / Manager / Staff

| Trang | Đường dẫn | Vai trò |
|-------|-----------|---------|
| Dashboard | `/Admin/Dashboard` | Tất cả |
| Quản lý xe | `/Admin/Car/Manage` | Tất cả |
| Duyệt xe mới | `/Admin/CarApproval` | Admin |
| Thùng rác xe | `/Admin/Car/Trash` | Admin, Manager |
| Quản lý đơn | `/Admin/Booking` | Tất cả |
| Quản lý người dùng | `/Admin/User` | Tất cả |
| Duyệt Partner | `/Admin/User/PartnerRequests` | Admin |
| Duyệt eKYC | `/Admin/User/EkycPending` | Admin |
| Quản lý tài xế | `/Admin/Driver` | Tất cả |
| Hợp đồng số | `/Admin/DigitalContract` | Tất cả |
| HĐ Đối tác/Tài xế | `/Admin/PartnerContract` | Admin |
| Thanh toán | `/Admin/Payment` | Admin |
| Mã khuyến mãi | `/Admin/Promotions` | Admin |
| Hộp thư hỗ trợ | `/Admin/Contacts` | Admin |
| Kiểm duyệt đánh giá | `/Admin/Reviews` | Admin |
| Bảo trì | `/Admin/Maintenance` | Tất cả |
| Hạng thành viên | `/Admin/Membership` | Admin |
| Danh mục hệ thống | `/Admin/Lookups` | Admin |
| Giá ngày lễ | `/Admin/HolidayPricing` | Admin |
| Khu vực | `/Admin/Area` | Admin, Manager |
| Dashboard Khu vực | `/Admin/AreaDashboard` | Tất cả |
| Phân quyền Khu vực | `/Admin/AreaStaff` | Admin, Manager |
| Bảo mật & Cảnh báo | `/Admin/Alert` | Admin |
| Snapshot / Sao lưu | `/Admin/Snapshot` | Admin |
| Bản đồ số | `/Tracking/Map` | Tất cả |
| Kết nối IoT | `/Tracking/Connect` | Tất cả |
| Báo cáo | `/Admin/Reports` | Admin |
| Audit Log | `/Admin/AuditLog` | Admin |

---

## Thanh Toán

### PayOS - Luồng Thanh Toán

```
Khách hàng chọn thanh toán
         │
         ▼
Tạo Payment Link (PayOS API)
         │
         ▼
Chuyển hướng sang trang PayOS
(VietQR / ATM / Visa-Mastercard)
         │
         ▼
    Thanh toán thành công
         │
         ▼
Webhook callback → Cập nhật trạng thái đơn
         │
         ▼
Gửi email xác nhận
```

### Deposit (Đặt cọc)

- Mặc định: 15.000.000 VND
- Trạng thái: `Pending` → `Holding` → `Refunded` (hoàn tiền) / `Deducted` (khấu trừ nếu hư hỏng)
- Hoàn tiền tự động qua Background Job (`DepositRefundJob`) chạy hàng ngày lúc 9:00 AM

### Mã Khuyến Mãi

- Giảm theo %: VD giảm 10% giá trị đơn
- Giảm số tiền cố định: VD giảm 50.000 VND
- Giới hạn số lần sử dụng
- Ngày hết hạn

---

## Bảo Mật

### Các Lớp Bảo Vệ

| Lớp | Công nghệ | Mục đích |
|-----|-----------|----------|
| Authentication | Cookie Authentication | Xác thực người dùng |
| Authorization | Policy-based (Area-based) | Phân quyền theo Area |
| Password | BCrypt | Hash mật khẩu |
| PII Encryption | ASP.NET Data Protection | Mã hóa CCCD/GPLX at-rest |
| CSRF | AutoValidateAntiforgeryToken | Chống CSRF attack |
| Rate Limiting | Custom Middleware | Chống brute force |
| Security Headers | Custom Middleware | X-Frame-Options, XSS Protection |
| Session Security | HttpOnly, Secure, SameSite | Cookie hardening |
| Account Lock | AccountLockMiddleware | Khóa tài khoản vi phạm |
| Audit Log | AuditLogMiddleware | Ghi nhận mọi request |
| Blacklist | IsBlacklisted flag | Cấm tài khoản |
| Input Validation | Data Annotations + FluentValidation | Validate dữ liệu đầu vào |
| Upload Size Limit | Kestrel 10MB max | Chống DoS upload |

### Rate Limiting

- Giới hạn số request trên IP trong khoảng thời gian
- Khóa tài khoản tạm thời sau nhiều lần đăng nhập thất bại
- Lockout tài khoản (IsLocked) với thời gian tùy chỉnh

### PII Protection

- CCCD, số GPLX được mã hóa trong database
- SHA-256 hash để check trùng lặp (không lưu plaintext)
- PIN 6 số để xem thông tin PII

### Audit Log

Mọi HTTP request (trừ static files, health check, hangfire) đều được ghi log:
- Ai (User)
- Làm gì (Action, Controller, Method)
- Khi nào (Timestamp)
- IP Address
- Request/Response details

---

## Background Jobs

Hệ thống sử dụng **Hangfire** để xử lý các tác vụ nền:

| Job | Tần suất | Chức năng |
|-----|---------|-----------|
| `contract-expiration-check` | Mỗi giờ | Kiểm tra hợp đồng hết hạn |
| `maintenance-alert-check` | Hàng ngày 8:00 AM | Gửi cảnh báo bảo trì định kỳ |
| `deposit-refund-process` | Hàng ngày 9:00 AM | Hoàn tiền đặt cọc |

---

## Testing

### Chạy Unit Tests

```bash
cd CarRentalSystem.Tests
dotnet test
```

### Chạy với Coverage Report

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Cấu trúc Test

```
CarRentalSystem.Tests/
├── UnitTests/
│   ├── Controllers/
│   │   ├── HomeControllerTests.cs
│   │   ├── CarControllerTests.cs
│   │   ├── BookingControllerTests.cs
│   │   ├── AuthControllerTests.cs
│   │   ├── AreaControllerTests.cs
│   │   └── AlertControllerTests.cs
│   └── Services/
│       ├── AuthServiceTests.cs
│       ├── BookingServiceTests.cs
│       ├── CarServiceTests.cs
│       ├── EmailServiceTests.cs
│       ├── RepositoryTests.cs
│       ├── RentalContractTests.cs
│       ├── SecurityMonitorServiceTests.cs
│       └── TrackingServiceTests.cs
├── IntegrationTests/
│   ├── AuthIntegrationTests.cs
│   ├── BookingIntegrationTests.cs
│   ├── CarIntegrationTests.cs
│   ├── PaymentIntegrationTests.cs
│   ├── TrackingIntegrationTests.cs
│   └── SmokeTests.cs
└── Helpers/
    ├── TestAsyncQueryProvider.cs
    ├── NullCacheService.cs
    └── MockRepositoryHelper.cs
```

### Smoke Tests

Kiểm tra các endpoint quan trọng:
- `/` - Trang chủ
- `/Auth/Login` - Đăng nhập
- `/Car` - Danh sách xe
- `/health` - Health check

### Integration Tests

Test tích hợp với database thực (SQL Server hoặc InMemory):
- Auth flow
- Booking flow
- Car management
- Payment processing
- GPS tracking

---

## SonarCloud Quality Gate

### Cấu Hình

File `sonar-project.properties`:

```
sonar.projectKey=CarRentalSystem
sonar.language=cs
sonar.dotnet.version=10.0
sonar.cs.opencover.reportsPaths=CarRentalSystem.Tests/TestResults/**/coverage.cobertura.xml
```

### Chạy Local Analysis

```bash
dotnet sonarscanner begin \
  /k:"CarRentalSystem" \
  /d:sonar.host.url="https://sonarcloud.io" \
  /d:sonar.token="your_token"

dotnet build
dotnet test
dotnet sonarscanner end
```

### Quality Gates

- **Code Smells**: Tối đa 10
- **Bugs**: 0 (Critical, Major)
- **Vulnerabilities**: 0
- **Security Hotspots**: Reviewed
- **Coverage**: Tối thiểu 70%

---

## Tài Liệu Hướng Dẫn

Chi tiết tại thư mục `docs/guides/`:

| File | Vai trò | Mô tả |
|------|---------|-------|
| [01-admin-guide.md](docs/guides/01-admin-guide.md) | Admin | Toàn quyền điều hành hệ thống |
| [02-customer-guide.md](docs/guides/02-customer-guide.md) | Customer | Thuê xe, quản lý đơn đặt, hạng thành viên |
| [03-partner-guide.md](docs/guides/03-partner-guide.md) | Partner | Đăng xe, quản lý đội xe, theo dõi doanh thu |
| [04-driver-guide.md](docs/guides/04-driver-guide.md) | Driver | Nhận chuyến, cập nhật trạng thái, nhận thanh toán |
| [05-manager-staff-guide.md](docs/guides/05-manager-staff-guide.md) | Manager, Staff | Quản lý theo khu vực được phân công |

### Tài Liệu Khác

| File | Mô tả |
|------|-------|
| `docs/product-backlog-full.md` | Product Backlog - danh sách tính năng |
| `docs/release-backlog.md` | Release Backlog - kế hoạch phát hành |
| `docs/car-rental-platform-system-design.md` | System Design Document |

---

## Đóng Góp

1. Fork repository
2. Tạo branch mới (`git checkout -b feature/TenTinhNang`)
3. Commit thay đổi (`git commit -m 'Add new feature'`)
4. Push lên branch (`git push origin feature/TenTinhNang`)
5. Tạo Pull Request

### Quy Tắc Code

- Code theo C# Coding Conventions
- Viết Unit Tests cho logic nghiệp vụ mới
- Chạy `dotnet build` và `dotnet test` trước khi commit
- Commit message bằng tiếng Việt, mô tả rõ ràng
- Đảm bảo SonarCloud Quality Gate pass trước khi merge

---

## Thông Tin Liên Hệ

- **Hotline:** 1900 1234 (24/7)
- **Email:** support@smartrental.vn
- **Website:** https://smartrental.vn

---

*Document version: 1.0.0*
*Last updated: 2026-05-05*
