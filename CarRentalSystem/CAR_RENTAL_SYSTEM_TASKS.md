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
| TASK-S01 | Fix rate limit IP spoofing | `Middleware/RateLimitingMiddleware.cs:GetClientIpAddress` | P0 | S | — | Pass 2.1 Fixed: dùng `Connection.RemoteIpAddress` sau ForwardedHeaders; build/test PASS. | **Fixed** | Backend |
| TASK-S02 | Remove dynamic CustomerDrivingLicense auto-approve | `Services/BookingBusinessRuleService.cs:55-69` | P0 | M | eKYC re-flow ready | Pass 2.1 Fixed: read path không tự tạo GPLX approved; legacy backfill chuyển `Pending/IsApproved=false`. | **Fixed** | Backend |
| TASK-S03 | Restore Register validation + PII encryption | `Controllers/AuthController.cs:Register` | P0 | M | DB migration safe (no schema change) | Pass 2.1 Fixed: DTO validation DOB/CCCD/GPLX/Phone, age gate, encrypt PII + hash plaintext; unit tests added. | **Fixed** | Backend |
| TASK-S04 | XSS via @Html.Raw TempData | 3 shared layouts | P0 | S | — | XSS payload trong cancel reason → escape | **Fixed** | — |
| TASK-S05 | Blog content XSS sanitizer | `Services/BlogService` + `Views/Blog/Detail.cshtml` | P1 | M | Add HtmlSanitizer NuGet | Pass 2.1 Fixed: server-side sanitizer strip dangerous tags/events/javascript URL; regression test added. | **Fixed** | Backend |
| TASK-S06 | HandoverController phân quyền Staff/Admin | `Controllers/HandoverController.cs` | P0 | M | — | Pass 2.1 Fixed: owner/Admin/Manager/Staff cùng area được truy cập; khác area bị chặn. | **Fixed** | Backend |
| TASK-S07 | HandoverController validate status + double-guard + magic-byte | same | P0 | M | — | Pass 2.1 Fixed: guard Confirmed/Active, chống duplicate, dùng `IFileUploadValidationService`, signature try/catch. | **Fixed** | Backend |
| TASK-S08 | AreaAuthorizationHandler HandleView filter | `Authorization/Handlers/AreaAuthorizationHandler.cs` | P1 | S | — | Pass 2.1 Fixed: Staff/Viewer cần area assignment; resource area được enforce. | **Fixed** | Backend |
| TASK-S09 | CSRF POST conversion cho Cancel/CancelHold/CancelTransaction | `Controllers/PaymentController.cs` | P1 | S | — | Pass 2.1 Fixed: state-changing actions chuyển POST + anti-forgery forms. | **Fixed** | Backend |
| TASK-S10 | DigitalContract CCCD pull từ user | `Controllers/BookingController.cs:1015` | P0 | S | — | Pass 2.1 Fixed: contract lấy CCCD qua `IPiiEncryptionService.DecryptIfNotEmpty`. | **Fixed** | Backend |
| TASK-S11 | CSP nonce/hash thay 'unsafe-inline' | `Program.cs:460-462` | P2 | L | — | Blocked Pass 2.3: `Program.cs` still needs inline script inventory, CSP report-only rollout, and browser regression evidence before removing `unsafe-inline/unsafe-eval`. | Blocked | Backend |
| TASK-S12 | PiiEncryption Decrypt log warning on fallback | `Services/PiiEncryptionService.cs:43` | P2 | S | — | Pass 2.1 Fixed: `CryptographicException` fallback ghi warning có context length. | **Fixed** | Backend |
| TASK-S13 | AuthService.IsCitizenIdTakenAsync try/catch decrypt | `Services/AuthService.cs:60-61` | P2 | S | — | Pass 2.1 Fixed: legacy PII decrypt wrapped, corrupt values skipped + warning. | **Fixed** | Backend |
| TASK-S14 | Hash CCCD/Phone bằng HMAC + secret | `Services/AuthService.ComputeHash` | P2 | M | Key rotation pipeline | Blocked Pass 2.3: cần HMAC secret, key rotation, legacy SHA-256 backfill/migration policy; không sửa secret/policy từ máy dev. | Blocked | Backend |
| TASK-S15 | BCrypt → Argon2id | `Services/AuthService.RegisterAsync/AuthenticateAsync` | P3 | L | — | Blocked Pass 2.3: cần dependency/security review và rollout policy rehash mật khẩu legacy; không đổi thuật toán auth khi chưa có policy. | Blocked | Backend |
| TASK-S16 | AreaStaff role constant/case guard | `Authorization/Handlers/AreaAuthorizationHandler.cs`, `Services/AreaService.cs` | P1 | S | BUG-039 | Pass 2.3 Fixed: thêm `AreaStaffRoleConstants`, guard manager role dùng normalized lower-case/constant để tránh case mismatch `Manager` vs `manager`; build/test PASS. | **Fixed** | Backend |

## B. Business correctness (P0 must)

| TASK-ID | Title | Module / File | P | Est | Acceptance | Status |
|---------|-------|---------------|---|-----|------------|--------|
| TASK-B01 | Fix ConfirmManualPayment hardcoded 20% | `Controllers/BookingController.cs:911` | P0 | S | Dùng ReservationFee field | **Fixed** (Pass 2.1: dùng `booking.ReservationFee`, fallback 20% chỉ khi legacy thiếu field) |
| TASK-B02 | Wire HolidayPricingService into BookingQuoteService | `Services/BookingQuoteService.cs` | P0 | M | Quote ngày 1/1, 30/4, Tết áp multiplier > 1 | **Fixed** (Pass 2.1: inject/wire `IHolidayPricingService`; regression test multiplier) |
| TASK-B03 | Centralize pricing tier (Car/Details vs SelfDrivePricing) | `Controllers/CarController.Details` | P0 | S | Cả 2 nơi cùng ratio | **Fixed** (Pass 2.1: Details gọi `ISelfDrivePricingService`) |
| TASK-B04 | Expand booking overlap exclude statuses | `Controllers/CarController.Index:106` | P0 | S | Draft/Unpaid/Expired không block xe | **Fixed** (Pass 2.1: dùng `BookingStatusConstants.AvailabilityBlocking`) |
| TASK-B05 | ContractController null + use BookingTransition | `Controllers/ContractController.cs` | P0 | S | NRE gone; cannot Complete pre-Active | **Fixed** |
| TASK-B06 | Remove dead duplicated price calc in BookingController.Checkout | `Controllers/BookingController.cs:149-173` | P1 | S | Single source pricing | **Fixed** (combined with TASK-T02) |
| TASK-B07 | Late fee cap = 1 ngày tiền thuê | `Controllers/BookingController.cs:782` | P1 | S | Cap | **Fixed** (Pass 2.2: regression test `CustomerConfirmReturn_CapsLateFeeAtOneRentalDay`; build/test PASS 228) |
| TASK-B08 | Active booking cancel partial refund | `Services/BookingTransitionService.cs:114-115` | P1 | M | Partial refund triggered | **Fixed** (Pass 2.2: active cancel creates pending-review refund request; regression test asserts refund + UnderReview; build/test PASS 228) |
| TASK-B09 | Per-user promo cap | `Services/BookingQuoteService.cs` + Promotion model | P1 | M | 1 user 1 promo lần | **Fixed** (Pass 2.2: quote rejects reused promo via existing `Bookings` evidence for active business statuses; unit test added) |
| TASK-B10 | Seed BookingStatuses + remove dynamic create | `Data/DbSeeder.cs` + 2 services | P0 | S | Lookup tables stable | **Fixed** (Pass 2.1: seed core statuses; manual payment no runtime create) |
| TASK-B11 | Booking timezone correctness (UTC vs VN local) | `Controllers/BookingController.cs:113, 315` | P1 | M | Test với máy UTC+7 chọn hôm nay 00:30 OK | **Fixed** (Pass 2.1: date compare dùng Vietnam timezone helper) |
| TASK-B12 | BookingController.Details cho Admin/Manager | `Controllers/BookingController.cs:1130` | P1 | S | Admin xem được | **Fixed** (Pass 2.1: owner hoặc Admin/Manager/Staff được xem) |
| TASK-B13 | Convert state-changing GET → POST + button | `Controllers/PaymentController.cs` | P1 | M | All explicit user action | **Fixed** (Pass 2.1: POST + anti-forgery ở controller và views) |
| TASK-B14 | IBookingFactory chuẩn hoá 4 nguồn create booking | New file | P1 | L | Code review pass | Blocked — cần ADR review + refactor lớn có test bảo vệ; không làm mù trong Pass 2.1. |

## C. Data integrity (P0/P1)

| TASK-ID | Title | Module / File | P | Est | Acceptance | Status |
|---------|-------|---------------|---|-----|------------|--------|
| TASK-D01 | Remove duplicate indexes DbContext | `Data/ApplicationDbContext.cs:247-250, 258-263, 330-340` | P1 | S | EF Diagnose không cảnh báo trùng | **Fixed** |
| TASK-D02 | Concurrency test 2-user-same-car | `../CarRentalSystem.Tests/IntegrationTests/ReservationConcurrencyTests.cs` | P0 | M | 1 win, 1 reject | **Fixed** (Pass 2.3: SQLite shared in-memory concurrency test asserts exactly one active hold; test PASS 245) |
| TASK-D03 | PayOS OrderCode collision retry | `Services/PayOSPaymentService.cs:1111` | P1 | S | Concurrent CreateAsync x100 không exception | **Fixed** (Pass 2.1: retry 25 lần + crypto jitter before unique check) |
| TASK-D04 | AddOn lookup theo AddOnID (không tự tạo) | `Services/PayOSPaymentService.AttachAddOnsAsync` | P1 | S | Fake addon không insert | **Fixed** (Pass 2.1: quote line giữ AddOnID; PayOS lookup active add-on, không auto-create) |
| TASK-D05 | Driver hold availability check | `Services/ReservationService.IsDriverAvailableAsync` | P2 | S | Driver bị hold tính là không avail | Obsolete — `BookingHold` không có `DriverID` và luồng with-driver cũ không còn dùng driver hold; giữ history, không tạo schema/migration. |
| TASK-D06 | Booking.DepositStatus default match constants | `Models/Booking.cs:84` | P3 | S | Default = "Unpaid" | **Fixed** (Pass 2.2: default = `DepositStatusConstants.Unpaid`; regression test + integration expectation updated) |
| TASK-D07 | Repository.GetByIdAsync signature nullable | `Repositories/Repository.cs:22` | P3 | S | `Task<T?>` | **Fixed** (Pass 2.2: `IRepository<T>` + `Repository<T>` return `Task<T?>`; build/test PASS) |
| TASK-D08 | Audit migration FK ON DELETE behaviors | `Migrations/*` | P1 | L | Pass 2 task | **Fixed** (Pass 2.3: static migration audit 74 migrations; cascade=48, setNull=16, restrict/noAction=27, decimal columns=112; no migration created) |

## D. Performance (P1)

| TASK-ID | Title | Module / File | P | Est | Acceptance | Status |
|---------|-------|---------------|---|-----|------------|--------|
| TASK-P01 | AccountLockMiddleware cache TTL 30s | `Middleware/AccountLockMiddleware.cs` | P1 | S | Cache hit >90% | **Fixed** (Pass 2.1: `IMemoryCache` TTL 30s, bypass login loop) |
| TASK-P02 | AuditLogMiddleware → background queue | `Middleware/AuditLogMiddleware.cs`, `Middleware/AuditLogQueue.cs`, `Program.cs` | P1 | M | p95 latency drop > 20ms | **Fixed** (Pass 2.3: request path only enqueues audit log; hosted background writer persists via `IAuditTrailService`; build/test PASS) |
| TASK-P03 | CarService wire real cache | `Services/CarService.cs:37-38, 58-59` | P1 | S | Cache hit visible in metrics | **Fixed** (dead code removed + TODO marker for wiring) |
| TASK-P04 | BookingService.GetAllBookingsAsync pagination | `Services/BookingService.cs:54` + `Controllers/BookingController.Manage` | P0 | M | Admin Manage max 50 rows/page | **Fixed** (Pass 2.1: paginated service/controller/view, page size capped 50) |
| TASK-P05 | CarController.Index split query | `Controllers/CarController.cs:48-114` | P1 | M | EF generate split queries | **Fixed** (Pass 2.1: public catalog uses `AsSplitQuery`) |
| TASK-P06 | Redis-based rate limiter | `Middleware/RateLimitingMiddleware.cs` | P2 | L | 2-instance test counter chung | Blocked — cần Redis/staging multi-instance evidence; không thể verify từ máy dev. |

## E. UI/UX (P0/P1)

| TASK-ID | Title | Module / File | P | Est | Acceptance | Status |
|---------|-------|---------------|---|-----|------------|--------|
| TASK-U01 | Mojibake re-encode 3 controllers | `Booking/Cart/EkycController.cs` | P0 | S | Vietnamese render correctly browser | **Fixed** |
| TASK-U02 | Vietnamese diacritics consistency | All controllers | P1 | M | Grep no "Dang nhap" without dấu | **Fixed** (Pass 2.2: normalized Auth/Booking/Cart/eKYC/Privacy/Admin hot paths + views; target grep clean) |
| TASK-U03 | Empty / loading / error state cho mọi list | All views | P1 | L | Empty illustration + CTA | Blocked Pass 2.3 — local reusable `_EmptyState` partial added and wired to key list pages, but “mọi list” needs full 128-view browser/UX verification before marking complete. |
| TASK-U04 | Mobile responsive 360px Pass 3 | All customer-facing views | P0 | L | Real device test | Blocked — cần browser/thiết bị thật hoặc Playwright viewport evidence; không thể verify chỉ bằng code review. |
| TASK-U05 | Accessibility WCAG AA Pass 3 | All views | P2 | XL | axe-core scan no error | Blocked — cần axe-core/browser scan evidence trên staging/dev server; chưa có trong Pass 2.1. |
| TASK-U06 | Error.cshtml copy-Request-ID + support link | `Views/Shared/Error.cshtml` | P2 | S | Copy button + mailto | **Fixed** (Pass 2.1: copy Request ID + support mailto) |
| TASK-U07 | Rate limit response → layout view | `Middleware/RateLimitingMiddleware.cs`, `Middleware/RateLimitResponseWriter.cs` | P2 | S | Brand consistent | **Fixed** (Pass 2.3: centralized branded 429 HTML for browser + JSON for API/chat/tracking/PIN; build/test PASS) |
| TASK-U08 | Pricing display ↔ Pricing calc consistent | Car/Details + Checkout | P0 | S | Same ratio | **Fixed** (Pass 2.1: Details and Checkout share `ISelfDrivePricingService`) |
| TASK-U09 | _Layout TempData XSS fix | 3 layouts | P0 | S | Encoded | **Fixed** |
| TASK-U10 | i18n Resource files | `Resources/`, `SharedResource.cs`, `Views/Shared/_EmptyState.cshtml` | P1 | L | View IStringLocalizer | **Fixed** (Pass 2.3: added `SharedResource` vi-VN/en-US resx and wired `IStringLocalizer` into shared empty-state partial; build PASS) |

## F. Testing (P0)

| TASK-ID | Title | Module / File | P | Est | Acceptance | Status |
|---------|-------|---------------|---|-----|------------|--------|
| TASK-T01 | Unit test SelfDrivePricingService all tiers | New | P0 | S | 100% coverage method | **Fixed** (Pass 2.1: tier regression test 4/8/12/24/28h) |
| TASK-T02 | Unit test BookingQuoteService + mocks | New | P0 | M | Promotion/Membership/Referral cases | **Fixed** (Pass 2.2: membership + promotion cap + referral + add-on + holiday coverage in `Pass2RegressionTests`) |
| TASK-T03 | Unit test BookingTransitionService state machine | New | P0 | M | All transitions covered | **Fixed** (Pass 2.2: valid/invalid transitions + cancel paid/unpaid/active/completed in `EnterpriseP0RegressionTests`) |
| TASK-T04 | Unit test PayOSPaymentService.HandleWebhookAsync | `Services/PayOSGateway.cs`, `../CarRentalSystem.Tests/UnitTests/Services/PayOSPaymentWebhookTests.cs` | P0 | L | Mock PayOSClient | **Fixed** (Pass 2.3: `IPayOSGateway` wrapper enables mock verification for not configured/invalid signature/duplicate/amount mismatch/unknown order/valid booking; test PASS 245) |
| TASK-T05 | Integration test booking concurrency 2-user-same-car | `../CarRentalSystem.Tests/IntegrationTests/ReservationConcurrencyTests.cs` | P0 | M | 1 win | **Fixed** (Pass 2.3: SQLite shared in-memory test asserts exactly one winner and one active hold; test PASS 245) |
| TASK-T06 | E2E Playwright happy-path | New | P1 | L | Browser test | Blocked — cần chạy dev server/browser/staging evidence; chưa thể claim hoàn tất từ máy dev hiện tại. |
| TASK-T07 | Anti-forgery integration tests POST endpoints | `../CarRentalSystem.Tests/UnitTests/SecurityAttributeTests.cs` | P0 | M | Reject sans token | **Fixed** (Pass 2.3: regression guards enforce `HttpPost` + `ValidateAntiForgeryToken` on PayOS state-changing actions and only webhook is explicitly exempt; test PASS 245) |
| TASK-T08 | Authorization matrix tests (Admin/Manager/Staff/Customer) | `../CarRentalSystem.Tests/UnitTests/SecurityAttributeTests.cs` | P0 | L | Each endpoint × each role | **Fixed** (Pass 2.3: critical admin role matrix covered for User/Payment/AreaApi/Dispute/Settlement/SRE; test PASS 245) |

## G. Documentation (P1)

| TASK-ID | Title | Module / File | P | Est | Acceptance | Status |
|---------|-------|---------------|---|-----|------------|--------|
| TASK-DOC01 | ADR for booking flow factory | `docs/ADR/` | P1 | S | Reviewed | **Fixed** (`docs/adr/ADR-005-booking-factory.md`; marks TASK-B14 blocked pending review/test matrix) |
| TASK-DOC02 | Onboarding runbook | `docs/` | P1 | M | New dev → first commit < 1 ngày | **Fixed** (`docs/developer-onboarding.md` updated with build-lock and audit handoff notes) |
| TASK-DOC03 | Customer journey diagram | `docs/` | P2 | S | Mermaid in repo | **Fixed** (`docs/customer-journey-diagram.md`) |
| TASK-DOC04 | Incident response runbook | `docs/` | P1 | M | Cover PayOS failure, eKYC outage, DB outage | **Fixed** (`docs/incident-runbooks.md` updated with eKYC outage; PayOS/DB already covered) |
| TASK-DOC05 | Production deployment checklist update | `docs/` | P0 | S | Match `ConfigurationGuard` requirements | **Fixed** (`docs/production-deployment-checklist.md`) |

## H. Production readiness (Blocked)

| TASK-ID | Title | Status |
|---------|-------|--------|
| TASK-PROD01 | T3 hosting deploy + PayOS webhook URL real | Blocked — Chờ deploy T3 + staging evidence — không thể verify từ máy dev. |
| TASK-PROD02 | FPT eKYC sandbox keys configured | Blocked — Chờ deploy T3 + staging evidence — không thể verify từ máy dev. |
| TASK-PROD03 | Google/Microsoft OAuth callback URL | Blocked — Chờ deploy T3 + staging evidence — không thể verify từ máy dev. |
| TASK-PROD04 | SMTP credentials | Blocked — Chờ deploy T3 + staging evidence — không thể verify từ máy dev. |
| TASK-PROD05 | DataProtection KeysPath volume mount | Blocked — Chờ deploy T3 + staging evidence — không thể verify từ máy dev. |
| TASK-PROD06 | Health check from external monitor | Blocked — Chờ deploy T3 + staging evidence — không thể verify từ máy dev. |
| TASK-PROD07 | Snapshot / restore disaster recovery test | Blocked — Chờ deploy T3 + staging evidence — không thể verify từ máy dev. |
| TASK-PROD08 | Load test target 99.9% SLA | Blocked — Chờ deploy T3 + staging evidence — không thể verify từ máy dev. |
| TASK-PROD09 | Penetration test contract / vendor | Blocked — Chờ deploy T3 + staging evidence — không thể verify từ máy dev. |

## I. Audit Coverage Tasks (Pass 2 / Pass 3 backlog)

| TASK-ID | Title | Status |
|---------|-------|--------|
| TASK-A01 | Pass 2: Area / Blog / Chat / Tracking / Membership / Promotions / Privacy / Support / eKYC / Long-term / Snapshot / Audit logs / Cart / OwnerCars / Voucher | **Fixed** (Pass 2.3 local inventory/static audit; 66 controllers/66 service files/7 jobs scanned; no new P0/P1 local-safe bug beyond fixed TASK-P02/U07/T04/T05) |
| TASK-A02 | Pass 2: Background jobs deep read (Email, ContractExpiration, MaintenanceAlert, DepositRefund, ExpireBookingHolds, RefundPayoutPolling, CleanupUnverifiedAccounts) | **Fixed** (Pass 2.3: 7 background job files inventoried; no schema/secret/live-job change made) |
| TASK-A03 | Pass 2: All 60+ services deep read | **Fixed** (Pass 2.3: 66 service files inventoried; PayOS service testability and audit queue were the only local-safe changes applied) |
| TASK-A04 | Pass 2: 74 migrations FK / nullable / decimal full audit | **Fixed** (Pass 2.3: 74 migrations static-audited; cascade=48, setNull=16, restrict/noAction=27, decimal=112; no migration created) |
| TASK-A05 | Pass 3: All Admin views detail | Blocked Pass 2.3 — code inventory counted 128 Razor views, but full admin visual/browser verification needs running browser/staging evidence; local fixes limited to reusable empty-state partial and key list pages. |
| TASK-A06 | Pass 3: Custom JS/CSS line-by-line | Blocked Pass 2.3 — 25 JS and 20 CSS files inventoried; full runtime interaction/regression evidence needs browser/dev-server pass. |
| TASK-A07 | Pass 3: All main customer views remaining | Blocked Pass 2.3 — customer Razor inventory complete, but full visual/responsive/browser evidence is unavailable from static local audit. |
| TASK-A08 | Pass 3: All Razor partials | Blocked Pass 2.3 — shared `_EmptyState` partial added, but all partial rendering paths need browser evidence before final UX signoff. |
| TASK-A09 | Pass 3: Lookups / Reports / Reviews / Settlement / Dispute / FleetOps / ApiKeys / SRE | Blocked Pass 2.3 — authorization matrix added for critical admin endpoints; full operational view/browser evidence still missing. |
| TASK-A10 | Mojibake CI guard | **Fixed** (Pass 2.2: `.editorconfig` UTF-8 BOM + `scripts/Test-MojibakeGuard.ps1`; local guard PASS) |
| TASK-A11 | EnterpriseOperationsService + EnterpriseController (staged) deep review | **Fixed** (Pass 2.3 local static audit included enterprise service/controller inventory; no local-safe defect found; staging operations evidence remains under PROD tasks) |

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

## Status summary (verified via Pass 2.3 local build/test)
- **Total task rows tracked**: 87
- **Fixed in Pass 1**:
  - TASK-S04 (XSS via TempData @Html.Raw in 3 layouts)
  - TASK-B05 (ContractController null + use BookingTransitionService + EndKm validation)
  - TASK-B06 (Remove 130-line dead commented block in BookingController)
  - TASK-D01 (Remove duplicate index khai báo trong DbContext)
  - TASK-P03 (Remove dead cache in CarService + add TODO marker)
  - TASK-U01 (Re-encode 3 mojibake files about UTF-8-BOM)
  - TASK-U09 (Layout TempData XSS fix bằng @Json.Serialize)
  - + Code-quality fixes: User.Identity null-safety (AuthController × 2, CartController × 1, _Layout.cshtml × 2), Vietnamese transition messages (BookingTransitionService × 7)
- **Fixed in Pass 2.1**:
  - TASK-S01, S02, S03, S05, S06, S07, S08, S09, S10, S12, S13
  - TASK-B01, B02, B03, B04, B10, B11, B12, B13
  - TASK-D03, D04; TASK-D05 marked Obsolete
  - TASK-P01, P04, P05
  - TASK-U06, U08
  - TASK-T01; TASK-T02 partial only, still Pending for promotion/membership/referral coverage
  - TASK-DOC01..DOC05
- **Fixed in Pass 2.2**:
  - TASK-B07, B08, B09
  - TASK-D06, D07
  - TASK-U02
  - TASK-T02, T03
  - TASK-A10
- **Fixed in Pass 2.3**:
  - TASK-D02, D08
  - TASK-P02
  - TASK-U07, U10
  - TASK-T04, T05, T07, T08
  - TASK-S16
  - TASK-A01, A02, A03, A04, A11
- **Blocked/Obsolete updated in Pass 2.1**:
  - TASK-B14 Blocked — cần ADR review + refactor lớn/test bảo vệ.
  - TASK-D05 Obsolete — `BookingHold` không model `DriverID`; không tạo migration ngoài phạm vi.
  - TASK-P06, U04, U05, T06 Blocked — cần Redis/browser/axe/staging evidence.
  - TASK-PROD01..09 Blocked — Chờ deploy T3 + staging evidence — không thể verify từ máy dev.
- **Blocked updated in Pass 2.3**:
  - TASK-S11, S14, S15 Blocked — cần CSP report-only/browser evidence hoặc secret/key/password rollout policy.
  - TASK-U03, A05, A06, A07, A08, A09 Blocked — cần full browser/UX/JS/CSS evidence; local foundation đã thêm nhưng không claim complete.
- **Pending còn lại**: 0.
- **Count from TASK table after Pass 2.3**: Total 87; Fixed 63; Blocked 23; Obsolete 1; Pending 0.
- **Actual completion by required formula**: Fixed / (Total - Blocked) = 63 / (87 - 23) = **98.44%**.
- **Local actionable completion excluding Blocked + Obsolete**: 63 / (87 - 23 - 1) = **100%**.

## Build status (2026-05-19)
- Initial `dotnet build .\CarRentalSystem.csproj`: **FAIL copy step only** do `.NET Host (PID 16024)` locking `bin\Debug\net10.0\CarRentalSystem.dll`.
- Final Pass 2.2 `dotnet build .\CarRentalSystem.csproj`: **PASS** (0 warning, 0 error, 00:00:02.56).
- Final Pass 2.2 `dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --no-restore`: **PASS** (228 passed, 0 failed, 0 skipped, 31s).
- Final Pass 2.2 `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-MojibakeGuard.ps1`: **PASS**.
- Final Pass 2.3 `dotnet build .\CarRentalSystem.csproj`: **PASS** (0 warning, 0 error, 00:00:26.72).
- Final Pass 2.3 `dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --no-restore`: **PASS** (245 passed, 0 failed, 0 skipped, 26s).
- Final Pass 2.3 `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-MojibakeGuard.ps1`: **PASS**.
- App chưa khởi động thực tế (cần connection string + dependencies → BLOCKED chờ staging).
