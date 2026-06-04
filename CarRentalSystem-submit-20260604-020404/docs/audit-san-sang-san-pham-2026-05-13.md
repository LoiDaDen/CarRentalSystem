# Audit san sang san pham - he thong cho thue xe tu lai

Ngay audit: 2026-05-13

Cap nhat sau implementation production MVP foundation:

- `dotnet build --no-restore`: thanh cong, 0 warning, 0 error.
- `dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --no-restore`: thanh cong 146/146.
- Secret trong config tracked da duoc dua ve placeholder; van bat buoc rotate credential da tung lo.
- Cart checkout da duoc dua qua multi-hold atomic va PayOS webhook moi convert booking.
- Refund payout MVP da co model/service/job/migration, mac dinh payout disabled va fallback `PendingManual`.
- Da them CI, IIS `web.config`, release/deploy/smoke scripts va runbook deploy.
- Da them readiness script, secret scan script va bao cao hoan thanh tai `docs/bao-cao-hoan-thanh-production-mvp-2026-05-13.md`.
- Release package full moi nhat: `artifacts/release-20260513-183223/`.
- Ket luan moi: du an da tien tu candidate beta len gan MVP production foundation, nhung van can staging production-like, verify PayOS live, va refactor not trang thai/admin/driver truoc khi go production voi khach that.

Pham vi: toan bo project ASP.NET Core MVC hien tai, gom `Program.cs`, cau hinh, Controllers, Areas, Services, Models, Data, Middleware, BackgroundJobs, Views, ViewModels, wwwroot custom, migrations/schema o muc can thiet, test project ben canh va tai lieu trong `docs`. Khong danh gia vendor lib trong `wwwroot/lib`, `bin`, `obj` nhu code nghiep vu.

## Ket luan nhanh

Du an da vuot muc CRUD/demo thong thuong. He thong co nhieu thanh phan quan trong: dang ky/dang nhap, xac minh email, eKYC, xe, booking, gio hang, PayOS, booking hold, ledger thanh toan, hop dong dien tu, ban giao xe, partner, driver, admin, area management, Hangfire jobs, audit log, PWA va health check.

Tuy vay, du an chua phai san pham hoan chinh/hoan hao de dua vao van hanh that. Muc san sang hien tai: khoang 65-70% neu tinh theo chuc nang co san, nhung chi khoang 45-55% neu tinh theo tieu chuan san pham cho thue xe tu lai that su, vi cac luong tien/giu cho/trang thai/phap ly/bao hiem/hoan coc/chong gian lan chua dong nhat va test dang khong chay.

## Xac minh ky thuat

- `dotnet build --no-restore`: thanh cong, 0 warning, 0 error.
- `dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --no-restore`: that bai o compile-time.
- Tong code duoc do nhanh, bo qua `bin`, `obj`, generated migrations va vendor lib: khoang 59.852 dong `.cs/.cshtml/.js/.css`.
- Co 306 file C#, 139 view Razor, 24 file JS, 20 file CSS, nhieu migration va tai lieu.

Loi test hien tai:

- `AuthControllerTests` goi constructor cu, thieu `IReferralService`.
- `BookingControllerTests` goi constructor cu, thieu `IBookingQuoteService`.
- Test goi `BookingController.Book(...)` thieu tham so `SelectedAddons`.
- Test `Register` con truyen `User`, trong khi controller hien dung `RegisterViewModel`.

## Diem da lam tot

- Co global antiforgery mac dinh trong MVC.
- Co `ReservationService`, `BookingHold`, timeout hold va job expire hold.
- PayOS da co unique order code, webhook signature verify, amount verification, ledger va event idempotency.
- Co `BookingTransitionService` voi precondition cho mot so transition quan trong.
- Co eKYC/OCR/face compare, PII encryption, PII hash de check trung, PIN xem PII.
- Co secure upload vao `App_Data/SecureUploads` thay vi public folder cho eKYC.
- Co phan quyen role, area management, partner/driver workflow.
- Co hangfire jobs, health check, audit log, CSP/security headers o muc co ban.
- Co soft delete cho nhieu entity va index DB cho mot so query quan trong.

## P0 - Can xu ly truoc khi go production

1. Secret dang nam trong file cau hinh tracked

`appsettings.Development.json` dang co connection string remote DB that va file nay dang tracked trong git. `.gitignore` cung co rule unignore `appsettings.Development.json`. Can rotate credential, dua ra user-secrets/env/secret manager va chi commit placeholder.

2. Test project dang khong build

Main app build duoc, nhung test suite khong compile. Khi test khong chay, moi thay doi booking/payment/eKYC deu khong co hang rao an toan. Can cap nhat constructor mocks, action signatures va chay lai unit/integration tests.

3. Cart/multi-car checkout chua dung chung ReservationService

Single booking da tao hold truoc khi thanh toan. Cart checkout van validate rieng, tao `BookingTransaction`, serialize snapshot, xoa cart item, chua tao hold cho tung xe. Neu user bo thanh toan hoac co user khac dat trung, du lieu de lech. Can tao multi-hold atomic cho cart.

4. Cart tinh gia add-on bi nguy co cong hai lan

`CartController.ToggleAddon` da cong add-on vao `EstimatedPrice`, nhung `CheckoutAll` lai tinh `itemTotal = item.EstimatedPrice + addonTotal`. Ket qua co the thu thua tien.

5. eKYC luu so CCCD/GPLX ma hoa nhung khong dong bo hash trong `EkycController`

Trong flow eKYC submit, code check trung bang hash nhung khi luu ket qua OCR chi set `CitizenId` va `DrivingLicenseNumber`, khong set `CitizenIdHash`/`DrivingLicenseHash`. `EligibilityService` lai yeu cau hash khong rong. Tai khoan co the duoc `Verified` nhung khong eligible hoac check trung bi sai sau OCR.

6. API tracking nhan du lieu xe dang anonymous

`TrackingController.ReceiveData` cho `[AllowAnonymous]` va khong thay API key/HMAC/device token. Bat ky ai cung co the spam toa do, tao alert sai, lam nhieu tracking record hoac gia mao hanh trinh.

7. Admin API search user/location khong co authorize

`Areas/Admin/Controllers/AreaApiController.cs` co route `/admin/api/users` va `/admin/api/locations`, tra ve ten/email role user va location, nhung khong co `[Authorize]`/`RoleAuthorize`. Day la ro ri du lieu noi bo.

8. CSRF bi tat o nhieu endpoint authenticated

`CartController` tat antiforgery cho nhieu POST dang thay doi gio hang; `AuthController` tat antiforgery cho PIN PII/backfill hash; webhook PayOS co ly do ngoai le, nhung cart/PII/browser JSON nen dung antiforgery header.

9. State machine chua duoc ap dung dong nhat

Co `BookingTransitionService`, nhung driver dashboard, admin confirm deposit, manual cart transaction, long-term rental van tu set status/trang thai xe/deposit. San pham that can moi thay doi booking/car/payment/deposit/driver di qua mot command/transition service.

10. Refund moi la manual/internal, chua la dong tien that

`DepositRefundJob` tao refund request sau 15 ngay, `PaymentLedgerService.MarkRefundPaidAsync` danh dau manual paid. Chua co tich hop refund PayOS/bank, reconciliation, retry, failure state, duyet khau tru damage/late/fuel.

## P1 - Rui ro nghiep vu lon

- Long-term rental khong dung `ReservationService`; chi check status `Confirmed/Active/CheckedOut`, bo sot hold/unpaid/pending theo constant moi.
- Long-term `SubmitRequest` dua `FullName`/`Phone` qua query string khi redirect sang preview, de lo PII trong URL/log/browser history.
- Partner edit xe bind truc tiep entity `Car` va `_db.Cars.Update(car)`, co nguy co overposting field khong duoc phep.
- Driver dashboard tu set booking/car status khi start/complete trip, khong qua `BookingTransitionService`, khong bat buoc handover/check-in/check-out.
- Admin `UpdateStatus` nhan string status tu request; transition service co validate mot phan, nhung UI/API van mo cua cho trang thai bat thuong va khong surface loi ro rang.
- Area/admin booking list lay all bookings roi filter in-memory, khong tot cho performance va de lo du lieu neu sau nay them endpoint JSON/pagination.
- SecureFile co chong traversal, nhung `folder` van la user input; nen whitelist folder va set `Cache-Control: no-store`.
- SnapshotService snapshot ca `Users`, contracts, bookings, PII encrypted/password hash vao DB va cho download JSON; can ma hoa snapshot, han che download, retention va restore co transaction/clear-data strategy.
- SeedController co endpoint seed xe admin-only nhung khong guard production; du lieu demo khong nen co endpoint runtime tren prod.
- `NoWarn` trong csproj dang tat nhieu nullable warnings, trong khi code service interface co nhieu method khai bao non-null nhung co the return null.

## P2 - Thieu de thanh san pham cho thue xe tu lai dung nghia

- Chua co chinh sach huy/no-show/refund theo moc thoi gian, loai xe, partner, voucher, payment state.
- Chua co quy trinh bao hiem/boi thuong/tai nan: claim, police report, garage quote, approval, payout.
- Chua co settlement day du sau tra xe: late fee, fuel fee, cleaning fee, damage deduction, toll/parking/phat nguoi, lien ket vao ledger.
- Chua co partner payout ledger: doanh thu partner hien chu yeu tinh dashboard, chua co doi soat, ky thanh toan, invoice, tax/VAT.
- Chua co driver payout/commission/penalty ledger day du.
- Chua co kich ban dispute/khieu nai: khach vs chu xe vs tai xe vs admin.
- Chua co immutable PDF/hash cho hop dong va bang chung ban giao; signature hien moi la flag/time, chua co IP/user-agent/hash noi dung.
- Chua co license expiry/class validation that su cho GPLX va rule xe 7 cho/loai bang.
- Chua co KYC manual review queue chat luong cao: ly do reject, audit nguoi duyet, SLA.
- Chua co policy inventory cho bao duong/inspection dang kyem/bao hiem het han theo tung xe.
- Chua co dinh vi IoT co xac thuc thiet bi, replay protection, geofence thuc, GPS lost escalation.
- Chua co observability san pham: metrics, tracing, alerting, error tracking, job dashboard policy, log retention.
- Chua co CI/CD pipeline dang tin: build, test, migration check, secret scan, dependency scan.
- Chua co backup/restore production an toan: snapshot hien nam trong DB, restore add row bang reflection, khong phai disaster recovery chuan.

## P3 - Chat luong UX/UI va bao tri

- Nhieu text/comment bi mojibake khi xem qua terminal, can chuan hoa UTF-8 de tranh loi hien thi/maintain.
- Mot so view dung placeholder/demo/simulation, vi du admin tracking live locations tra random toa do.
- Anh xe seed dung Unsplash/generic, chua dam bao anh that cua xe doi tac/fleet.
- PWA co manifest/sw, nhung can audit offline behavior voi booking/payment de tranh thao tac offline sai.
- CSP con dung `unsafe-inline`/`unsafe-eval`, hop ly cho giai doan dev nhung chua ly tuong cho prod.
- CORS `AllowAnyOrigin/AnyHeader/AnyMethod` nen dong lai theo domain that.

## Danh gia module

Auth/account: kha day du ve login/register/email OTP/password reset/reCAPTCHA/lock/blacklist, nhung test gãy, rate limit con dua vao status redirect, PII PIN endpoint bo CSRF.

eKYC: co pipeline tot hon demo, nhung hash sync, license expiry/class, manual review, provider error handling va file validation chua dong nhat.

Xe/inventory: model nhieu field, co approval partner, soft delete, location, image; thieu inventory lock dong nhat cho cart/long-term/admin va thieu rule bao hiem/maintenance bat buoc.

Booking: single booking da tien bo voi hold+PayOS. Cart, manual payment, admin/driver/long-term con tach logic.

Payment/refund: PayOS webhook da tot hon, nhung refund/payout/reconciliation chua production-grade.

Contract/handover: co digital contract va handover photo/signature/odo/fuel. Chua co immutable evidence package, contract PDF/hash, counterparty signing policy ro rang, settlement ledger.

Partner/driver: co luong dang ky/duyet/hop dong/dashboard. Can whitelist model binding, payout ledger, availability lock, role/original-role policy ro hon.

Admin/area: nhieu man hinh quan tri, co role/area service. Can bit API chua authorize, day filter vao DB, route moi transition qua service.

Ops/security: co health check, audit, Hangfire, config guard. Can secret hygiene, CORS, tracking auth, CI, backup, monitoring.

## Thu tu uu tien de bien thanh san pham that

1. Rotate secrets, bo real credential khoi git, them secret scanning.
2. Sua test compile va bat CI build/test.
3. Hop nhat booking/cart/long-term/admin qua ReservationService + BookingTransitionService.
4. Sua eKYC hash sync va bat EligibilityService truoc hold/payment/pickup.
5. Bit CSRF/anonymous API: cart, PII PIN, area API, tracking device auth.
6. Sua cart add-on double count va tao multi-hold atomic.
7. Them refund/payout/settlement ledger that su.
8. Hoan thien legal evidence: contract hash/PDF, explicit signatures, IP/user-agent.
9. Them damage/fuel/late/toll/violation workflow.
10. Dong CORS/CSP, snapshot security, seed/prod guard, backup/monitoring.

## Ket luan

Du an co nen tang lon va dang o muc prototype nang/candidate beta noi bo. Chua nen go production voi khach hang that va tien that neu chua xu ly P0/P1. Khong the goi la hoan chinh hay hoan hao, nhung da co khung tot de dua len san pham neu tap trung vao dong nhat nghiep vu, an toan thanh toan, phap ly, hoan coc, test va van hanh.
