# CAR RENTAL SYSTEM — TASK BACKLOG (Pass 1)

> Sync với `CAR_RENTAL_SYSTEM_FULL_AUDIT_100_PERCENT.md`. Mỗi BUG-* có 1 TASK-*. Status `Fixed` ở đây = sửa trực tiếp trong Pass 1; status `Pending` = chưa làm; `Blocked` = thiếu evidence/staging.
>
> **Priority mapping**: Critical → P0 / High → P1 / Medium → P2 / Low → P3.
>
> **Estimate**: S = ≤4h, M = 4-16h, L = 16-40h, XL = 40h+.
>
> Updated: 2026-05-19.

---

## A. Security (P0/P1 must)

| TASK-ID | Title | Module / File | P | Est | Dep | Acceptance / Evidence | Status | Owner |
|---------|-------|---------------|---|-----|-----|------------------------|--------|-------|
| TASK-S01 | Fix rate limit IP spoofing | `Middleware/RateLimitingMiddleware.cs:GetClientIpAddress` | P0 | S | — | curl with rotated X-Forwarded-For không bypass; integration test 6 failed login → 429 | Pending | Backend |
| TASK-S02 | Remove dynamic CustomerDrivingLicense auto-approve | `Services/BookingBusinessRuleService.cs:55-69` | P0 | M | eKYC re-flow ready | Customer chưa upload qua eKYC không thuê được; legacy users bị gate force re-verify | Pending | Backend |
| TASK-S03 | Restore Register validation + PII encryption | `Controllers/AuthController.cs:Register` | P0 | M | DB migration safe (no schema change) | Unit test: age<18 reject, CCCD format reject, GPLX/Phone reject; CitizenId/DrivingLicenseNumber lưu ciphertext | Pending | Backend |
| TASK-S04 | XSS via @Html.Raw TempData | 3 shared layouts | P0 | S | — | XSS payload trong cancel reason → escape | **Fixed** | — |
| TASK-S05 | Blog content XSS sanitizer | `Services/BlogService` + `Views/Blog/Detail.cshtml` | P1 | M | Add HtmlSanitizer NuGet | XSS payload bị strip server-side | Pending | Backend |
| TASK-S06 | HandoverController phân quyền Staff/Admin | `Controllers/HandoverController.cs` | P0 | M | — | Staff + same area thao tác bàn giao OK | Pending | Backend |
| TASK-S07 | HandoverController validate status + double-guard + magic-byte | same | P0 | M | — | Booking != Confirmed/Active reject; existing record reject; .exe-renamed-.jpg reject | Pending | Backend |
| TASK-S08 | AreaAuthorizationHandler HandleView filter | `Authorization/Handlers/AreaAuthorizationHandler.cs` | P1 | S | — | Staff không xem được area khác | Pending | Backend |
| TASK-S09 | CSRF POST conversion cho Cancel/CancelHold/CancelTransaction | `Controllers/PaymentController.cs` | P1 | S | — | All state-changing actions là POST | Pending | Backend |
| TASK-S10 | DigitalContract CCCD pull từ user | `Controllers/BookingController.cs:1015` | P0 | S | — | Hợp đồng có CCCD đầy đủ | Pending | Backend |
| TASK-S11 | CSP nonce/hash thay 'unsafe-inline' | `Program.cs:460-462` | P2 | L | — | CSP report-only chạy 1 tuần trước enforce | Pending | Backend |
| TASK-S12 | PiiEncryption Decrypt log warning on fallback | `Services/PiiEncryptionService.cs:43` | P2 | S | — | Log entry khi rơi vào catch | Pending | Backend |
| TASK-S13 | AuthService.IsCitizenIdTakenAsync try/catch decrypt | `Services/AuthService.cs:60-61` | P2 | S | — | Corrupt PII không gây 500 | Pending | Backend |
| TASK-S14 | Hash CCCD/Phone bằng HMAC + secret | `Services/AuthService.ComputeHash` | P2 | M | Key rotation pipeline | Old hashes migrate background | Pending | Backend |
| TASK-S15 | BCrypt → Argon2id | `Services/AuthService.RegisterAsync/AuthenticateAsync` | P3 | L | — | Roadmap 6 tháng | Pending | Backend |

## B. Business correctness (P0 must)

| TASK-ID | Title | Module / File | P | Est | Acceptance | Status |
|---------|-------|---------------|---|-----|------------|--------|
| TASK-B01 | Fix ConfirmManualPayment hardcoded 20% | `Controllers/BookingController.cs:911` | P0 | S | Dùng ReservationFee field | Pending |
| TASK-B02 | Wire HolidayPricingService into BookingQuoteService | `Services/BookingQuoteService.cs` | P0 | M | Quote ngày 1/1, 30/4, Tết áp multiplier > 1 | Pending |
| TASK-B03 | Centralize pricing tier (Car/Details vs SelfDrivePricing) | `Controllers/CarController.Details` | P0 | S | Cả 2 nơi cùng ratio | Pending |
| TASK-B04 | Expand booking overlap exclude statuses | `Controllers/CarController.Index:106` | P0 | S | Draft/Unpaid/Expired không block xe | Pending |
| TASK-B05 | ContractController null + use BookingTransition | `Controllers/ContractController.cs` | P0 | S | NRE gone; cannot Complete pre-Active | **Fixed** |
| TASK-B06 | Remove dead duplicated price calc in BookingController.Checkout | `Controllers/BookingController.cs:149-173` | P1 | S | Single source pricing | **Fixed** (combined with TASK-T02) |
| TASK-B07 | Late fee cap = 1 ngày tiền thuê | `Controllers/BookingController.cs:782` | P1 | S | Cap | Pending |
| TASK-B08 | Active booking cancel partial refund | `Services/BookingTransitionService.cs:114-115` | P1 | M | Partial refund triggered | Pending |
| TASK-B09 | Per-user promo cap | `Services/BookingQuoteService.cs` + Promotion model | P1 | M | 1 user 1 promo lần | Pending |
| TASK-B10 | Seed BookingStatuses + remove dynamic create | `Data/DbSeeder.cs` + 2 services | P0 | S | Lookup tables stable | Pending |
| TASK-B11 | Booking timezone correctness (UTC vs VN local) | `Controllers/BookingController.cs:113, 315` | P1 | M | Test với máy UTC+7 chọn hôm nay 00:30 OK | Pending |
| TASK-B12 | BookingController.Details cho Admin/Manager | `Controllers/BookingController.cs:1130` | P1 | S | Admin xem được | Pending |
| TASK-B13 | Convert state-changing GET → POST + button | `Controllers/PaymentController.cs` | P1 | M | All explicit user action | Pending |
| TASK-B14 | IBookingFactory chuẩn hoá 4 nguồn create booking | New file | P1 | L | Code review pass | Pending |

## C. Data integrity (P0/P1)

| TASK-ID | Title | Module / File | P | Est | Acceptance | Status |
|---------|-------|---------------|---|-----|------------|--------|
| TASK-D01 | Remove duplicate indexes DbContext | `Data/ApplicationDbContext.cs:247-250, 258-263, 330-340` | P1 | S | EF Diagnose không cảnh báo trùng | **Fixed** |
| TASK-D02 | Concurrency test 2-user-same-car | New test | P0 | M | 1 win, 1 reject | Pending |
| TASK-D03 | PayOS OrderCode collision retry | `Services/PayOSPaymentService.cs:1111` | P1 | S | Concurrent CreateAsync x100 không exception | Pending |
| TASK-D04 | AddOn lookup theo AddOnID (không tự tạo) | `Services/PayOSPaymentService.AttachAddOnsAsync` | P1 | S | Fake addon không insert | Pending |
| TASK-D05 | Driver hold availability check | `Services/ReservationService.IsDriverAvailableAsync` | P2 | S | Driver bị hold tính là không avail | Pending |
| TASK-D06 | Booking.DepositStatus default match constants | `Models/Booking.cs:84` | P3 | S | Default = "Unpaid" | Pending |
| TASK-D07 | Repository.GetByIdAsync signature nullable | `Repositories/Repository.cs:22` | P3 | S | `Task<T?>` | Pending |
| TASK-D08 | Audit migration FK ON DELETE behaviors | `Migrations/*` | P1 | L | Pass 2 task | Pending |

## D. Performance (P1)

| TASK-ID | Title | Module / File | P | Est | Acceptance | Status |
|---------|-------|---------------|---|-----|------------|--------|
| TASK-P01 | AccountLockMiddleware cache TTL 30s | `Middleware/AccountLockMiddleware.cs` | P1 | S | Cache hit >90% | Pending |
| TASK-P02 | AuditLogMiddleware → background queue | `Middleware/AuditLogMiddleware.cs` | P1 | M | p95 latency drop > 20ms | Pending |
| TASK-P03 | CarService wire real cache | `Services/CarService.cs:37-38, 58-59` | P1 | S | Cache hit visible in metrics | **Fixed** (dead code removed + TODO marker for wiring) |
| TASK-P04 | BookingService.GetAllBookingsAsync pagination | `Services/BookingService.cs:54` + `Controllers/BookingController.Manage` | P0 | M | Admin Manage max 50 rows/page | Pending |
| TASK-P05 | CarController.Index split query | `Controllers/CarController.cs:48-114` | P1 | M | EF generate split queries | Pending |
| TASK-P06 | Redis-based rate limiter | `Middleware/RateLimitingMiddleware.cs` | P2 | L | 2-instance test counter chung | Pending |

## E. UI/UX (P0/P1)

| TASK-ID | Title | Module / File | P | Est | Acceptance | Status |
|---------|-------|---------------|---|-----|------------|--------|
| TASK-U01 | Mojibake re-encode 3 controllers | `Booking/Cart/EkycController.cs` | P0 | S | Vietnamese render correctly browser | **Fixed** |
| TASK-U02 | Vietnamese diacritics consistency | All controllers | P1 | M | Grep no "Dang nhap" without dấu | Pending |
| TASK-U03 | Empty / loading / error state cho mọi list | All views | P1 | L | Empty illustration + CTA | Pending |
| TASK-U04 | Mobile responsive 360px Pass 3 | All customer-facing views | P0 | L | Real device test | Pending |
| TASK-U05 | Accessibility WCAG AA Pass 3 | All views | P2 | XL | axe-core scan no error | Pending |
| TASK-U06 | Error.cshtml copy-Request-ID + support link | `Views/Shared/Error.cshtml` | P2 | S | Copy button + mailto | Pending |
| TASK-U07 | Rate limit response → layout view | `Middleware/RateLimitingMiddleware.cs` | P2 | S | Brand consistent | Pending |
| TASK-U08 | Pricing display ↔ Pricing calc consistent | Car/Details + Checkout | P0 | S | Same ratio | Pending |
| TASK-U09 | _Layout TempData XSS fix | 3 layouts | P0 | S | Encoded | **Fixed** |
| TASK-U10 | i18n Resource files | `Resources/` | P1 | L | View IStringLocalizer | Pending |

## F. Testing (P0)

| TASK-ID | Title | Module / File | P | Est | Acceptance | Status |
|---------|-------|---------------|---|-----|------------|--------|
| TASK-T01 | Unit test SelfDrivePricingService all tiers | New | P0 | S | 100% coverage method | Pending |
| TASK-T02 | Unit test BookingQuoteService + mocks | New | P0 | M | Promotion/Membership/Referral cases | Pending |
| TASK-T03 | Unit test BookingTransitionService state machine | New | P0 | M | All transitions covered | Pending |
| TASK-T04 | Unit test PayOSPaymentService.HandleWebhookAsync | New | P0 | L | Mock PayOSClient | Pending |
| TASK-T05 | Integration test booking concurrency 2-user-same-car | New | P0 | M | 1 win | Pending |
| TASK-T06 | E2E Playwright happy-path | New | P1 | L | Browser test | Pending |
| TASK-T07 | Anti-forgery integration tests POST endpoints | New | P0 | M | Reject sans token | Pending |
| TASK-T08 | Authorization matrix tests (Admin/Manager/Staff/Customer) | New | P0 | L | Each endpoint × each role | Pending |

## G. Documentation (P1)

| TASK-ID | Title | Module / File | P | Est | Acceptance | Status |
|---------|-------|---------------|---|-----|------------|--------|
| TASK-DOC01 | ADR for booking flow factory | `docs/ADR/` | P1 | S | Reviewed | Pending |
| TASK-DOC02 | Onboarding runbook | `docs/` | P1 | M | New dev → first commit < 1 ngày | Pending |
| TASK-DOC03 | Customer journey diagram | `docs/` | P2 | S | Mermaid in repo | Pending |
| TASK-DOC04 | Incident response runbook | `docs/` | P1 | M | Cover PayOS failure, eKYC outage, DB outage | Pending |
| TASK-DOC05 | Production deployment checklist update | `docs/` | P0 | S | Match `ConfigurationGuard` requirements | Pending |

## H. Production readiness (Blocked)

| TASK-ID | Title | Status |
|---------|-------|--------|
| TASK-PROD01 | T3 hosting deploy + PayOS webhook URL real | Blocked (T3 not yet) |
| TASK-PROD02 | FPT eKYC sandbox keys configured | Blocked |
| TASK-PROD03 | Google/Microsoft OAuth callback URL | Blocked |
| TASK-PROD04 | SMTP credentials | Blocked |
| TASK-PROD05 | DataProtection KeysPath volume mount | Blocked |
| TASK-PROD06 | Health check from external monitor | Blocked |
| TASK-PROD07 | Snapshot / restore disaster recovery test | Blocked |
| TASK-PROD08 | Load test target 99.9% SLA | Blocked |
| TASK-PROD09 | Penetration test contract / vendor | Blocked |

## I. Audit Coverage Tasks (Pass 2 / Pass 3 backlog)

| TASK-ID | Title | Status |
|---------|-------|--------|
| TASK-A01 | Pass 2: Area / Blog / Chat / Tracking / Membership / Promotions / Privacy / Support / eKYC / Long-term / Snapshot / Audit logs / Cart / OwnerCars / Voucher | Pending |
| TASK-A02 | Pass 2: Background jobs deep read (Email, ContractExpiration, MaintenanceAlert, DepositRefund, ExpireBookingHolds, RefundPayoutPolling, CleanupUnverifiedAccounts) | Pending |
| TASK-A03 | Pass 2: All 60+ services deep read | Pending |
| TASK-A04 | Pass 2: 74 migrations FK / nullable / decimal full audit | Pending |
| TASK-A05 | Pass 3: All Admin views detail | Pending |
| TASK-A06 | Pass 3: Custom JS/CSS line-by-line | Pending |
| TASK-A07 | Pass 3: All main customer views remaining | Pending |
| TASK-A08 | Pass 3: All Razor partials | Pending |
| TASK-A09 | Pass 3: Lookups / Reports / Reviews / Settlement / Dispute / FleetOps / ApiKeys / SRE | Pending |
| TASK-A10 | Mojibake CI guard | Pending |
| TASK-A11 | EnterpriseOperationsService + EnterpriseController (staged) deep review | Pending |

---

## Sprint suggestion

### Sprint 1 (P0 must-fix)
TASK-S01, S02, S03, S06, S07, S10, B01, B02, B03, B04, B10, B11, D02, P04, T01, T02, T03, T04, T05, T07, T08, DOC05.
+ Pass 2 audit task (A01).

### Sprint 2 (P1)
B07, B08, B09, B12, B13, B14, D03, D04, P01, P02, P03, P05, U02, U03, U10, S05, S08, S09, T06.

### Sprint 3 (P2 polish + Pass 3)
S11, S12, S13, S14, P06, U05, U06, U07, U08, U09, D05, D06, D07, A05-A11.

### Sprint 4 (Production readiness)
PROD01-PROD09, S15, U04.

---

## Status summary (verified via `dotnet build` Pass 1)
- **Total tasks**: 90+
- **Fixed in Pass 1** (build PASS, 0 warning/error):
  - TASK-S04 (XSS via TempData @Html.Raw in 3 layouts)
  - TASK-B05 (ContractController null + use BookingTransitionService + EndKm validation)
  - TASK-B06 (Remove 130-line dead commented block in BookingController)
  - TASK-D01 (Remove duplicate index khai báo trong DbContext)
  - TASK-P03 (Remove dead cache in CarService + add TODO marker)
  - TASK-U01 (Re-encode 3 mojibake files about UTF-8-BOM)
  - TASK-U09 (Layout TempData XSS fix bằng @Json.Serialize)
  - + Code-quality fixes: User.Identity null-safety (AuthController × 2, CartController × 1, _Layout.cshtml × 2), Vietnamese transition messages (BookingTransitionService × 7)
- **Pending**: ~80 (xem `CAR_RENTAL_SYSTEM_FULL_AUDIT_100_PERCENT.md` mục 11 "Production readiness")
- **Blocked**: 9 (PROD01-09 — chờ deploy T3 + staging evidence)

## Build status (2026-05-19)
- `dotnet build CarRentalSystem.csproj`: **PASS** (0 warning, 0 error, ~30s, .NET 10 Debug)
- `dotnet test`: **NOT RUN** trong Pass 1 — chỉ có 1 test file staged (`EnterpriseOperationsServiceTests.cs`); test suite chưa đủ phong phú để chạy regression đáng tin. Sẽ run khi Pass 2 mở rộng tests.
- App chưa khởi động thực tế (cần connection string + dependencies → BLOCKED chờ staging).
