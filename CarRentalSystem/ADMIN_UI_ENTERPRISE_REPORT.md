# ADMIN UI ENTERPRISE REPORT

Ngày hoàn thành: 11/06/2026

## 1. Tổng quan

Admin Area đã được nâng cấp thành enterprise dashboard dùng shell riêng, không phụ thuộc layout Staff/Manager. Toàn bộ Razor view trong `Areas/Admin/Views/**` nhận lớp giao diện chuẩn hóa thông qua `_ViewStart`, Admin shell và CSS scoped `.theme-admin`.

Các thay đổi nổi bật:

- Sidebar cố định, chia nhóm rõ ràng, active theo route, tự mở nhóm hiện tại, hỗ trợ collapse/offcanvas.
- Topbar sticky có tiêu đề, quick table search, notification, theme switch, avatar và logout POST.
- Breadcrumb, flash message và empty state dùng component Admin chung.
- Dashboard null-safe gồm 8 KPI, quick actions, 3 chart, warning panel và 4 bảng dữ liệu gần nhất.
- Net revenue lấy từ payment ledger; mốc ngày dashboard dùng ngày nghiệp vụ Việt Nam.
- Support có queue HTML cho browser, giữ JSON qua `format=json`, `Accept: application/json` và endpoint `/Admin/Support/Data`.
- Table, form, badge, card, modal, validation và detail metadata được chuẩn hóa bằng lớp CSS Admin dùng chung.
- Các trang Privacy, Support, SLO và Settlement được refactor trực tiếp thành queue/dashboard enterprise.
- PIN gate và masking PII của User được giữ nguyên.

## 2. File đã sửa hoặc tạo

### Admin shell và shared UI

- `Areas/Admin/Views/Shared/_AdminLayout.cshtml`
- `Areas/Admin/Views/Shared/_AdminSidebar.cshtml`
- `Areas/Admin/Views/Shared/_AdminTopbar.cshtml`
- `Areas/Admin/Views/Shared/_AdminBreadcrumb.cshtml`
- `Areas/Admin/Views/Shared/_AdminFlash.cshtml`
- `Areas/Admin/Views/Shared/_AdminEmptyState.cshtml`
- `Areas/Admin/Views/Shared/_EmptyState.cshtml`
- `wwwroot/css/admin.css`
- `wwwroot/js/admin.js`

### Dashboard, Support và các màn hình trọng điểm

- `Areas/Admin/Models/DashboardViewModel.cs`
- `Areas/Admin/Controllers/DashboardController.cs`
- `Areas/Admin/Views/Dashboard/Index.cshtml`
- `wwwroot/js/admin/dashboard.js`
- `Services/RevenueMetricsService.cs`
- `Areas/Admin/Controllers/SupportController.cs`
- `Areas/Admin/Views/Support/Index.cshtml`
- `Areas/Admin/Views/Privacy/Index.cshtml`
- `Areas/Admin/Views/Sre/Slo.cshtml`
- `Areas/Admin/Views/Settlement/Index.cshtml`
- `Helpers/UiLabels.cs`

### Kiểm thử

- `../CarRentalSystem.Tests/UnitTests/Controllers/AdminDashboardControllerTests.cs`
- `../CarRentalSystem.Tests/UnitTests/Controllers/AdminSupportControllerTests.cs`
- `../CarRentalSystem.Tests/UnitTests/Services/RevenueMetricsServiceTests.cs`
- `../playwright/e2e/06-admin-ops/admin-enterprise-ui.spec.js`

## 3. Layout và component

- Admin Area tiếp tục dùng `Areas/Admin/Views/_ViewStart.cshtml` trỏ riêng tới `_AdminLayout.cshtml`.
- CSS variables gồm `--admin-primary`, `--admin-sidebar-bg`, `--admin-bg`, `--admin-card-bg`, `--admin-border`, `--admin-text`, `--admin-muted`.
- Có light/dark mode, lưu theme và trạng thái sidebar trong `localStorage`.
- Responsive đã kiểm tra tại 1440x900, 1366x768, 768x1024 và 390x844.
- Logout trong sidebar/topbar là POST form có anti-forgery do ASP.NET Core Form Tag Helper sinh.
- Không thêm menu cho route không tồn tại và không tạo UI giả cho API-only controller.

## 4. Route đã kiểm tra

Các route sau đã được smoke test qua Playwright và không có lỗi 500:

- `/Admin/Dashboard`
- `/Admin/User`
- `/Admin/Car` - tồn tại và redirect tới `Manage`
- `/Admin/Booking`
- `/Admin/Payment`
- `/Admin/CarApproval`
- `/Admin/Enterprise`
- `/Admin/Reports`
- `/Admin/Promotions`
- `/Admin/Reviews`
- `/Admin/Tracking`
- `/Admin/Maintenance`
- `/Admin/Contacts`
- `/Admin/Support`
- `/Admin/Privacy`
- `/Admin/AuditLog`
- `/Admin/Sre/Slo`
- `/Admin/Lookups`

Support compatibility:

- `/Admin/Support` trả HTML cho browser.
- `/Admin/Support?format=json` trả JSON.
- `/Admin/Support/Data` là endpoint JSON rõ ràng.

Không tự tạo các route chưa tồn tại: `/Admin/Roles`, `/Admin/Handover`, `/Admin/Revenue`, `/Admin/Security`, `/Admin/Settings` và Change Password. Contracts tiếp tục dùng `/Admin/PhysicalContracts`; danh mục xe dùng `/Admin/Lookups`; security/operations dùng Alert, AuditLog và SRE.

## 5. Kết quả build và test

- `dotnet build CarRentalSystem.csproj --no-restore`: PASS, 0 warnings, 0 errors.
- `dotnet test ../CarRentalSystem.Tests/CarRentalSystem.Tests.csproj --no-restore`: PASS, 341/341.
- App local profile `playwright`: `/live` trả HTTP 200.
- Playwright `playwright/e2e/06-admin-ops/*.spec.js`: PASS, 12/12.
- Dashboard regression test xác nhận fallback null-safe và ledger revenue.
- Support regression test xác nhận HTML/JSON content negotiation.
- Playwright xác nhận sidebar/topbar/breadcrumb, active menu, logout POST, dark/light, route smoke và responsive.

## 6. Ảnh hưởng Admin, Staff và Manager

- Admin dùng layout riêng trong `Areas/Admin/Views/Shared/_AdminLayout.cshtml`.
- Không chỉnh `_StaffLayout.cshtml` hoặc `_ManagerLayout.cshtml` trong hạng mục này.
- Không chỉnh legacy `Views/Shared/_AdminLayout.cshtml` trong hạng mục này.
- Playwright xác nhận Staff và Manager không nhận class/layout Admin.
- Không thay đổi route, model binding, authorization policy, anti-forgery hoặc business workflow hiện có.
- Không chạy hoặc tạo database migration.

Lưu ý: worktree đã có sẵn các thay đổi khác tại legacy layout và Staff/Manager trước hạng mục này; các thay đổi đó được giữ nguyên, không revert.

## 7. Điểm còn lại

- Một số view đặc thù như map, camera, snapshot, enterprise và lookup vẫn giữ CSS/JS cục bộ để không phá hành vi chuyên biệt; chúng vẫn nhận shell và style Admin chung.
- Tracking đã dùng Leaflet với tile OpenStreetMap thống nhất và không còn yêu cầu Google Maps API key.
- Các API-only controller như ApiKeys, FleetOps và AreaStats tiếp tục không có menu UI.

## 8. Hướng dẫn demo

1. Chạy `dotnet run --launch-profile http`.
2. Đăng nhập bằng tài khoản có role Admin.
3. Mở `/Admin/Dashboard` để xem KPI, chart, warning panel và quick actions.
4. Thử collapse sidebar trên desktop và mở offcanvas ở tablet/mobile.
5. Dùng nút theme trên topbar để chuyển light/dark.
6. Mở `/Admin/Support` để xem queue HTML; mở `/Admin/Support?format=json` để kiểm tra JSON compatibility.
7. Mở `/Admin/User` để xác nhận PIN gate/PII vẫn được bảo vệ.
8. Mở `/Staff/Dashboard` và `/Manager/Dashboard` để xác nhận layout portal không bị ảnh hưởng.
