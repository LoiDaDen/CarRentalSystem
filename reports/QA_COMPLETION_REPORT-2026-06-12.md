# QA & Completion Report

## 1. Tổng quan
- Ngày test: 2026-06-12
- Môi trường: Windows, ASP.NET Core Development, mock/sandbox providers, SQL Server LocalDB
- Framework: .NET 10, ASP.NET Core MVC + Areas, Entity Framework Core 10
- Database: `CarRentalSystem_Local` (demo/smoke), `CarRentalSystem_Playwright` (E2E phá hủy)
- URL chạy thật: `http://localhost:5168`
- Tài khoản đã test: Guest, Customer, Partner, Staff, Manager, Admin
- Backup Local DB: `CarRentalSystem/App_Data/Backups/CarRentalSystem_Local_before_QA_20260612_023951.bak`
- Kết luận chung: **PARTIAL PASS toàn checklist; PASS cho phạm vi demo xe tự lái**

Hệ thống đủ để demo luồng thuê xe tự lái, RBAC, duyệt xe, mock payment, vận hành và dashboard. Dịch vụ xe có tài xế vẫn chủ động tắt; 17 test phụ thuộc vendor/Production được để pending, không giả lập PASS.

## 2. Kết quả build/run
| Hạng mục | Kết quả | Ghi chú |
|---|---|---|
| `dotnet restore` | PASS | Package đã cập nhật đầy đủ |
| `dotnet build` | PASS | 0 warning, 0 error |
| `dotnet run` | PASS | Đang chạy tại `http://localhost:5168` |
| `dotnet test` | PASS | 354/354 pass, 0 fail, 0 skip |
| E2E test | PASS phạm vi mock | Playwright A-to-Z: 48 specs, 697 tests, 680 pass, 17 pending chủ ý, 0 fail |

## 3. Kết quả test theo role
| Role | Kết quả | Lỗi phát hiện | Đã sửa |
|---|---|---|---|
| Guest | PASS | Cần xác minh route nội bộ không lộ | Route nội bộ redirect login; public/health smoke 200 |
| Customer | PASS | Xe Pending/Maintenance có thể lọt vào một số entry point | Áp dụng shared rentable policy tại public, quote, cart, checkout và reservation |
| Partner | PASS có giới hạn | Thiếu trạng thái/lý do từ chối rõ ràng | Xe mới Pending; hiển thị Rejected và lý do; chặn dữ liệu xe không thuộc owner |
| Staff | PASS | Không phát hiện lộ quyền Admin trong E2E | Policy/layout Staff được xác minh |
| Manager | PASS | Không phát hiện lộ quyền duyệt xe Admin-only | Policy/layout Manager được xác minh |
| Admin | PASS | `/Admin/Tracking/Map` trả 404; approval state chưa nhất quán | Bổ sung action Map; chuẩn hóa approve/reject và route Admin |

## 4. Kết quả test workflow
| Workflow | Kết quả | Lỗi phát hiện | File đã sửa |
|---|---|---|---|
| Thuê xe tự lái | PASS | Thiếu quy tắc thuê dùng chung; thiếu chặn ngày quá khứ tại service cuối | `CarRentabilityPolicy.cs`, booking/cart/car/home services/controllers |
| Xe có tài xế/đưa đón | DISABLED đúng thiết kế | Không triển khai public workflow | Đã xác minh không mở public API/không trộn vào self-drive |
| Partner đăng xe | PASS có giới hạn | Xe mới cần explicit Pending | `OwnerCarsController.cs`, owner portal view models/views |
| Admin duyệt xe | PASS | Approve/reject chưa lưu trạng thái và lý do rõ ràng | `CarApprovalController.cs`, `Car.cs`, migration, view |
| Thanh toán mock | PASS | Cần xác minh idempotency/callback/amount bằng mock | Regression + Playwright mock suite |
| Hợp đồng | PASS phạm vi hiện có | Không gọi eKYC/ký thật | Playwright mock workflow |
| Review | PASS | Không phát hiện lỗi High trong workflow chính | Playwright A-to-Z |
| Promotion | PASS phạm vi rule hiện có | Không tuyên bố rule chưa tồn tại | Seed + automated tests hiện có |
| Maintenance | PASS | Tạo maintenance chưa khóa xe ngay | `MaintenanceController.cs`, rentable policy |
| GPS mock | PASS | Admin Tracking Map thiếu action | `TrackingController.cs`; no-data/mock không crash |
| Report | PASS | Cần kiểm tra route/menu và empty data | Admin/Manager/Staff dashboard + Playwright; Local smoke 200 |

## 5. Danh sách lỗi đã sửa
| ID | Mức độ | Mô tả lỗi | Nguyên nhân | File sửa | Kết quả |
|---|---|---|---|---|---|
| QA-001 | High | Duyệt xe chưa tạo trạng thái Approved/Available nhất quán | Approval dựa vào cờ rời rạc | Model, migration, Admin CarApproval | PASS |
| QA-002 | Critical | Xe Pending/Maintenance có thể lọt qua một số đường tạo booking | Không có rentable policy dùng chung | `CarRentabilityPolicy.cs` và các entry point | PASS |
| QA-003 | High | Từ chối xe không lưu/hiển thị lý do rõ ràng | Thiếu approval state/reason | Car model/controller/view | PASS |
| QA-004 | High | Tạo lịch bảo trì chưa chuyển xe sang Maintenance | Controller chỉ tạo log | Admin Maintenance controller | PASS |
| QA-005 | High | QA admin seeder bỏ qua tài khoản đã tồn tại | Seeder chỉ xử lý create | `QaAdminSeeder.cs` | PASS |
| QA-006 | High | Local thiếu demo seed deterministic/idempotent | Seeder demo chưa đầy đủ | `InternalPortalDemoSeeder.cs`, startup seed order | PASS |
| QA-007 | Medium | `/ready` có thể fail giả khi Playwright dùng mock vendor | Readiness yêu cầu config vendor thật | `Program.cs` | PASS |
| QA-008 | Medium | Smoke test InMemory thỉnh thoảng fail do DB dùng chung | Static database gây race | `SmokeTests.cs` | PASS |
| QA-009 | High | `/Admin/Tracking/Map` trả 404 dù đã có view | Controller thiếu action | `Areas/Admin/Controllers/TrackingController.cs` | PASS |
| QA-010 | High | PIN danh sách khách hàng phụ thuộc PIN PII cá nhân của tài khoản Admin | Luồng PIN chung bị đổi sang kiểm tra `User.PiiPinHash` | `Areas/Admin/Controllers/UserController.cs`, view và regression test | PASS |

## 6. Danh sách lỗi còn lại nếu có
| ID | Mức độ | Mô tả | Ảnh hưởng | Đề xuất |
|---|---|---|---|---|
| REM-001 | Medium / feature gap | Public workflow xe có tài xế đang tắt | Không demo được dịch vụ đưa đón | Giữ tắt đến khi có yêu cầu nghiệp vụ và test riêng |
| REM-002 | Medium / feature gap | Partner chưa có workflow edit/resubmit đầy đủ để xe Approved tự quay lại Pending | Chưa demo được vòng sửa xe sau duyệt | Bổ sung owner edit + audit/resubmit ở sprint sau |
| REM-003 | Low / external | 17 E2E test Production/real vendor pending chủ ý | Chưa xác minh PayOS/SMS/eKYC/GPS thật | Chỉ chạy trên sandbox/Production có credential và phê duyệt |

Không còn lỗi Blocker/Critical/High đã biết trong phạm vi workflow demo tự lái.

## 7. File/code đã thay đổi
Các file dưới đây thuộc phạm vi sửa QA này; toàn bộ thay đổi đã có sẵn trong dirty worktree trước baseline được giữ nguyên.

- Approval/model/migration: `Models/BookingStatusConstants.cs`, `Models/Car.cs`, `Data/ApplicationDbContext.cs`, `Migrations/20260611194853_AddCarApprovalWorkflow*`, `Migrations/ApplicationDbContextModelSnapshot.cs`
- Rentable/booking policy: `Services/CarRentabilityPolicy.cs`, `Services/CarService.cs`, `Services/BookingQuoteService.cs`, `Services/ReservationService.cs`
- Public workflow: `Controllers/HomeController.cs`, `Controllers/CarController.cs`, `Controllers/CartController.cs`, `Controllers/BookingController.cs`, `Controllers/LongTermController.cs`
- Owner/admin workflow: `Controllers/OwnerCarsController.cs`, `ViewModels/OwnerPortalViewModels.cs`, `Views/OwnerCars/Index.cshtml`, `Areas/Admin/Controllers/CarApprovalController.cs`, `Areas/Admin/Views/CarApproval/Index.cshtml`, `Areas/Admin/Controllers/CarController.cs`, `Areas/Admin/Controllers/MaintenanceController.cs`, `Areas/Admin/Controllers/TrackingController.cs`
- Seed/readiness: `Data/QaAdminSeeder.cs`, `Data/InternalPortalDemoSeeder.cs`, `Data/BusinessReferenceDataSeeder.cs`, `Data/PlaywrightTestDataSeeder.cs`, `Program.cs`
- Tests: `CarRentalSystem.Tests/IntegrationTests/SmokeTests.cs`, `CarRentalSystem.Tests/IntegrationTests/ReservationConcurrencyTests.cs`, `CarRentalSystem.Tests/UnitTests/Controllers/CarControllerTests.cs`, `CarRentalSystem.Tests/UnitTests/Services/CoreRentalWorkflowRegressionTests.cs`
- PIN danh sách khách hàng: `Areas/Admin/Controllers/UserController.cs`, `Areas/Admin/Views/User/Index.cshtml`, `CarRentalSystem.Tests/UnitTests/Controllers/AdminUserControllerPinTests.cs`
- Local-only ignored configuration: `.env`
- Generated Playwright traceability reports were refreshed by the A-to-Z run.

## 8. Tài khoản demo
| Role | Email | Password | Ghi chú |
|---|---|---|---|
| Admin được cung cấp | `hloi78274@gmail.com` | `Admin@123` | Local-only QA upsert, verified/unlocked |
| Admin demo | `admin@admin.com` | `Admin@123` | Demo seed |
| Manager | `manager@demo.com` | `Manager@123` | Demo seed |
| Staff | `staff@demo.com` | `Staff@123` | Demo seed |
| Customer | `customer@demo.com` | `Customer@123` | Có GPLX verified |
| Customer 2 | `customer2@demo.com` | `Customer@123` | Có GPLX verified |
| Partner | `partner@demo.com` | `Partner@123` | Partner approved |
| Partner 2 | `partner2@demo.com` | `Partner@123` | Partner approved |

## 9. Cách chạy project
```powershell
cd D:\CarRentalSystem\CarRentalSystem\CarRentalSystem\CarRentalSystem
dotnet restore CarRentalSystem.csproj
dotnet build CarRentalSystem.csproj --no-restore
dotnet ef database update --project CarRentalSystem.csproj
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5168'
dotnet run --project CarRentalSystem.csproj --no-build --no-launch-profile
```

Chạy test:
```powershell
dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Run-PlaywrightA2Z.ps1 -VendorMode Mock
```

## 10. Kết luận
Hệ thống **đủ demo/nộp cho phạm vi thuê xe tự lái**: restore/build/run PASS, Admin được cung cấp đăng nhập thành công, RBAC và dữ liệu thuộc quyền được E2E kiểm tra, xe Pending/Rejected/Maintenance không public/không đặt được, mock payment và workflow vận hành chính PASS, dashboard/menu chính không còn lỗi 500/404 đã biết.

Kết luận toàn checklist là **PARTIAL PASS** vì dịch vụ xe có tài xế đang chủ động tắt và các tích hợp vendor thật chưa được phép chạy. Không có thanh toán, SMS, eKYC hoặc GPS thật được gọi trong đợt QA này.
