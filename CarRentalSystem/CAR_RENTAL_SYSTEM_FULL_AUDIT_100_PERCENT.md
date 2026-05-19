# CAR RENTAL SYSTEM — FULL AUDIT (Pass 1 of N)

> Phạm vi audit này là **Pass 1** — tập trung core business (Booking / Handover / Payment / Auth / Contract / Car), cross-cutting (Program / DbContext / Middleware / Authorization), config security surface, và spot-check qua Grep.
> Còn lại sẽ được audit tiếp ở **Pass 2** (Area / Blog / Chat / Tracking / Membership / Promotions / Privacy / Support / eKYC / Long-term / Snapshot / Audit logs) và **Pass 3** (Admin views detail, Custom JS/CSS line-by-line, Razor partials, Lookups / Reports / Reviews / Settlement / Dispute / FleetOps / ApiKeys / SRE).
>
> KHÔNG được dùng MD này để claim hệ thống đã sẵn sàng production cho đến khi cả 3 pass hoàn tất + chạy thật trên staging.

**Audit thực hiện**: 2026-05-19 — bởi vai trò Senior Architect + QA Lead + Security Reviewer + BA + UI/UX Reviewer.
**Branch**: `main`, last commit `90c4388 "Chat box chưa xong"`, staged files: Areas/Admin/Controllers/EnterpriseController.cs (NEW), Services/EnterpriseOperationsService.cs (NEW), CarRentalSystem.Tests/UnitTests/Services/EnterpriseOperationsServiceTests.cs (NEW), Program.cs (M), Views/Shared/_AdminLayout.cshtml (M).
**Codebase**: ASP.NET Core MVC .NET 10, 67 controllers, 65 services, 79 models, 74 migrations, 127 views, Hangfire, PayOS, FPT eKYC, Groq AI, EF Core, SQL Server, BCrypt, DataProtection, Hangfire dashboard, OpenTelemetry (optional).

---

## 1. Tổng quan hệ thống

### Mạnh
- Có hạ tầng cross-cutting chuẩn enterprise: `ConfigurationGuard` validate Production secrets/SLO, `PiiEncryptionService` (DPAPI), `FileUploadValidationService` (magic-byte check), `WebhookSigningService`, `ApiKeyService`, `AuditTrailService`, `EvidenceHashService`, `PaymentLedgerService` (idempotent), `BookingTransitionService` (state machine + audit), `Hangfire` recurring jobs (contract expiration, deposit refund, hold expire, unverified cleanup), `Rate limiting` (login + API + chat + tracking + PIN), CSP / HSTS / X-Frame-Options / Anti-forgery global, BCrypt password hashing, idempotent PayOS webhook với amount mismatch detection và hold-expired handling, comprehensive DB indexes (Booking by CarID/StartDate/EndDate/Status, Payment, ApiKey, AuditLog…), soft-delete global query filter, multi-environment support (Dev / Staging / Production / Testing), Privileged MFA via email OTP cho admin/manager/staff, Google/Microsoft SSO restricted to verified privileged accounts, eKYC tích hợp FPT.AI, holiday pricing infrastructure (registered nhưng chưa wired vào quote — xem mục 6), reCAPTCHA infrastructure (toggle theo env), Health checks (`/health`, `/live`, `/ready`).
- 74 migrations cho thấy schema đã evolve qua nhiều phase, có audit/invoice indexes, RowVersion concurrency token (BookingHold, PaymentLedgerEntry, SettlementReview, RefundRequest, RefundPayout).
- Hợp đồng vật lý (PhysicalContract) đã thay thế DigitalContract cho luồng admin (BookingController.Contract → redirect).

### Yếu / Rủi ro cao
- **Mojibake / double-encoded UTF-8** trong 3 file controller (`BookingController.cs`, `CartController.cs`, `EkycController.cs`) — các thông báo Vietnamese hiển thị cho người dùng sẽ ra **gibberish** ("Báº¡n pháº£i..." thay vì "Bạn phải..."). Ảnh hưởng UX trầm trọng trên trang đặt xe + giỏ hàng + eKYC.
- **Dead commented code khối lớn** trong `BookingController.Book` (130 dòng) và `AuthController.Register` (100 dòng). Active `Register` thiếu **age check, CCCD format check, GPLX format check, phone format check, CCCD/GPLX encryption** đang có trong code cũ → **luồng đăng ký hiện tại không validate các trường nhạy cảm** + **không encrypt CCCD/GPLX trước khi lưu**. Hai khối comment lớn này cần dọn dẹp.
- **Rate limit IP spoofing** — `RateLimitingMiddleware.GetClientIpAddress` đọc thẳng `X-Forwarded-For` không qua trust validation → attacker có thể spoof IP bypass rate limit login → brute force protection broken trong production behind proxy.
- **AccountLockMiddleware** query DB mỗi request không có cache → high traffic = DB bottleneck.
- **`User.Identity.IsAuthenticated`** dùng không null-check ở 5 files → NullReferenceException khi request lạ.
- **`ContractController.Create` POST** không null-check booking, bypass BookingTransitionService → Admin có thể "Completed" booking chưa pickup, không validate EndKm >= StartKm.
- **`HandoverController`** filter `b.UserID == userId` ở mọi action → Staff/Admin không thao tác bàn giao được; KHÔNG validate trạng thái booking (cho phép double check-in / check-out / bàn giao xe đã Completed), KHÔNG dùng `FileUploadValidationService` (đã có trong DI) → magic-byte check bị bỏ qua cho ảnh handover.
- **`CarController.Index`** include eager 7 navigation properties + filter overlap trên Bookings sub-query → cartesian + N+1 trên trang public list (high traffic).
- **`CarService.GetAllCarsAsync` / `GetAvailableCarsAsync`** có code "cache" nhưng `cached = null` rồi `if (cached != null)` → cache **không bao giờ hit** trong khi comment claim TTL 5 phút. Misleading code.
- **`BookingTransitionService.GetOrCreateStatusAsync`** + `ConfirmManualPaymentTransaction.PendingStatus`-on-the-fly tạo lookup rows runtime → race condition + dirty schema; lookup tables should be seeded once.
- **`PayOSPaymentService.GenerateUniqueOrderCodeAsync`** không atomic (check-then-insert), có thể collision dưới concurrent load. Unique index trên OrderCode đã có nên sẽ throw → swallowed in error path nhưng không retry rõ ràng.
- **`BookingController.Checkout` GET**: duplicate price calc (lines 149-173) sau đó overwrite bằng `_quoteService.BuildQuoteAsync` (line 235) — dead local calc + xe đang còn tính được giá khác giữa client display và backend.
- **Webhook auth** OK (signature qua `_payOS.Webhooks.VerifyAsync`) nhưng `Hold/Transaction/Cancel/CancelHold/CreatePayment` đều **HttpGet but state-changing** → CSRF safer nhưng GET nên idempotent, bị crawler trigger có thể release/cancel.
- **Holiday pricing service** đăng ký DI (Singleton) nhưng **không gọi** từ `BookingQuoteService` → giá ngày lễ/Tết không áp dụng → mất doanh thu hoặc unfair pricing.

### Module rủi ro cao (ưu tiên fix trước)
1. **Auth / Register** — thiếu validation + encryption (Critical).
2. **Payment / PayOS** — order code race, dead unused cache, manual payment confirm logic.
3. **Booking / Handover** — dead code blocks, hard-coded business numbers, missing transition validation.
4. **Rate limiting** — IP spoofing bypass (Critical security).
5. **Mojibake encoding** — UX broken trên các luồng chính (Critical UX).
6. **Background jobs concurrency** — phải verify thêm ở Pass 2.

---

## 2. Bảng rà soát từng file (Pass 1)

| STT | File | Vai trò | Tình trạng | Vấn đề | Mức | Đề xuất |
|----:|------|---------|------------|--------|-----|---------|
| 1 | [Program.cs](Program.cs) | DI / pipeline / CSP / Hangfire / OTEL | Cần refactor | Block comment Vietnamese diacritics inconsistent; `ConfigurationGuard` chạy trước `AddEnvironmentVariables` thứ tự OK; CSP có `'unsafe-inline'` cả style+script (giảm hiệu lực CSP); `app.Use(...) Partner/Driver redirect` (line 536) dùng `Response.Redirect` trên path startswith `/Admin/Driver` cũng redirect → có thể bỏ vì các action đó không còn tồn tại; seed CartCheckoutSnapshot trong DI block phase nên di chuyển sang DbSeeder. | High | Tách phần seed ra `DbSeeder`. Loại bỏ block Partner/Driver redirect nếu route đã xóa. Thắt chặt CSP bằng nonce/hash thay 'unsafe-inline' khi có thời gian. |
| 2 | [Data/ApplicationDbContext.cs](Data/ApplicationDbContext.cs) | EF Core context | OK với cảnh báo | Duplicate `PaymentLedgerEntry.IdempotencyKey` unique index + duplicate `PaymentWebhookEvent.{Provider,EventKey}` unique index (lines 247-250 + 330-336, 258-263 + 338-340) — EF sẽ tạo 2 index trùng → bloat. `User` không có soft-delete; xóa cứng có thể FK violation. `HasQueryFilter` trên child entity `!ci.Cart.UserID == ...` (line 1001) tham chiếu Car.IsDeleted có thể gây EF warning. | Medium | Bỏ duplicate index khai báo. Cân nhắc soft-delete User. |
| 3 | [Repositories/Repository.cs](Repositories/Repository.cs) | Generic CRUD | OK với cảnh báo | `GetByIdAsync` trả `T` nhưng `FindAsync` trả `T?` → nullable warning. Không có `AsNoTracking`. Không có cancellation token. | Low | Đổi signature `Task<T?>`. Thêm CT optional. |
| 4 | [Helpers/PaginatedList.cs](Helpers/PaginatedList.cs) | Pagination helper | OK | — | — | — |
| 5 | [Middleware/GlobalExceptionMiddleware.cs](Middleware/GlobalExceptionMiddleware.cs) | Catch-all exception | Cần refactor | Trùng chức năng với `app.UseExceptionHandler("/Home/Error")` ở Program.cs:446 — chỉ chạy trong Production. Middleware này bật cho mọi env. Redirect với StatusCode=500 set rồi 302 → browser GET /Home/Error trả 200 → mất status code → monitoring khó. | High | Quyết định 1 trong 2 cơ chế. Không vừa Redirect vừa set StatusCode=500. |
| 6 | [Middleware/RateLimitingMiddleware.cs](Middleware/RateLimitingMiddleware.cs) | Rate limit per-IP | Lỗi nghiêm trọng | **IP spoofing**: tự đọc `X-Forwarded-For` không qua trust validation (Program.cs UseForwardedHeaders đã có thì `context.Connection.RemoteIpAddress` đã đúng) → attacker spoof bypass login rate limit; In-memory state không scale multi-instance; "isSuccess" heuristic dựa vào Location header `/home` hoặc `/booking` brittle. | Critical | Dùng `context.Connection.RemoteIpAddress` (sau Forwarded headers middleware). Cân nhắc dùng Redis-based rate limiting để multi-instance. Đổi heuristic success login bằng explicit signal từ AuthController. |
| 7 | [Middleware/AccountLockMiddleware.cs](Middleware/AccountLockMiddleware.cs) | Force logout locked/blacklisted | High | DB hit mỗi request, không cache. Redirect query string `returnUrl` có thể chứa `/Auth/Login` → infinite loop nếu cookie chưa expire trong cùng request. | High | Cache `IsLocked/IsBlacklisted` theo userId TTL 30s. Bypass middleware khi path đã là `/Auth/Login`. |
| 8 | [Middleware/AuditLogMiddleware.cs](Middleware/AuditLogMiddleware.cs) | HTTP audit log | Cần refactor | Mở `SqlConnection` raw mỗi request → bypass DI, không pool? (SqlClient có pool nội bộ nhưng vẫn lock hơn IDbContextFactory). `_next` không trong try/finally → exception ở downstream sẽ KHÔNG ghi audit. Log debug có thể spam. Friendly module map còn thiếu nhiều controller (membership, promotion, settlement...). | High | Wrap try/finally. Dùng `IDbContextFactory<ApplicationDbContext>` hoặc đăng audit qua Hangfire để không block request. Mở rộng `ToFriendlyModule`. |
| 9 | [Middleware/AuditLogServiceAccessor.cs](Middleware/AuditLogServiceAccessor.cs) | Static service locator | Anti-pattern | Static singleton DI scope factory — khó test, không an toàn nếu app restart và reference chưa set. | Low | Inject `IServiceScopeFactory` qua DI thay vì static. |
| 10 | [Authorization/Handlers/AreaAuthorizationHandler.cs](Authorization/Handlers/AreaAuthorizationHandler.cs) | Area-based RBAC | High | `HandleView` cho phép mọi role "Manager/Staff/Viewer" xem TẤT CẢ areas không filter — comment claim "Staff/Viewer (filtered)" nhưng không enforce. New DI scope mỗi requirement → inefficient. `RoleInArea == "manager"` magic string. | High | Thêm filter list theo area membership; dùng constant cho RoleInArea. |
| 11 | [Authorization/Requirements/AreaRequirements.cs](Authorization/Requirements/AreaRequirements.cs) | Marker types | OK | — | — | — |
| 12 | [Controllers/BookingController.cs](Controllers/BookingController.cs) **(MOJIBAKE)** | Customer booking flow | Critical | **Toàn bộ string Vietnamese bị double-encoded UTF-8** (`Báº¡n` thay `Bạn`); `Book()` chứa 130 dòng commented dead code; `Checkout` GET có duplicate price calc bị overwrite; `Checkout.Book` validate `StartDate.Date < DateTime.UtcNow.Date` so sánh local vs UTC sai timezone; `ConfirmManualPaymentTransaction` tạo BookingStatus row runtime (race) + hardcode VN strings trong template hợp đồng + `CustomerIdentityCard = ""` (CCCD trống trên hợp đồng); `Details` chỉ owner xem được; `Contract/TransactionContract/SignContract` legacy redirects chiếm chỗ. | Critical | (1) Fix encoding 3 file; (2) Xóa block commented; (3) Đổi `DateTime.UtcNow.Date` sang local TimeZoneInfo VN; (4) Pull CCCD từ user khi tạo DigitalContract; (5) Cho Admin/Manager xem Details; (6) Xóa 3 action legacy. |
| 13 | [Controllers/HandoverController.cs](Controllers/HandoverController.cs) | Bàn giao xe | Critical | Filter `b.UserID == userId` ở mọi action → Staff/Admin không thao tác bàn giao được; KHÔNG check trạng thái booking trước khi cho phép Submit; cho phép tạo nhiều CheckIn/CheckOut record liên tiếp; KHÔNG dùng `FileUploadValidationService` đã có trong DI; `SignatureBase64.Substring(IndexOf(",")+1)` crash nếu thiếu prefix; `Convert.FromBase64String` không try/catch; ảnh CheckOut tự set `ActualReturnDate = UtcNow` trong khi customer chưa confirm return → race với `CustomerConfirmReturn`. | Critical | (1) Cho Staff/Admin/Manager truy cập; (2) Validate booking status trước Submit; (3) Guard double-handover; (4) Inject + dùng `IFileUploadValidationService`; (5) Try/catch + null check signature parsing; (6) Bỏ `ActualReturnDate` set ở handover, để `CustomerConfirmReturn` quản lý. |
| 14 | [Controllers/PaymentController.cs](Controllers/PaymentController.cs) | PayOS payment | High | `CreatePayment/CancelHold/CancelTransaction/Cancel` là HttpGet nhưng đổi state → crawler/browser prefetch có thể trigger; `Webhook [IgnoreAntiforgeryToken]` đúng pattern (signature verify ở service); dead `#if false` block; conditional `transaction.BookingTransaction == null ? "Index","Cart" : "Index","Cart"` cùng nhánh; double-query "latestTransaction"; TempData "Success" set kể cả khi reconcile thất bại. | High | Convert state-changing GET → POST + button form. Xóa #if false. Refactor dual conditional. Dedupe query. Chỉ set Success khi thực sự paid. |
| 15 | [Services/PayOSPaymentService.cs](Services/PayOSPaymentService.cs) | PayOS integration | High | Signature verify ✓; amount mismatch ✓; idempotency ✓; **`GenerateUniqueOrderCodeAsync`** check-then-insert race (unique index protect nhưng error path không retry); **`AttachAddOnsAsync`** lookup AddOn theo Name (collision) + tự tạo AddOn từ user input → pollute lookup table; `ResolveReservationFee` fallback 20% hardcode; PayOS API call ngoài DB transaction (PayOS có order, DB không có row → mismatch nếu DB fail). | Critical | (1) Retry loop có jitter cho OrderCode; (2) Lưu `AddOnID` trong quote, lookup theo ID; (3) Cấm tạo AddOn từ webhook; (4) Đưa reservation % vào config; (5) Wrap PayOS call trong DB transaction hoặc compensate khi DB fail. |
| 16 | [Services/BookingService.cs](Services/BookingService.cs) | Booking CRUD | Medium | `AutoCancelExpiredBookings` không được gọi từ đâu (dead code path). `GetAllBookingsAsync` không pagination, không filter → tải toàn bộ booking về RAM trong admin Manage. `CreateBookingAsync` xử lý null status bằng fallback Pending → hidden default. | High | Đưa AutoCancelExpiredBookings vào Hangfire job hoặc loại bỏ. Thêm pagination cho admin. |
| 17 | [Services/BookingTransitionService.cs](Services/BookingTransitionService.cs) | Booking state machine | High | Validation message tiếng Anh → user thấy tiếng Anh; `GetOrCreateStatusAsync` race condition; thiếu transition cho `Pending → Expired` (chỉ có Unpaid→Expired); active booking cancel với partial payment không có refund logic; `HandoverRecords.Any(h => h.Type == "CheckIn")` không check IsDeleted (nhưng global soft-delete filter trên HandoverRecord có cover). | High | Vietnamese messages; seed BookingStatuses + remove dynamic create; thêm transitions còn thiếu; partial-payment refund flow. |
| 18 | [Services/ReservationService.cs](Services/ReservationService.cs) | Hold / availability | Medium | `CreateHoldsAsync` SaveChanges trong loop → mỗi save tạo IDENT change; nếu fail giữa loop, prior holds rollback OK do outer transaction; `IsDriverAvailableAsync` không xét hold table cho driver; magic strings nhưng đã có constants. | Medium | Group save 1 lần cuối transaction; thêm hold check cho driver. |
| 19 | [Services/BookingQuoteService.cs](Services/BookingQuoteService.cs) | Pricing quote | High | `HoldingFee = totalPrice * 0.2m` hardcode; `CollateralAmount = car.DefaultDeposit` không thu trong holding (chỉ thu khi nhận xe?) — nghiệp vụ cần xác minh; **Holiday pricing không được tính** (HolidayPricingService có DI nhưng quote không gọi); addon parse từ string `"name|price"` không validate khớp DB; promotion check không kiểm tra `IsAvailableForUser` (1 user 1 lần?). | High | Config holding%; clarify deposit flow; wire `IHolidayPricingService.GetMultiplierAsync`; pin addon by AddOnID; thêm per-user promo cap. |
| 20 | [Services/BookingBusinessRuleService.cs](Services/BookingBusinessRuleService.cs) | Business rules | High | `GetValidCustomerDrivingLicenseAsync` **dynamic create** CustomerDrivingLicense in read path với `IsApproved=true` từ legacy User fields → ghost approval bypass admin review. | Critical | Loại bỏ dynamic create; force user phải re-upload qua eKYC flow. |
| 21 | [Services/EligibilityService.cs](Services/EligibilityService.cs) | Pre-checkout check | OK | Magic string "Verified" — should be constant. | Low | Dùng constant. |
| 22 | [Services/SelfDrivePricingService.cs](Services/SelfDrivePricingService.cs) | Pricing matrix | OK | Pricing tiers (4h/8h/12h/24h) hardcode 0.4/0.7/0.85/1.0 ratio. Magic numbers. | Low | Đưa ratio vào config. |
| 23 | [Services/AuthService.cs](Services/AuthService.cs) | Auth + PII lookup | Medium | BCrypt OK; legacy fallback decrypt PII to compare có thể throw nếu PII corrupted (no try/catch); SHA256 unsalted cho hash uniqueness — acceptable but rainbow-able if DB leak. | Medium | Try/catch decrypt fallback; chuyển sang HMAC-SHA256 với secret key. |
| 24 | [Controllers/AuthController.cs](Controllers/AuthController.cs) **(partial)** | Auth flow | Critical | `User.Identity.IsAuthenticated` không null check (lines 59, 413); active `Register` thiếu age/CCCD/GPLX/Phone validation + encryption (100 dòng commented dead code chứa logic đúng); messaging mix có/không diacritics; `PasswordHash = model.Password` confusing naming nhưng functional vì service hash sau; OTP TTL 5min OK; MFA attempts cap 5 OK. | Critical | (1) Bỏ commented; restore validation + encryption vào active method; (2) Null-safe User.Identity; (3) Chuẩn hoá VN messages có diacritics. |
| 25 | [Controllers/ContractController.cs](Controllers/ContractController.cs) | Admin create rental contract | Critical | Không null-check booking sau `GetBookingByIdAsync` → NRE; `UpdateBookingStatusAsync("Completed")` bypass transition validation; không validate EndKm ≥ StartKm; không kiểm tra customer đã return chưa. | Critical | (1) Null check; (2) Dùng BookingTransitionService; (3) Validate EndKm; (4) Verify return state. |
| 26 | [Controllers/CarController.cs](Controllers/CarController.cs) | Customer + Admin car list | High | `Index` query include 7 navigation properties + sub-Any → cartesian + N+1; overlap filter chỉ exclude "Cancelled, Completed" → Draft/Unpaid block xe nhầm; hardcode pricing ratio 0.25/0.40/0.55/1.0 trên Details (khác `SelfDrivePricingService` 0.4/0.7/0.85/1.0 → **inconsistent giá hiển thị**!); hardcode deposit 500000, insurance 3000000; cookie RegionID dễ tamper. | Critical | Sửa pricing constants chung; remove unnecessary includes; mở rộng exclude list status; di chuyển constants vào shared service. |
| 27 | [Services/CarService.cs](Services/CarService.cs) | Car CRUD + cache | High | **Cache dead**: `List<Car>? cached = null; if (cached != null) return cached;` — never hits cache nhưng comment claim TTL 5 phút → misleading. `DeleteCarAsync` manual soft-delete trùng với `ApplySoftDeleteRules`. | High | (1) Wire `_cache.GetOrSetAsync(...)` thật; (2) Đơn giản hóa delete. |
| 28 | [Models/Booking.cs](Models/Booking.cs) | Booking entity | Medium | `DepositStatus="Pending"` default nhưng `DepositStatusConstants` không có "Pending"; `ContractTypeCode="SELF_DRIVE_CONTRACT"` hardcode default. | Medium | Align default với constants. |
| 29 | [Services/ConfigurationGuard.cs](Services/ConfigurationGuard.cs) | Production config validation | OK | Comprehensive checks ✓ | — | — |
| 30 | [Services/PiiEncryptionService.cs](Services/PiiEncryptionService.cs) | PII encryption | OK với cảnh báo | `Decrypt` swallow CryptographicException → fallback plaintext masks data corruption. | Medium | Log warning khi fallback; có flag strict mode trong production. |
| 31 | [Services/FileUploadValidationService.cs](Services/FileUploadValidationService.cs) | Magic-byte file check | OK | Service tốt; nhưng **HandoverController không inject/dùng** — bug ở consumer. | High | Inject + dùng trong tất cả upload paths (Handover, Car image, Avatar, Blog). |
| 32 | [Views/Shared/Error.cshtml](Views/Shared/Error.cshtml) | Error page | OK | Không leak stack/PII. | — | — |
| 33 | [Views/Shared/_Layout.cshtml](Views/Shared/_Layout.cshtml) | Public layout | High | `text: "@Html.Raw(TempData["Success"])"` — TempData có thể chứa user reason → stored XSS qua TempData. | High | Bỏ `@Html.Raw`, dùng `@TempData["Success"]` raw escape; HTML-encode trước khi nhúng JS. |
| 34 | [Views/Shared/_AdminLayout.cshtml](Views/Shared/_AdminLayout.cshtml) | Admin layout (modified) | High | Tương tự XSS qua TempData @Html.Raw lines 541, 552. | High | Như trên. |
| 35 | [Views/Shared/_AreaManagerLayout.cshtml](Views/Shared/_AreaManagerLayout.cshtml) | Area Manager layout | High | Tương tự XSS lines 236, 247, 258. | High | Như trên. |
| 36 | [Views/Blog/Detail.cshtml](Views/Blog/Detail.cshtml) | Blog detail | High | `@Html.Raw(blog.Content)` — nếu blog editor chấp nhận HTML rich text, attacker (Admin tấn công self / nội bộ) stored XSS. | Medium | Sanitize HTML server-side (HtmlSanitizer NuGet) trước khi lưu, hoặc render markdown. |
| 37 | [Views/Guide/Index.cshtml](Views/Guide/Index.cshtml) | Guide page | Medium | `@Html.Raw(roleName)` line 797 — nếu roleName từ user input → XSS. | Medium | Encode hoặc enum-restrict. |
| 38 | [appsettings.json](appsettings.json) | Default config | OK | Tất cả secret là empty string. Không leak. | — | — |
| 39 | [appsettings.Development.json](appsettings.Development.json) | Dev config | OK | Tương tự — không có secret thực. | — | — |
| 40 | [appsettings.Production.example.json](appsettings.Production.example.json) | Example only | OK | (chưa đọc chi tiết — sample template) | — | — |

> **Spot-check qua Grep** (các file chưa đọc chi tiết):
> - **Mojibake**: chỉ 3 file (`Controllers/BookingController.cs`, `Controllers/CartController.cs`, `Controllers/EkycController.cs`). Các file Vietnamese khác encode đúng UTF-8 ✓.
> - **`@Html.Raw`**: 22 vị trí — chia: an toàn (Json.Serialize: 11 vị trí), XSS-risky (TempData: 6, Blog content: 1, role label: 1, user-rendered data: 3). Liệt kê trong mục 7.
> - **`User.Identity.IsAuthenticated`** không null check: 5 files (`AuthController`, `CarController`, `CartController`, `Views/Car/Details.cshtml`, `Views/Shared/_Layout.cshtml`).
> - **Plaintext password assignment** thực ra chỉ có 1 case `AuthController.cs:481 PasswordHash = model.Password` → an toàn vì `AuthService.RegisterAsync` line 125 BCrypt-hash field này, nhưng naming dễ gây nhầm.

Bảng đầy đủ các file còn lại (~300 file) sẽ được mở rộng ở Pass 2/3. Tạm thời nhóm chung:

| Nhóm | File | Trạng thái tạm |
|------|------|-----------------|
| 26 Customer-facing controllers | `Cart, Chat, Ekyc, Guide, Home, LongTerm, OwnerCars, PhysicalContracts, Privacy, Review, SecureFile, SelfService, Support, Tracking, Voucher, CarCatalog, Alert, Ai, Blog, BlogApi, Seed, Dashboard, Membership` | **Spot-check pending → Pass 2** |
| 38 Admin controllers | Tất cả `Areas/Admin/Controllers/*` ngoài CarController/BookingController/PaymentController đã spot | **Spot-check pending → Pass 2** |
| 60+ services khác | `EmailService, GroqChatService, GeminiChatService, ChatSessionService, EnterpriseOperationsService, EnterpriseTelemetryService, SnapshotService, SettlementReviewService, RefundService, BillingService, BlogService, AreaService, TrackingService, DeliveryService, DeliveryScopeService, HolidayPricingService, QrCccdParserService, AuditTrailService, ContractClassificationService, EvidenceHashService, DisputeExportService, PrivacyRequestService, SupportTicketService, ApiKeyService, WebhookSigningService, CustomerSelfServiceService, BookingTransitionService, PaymentTransitionService, LongTermRentalService, LocaleFormatService, NoopProductAnalyticsService, SloMetricsService, MembershipService(?), ReferralService, FptEkycService, CatalogCacheService, CacheService, PaymentLedgerService, PaymentUrlResolver, PayOSPayoutService, RefundPayoutPollingJob` | **Deep read pending → Pass 2** |
| 7 Background jobs | `EmailBackgroundJob, ContractExpirationJob, MaintenanceAlertJob, DepositRefundJob, ExpireBookingHoldsJob, RefundPayoutPollingJob, CleanupUnverifiedAccountsJob` | **Deep read pending → Pass 2** |
| Custom JS/CSS | `wwwroot/js/{global-calendar,smartrental-map,car-details,site,pwa}.js`, `wwwroot/css/{admin-theme,blog,car-details,site}.css` | **Deep read pending → Pass 3** |
| 60 admin views | Tất cả `Areas/Admin/Views/*` ngoài layout | **Deep read pending → Pass 3** |
| 68 main views | Đã spot 3 layout + Error + Blog/Detail + Guide/Index. Còn lại chi tiết → Pass 3 |
| 74 migrations | Đã scan chỉ index/decimal. Chi tiết FK/ON DELETE/nullable → Pass 2. | |

---

## 3. Danh sách lỗi cần sửa (Pass 1)

| Mã | Mức | File | Mô tả | Tác động | Cách reproduce | Cách sửa đề xuất | Trạng thái |
|----|-----|------|-------|----------|----------------|-------------------|------------|
| BUG-001 | Critical | `Controllers/BookingController.cs`, `Controllers/CartController.cs`, `Controllers/EkycController.cs` | Source file lưu UTF-8 nhưng nội dung Vietnamese đã bị double-encoded → bytes là "Báº¡n" thay vì "Bạn". | UI hiển thị gibberish khắp luồng đặt xe / giỏ hàng / eKYC. Sai chuẩn UX. | Mở 1 trong 3 file thấy `"Báº¡n pháº£i xÃ¡c nháº­n..."`. Test browser sẽ render đúng vậy. | Re-encode bằng cách decode wrong-UTF-8 → Windows-1252 → UTF-8 cho từng string literal. (Áp dụng ở mục Safe Fixes.) | **Fixed** |
| BUG-002 | Critical | `Controllers/BookingController.cs` lines 149-173 + 465-595 | Dead duplicated price-calc + 130 dòng commented atomic-booking block | Confusing maintenance, dễ vô tình revert sang logic cũ; obscure flow. | Xem file. | Xóa block commented + xóa duplicate calc trong Checkout (đã được overwrite bởi quote service). | **Fixed** |
| BUG-003 | High | `Controllers/BookingController.cs:113` | `NextInspectionDate < DateTime.UtcNow` so sánh date UTC với date local user | Có thể block xe nhầm khi user ở VN cuối ngày. | — | Convert sang `TimeZoneInfo` Asia/Ho_Chi_Minh hoặc dùng `DateOnly`. | Pending |
| BUG-004 | High | `Controllers/BookingController.cs:222` | Re-fetch user đã load ở line 69 | Hai DB roundtrip cho cùng user. | — | Reuse `user` variable. | Pending |
| BUG-005 | High | `Controllers/BookingController.cs:315` | `StartDate.Date < DateTime.UtcNow.Date` (UTC) | Khách VN chọn `hôm nay` (00:30 sáng VN = 17:30 hôm qua UTC) → có thể bị block. | — | Compare TimeZoneInfo VN. | Pending |
| BUG-006 | Critical | `Controllers/BookingController.cs:885 ConfirmManualPayment` | Hardcode `Amount = TotalPrice * 0.2m` cho "Manual Transfer" | Tiền cọc sai khi nghiệp vụ thay đổi 20%. | — | Dùng `ReservationFee` field; nếu rỗng, gọi shared `ResolveReservationFee`. | Pending |
| BUG-007 | Critical | `Controllers/BookingController.cs:1010-1047` | DigitalContract auto-create với `CustomerIdentityCard=""` và hardcoded TermsAndConditions VN | Hợp đồng pháp lý có CCCD trống → vô hiệu. | — | Pull `user.CitizenId` (đã decrypt via PiiEncryption). Move template ra resource. | Pending |
| BUG-008 | Critical | `Controllers/BookingController.cs:944-947, 1106` | `ConfirmManualPaymentTransaction` tạo BookingStatus row runtime nếu thiếu | Race condition; thường lookup không seed → 2 user cùng thanh toán đầu tiên tạo trùng. | — | Seed BookingStatuses trong DbSeeder. Throw nếu thiếu thay vì auto-create. | Pending |
| BUG-009 | Critical | `Middleware/RateLimitingMiddleware.cs:245-253` | Tự đọc `X-Forwarded-For` không qua trust validation | Attacker spoof IP → bypass login brute-force protection. | curl với `-H 'X-Forwarded-For: 1.2.3.4'` từ random IP, mỗi request đổi → counter không tăng. | Dùng `context.Connection.RemoteIpAddress` (đã được UseForwardedHeaders cập nhật theo trust config). | Pending |
| BUG-010 | Critical | `Controllers/HandoverController.cs:41, 81, 98` | `WHERE BookingID == bookingId && UserID == userId` ở mọi action | Staff/Admin/Manager không bàn giao được; customer bàn giao tự upload ảnh → fraud risk. | Login Staff → vào /Handover/Capture?bookingId=1 → 404. | Cho phép `[Authorize(Roles="Admin,Manager,Staff")]` truy cập booking thuộc area mình. Customer chỉ ký SignatureBase64. | Pending |
| BUG-011 | Critical | `Controllers/HandoverController.cs:92 Submit` | Không validate trạng thái booking; cho phép double check-in/out; không dùng FileUploadValidationService | (1) Customer bàn giao xe đã Completed → record orphan; (2) Upload .jpg với magic-byte exe → bypass extension check. | — | Add `Booking.BookingStatus == Confirmed (CheckIn) hoặc Active (CheckOut)` check; query existing same-type record. Inject `IFileUploadValidationService` và dùng. | Pending |
| BUG-012 | High | `Controllers/HandoverController.cs:145-149` | Signature base64 parse: `Substring(IndexOf(",")+1)` không null-safe + `Convert.FromBase64String` không try/catch | Malformed input → 500 error. | POST với SignatureBase64="abc" (no comma). | Use `if (SignatureBase64.Contains(",")) ... else use whole`. Wrap try/catch FormatException. | Pending |
| BUG-013 | High | `Controllers/HandoverController.cs:159` | Set `ActualReturnDate=UtcNow` ở handover CheckOut trong khi `CustomerConfirmReturn` cũng set | Double assignment; nếu user upload handover rồi cancel → ActualReturnDate đã set sai. | — | Bỏ; chỉ `CustomerConfirmReturn` set ActualReturnDate. | Pending |
| BUG-014 | Critical | `Controllers/AuthController.cs:447-498 Register (active)` | Thiếu validation CCCD/GPLX/Phone/Age + KHÔNG encrypt CCCD/GPLX trước khi lưu (đoạn code đúng nằm trong block /* ... */ comment) | (1) Customer dưới 18 đăng ký được; (2) CCCD/GPLX không encrypt → leak DB = leak danh tính khách hàng; (3) format invalid → DB chứa rác. | Đăng ký với DOB hôm nay → success. | Restore validation + encryption từ commented block; KHÔNG xóa khi chưa migrate dữ liệu cũ. | Pending |
| BUG-015 | Critical | `Controllers/AuthController.cs:59, 413` + 5 file khác | `User.Identity.IsAuthenticated` không null check | NullReferenceException khi `User.Identity == null`. | — | Dùng `User.Identity?.IsAuthenticated == true`. | **Fixed** (Auth+Car controllers) |
| BUG-016 | High | `Middleware/AccountLockMiddleware.cs:27` | Query Users mỗi request, không cache | DB tải nặng. | — | Cache `MemoryCache` TTL 30s key=`UserLock:{userId}`. | Pending |
| BUG-017 | High | `Middleware/AuditLogMiddleware.cs:51-58` | `_next` không trong try/finally; exception ở downstream → audit không ghi | Mất audit log cho request bị 500. | — | Wrap try/finally; bắt exception throw lại sau khi log. | Pending |
| BUG-018 | High | `Authorization/Handlers/AreaAuthorizationHandler.cs:83-84` | `HandleView` cho phép Staff/Viewer xem TẤT CẢ area | IDOR cross-area visibility. | Login Staff thuộc Area A → /Admin/Area/Show?id=B → 200. | Filter by area membership. | Pending |
| BUG-019 | Critical | `Services/BookingBusinessRuleService.cs:55-69` | Auto-create CustomerDrivingLicense với IsApproved=true từ legacy User fields | Bypass admin approval; customer chưa upload GPLX cũng được thuê xe nếu User có DrivingLicenseHash. | — | Yêu cầu re-verify qua eKYC; không auto-create approved record. | Pending |
| BUG-020 | High | `Services/CarService.cs:37-38, 58-59` | Dead cache: `cached = null; if (cached != null) return cached` | False sense of cache; performance không thực sự cải thiện. | — | Wire `_cache.GetOrSetAsync(key, factory, ttl)`. | **Fixed** (cleanup dead lines + add real cache hook) |
| BUG-021 | High | `Controllers/CarController.cs:200-207` vs `Services/SelfDrivePricingService.cs:29-32` | Hai bộ pricing ratio khác nhau (0.25/0.40/0.55 vs 0.4/0.7/0.85) | Giá hiển thị trên Car/Details != giá tính lúc Checkout. | Mở Details xe X (price 1tr) thấy 4h=250k. Checkout 4h thấy 400k. | Loại bỏ pricing trong Details, tham chiếu `ISelfDrivePricingService` shared. | Pending |
| BUG-022 | High | `Controllers/CarController.cs:107 overlap exclude` | Chỉ exclude {"Cancelled","Completed"} → Draft/Unpaid/Expired/Rejected block xe nhầm | Xe bị "khóa" khi 1 customer mở Checkout rồi bỏ đi (BookingStatus = Draft/Unpaid) → trong list bị lọc khỏi Available. | — | Dùng `BookingStatusConstants.AvailabilityBlocking` (Pending, Confirmed, Active). | Pending |
| BUG-023 | High | `Controllers/CarController.cs:200-207, 205-206` | Hardcoded `DiscountPercent=17, DepositAmount=500000, InsuranceFee=3000000` | Magic numbers không khớp dữ liệu xe thực. | — | Lấy từ `Car.DefaultDeposit` và HolidayPricing/InsurancePackages. | Pending |
| BUG-024 | High | `Views/Shared/_Layout.cshtml:590,601`, `_AdminLayout.cshtml:541,552`, `_AreaManagerLayout.cshtml:236,247,258` | `text: "@Html.Raw(TempData[...])"` → stored XSS qua TempData | Customer cancel với reason `<script>alert(1)</script>` → Admin mở Manage page → XSS execute. | — | Bỏ Html.Raw; SweetAlert tự HTML-escape `text`. | **Fixed** |
| BUG-025 | High | `Controllers/ContractController.cs:38-47` | Không null check booking; bypass transition service; không validate EndKm | Admin tạo contract cho booking đã xoá → NRE; "Completed" booking chưa pickup. | — | Add null check; gọi `IBookingTransitionService.TransitionAsync`. | **Fixed** |
| BUG-026 | High | `Controllers/PaymentController.cs:88, 141, 274` | HttpGet với side-effects (CancelHold/CancelTransaction/Cancel) | Crawler/prefetch trigger cancel; CSRF GET pattern. | — | Convert thành POST + form button hoặc add explicit confirm action. | Pending |
| BUG-027 | High | `Services/PayOSPaymentService.cs:1111` | OrderCode generate non-atomic check-then-insert | Hiếm xảy ra dưới tải bình thường, nhưng dưới spike collide → exception bubble. | — | Retry loop với jitter; rely on unique index + catch DbUpdateException. | Pending |
| BUG-028 | High | `Services/PayOSPaymentService.cs:1013-1037 AttachAddOnsAsync` | Lookup AddOn theo Name; tự tạo AddOn nếu không thấy | Customer truyền `"FakeName|1"` → tạo AddOn rác trong DB từ webhook flow. | — | Lookup theo `AddOnID` trong quote snapshot; throw nếu không tồn tại. | Pending |
| BUG-029 | High | `Services/BookingQuoteService.cs` | Holiday pricing không apply; HoldingFee hardcoded 20%; promotion no per-user cap | Mất doanh thu ngày Tết/lễ; user abuse promo. | — | Inject `IHolidayPricingService`; per-user usage. | Pending |
| BUG-030 | High | `Services/BookingTransitionService.cs:152, 157, 167` | Error messages tiếng Anh | UX inconsistent (mọi nơi khác Vietnamese). | — | Vietnamese. | **Fixed** |
| BUG-031 | Medium | `Data/ApplicationDbContext.cs:247-250, 258-263, 330-340` | Duplicate index khai báo `PaymentLedgerEntry.IdempotencyKey`, `PaymentWebhookEvent.{Provider,EventKey}` | EF generate thừa index; storage bloat. | — | Bỏ duplicate. | **Fixed** |
| BUG-032 | Medium | `Middleware/GlobalExceptionMiddleware.cs` + `Program.cs:446` | Vừa có custom exception middleware vừa `app.UseExceptionHandler` | 2 cơ chế đè nhau; behaviour không deterministic. | — | Giữ 1 cơ chế. | Pending |
| BUG-033 | Medium | `Controllers/AuthController.cs` rải rác | Mix có/không diacritics Vietnamese ("Dang nhap" vs "Đăng nhập") | Inconsistent UX. | — | Chuẩn hoá có dấu. | Pending |
| BUG-034 | Medium | `Services/AuthService.cs:60-61` | `_piiEncryption.DecryptIfNotEmpty(stored)` không try/catch | Corrupt PII → 500. | — | Try/catch + log. | Pending |
| BUG-035 | Medium | `Services/PiiEncryptionService.cs:43` | `Decrypt` swallow CryptographicException → trả plaintext | Mask data corruption. | — | Log warning với key context. | Pending |
| BUG-036 | Medium | `Views/Blog/Detail.cshtml:43` | `@Html.Raw(blog.Content)` | Stored XSS qua blog. | — | HtmlSanitizer hoặc Markdown rendering. | Pending |
| BUG-037 | Low | `Models/Booking.cs:84` | Default `DepositStatus="Pending"` không khớp constants | Inconsistency. | — | Đổi thành `DepositStatusConstants.Unpaid` hoặc "Holding". | Pending |
| BUG-038 | Low | `Repositories/Repository.cs:22` | `Task<T> GetByIdAsync` nhưng FindAsync trả `T?` | Nullable warning. | — | Đổi signature. | Pending |
| BUG-039 | Critical | `Authorization/Handlers/AreaAuthorizationHandler.cs:140-143` | `RoleInArea == "manager"` magic string | Typo risk; nếu lookup table lưu "Manager" hoa thì handler fail-closed (DOS). | — | Hằng số `AreaStaffRoleConstants`. | Pending |

> *Note*: Đánh số dừng ở BUG-039 cho Pass 1. Pass 2/3 sẽ tiếp tục.

---

## 4. Danh sách phần cần làm lại (sơ sài / sai kiến trúc)

| Module | Vấn đề | Hướng làm lại enterprise | Ưu tiên |
|--------|--------|--------------------------|---------|
| **Booking flow concentration** | Logic đặt xe split giữa `BookingController.Book`, `BookingController.ConfirmManualPaymentTransaction`, `PayOSPaymentService.ConvertPaidHoldToBookingAsync`, `PayOSPaymentService.ConvertPaidCartToBookingsAsync` — 4 chỗ tạo Booking với 4 bộ field defaults khác nhau. | Trích `IBookingFactory.CreateFromQuoteAsync(quote, source)` chuẩn hoá tạo booking. | P0 |
| **Pricing duplication** | `BookingController.Checkout` (149-173) duplicate `SelfDrivePricingService`; `CarController.Details` (200-207) lại có pricing tier khác → giá hiển thị ≠ giá tính | Chỉ tính giá qua `ISelfDrivePricingService` ở 1 chỗ; FE chỉ display, không tính. | P0 |
| **HandoverController** | Sơ sài: không phân quyền role, không validate status, signature parse fragile, không dùng FileUploadValidation. | Rewrite: tách `IHandoverService` với business rule check; ownership theo area; thêm storage path config; sửa Capture/Submit cho Staff. | P0 |
| **ContractController** | 1 file 53 dòng làm 1 thứ duy nhất + bypass transition service → unsafe. | Loại bỏ controller cũ; dùng PhysicalContracts thay thế (đã có redirect ở BookingController). | P1 |
| **CarService cache** | Cache infrastructure có nhưng dead code → comment lừa. | Dùng `ICatalogCacheService` đã có sẵn cho catalog browsing. | P1 |
| **Rate limiting** | In-memory + IP spoof bypass → không safe cho cluster. | Rewrite trên Redis (đã có RedisConnection optional). | P0 |
| **Admin redirects** | Program.cs line 536+ hardcode redirect `/Partner`, `/Driver`, `/Admin/Driver`, `/Admin/PartnerContract`, `/Admin/DigitalContract` → ngụ ý có ít nhất 5 module bị legacy-stub. | Xoá controllers & views cũ, dọn route map. | P2 |
| **Mojibake source files** | 3 file controller có thể đã bị mở-save bằng editor encoding sai. | Fix encoding; thêm `.editorconfig` `charset=utf-8-bom`. | P0 |
| **Comment dead code** | `BookingController.Book`, `AuthController.Register` chứa hàng trăm dòng commented. | Xóa hoàn toàn; sử dụng git history nếu cần tham chiếu. | P0 |
| **Manual SQL trong audit middleware** | Bypass DbContext, lock connection riêng. | Dùng background queue (Hangfire) ghi audit. | P1 |

---

## 5. Danh sách phần cần làm thêm để đạt 100%

| Task | Mục tiêu | Module / File | Lý do | Ưu tiên | Acceptance | Evidence |
|------|----------|---------------|-------|---------|------------|----------|
| Restore validation/encryption Register | An toàn dữ liệu CCCD/GPLX | `AuthController.cs` | Hiện không encrypt + không validate | P0 | Unit test cover: age<18 reject, CCCD format invalid reject, GPLX/Phone format reject, CitizenId được encrypt at-rest | DB row inspection + tests pass |
| Holiday pricing wire | Áp dụng `HolidayPricingService` vào quote | `BookingQuoteService.cs` | Đã DI nhưng không dùng | P0 | Tết booking giá ×1.5 vs ngày thường | Test + booking VND match |
| Concurrency test 2-user-same-car | Đảm bảo Serializable + hold prevent double-book | `ReservationService` + `BookingTransitionService` | Mới thấy code chứ chưa thấy test | P0 | Test launch 2 task concurrent → 1 win, 1 fail with "không available" | xUnit tests |
| HandoverController phân quyền | Cho Staff/Admin thao tác | `HandoverController.cs` | Customer-only hiện tại không enterprise | P0 | Staff role + same area → 200 | Integration test |
| Mojibake CI guard | Prevent re-introduce | `.editorconfig` + CI script | Avoid regression | P1 | CI fail nếu source chứa `Ã.{1,3}` pattern Vietnamese | CI artifact |
| FileUploadValidation usage audit | Mọi upload đi qua magic-byte check | Tất cả `Handover/Car/Avatar/Blog upload` | Chỉ định service nhưng nhiều nơi không dùng | P0 | Grep `_carService.AddCarImageAsync\|HandoverRecord\|UploadAvatar` đều có wrapped validation | Grep + manual test |
| Idempotent Booking creation factory | Centralize booking create logic | New `IBookingFactory` | 4 chỗ create booking với defaults khác | P1 | 1 entry point | Code review |
| Per-user promo cap | Tránh abuse | `BookingQuoteService` + `Promotion` model | Hiện chỉ check `UsedCount < UsageLimit` global | P1 | 1 user 1 lần dùng promo X | Test |
| Redis-based rate limiter | Multi-instance | `RateLimitingMiddleware` | In-memory không scale | P1 | 2 instances test cùng IP đếm chung | E2E |
| Caching AccountLockMiddleware | Giảm DB load | `AccountLockMiddleware` | DB-per-request | P1 | Cache hit > 90% | Metrics |
| Pagination admin BookingManage | Tránh OOM | `BookingController.Manage`, `BookingService.GetAllBookingsAsync` | Load all | P0 | < 100 rows / page | Manual test |
| Audit middleware queue | Không block HTTP | `AuditLogMiddleware` | Raw SQL trên request thread | P1 | p95 latency drop > 20ms | Benchmark |
| Blog content sanitizer | Chống XSS | `BlogService` + `Views/Blog/Detail` | `@Html.Raw(content)` | P1 | XSS payload bị strip | Test |
| Webhook replay protection | Chống replay PayOS webhook | `PayOSPaymentService.HandleWebhookAsync` | EventKey dedupe nhưng key thuần (orderCode+code+amount) có thể replay nếu PayOS resend → đã có dedupe ✓ | OK | — | — |
| eKYC error handling Pass 2 | Comprehensive | `EkycController`, `FptEkycService` | Mojibake + chưa đọc detail | P0 | Pass 2 audit | — |
| Unit test BookingTransitionService | Đảm bảo state machine đúng | Test project | Chỉ có 1 test file staged | P0 | All transitions covered | Tests |
| Test PayOSPaymentService webhook flow | Test mock signature | Test project | Critical money path | P0 | Tests | — |
| Localization Resources/ | Vietnamese resource file | `Resources/` | Resource path declared ở Program.cs nhưng không thấy file | P1 | View use IStringLocalizer | — |

---

## 6. Sai nghiệp vụ

| # | Nghiệp vụ đang sai | Luồng đúng phải là | File cần sửa | Đề xuất |
|---|-------------------|--------------------|--------------|---------|
| 1 | `ConfirmManualPayment` dùng `TotalPrice*0.2` cho deposit | Phải dùng `Booking.DepositAmount` hoặc `ReservationFee` đã quote sẵn | `BookingController.cs:911` | Sửa hardcode → `booking.ReservationFee > 0 ? booking.ReservationFee : Math.Round(...0.2,0)` |
| 2 | Holiday pricing không tính | Quote ngày lễ/Tết phải gọi `HolidayPricingService.GetMultiplierAsync(date)` | `BookingQuoteService.cs` | Inject + wire |
| 3 | `BookingBusinessRuleService.GetValidCustomerDrivingLicenseAsync` auto-approve license từ User legacy fields | Customer phải re-upload GPLX qua eKYC để được approved | `BookingBusinessRuleService.cs` | Loại bỏ dynamic create |
| 4 | `Register` thiếu validate Age 18+, CCCD format 12 số, GPLX format 12 số | Phải reject ngay | `AuthController.cs:Register` | Restore validation block |
| 5 | `ContractController.Create` Admin "Completed" booking bất kỳ trạng thái | Phải sau Active + return | `ContractController.cs` | Bypass cần gỡ |
| 6 | `HandoverController` cho phép multiple CheckIn/CheckOut | 1 booking 1 CheckIn + 1 CheckOut | `HandoverController.cs` | Guard |
| 7 | Customer tự upload bàn giao xe → fraud risk | Staff bàn giao tại điểm + signature 2 bên | `HandoverController.cs` | Re-design |
| 8 | `CarController.Details` pricing tier 0.25/0.40/0.55 khác `SelfDrivePricingService` 0.4/0.7/0.85 | Hiển thị giá phải đúng giá tính thuê | `CarController.cs:200-207` | Centralize |
| 9 | `CarController.Index` overlap exclude thiếu Draft/Unpaid/Expired/Rejected | Phải dùng `BookingStatusConstants.AvailabilityBlocking` consistently | `CarController.cs:106` | Sửa |
| 10 | `CustomerConfirmReturn` set ActualReturnDate trước SaveChanges nhưng `HandoverController.Submit CheckOut` cũng set | 1 nguồn duy nhất | `HandoverController.cs:159` | Bỏ ở Handover |
| 11 | DigitalContract auto-create với CCCD trống | Hợp đồng phải có CCCD/Phone/Name | `BookingController.cs:1015` | Pull từ user |
| 12 | Late fee grace 30 phút hardcode | Config theo policy | `BookingController.cs:780` | Config |
| 13 | Late fee `OverTimeFee * (int)Math.Ceiling(totalHours)` — không cap | Nên cap = giá thuê 1 ngày | `BookingController.cs:782` | Cap |
| 14 | Cancel booking active không refund partial payment | Phải refund | `BookingTransitionService.cs:114-115` | Add path |

---

## 7. Lỗi UI/UX

| Màn hình | Vấn đề | Tác động | Đề xuất redesign |
|----------|--------|----------|-------------------|
| Toàn bộ luồng đặt xe / giỏ hàng / eKYC | Mojibake hiển thị "Báº¡n", "Ä‘áº·t" | User không đọc được thông báo lỗi/thành công | Re-encode 3 file (Pass 1 fixed). |
| Toast / SweetAlert trên _Layout, _AdminLayout, _AreaManagerLayout | `@Html.Raw(TempData)` → XSS + cũng render thẻ HTML từ message thành thật | Vulnerability + có thể vỡ layout | Đổi sang `text: @Json.Serialize(...)` hoặc bỏ Html.Raw. |
| Blog Detail | `@Html.Raw(blog.Content)` | XSS | HtmlSanitizer. |
| `Views/Guide/Index.cshtml:797` | `@Html.Raw(roleName)` | XSS reflective | Encode. |
| Login error message | Mix có / không diacritic Vietnamese | Inconsistent | Chuẩn hoá. |
| `Views/Shared/Error.cshtml` | Quá simple, không có "Request ID copy" button, không có "Báo lỗi" link | Hỗ trợ khách hàng kém | Thêm copy button + support contact. |
| Rate limit response | `Qua nhieu lan dang nhap` không có diacritics + plain HTML inline | Brand không đồng nhất | Render layout-based view. |
| `Areas/Admin/Views/Dashboard/Index.cshtml` | `@Html.Raw(Json.Serialize(...))` OK an toàn nhưng nếu dataset rỗng → chart blank | UX | Show empty state. |
| `Views/Car/Details.cshtml`, `Views/Car/Index.cshtml` | Pricing tier khác Checkout (BUG-021) | Trust loss | Centralize. |
| `Views/Handover/Capture.cshtml` | (chưa đọc detail) | — | Pass 3 |
| `Views/Booking/Manage.cshtml` | Admin manage list không pagination → trang treo > 100 booking | UX + perf | Pagination. |
| `Areas/Admin/Views/Enterprise/Index.cshtml` (NEW) | Chưa audit | — | Pass 2 |

---

## 8. Lỗi security

| # | Rủi ro | Mức | Vị trí | Cách xử lý | Note |
|---|--------|-----|--------|-------------|------|
| 1 | Rate limit IP spoofing | Critical | `RateLimitingMiddleware.cs:245-253` | Dùng `context.Connection.RemoteIpAddress` sau `UseForwardedHeaders` | KHÔNG đụng connection string |
| 2 | Stored XSS qua TempData @Html.Raw | High | 3 layout views | Bỏ Html.Raw / dùng `@Json.Serialize` | Đã fix |
| 3 | Stored XSS Blog | Medium | `Views/Blog/Detail.cshtml:43` | HtmlSanitizer | — |
| 4 | Bypass GPLX approval | Critical | `BookingBusinessRuleService.cs` | Loại bỏ auto-create | — |
| 5 | Register thiếu CCCD encrypt | Critical | `AuthController.Register` | Restore validation + encrypt block | — |
| 6 | Customer fraud bàn giao | High | `HandoverController` | Staff bàn giao | — |
| 7 | Webhook unauth fake | OK (mitigated) | `PaymentController.Webhook` | Đã signature verify ✓ | — |
| 8 | IDOR cross-area | High | `AreaAuthorizationHandler.HandleView` | Filter | — |
| 9 | CSRF + state-changing GET | High | `PaymentController.Cancel*` | Convert POST | — |
| 10 | DB connection string log | OK | `AuditLogMiddleware:44-48` | Mask regex ✓ | — |
| 11 | DataProtection key path | OK if mounted | `Program.cs:157-161` | Configured volume mount in production | — |
| 12 | SQL injection | OK | Mọi query qua EF/parameterized SqlCommand | ✓ | — |
| 13 | Mass assignment Booking | High | `BookingController.Book(Booking booking, ...)` | Customer truyền `BookingStatusID/DepositStatus/TotalPrice/PromotionID/DriverID/UserID` qua form post → server ghi đè rồi nhưng vẫn risk nếu skip validate | Dùng DTO riêng cho input | — |
| 14 | Hangfire dashboard | OK | `Program.cs:574` | Admin role only ✓ | — |
| 15 | Swagger | OK | Production tắt; nếu bật yêu cầu Admin/Manager | ✓ | — |
| 16 | CSP `'unsafe-inline'` | Medium | `Program.cs:460-462` | Refactor sang nonce/hash | Long-term |
| 17 | CORS Production check | OK | `ConfigurationGuard` reject wildcard | ✓ | — |
| 18 | Anti-forgery global | OK | `Program.cs:50` AutoValidate ✓ | — | — |
| 19 | BCrypt | OK | `AuthService.AuthenticateAsync` | ✓ | — |
| 20 | Session cookie | OK | `Program.cs:357-368` HttpOnly + Secure(Production) + Lax | ✓ | — |
| 21 | Auth cookie | OK | `Program.cs:291-309` HttpOnly + Secure(Production) | ✓ | — |
| 22 | reCAPTCHA disabled in commented code | Medium | `AuthController.Login,Register` | Production-gated config exists | OK |
| 23 | MFA OTP brute force | Mitigated | `AuthController.VerifyMfa` cap 5 attempts ✓ | — | — |
| 24 | Privileged SSO restricted | OK | `AuthController.ExternalLoginCallback` | Only existing verified privileged ✓ | — |
| 25 | `PiiEncryptionService.Decrypt` swallow exception | Medium | Log warning | — | — |
| 26 | Open redirect | OK | `IsSafeLocalReturnUrl` → `Url.IsLocalUrl` ✓ | — | — |
| 27 | Hangfire jobs ghi log có thể chứa email | Low | `EmailBackgroundJob.SendOtpEmail` log? | Pass 2 check | — |
| 28 | App_Data secure upload path | OK | Outside wwwroot ✓ | — | — |
| 29 | `/SecureFile/GetSecureImage` ownership | Pending | `SecureFileController` chưa đọc | Pass 2 | — |

**KHÔNG sửa connection string / API key / secret nào trong audit này.**

---

## 9. Database / Performance issues

| # | Vấn đề | Vị trí | Tác động | Đề xuất |
|---|--------|--------|----------|---------|
| 1 | `CarController.Index` 7 nested Include + Bookings sub-Any | `CarController.cs:48-114` | Cartesian + N+1; slow public car list | Split query: id list trước, hydrate bằng query thứ 2 với split queries |
| 2 | `BookingService.GetAllBookingsAsync` không pagination | `BookingService.cs:54` | Admin Manage tải full bảng | Add `Skip/Take`; trả `PaginatedList<Booking>` |
| 3 | Duplicate index khai báo PaymentLedgerEntry/PaymentWebhookEvent | `DbContext.cs` | Storage bloat + insert slowdown | Fix |
| 4 | `AccountLockMiddleware` query per request | `AccountLockMiddleware.cs:27` | Mỗi request +1 DB round-trip | Cache 30s TTL |
| 5 | `AuditLogMiddleware` insert raw SQL per request | `AuditLogMiddleware.cs:100` | +5-20ms p99 | Background queue |
| 6 | `CarService` cache dead | `CarService.cs:37-38, 58-59` | Comment lừa, actual perf không cache | Wire real cache |
| 7 | `BookingQuoteService.BuildQuoteAsync` 3-5 separate queries | `BookingQuoteService.cs` | OK cho per-request nhưng pricing không cache | Cache contract types |
| 8 | Driver concurrency check không xét hold | `ReservationService.IsDriverAvailableAsync` | Double-book driver hiếm xảy ra | Add hold check |
| 9 | `OrderCode` collision race | `PayOSPaymentService.GenerateUniqueOrderCodeAsync:1111` | Concurrent checkout có thể fail | Retry + unique index throw |
| 10 | Soft delete query filter trên child entity tham chiếu parent | `DbContext.cs:1001-1024` | EF có thể warning Cartesian explosion với multi-level | Verify queries with `.AsSplitQuery()` |
| 11 | Booking index `CarID, StartDate, EndDate, BookingStatusID` OK | ✓ | Composite index hỗ trợ overlap query | — |
| 12 | Booking `CreatedAt` index + composite indexes ✓ | ✓ | — | — |
| 13 | `Cars.LicensePlate` unique ✓ + `VIN` unique ✓ | ✓ | — | — |
| 14 | Trạng thái mồ côi: BookingHold expired nhưng `Status=Active` → ExpireBookingHoldsJob 5min batch — Pass 2 audit job logic | — | Hold giữ chỗ quá hạn | Pass 2 |
| 15 | `Promotions.UsedCount` atomic update via `ExecuteUpdateAsync` ✓ | ✓ | — | — |
| 16 | `BookingTransitionService` Serializable transaction ✓ | ✓ | Concurrency OK | — |
| 17 | `Decimal(18,2)` cho tiền, `Decimal(5,2)`/`(4,2)` cho %, `Decimal(10,2)` cho ShippingDistance | OK | — | — |
| 18 | FK `OnDelete(Restrict)` cho Booking → User/Car/Location/ServiceType ✓ | ✓ | Không cascade xóa khách | — |
| 19 | `RowVersion` concurrency token: BookingHold, PaymentLedgerEntry, SettlementReview, RefundRequest, RefundPayout ✓ | ✓ | — | — |

---

## 10. Testing checklist

### Unit test cần có
- [ ] `SelfDrivePricingService.Calculate` các tier (1h, 4h, 8h, 12h, 24h, 25h, 168h)
- [ ] `BookingQuoteService.BuildQuoteAsync` với promotion / membership / referral / addon
- [ ] `BookingTransitionService` mọi cặp transition hợp lệ + invalid + cancel paths
- [ ] `BookingBusinessRuleService.GetValidCustomerDrivingLicenseAsync` (sau khi loại bỏ auto-create)
- [ ] `EligibilityService` 6 failure codes
- [ ] `ReservationService.CheckCarAvailabilityAsync` overlap edge (touching dates, ignore ids)
- [ ] `ReservationService.CreateHoldAsync` concurrent (Serializable behaviour)
- [ ] `AuthService.AuthenticateAsync` correct / wrong password / null
- [ ] `PiiEncryptionService` round-trip
- [ ] `FileUploadValidationService` magic-byte true/false matrix
- [ ] `PaymentLedgerService` idempotency
- [ ] `PayOSPaymentService.HandleWebhookAsync` mocked PayOSClient: valid, invalid signature, amount mismatch, duplicate, expired hold, unknown order

### Integration test (testserver)
- [ ] Full happy path: register → eKYC stub → search → checkout → PayOS webhook → confirm → handover → return → refund
- [ ] Concurrency: 2 user same car same time → 1 success / 1 reject
- [ ] Anti-forgery on every POST
- [ ] Admin only `/Admin/*`, Customer only `/Booking/*`
- [ ] `/health`, `/live`, `/ready` return 200 in DEV
- [ ] Rate limit login 6 failed POSTs → 429

### E2E (Playwright)
- [ ] Login flow
- [ ] Booking flow with delivery
- [ ] Cart checkout multi car
- [ ] Cancel booking
- [ ] Handover capture (image upload)

### Manual test
- [ ] Tất cả màn hình mobile 360px / tablet 768px / desktop 1280px+
- [ ] Form Vietnamese diacritics input
- [ ] Long contract reason cancel
- [ ] Upload .exe rename .jpg → reject
- [ ] PayOS sandbox webhook
- [ ] Hangfire dashboard `/hangfire` admin login
- [ ] Swagger `/swagger` (production gated)

---

## 11. Production readiness checklist

### Must fix BEFORE production
- [ ] BUG-001 Mojibake 3 controllers (Fixed in Pass 1)
- [ ] BUG-007 DigitalContract auto with empty CCCD
- [ ] BUG-009 Rate limit IP spoofing
- [ ] BUG-010, 011, 012, 013 HandoverController toàn diện
- [ ] BUG-014 AuthController.Register restore validation + encryption
- [ ] BUG-019 Bypass GPLX approval
- [ ] BUG-024 XSS via @Html.Raw TempData (Fixed Pass 1)
- [ ] BUG-025 ContractController null + transition (Fixed Pass 1)
- [ ] Concurrency test 2-user-same-car (pending tests)
- [ ] Run all migrations on staging + verify schema match snapshot
- [ ] Verify connection string / secrets configured in T3 hosting

### Should fix soon
- [ ] BUG-002, 003, 004, 005, 006, 008 (Booking issues)
- [ ] BUG-016 AccountLockMiddleware cache
- [ ] BUG-017 AuditLog try/finally
- [ ] BUG-018 Area handler view filter
- [ ] BUG-021, 022, 023 CarController inconsistencies
- [ ] BUG-026 Payment GET-state-changing
- [ ] BUG-027 OrderCode collision retry
- [ ] BUG-028 AddOn lookup
- [ ] BUG-029 Holiday pricing + per-user promo
- [ ] BUG-030 Vietnamese transition messages (Fixed)
- [ ] BUG-031 Duplicate index (Fixed)

### Nice to have
- [ ] BUG-032 Exception middleware dedupe
- [ ] BUG-033 Diacritics consistency
- [ ] BUG-034 PII decrypt try/catch
- [ ] BUG-035 PII decrypt log warning
- [ ] BUG-036 Blog sanitizer
- [ ] BUG-037 DepositStatus default
- [ ] BUG-038 Repository signature
- [ ] CSP nonce/hash
- [ ] Resource files Vietnamese

### Blocked until staging / hosting evidence
- [ ] PayOS webhook URL từ T3 hosting tới `/Payment/Webhook`
- [ ] FPT eKYC sandbox keys
- [ ] Google/Microsoft OAuth callback URL
- [ ] Email SMTP credentials
- [ ] Hangfire SQL persistence working trên T3
- [ ] DataProtection KeysPath volume mount
- [ ] Redis (optional)
- [ ] OpenTelemetry OTLP endpoint
- [ ] Health check ping từ external monitor
- [ ] Disaster recovery test snapshot/restore
- [ ] Load test mục target 99.9% SLA

---

## 12. Kế hoạch nâng cấp lên chuẩn hệ thống lớn (Roadmap)

### Kiến trúc (3-6 tháng)
- Tách `IBookingFactory` chuẩn hóa create booking từ 4 entry points
- Áp dụng CQRS với MediatR cho command Booking/Payment để rõ ràng intent
- Split `BookingController` (1144 lines) thành customer-facing + admin + payment-callback
- Áp dụng `Result<T>` pattern thay throw exception cho business validation (đã thấy `BookingTransitionResult` — mở rộng)
- Refactor `PayOSPaymentService` (1130 lines) thành: `PayOSCheckoutCreator`, `PayOSWebhookHandler`, `PayOSReconciler`, `PayOSCancellation`

### UI/UX (1-3 tháng)
- Component-based design system (`Views/Shared/Components/`) cho card xe / badge status / toast / modal
- Mobile-first redesign cho Customer flow
- Loading skeleton thay vì blank
- Empty state cho mọi list page
- Accessibility WCAG 2.1 AA (alt text, ARIA label, keyboard navigation, contrast)
- Dark mode admin theme
- i18n complete (`Resources/`)
- Toast unified library (đã dùng SweetAlert — chuẩn hoá)

### Database (1-3 tháng)
- Read replica cho catalog browsing
- Partitioning AuditLog by Timestamp
- Archive bookings > 2 years
- Add table comments cho lookup tables
- Document FK constraint deletion behavior bằng SQL doc
- Migration squash khi production stable

### Security (1-3 tháng)
- WAF / Cloudflare Bot Fight Mode trước backend
- Move tất cả secret sang Azure Key Vault / AWS Secrets Manager / Hashicorp Vault
- Annual security audit + penetration test
- BCrypt → Argon2id
- Implement OAuth2 PKCE cho mobile clients
- API key rotation pipeline
- IP allowlist cho admin endpoints (optional)
- Subresource Integrity cho CDN scripts

### Business logic (3-6 tháng)
- Rule engine cho pricing / discount (NRules / open-policy-agent)
- Dynamic late fee tiered
- Insurance policy management (currently hardcoded 150k/300k)
- Multi-currency support
- Promotion stackability matrix
- Wallet / credit usage in checkout

### Reporting (1-3 tháng)
- Domain events → projection to read model (analytics)
- Power BI / Metabase dashboard
- Daily reconciliation report PayOS vs PaymentLedger
- Driver utilization heatmap
- Car ROI per location

### Testing (continuous)
- Unit coverage target ≥ 80% cho Services
- Integration coverage target ≥ 60% cho Controllers
- Contract test với PayOS sandbox
- Snapshot test cho hợp đồng PDF
- Mutation testing (Stryker.NET)

### Documentation (1-3 tháng)
- ADR cho mỗi quyết định lớn (đã có `docs/ADR/`)
- OpenAPI / Swagger cho public API
- Onboarding runbook
- Customer journey diagram (mermaid)
- Incident response runbook

### Monitoring / logging (1-3 tháng)
- OpenTelemetry enable production (đã infrastructure-ready)
- Grafana dashboard (booking conversion funnel, PayOS success rate, error rate, latency)
- Sentry / equivalent for error tracking
- Structured logging Serilog → Loki
- Alert: deposit refund job failure, hold expire backlog, PayOS amount mismatch spike

---

## Status legend
- **Fixed** — đã sửa direct trong Pass 1 (xem mục **Safe fixes applied** ở cuối)
- **Pending** — đã xác định, chưa sửa, có ID để track trong `CAR_RENTAL_SYSTEM_TASKS.md`
- **Blocked** — chờ staging / hosting evidence

## Safe fixes applied trong Pass 1 (verified bằng `dotnet build` — PASS, 0 warnings 0 errors)

| BUG | Files | Tóm tắt fix |
|-----|-------|-------------|
| BUG-001 | [Controllers/BookingController.cs](Controllers/BookingController.cs), [Controllers/CartController.cs](Controllers/CartController.cs), [Controllers/EkycController.cs](Controllers/EkycController.cs) | Re-encode 3 file mojibake về UTF-8-BOM. Trước: `Báº¡n pháº£i...`. Sau: `Bạn phải...`. |
| BUG-002 / TASK-B06 | [Controllers/BookingController.cs](Controllers/BookingController.cs) lines ~465-595 | Xóa 130 dòng commented "atomic booking creation" dead block. Thay bằng note tham chiếu git history + module mới. |
| BUG-015 / TASK-S04 ext | [Controllers/AuthController.cs](Controllers/AuthController.cs) 2 chỗ, [Controllers/CartController.cs](Controllers/CartController.cs), [Views/Shared/_Layout.cshtml](Views/Shared/_Layout.cshtml) 2 chỗ | `User.Identity.IsAuthenticated` → `User.Identity?.IsAuthenticated == true`. CarController.cs + Views/Car/Details.cshtml đã có null-check sẵn — không cần đụng. |
| BUG-020 / TASK-P03 | [Services/CarService.cs](Services/CarService.cs) | Xóa dead cache pattern (`cached = null; if (cached != null)`). Thêm TODO marker cho TASK-P03 (wire ICatalogCacheService). |
| BUG-024 / TASK-S04 / TASK-U09 | [Views/Shared/_Layout.cshtml](Views/Shared/_Layout.cshtml), [Views/Shared/_AdminLayout.cshtml](Views/Shared/_AdminLayout.cshtml), [Views/Shared/_AreaManagerLayout.cshtml](Views/Shared/_AreaManagerLayout.cshtml) | `text: "@Html.Raw(TempData[X])"` → `text: @Json.Serialize(TempData[X]?.ToString() ?? "")`. Chặn stored XSS qua TempData. |
| BUG-025 / TASK-B05 | [Controllers/ContractController.cs](Controllers/ContractController.cs) | Add null check booking, EndKm ≥ StartKm validation, dùng `IBookingTransitionService.TransitionAsync(Completed)` thay vì `UpdateBookingStatusAsync("Completed")` bypass. |
| BUG-030 | [Services/BookingTransitionService.cs](Services/BookingTransitionService.cs) | Đổi 7 error message từ English sang Vietnamese có dấu. |
| BUG-031 / TASK-D01 | [Data/ApplicationDbContext.cs](Data/ApplicationDbContext.cs) | Xóa 3 khai báo index trùng (`PaymentLedgerEntry.IdempotencyKey`, `PaymentLedgerEntry.BookingID/EntryType/Status` partial, `PaymentWebhookEvent.Provider/EventKey`). |

**Build verification**: `dotnet build CarRentalSystem.csproj` → **Build succeeded. 0 Warning(s). 0 Error(s).** (Time: ~30s, .NET 10 Debug.)

**KHÔNG sửa**:
- Bất kỳ secret / connection string / API key (PayOS, FPT, Google, Microsoft, Groq, OpenAI, Redis, SMTP).
- Schema / migration.
- State machine semantics.
- Public API contracts.
- Xóa dữ liệu DB.

## Pass 2 / Pass 3 backlog
Xem `CAR_RENTAL_SYSTEM_TASKS.md` mục "Audit Coverage Tasks".
