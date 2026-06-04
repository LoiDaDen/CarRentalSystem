# Release checklist - production MVP foundation

Ngay cap nhat: 2026-05-13

## Trang thai hien tai

- Build local: `dotnet build --no-restore` pass.
- Test local: `dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --configuration Release --no-restore /p:UseAppHost=false` pass 154/154.
- Migration script check: `dotnet ef migrations script --idempotent --no-build` pass.
- Readiness script: `scripts/Test-ProductionReadiness.ps1` pass, report tai `artifacts/readiness-20260513-183153.txt`.
- Release package script: `scripts/New-ProductionMvpRelease.ps1` pass day du build + test + migration + publish, package tai `artifacts/release-20260513-183223/`.
- DB dev da duoc dua lai migration `20260513015851_AddPasswordResetToken` sau thao tac EF remove nham.

Ghi chu local: build/publish release da chuyen sang scratch output rieng trong release package va `UseAppHost=false`, nen khong con phu thuoc vao `bin/Release`/`obj/Release` dang bi process khac giu file.

## Da hoan thanh trong vong production MVP foundation

- Xoa secret khoi config tracked, dung placeholder trong `appsettings*.json`.
- Them `ConfigurationGuard` fail-fast cho Production.
- Dong CORS theo allowlist.
- Them DataProtection key path cho IIS/Production.
- Them tracking HMAC + timestamp + device id.
- Them privileged MFA bang email OTP cho Admin/Manager/Staff trong Production.
- Them rate limit rieng cho tracking ingest.
- Bao ve `/admin/api/users` va `/admin/api/locations`.
- Tat runtime seed/demo endpoint ngoai Development.
- Them no-store cho secure file.
- Cart POST va PII PIN POST dung antiforgery token.
- eKYC OCR sync hash CCCD/GPLX.
- Cart checkout dung multi-hold atomic qua `ReservationService`.
- PayOS webhook la diem convert cart thanh bookings.
- Return URL PayOS chi dua ve trang dang xac nhan.
- Add-on cart khong ghi de base price va khong double count khi checkout.
- Them `RefundPayout`, refund service, payout provider, polling job.
- Payout disabled hoac loi permission chuyen `PendingManual`.
- CI workflow: restore/build/test/migration script/secret scan.
- IIS `web.config`.
- Release/deploy/smoke test scripts.
- Secret scan script local: `scripts/Test-TrackedSecrets.ps1`.
- One-shot readiness script: `scripts/Test-ProductionReadiness.ps1`.
- IIS environment variable helper: `scripts/Set-IisEnvironmentVariables.ps1`.
- Project exclude ro `bin/`, `obj/`, `artifacts/` khoi compile item.
- Deploy runbook Windows IIS + SQL Server.

## Viec bat buoc truoc staging

- Rotate DB password da tung xuat hien trong config.
- Cau hinh secret bang IIS environment variables.
- Tao package bang `scripts/New-ProductionMvpRelease.ps1`.
- Apply `database-migration-idempotent.sql` tren staging.
- Chay `scripts/Test-StagingSmoke.ps1`.
- Test PayOS webhook voi domain staging public HTTPS.
- Giu `PayOS:PayoutEnabled=false` cho toi khi payout duoc verify.

## Viec bat buoc truoc production

- Backup DB production.
- Backup site folder IIS.
- Chay staging production-like it nhat mot vong thanh cong.
- Xac nhan `/health` OK.
- Xac nhan admin API anonymous bi chan.
- Xac nhan tracking thieu HMAC bi chan.
- Xac nhan Admin/Manager/Staff bi bat MFA khi dang nhap production.
- Xac nhan cart 3 xe neu 1 xe fail thi rollback hold.
- Xac nhan duplicate PayOS webhook idempotent.
- Xac nhan amount mismatch bi reject.
- Xac nhan refund payout disabled => `PendingManual`, khong danh dau `Refunded`.

## Rui ro con lai sau vong nay

- Chua refactor 100% moi flow admin/driver/manual qua `BookingTransitionService`.
- Chua co UI day du cho khach/admin nhap thong tin bank refund.
- PayOS payout signature can test voi merchant account that truoc khi bat live.
- Long-term rental da check availability qua `ReservationService`, nhung chua chuyen hoan toan thanh inquiry/quote + hold/deposit.
- Snapshot/backup DB noi bo van can chinh sach retention/ma hoa rieng.
- CSP con dung `unsafe-inline`/`unsafe-eval`, nen giam dan sau khi UI on dinh.

## Lenh chot release

```powershell
dotnet build --no-restore
dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --no-restore
.\scripts\New-ProductionMvpRelease.ps1
```

## Go/no-go ngan gon

Go staging neu:

- Build/test pass.
- Migration script tao duoc.
- Package publish tao duoc.
- Secret staging da cau hinh bang env vars.

Go production neu:

- Staging pass full smoke test.
- PayOS live webhook pass.
- Backup/rollback da tap duyet.
- Team da dong y chua bat payout auto neu PayOS payout chua verified.
