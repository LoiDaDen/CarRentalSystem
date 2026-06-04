# Go Production Roadmap - CarRentalSystem

Ngay lap: 2026-05-13

Cap nhat sau vong implementation production MVP foundation:

- `dotnet build --no-restore` da pass.
- `dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --no-restore` da pass 146/146.
- Da them runbook deploy: `docs/deploy-runbook-windows-iis-sqlserver.md`.
- Da them release checklist: `docs/release-checklist-2026-05-13.md`.
- Da them script dong goi release: `scripts/New-ProductionMvpRelease.ps1`.
- Da them script deploy staging IIS: `scripts/Deploy-IisStaging.ps1`.
- Da them script smoke test staging: `scripts/Test-StagingSmoke.ps1`.
- Bao cao hoan thanh local: `docs/bao-cao-hoan-thanh-production-mvp-2026-05-13.md`.
- Release package full moi nhat: `artifacts/release-20260513-183223/`.
- Readiness report moi nhat: `artifacts/readiness-20260513-183153.txt`.
- Van chua nen go production that neu chua rotate secret, test staging IIS + SQL Server, verify PayOS webhook live va giu payout auto disabled cho den khi provider payout duoc kiem chung.

Muc tieu tai lieu nay: gom toan bo y kien, y tuong va checklist de dua web cho thue xe tu lai tu muc "prototype/beta noi bo" len muc "production-grade" co the chay voi khach hang that, xe that, tien that va tranh cac loi nghiem trong ve tien, phap ly, an toan va van hanh.

Ghi chu thuc te: khong co he thong nao "hoan hao tuyet doi". Muc tieu dung la "production-grade": build/test on dinh, duoc monitor, co quy trinh xu ly su co, nghiep vu dong nhat, tien va phap ly co doi soat, co kha nang rollback/backup, va cac rui ro lon duoc kiem soat.

## 1. Tuyen ngon san pham can dat

He thong can tra loi duoc 7 cau hoi truoc khi go production:

1. Khach dat xe co bi double booking khong?
2. Tien thanh toan, coc, hoan coc, khau tru co doi soat duoc khong?
3. Khach thue co du dieu kien phap ly/giay phep lai xe khong?
4. Hop dong va bang chung ban giao co du suc dung khi tranh chap khong?
5. Chu xe/doi tac/tai xe/admin co bi vuot quyen khong?
6. He thong co phat hien va xu ly su co xe/thanh toan/hop dong/tre han khong?
7. Khi loi xay ra, team co biet ngay, khoanh vung duoc, rollback duoc, va khong mat du lieu khong?

Neu chua tra loi chac duoc 7 cau tren, chua nen go production.

## 2. Muc do san sang hien tai

Hien du an da co nen tang tot:

- MVC app build duoc tren .NET 10.
- Co auth, email verification, password reset, eKYC, PayOS, booking, cart, long-term rental, partner, driver, admin.
- Co `ReservationService`, `BookingHold`, PayOS transaction, payment ledger, webhook event.
- Co contract, handover, audit log, Hangfire jobs, health check, area management.

Nhung cac diem san pham that con thieu:

- Test project dang khong compile.
- Secret DB dang nam trong config tracked.
- Booking/cart/long-term/manual/admin/driver chua dung chung mot luong nghiep vu.
- Refund/payout/settlement con manual va noi bo.
- eKYC hash sync bi thieu trong flow submit.
- Tracking API va area API con ho bao mat.
- Chua co quy trinh tranh chap, damage, insurance, partner payout, driver payout production-grade.

## 3. Nguyen tac kien truc can thong nhat

1. Mot nguon su that cho inventory

Tat ca luong dat xe phai di qua `ReservationService`:

- Single booking.
- Cart/multi-car booking.
- Long-term rental.
- Admin tao booking thu cong.
- Partner/staff tao booking ho khach.
- Gia han/huy/doi lich.

Khong controller nao tu viet overlap query rieng.

2. Mot nguon su that cho trang thai

Tat ca thay doi `BookingStatus`, `DepositStatus`, `PaymentStatus`, `CarStatus`, `DriverBooking.Status` phai di qua transition service:

- `BookingTransitionService`.
- `PaymentTransitionService` hoac mo rong `PaymentLedgerService`.
- `DriverAssignmentService`.
- `CarAvailabilityService`.

Khong action nao set string truc tiep nhu `"Completed"`, `"Holding"`, `"Active"` neu khong qua service.

3. Mot nguon su that cho tien

Moi dong tien phai co ledger:

- Thu phi giu cho.
- Thu tien thue con lai.
- Thu coc tai san neu co.
- Khau tru tre han/ve sinh/xang/hong hoc/phat nguoi.
- Hoan coc.
- Payout cho partner.
- Payout cho driver.
- Phi nen tang/VAT/invoice.

Booking chi la nghiep vu; ledger moi la tai chinh.

4. Khong tin client

Client co the gui sai gia, sai ngay, sai xe, sai driver, sai addon. Server phai tinh lai tat ca:

- Gia thue.
- Bao hiem.
- Add-on.
- Driver fee.
- Delivery fee.
- Voucher.
- Membership.
- Referral.
- Deposit.

5. Dung state machine thay vi string lung tung

Can co bang/enum allowed transitions:

- Draft -> Unpaid -> Pending -> Confirmed -> Active -> Completed.
- Draft/Unpaid/Pending -> Expired/Cancelled.
- Confirmed -> Cancelled/PendingRefund.
- Active -> Completed/UnderReview.
- Completed -> PendingRefund -> Refunded/Deducted/Disputed.

Moi transition can luu:

- From status.
- To status.
- Actor user id.
- Actor role.
- Reason.
- Timestamp.
- Preconditions da pass.
- Audit id.

## 4. P0 - Viec bat buoc truoc production

### 4.1 Secret va config

Can lam ngay:

- Rotate password DB dang nam trong `appsettings.Development.json`.
- Xoa credential that khoi git history neu repo da share ra ngoai.
- Dung User Secrets cho local, environment variables/secret manager cho deploy.
- `appsettings.Development.json` chi nen la placeholder local hoac bo khoi tracked.
- Them secret scanning vao CI: gitleaks/trufflehog.
- Tach ro config local/staging/production.

Checklist done:

- Khong file tracked nao co password/API key/token.
- Production fail-fast neu thieu PayOS/Recaptcha/FPT/SMTP.
- Co tai lieu cach cau hinh secret.

### 4.2 Sua test project

Can sua:

- Cap nhat `AuthControllerTests` theo constructor moi.
- Cap nhat `BookingControllerTests` theo constructor/action moi.
- Doi test register tu `User` sang `RegisterViewModel`.
- Them mock cho `IReferralService`, `IBookingQuoteService`, `IRecaptchaService`, `ApplicationDbContext`.
- Dam bao `dotnet test` chay duoc.

Checklist done:

- `dotnet build` pass.
- `dotnet test` pass.
- CI chay build/test tu dong tren moi PR.
- Khong merge code neu test fail.

### 4.3 Dong nhat reservation/hold

Can lam:

- Cart checkout tao hold cho tung xe trong mot transaction serializable.
- Long-term rental tao hold truoc khi tao booking/payment.
- Admin/manual booking cung dung hold service.
- Hold co `HoldToken`, `ExpiresAt`, `QuoteSnapshotJson`, `CreatedBy`, `Source`.
- Expire job release hold va audit.
- Payment success chi tao booking tu hold hop le.
- Neu tien ve sau khi hold het han: chuyen `UnderReview`, khong auto tao booking.

Checklist done:

- Khong con overlap query rieng trong controller.
- Hai user dat cung xe cung gio: chi 1 nguoi giu cho duoc.
- Cart 3 xe: neu 1 xe fail thi rollback toan bo hold.

### 4.4 Sua cart checkout

Can lam:

- Sua loi add-on double count: `EstimatedPrice` phai ro la base hay total.
- Khong xoa cart item truoc khi co terminal payment state.
- Snapshot cart phai co version va hash.
- Gia trong snapshot chi dung de audit; khi tao booking can verify lai voi quote engine.
- Cart transaction phai co PayOS/ledger rieng hoac multi-hold payment.

Checklist done:

- User bo thanh toan: cart van con hoac recover duoc.
- User thanh toan tre: booking khong tao neu hold het han.
- Add-on khong bi tinh hai lan.

### 4.5 eKYC va eligibility

Can lam:

- Trong `EkycController.Submit`, khi luu `CitizenId` va `DrivingLicenseNumber` phai set hash tuong ung.
- Tat ca booking/hold/payment/pickup phai goi `EligibilityService`.
- Eligibility can check:
  - Email verified.
  - eKYC verified.
  - CCCD hash co.
  - GPLX hash co.
  - License class hop le voi xe.
  - License expiry chua het han.
  - Account khong locked/blacklisted.
  - No unpaid debt/dispute severe.
- Them manual review queue cho eKYC flagged/pending.

Checklist done:

- User chua eKYC khong tao hold duoc.
- User eKYC verified nhung thieu hash khong xay ra.
- Duplicate CCCD/GPLX bi chan.

### 4.6 Bao mat endpoint

Can lam:

- Them authorize cho `/admin/api/users` va `/admin/api/locations`.
- Tracking API phai co device authentication:
  - API key per device.
  - HMAC signature.
  - Timestamp nonce chong replay.
  - Rate limit theo device.
- Bo `[IgnoreAntiforgeryToken]` khoi cart va PII PIN endpoints, hoac bat buoc antiforgery header cho AJAX.
- CORS dong lai theo domain production.
- Rate limit theo user + IP cho login, PIN, payment, eKYC, tracking.

Checklist done:

- Anonymous khong doc duoc admin API.
- Anonymous khong gui duoc tracking fake.
- CSRF test pass voi cart/PII.

## 5. P1 - Hoan thien nghiep vu tien va phap ly

### 5.1 Payment ledger day du

Can co cac bang/luong:

- `PaymentLedgerEntry`: append-only.
- `PaymentIntent`: PayOS checkout/request.
- `PaymentWebhookEvent`: idempotency.
- `RefundRequest`: duyet hoan tien.
- `DeductionRequest`: khau tru coc.
- `PartnerPayout`: thanh toan doi tac.
- `DriverPayout`: thanh toan tai xe.
- `Invoice`: hoa don/receipt.

Moi ledger entry can co:

- Provider.
- ProviderTransactionId.
- OrderCode.
- Amount.
- Currency.
- EntryType.
- Status.
- IdempotencyKey.
- PostedAt.
- ReconciledAt.
- Related booking/transaction/refund/payout.

Can co reconciliation screen:

- Tien PayOS bao ve.
- Tien DB ghi nhan.
- Payment pending qua lau.
- Webhook duplicate.
- Amount mismatch.
- Manual override co ly do.

### 5.2 Refund/hoan coc

Can co workflow:

1. Booking completed.
2. Tao return handover.
3. Settlement calculator tinh late/fuel/cleaning/damage/toll/violation.
4. Neu khong co khau tru: chuyen `PendingRefund`.
5. Sau holding period, tao refund request.
6. Admin/finance approve.
7. Goi refund/payout provider hoac ghi manual bank transfer.
8. Mark paid/reconciled.
9. Customer nhan thong bao.

Trang thai nen co:

- Holding.
- PendingSettlement.
- PendingRefund.
- RefundApproved.
- RefundProcessing.
- Refunded.
- Deducted.
- PartiallyRefunded.
- Disputed.
- Failed.

### 5.3 Damage/fuel/late/cleaning settlement

Can them:

- `SettlementCase`.
- `SettlementLineItem`.
- `DamageReport`.
- `ViolationTicket`.
- `TollCharge`.
- `FuelCharge`.
- `LateReturnCharge`.

Moi line item co:

- Amount.
- Evidence.
- Actor.
- ApprovedBy.
- CustomerVisibleReason.
- Status.

UI can co:

- Staff nhap fuel/odo/photo khi return.
- System tinh late fee tu `ActualReturnDate`.
- Admin them damage quote.
- Customer xem va chap nhan/khieu nai.
- Ket qua day vao ledger.

### 5.4 Hop dong production-grade

Can nang cap:

- Tao PDF hop dong tai thoi diem ky.
- Hash noi dung hop dong.
- Luu IP/user-agent/time/signature image/actor id.
- Khong auto-sign counterparty neu chua co chinh sach ro.
- Dung verified identity snapshot, khong tin field user nhap luc ky.
- Contract versioning theo policy version.
- Moi sua doi hop dong tao amendment, khong overwrite.

Can co hop dong rieng:

- Customer rental contract.
- Partner contract.
- Driver contract.
- Long-term rental contract.
- Damage settlement agreement.

### 5.5 Insurance va liability

Can bo sung:

- Thong tin bao hiem theo xe: nha bao hiem, policy number, expiry, coverage.
- Dieu kien xe duoc cho thue: dang kiem, bao hiem, bao duong.
- Insurance tier theo booking.
- Claim workflow neu tai nan.
- Tai lieu can upload: bien ban cong an, hinh anh, quote garage, hoa don sua chua.
- Rule ai tra phan nao: khach/partner/platform/insurance.

## 6. P2 - Hoan thien marketplace partner va driver

### 6.1 Partner

Can lam:

- Partner car add/edit dung view model, whitelist field.
- Material change cua xe phai ve status `PendingApproval`.
- Partner revenue khong tinh bang dashboard tam; phai co ledger/payout.
- Moi booking completed tao receivable/payable cho partner.
- Payout monthly co export, status, paid date, bank reference.
- Partner co dispute center.
- Partner co policy anh that cua xe, giay to xe, bao hiem, dang kiem.

Partner payout formula can la:

- Gross rental.
- Tru voucher/platform-funded hay partner-funded.
- Tru platform fee.
- Tru insurance/add-on/driver neu khong thuoc partner.
- Tru penalty/refund/damage share.
- Net payout.

### 6.2 Driver

Can lam:

- Driver availability dung hold/schedule, khong chi `IsAvailable`.
- Driver assignment phai check overlap trong transaction.
- Driver start/complete trip phai qua transition service.
- Driver khong duoc set booking completed neu thieu handover/return evidence.
- Driver payout ledger.
- Driver contract active moi duoc nhan booking.
- Driver license expiry/class/background check.
- Driver no-show/cancel policy.

### 6.3 Admin/Manager/Staff

Can lam:

- Area filter day vao query DB.
- Tat ca admin mutation co antiforgery.
- Manager/staff policy theo area/resource, khong chi role.
- Admin action nguy hiem can ly do va audit:
  - Confirm manual payment.
  - Refund paid.
  - Deduct deposit.
  - Force cancel.
  - Restore snapshot.
  - Change role.
  - Verify eKYC manually.
- Admin PIN nen thay bang step-up authentication tot hon:
  - Re-enter password.
  - TOTP.
  - WebAuthn.

## 7. P3 - Van hanh production

### 7.1 CI/CD

Pipeline toi thieu:

- Restore/build.
- Unit tests.
- Integration tests.
- EF migration script generate/check.
- Secret scan.
- Dependency vulnerability scan.
- Format/lint.
- Publish artifact.
- Deploy staging.
- Smoke test staging.
- Manual approval production.
- Deploy production.
- Health check.
- Rollback plan.

### 7.2 Database

Can co:

- Migration khong tu chay bat buoc tren startup production.
- Backup schedule.
- Restore drill hang thang.
- Point-in-time recovery neu dung managed DB.
- Index review cho booking availability.
- Retention:
  - Audit log.
  - Tracking.
  - Webhook event.
  - eKYC files.
  - Contract PDFs.
- Data classification:
  - Public.
  - Internal.
  - Sensitive.
  - PII.
  - Payment.

### 7.3 Observability

Can co dashboard:

- Request rate/error rate/latency.
- Booking conversion funnel.
- PayOS pending/success/fail/mismatch.
- Hold created/expired/converted.
- Double booking prevention events.
- Hangfire job success/fail.
- eKYC success/fail/provider error.
- Tracking ingestion rate.
- Refund pending aging.
- Admin high-risk actions.

Alert can co:

- Payment webhook fail.
- Amount mismatch.
- Hold conversion fail sau payment.
- DB connection fail.
- Job fail 3 lan lien tiep.
- Disk storage gan day.
- Tracking spam.
- Error rate tang dot bien.

### 7.4 Backup va snapshot

Snapshot hien tai nen chi dung noi bo/dev. Production can:

- Backup DB native.
- Backup files/object storage.
- Snapshot ma hoa.
- Khong download raw JSON chua PII neu khong co step-up auth.
- Restore vao staging truoc, khong restore truc tiep production neu khong co runbook.
- Restore phai co transaction/clear existing/foreign key strategy.

### 7.5 File storage

Can chuyen upload sang object storage/private blob:

- eKYC docs.
- Contract PDFs.
- Handover photos.
- Driver avatars.
- Car images.

Can co:

- Signed URL ngan han.
- Malware scanning.
- MIME magic validation.
- File size limit.
- Image recompression/strip EXIF.
- Retention/deletion policy.
- No public listing.

## 8. Bao mat can dat

Checklist:

- HTTPS only.
- HSTS production.
- CORS allowlist.
- CSP khong `unsafe-eval`; giam dan `unsafe-inline`.
- Cookie Secure, HttpOnly, SameSite hop ly.
- CSRF cho browser mutation.
- AuthZ per resource.
- Rate limit login/PIN/eKYC/payment/tracking.
- Device auth cho IoT.
- Secrets khong trong repo.
- PII encryption + key persistence/rotation plan.
- Audit high-risk actions.
- No sensitive docs public.
- Error page khong leak stack trace production.
- Security headers.
- Dependency scan.
- Pen test truoc production.

## 9. UX san pham can hoan thien

Customer:

- Dat xe ro rang theo buoc: chon xe -> quote -> hold -> pay -> contract -> pickup -> return -> refund.
- Hien countdown hold.
- Hien phi minh bach: tien thue, bao hiem, phi dich vu, delivery, addon, driver, voucher, tien giu cho, coc tai san, con lai.
- Trang booking detail co timeline.
- Trang hoan coc co status va ETA.
- Trang dispute/ho tro.
- Thong bao email/SMS/in-app cho moi moc quan trong.

Partner:

- Onboarding co checklist giay to xe.
- Trang duyet xe co ly do reject.
- Calendar xe.
- Revenue/payout statement.
- Damage/dispute center.

Driver:

- Calendar lich chay.
- Check-in/check-out task list.
- Earnings/payout.
- Contract status.

Admin:

- Dashboard viec can lam hom nay:
  - eKYC pending.
  - Partner pending.
  - Car approval.
  - Refund pending.
  - Damage disputes.
  - Payment mismatch.
  - Overdue returns.
- Action nguy hiem co confirm + reason.
- Search/filter server-side.

## 10. Test scenarios bat buoc

Reservation:

- Hai user dat cung xe cung khoang gio cung luc.
- Cart co 3 xe, 1 xe bi hold boi user khac -> rollback.
- Payment ve sau hold expired.
- Hold expired job release dung.

Payment:

- Webhook duplicate.
- Webhook wrong amount.
- Webhook valid signature nhung order khong ton tai.
- User cancel PayOS.
- Success callback den truoc webhook.
- Manual payment confirm khong duoc vuot precondition.

eKYC:

- OCR thanh cong -> hash duoc set.
- CCCD trung -> reject.
- GPLX trung -> reject.
- Face mismatch -> reject.
- Provider down -> pending/flagged, khong verified sai.
- User chua eKYC khong dat xe duoc.

Booking state:

- Pending -> Confirmed can paid.
- Confirmed -> Active can contract + check-in.
- Active -> Completed can check-out.
- Completed -> refund workflow.
- Cancel tung status.

Cart:

- Add-on khong double count.
- Gia server khong tin client.
- Cart abandoned recover.

Partner/driver:

- Partner overpost status/owner bi chan.
- Driver overlap bi chan.
- Driver khong the complete thieu handover.
- Staff khong xem/sua booking ngoai area.

Security:

- Anonymous khong doc admin api.
- Anonymous khong push tracking.
- CSRF cart fail neu thieu token.
- PIN brute force bi limit.

Ops:

- DB down -> health fail.
- PayOS config thieu production -> app fail-fast.
- Hangfire job fail -> alert.

## 11. Acceptance criteria truoc ngay go-live

Khong go-live neu con bat ky dieu nao sau:

- Con secret that trong repo.
- `dotnet test` khong pass.
- Co anonymous endpoint doc/sua du lieu noi bo.
- Cart/single/long-term dung availability khac nhau.
- Payment webhook chua idempotent/verify amount.
- eKYC verified nhung khong set hash.
- Admin co the confirm booking unpaid.
- Driver co the complete trip thieu return handover.
- Hoan coc chi set status nhung khong co ledger/request.
- Chua co backup/restore runbook.
- Chua co monitoring loi thanh toan.

Go-live duoc neu:

- P0 done 100%.
- P1 done it nhat cac phan payment/refund/state/security.
- CI xanh.
- Staging smoke test xanh.
- Co rollback plan.
- Co support/admin runbook.
- Co nguoi truc production trong giai doan dau.

## 12. Lo trinh de lam

### Sprint 1 - Safety gate

- Rotate secret.
- Fix tests compile.
- Add CI.
- Authorize admin area API.
- Add tracking device auth.
- Remove CSRF ignore for cart/PII.
- Fix eKYC hash sync.

### Sprint 2 - Reservation unification

- Multi-hold for cart.
- Long-term hold.
- Admin/manual booking through hold.
- Remove duplicate overlap logic.
- Add concurrency tests.

### Sprint 3 - Payment and refund

- Finish payment ledger.
- Refund request approval.
- Manual refund reconciliation.
- Payment mismatch dashboard.
- Cart PayOS flow.

### Sprint 4 - State machine

- Enforce all booking/car/deposit/driver transitions.
- Refactor driver dashboard/admin booking actions.
- Add transition audit.
- Add precondition tests.

### Sprint 5 - Contract/handover/settlement

- Contract PDF/hash.
- Explicit signatures.
- Return settlement calculator.
- Damage/fuel/late/cleaning line items.
- Dispute workflow.

### Sprint 6 - Partner/driver finance

- Partner payout ledger.
- Driver payout ledger.
- Monthly statements.
- Commission rules.
- Payout export/reconciliation.

### Sprint 7 - Production ops

- Monitoring dashboards.
- Alert rules.
- Backup/restore drill.
- File storage migration.
- CSP/CORS hardening.
- Load test.
- Pen test.

## 13. Y tuong nang cap de web "xin" hon sau production

- Realtime booking calendar per car.
- Dynamic pricing theo ngay le, demand, lead time.
- Smart fraud score cho user/booking.
- OCR giay dang kiem/bao hiem xe cho partner.
- Auto route/geofence theo hanh trinh da khai bao.
- Mobile PWA task mode cho staff ban giao xe.
- QR check-in/check-out tai diem giao xe.
- Customer wallet.
- Partial payment/installment cho long-term rental.
- In-app chat gan voi booking/dispute.
- SLA dashboard cho support.
- AI assistant noi bo cho admin tra cuu booking/payment/dispute.
- Customer NPS sau moi trip.
- Partner quality score.
- Driver reliability score.
- Auto suggest maintenance theo odo/trip.
- Export accounting format.
- Multi-tenant/area-level operational dashboards.

## 14. Ket luan chien luoc

Du an nen di theo chien luoc:

1. Dong bang them feature lon trong ngan han.
2. Sua P0/P1 de he thong khong mat tien, khong double booking, khong lo PII.
3. Dua len staging production-like.
4. Chay pilot noi bo voi xe/test payment hoac sandbox.
5. Chay limited beta voi it xe, it khu vuc, co nguoi truc.
6. Sau khi metrics on dinh moi mo rong.

De "hoan chinh va gan hoan hao", diem quan trong nhat khong phai them nhieu man hinh nua, ma la lam cho cac luong san co tro nen dang tin: dat xe khong trung, tien khong lech, phap ly co bang chung, hoan coc co doi soat, quyen truy cap dung, va khi su co xay ra team biet cach xu ly.
