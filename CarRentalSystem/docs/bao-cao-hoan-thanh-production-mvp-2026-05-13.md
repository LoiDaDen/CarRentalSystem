# Bao cao hoan thanh production MVP foundation

Ngay bao cao: 2026-05-13

## Ket luan ngan gon

Da hoan thanh tat ca viec co the lam trong repo/local de dua CarRentalSystem toi moc **production MVP foundation**: code build/test xanh, co release package, co migration script idempotent, co runbook Windows IIS + SQL Server, co script deploy/smoke/secret scan/readiness, va cac loi P0 lon ve secret, cart hold, PayOS webhook, refund payout fallback, CSRF, tracking HMAC da duoc xu ly.

Chua the tu minh deploy production/staging that trong may nay vi con thieu thong tin ha tang: IIS site/app pool, domain staging HTTPS, SQL Server staging, secret PayOS/SMTP/FPT/Recaptcha va quyen truy cap server. Phan nay da duoc dong goi thanh script/runbook de chay ngay khi co thong tin.

## Artifact da tao

- Release package full: `artifacts/release-20260513-183223/`
- Publish folder: `artifacts/release-20260513-183223/publish/`
- Migration SQL: `artifacts/release-20260513-183223/database-migration-idempotent.sql`
- Manifest: `artifacts/release-20260513-183223/release-manifest.txt`
- Readiness report: `artifacts/readiness-20260513-183153.txt`
- Migration check SQL: `artifacts/migration-check-20260513-183153.sql`

## Ket qua verify

- PowerShell scripts parse: pass.
- Tracked secret scan: pass.
- `dotnet build`: pass, 0 warning, 0 error.
- `dotnet test`: pass 146/146.
- EF migration idempotent script: pass.
- Release package full: pass, `TestsRun=True`.
- Package contains `publish/CarRentalSystem.dll`: pass.
- Package contains `publish/web.config`: pass.
- Package contains `database-migration-idempotent.sql`: pass, size 156022 bytes.

## File/script moi quan trong

- `scripts/New-ProductionMvpRelease.ps1`: restore, build Release scratch, test, tao migration script, publish.
- `scripts/Test-ProductionReadiness.ps1`: chay parse script, secret scan, build, test, migration check.
- `scripts/Test-TrackedSecrets.ps1`: scan file tracked/untracked khong ignored de chan secret that.
- `scripts/Deploy-IisStaging.ps1`: backup site, apply migration tuy chon, copy publish, restart app pool.
- `scripts/Set-IisEnvironmentVariables.ps1`: set IIS environment variables tu file `.env` ngoai repo.
- `scripts/Test-StagingSmoke.ps1`: smoke test `/health`, `/`, `/Auth/Login`.
- `.github/workflows/ci.yml`: restore/build/test/secret scan/migration script/gitleaks.
- `web.config`: IIS hosting config.
- `appsettings.Production.example.json`: production config example khong credential that.
- `docs/deploy-runbook-windows-iis-sqlserver.md`: runbook deploy/rollback.
- `docs/release-checklist-2026-05-13.md`: checklist release hien tai.

## Cac hang muc P0 da dong

- Secret tracked duoc thay bang placeholder.
- Production fail-fast neu thieu secret/cau hinh bat buoc.
- CORS dong theo allowlist.
- DataProtection key ring co path cho IIS.
- Cart checkout dung multi-hold atomic qua `ReservationService`.
- Cart item khong bi xoa truoc webhook/payment success.
- Add-on cart khong bi double count.
- PayOS webhook la diem xac nhan tien va convert booking.
- PayOS return URL chi hien thi trang dang xac nhan.
- eKYC OCR sync `CitizenIdHash` va `DrivingLicenseHash`.
- Cart/PII POST dung anti-forgery token.
- Admin area API yeu cau Admin/Manager.
- Tracking ingest yeu cau device id/timestamp/HMAC va co rate limit.
- Seed/demo endpoint bi chan ngoai Development.
- Secure file co no-store cache.
- Refund payout MVP co model/service/job, fallback `PendingManual` khi payout disabled/permission error.
- Ledger chi danh dau refunded sau payout success trong flow polling.

## Viec chua the lam neu thieu ha tang

- Chua deploy IIS staging that.
- Chua apply migration len SQL Server staging.
- Chua smoke test qua URL staging HTTPS.
- Chua test PayOS webhook live voi public callback URL.
- Chua verify PayOS payout live/sandbox voi credential merchant that.
- Chua rotate DB password hoac secret that tren server vi khong co quyen/credential server trong yeu cau.

## Lenh dung tiep

Tao lai package:

```powershell
.\scripts\New-ProductionMvpRelease.ps1
```

Kiem tra readiness:

```powershell
.\scripts\Test-ProductionReadiness.ps1
```

Set IIS env vars tren server:

```powershell
.\scripts\Set-IisEnvironmentVariables.ps1 `
  -SiteName "CarRentalSystem" `
  -EnvFile C:\CarRentalSystem\secrets\.env.staging
```

Deploy staging:

```powershell
.\scripts\Deploy-IisStaging.ps1 `
  -PackageDir .\artifacts\release-20260513-183223 `
  -SitePath C:\inetpub\CarRentalSystem `
  -AppPoolName CarRentalSystem
```

Smoke test:

```powershell
.\scripts\Test-StagingSmoke.ps1 -BaseUrl https://staging.your-domain.example
```

## Dieu kien go staging

- Co SQL Server staging va backup truoc migration.
- Co IIS site/app pool staging.
- Co HTTPS domain staging public de PayOS goi webhook.
- Env vars staging duoc set day du.
- `PayOS:PayoutEnabled=false` cho den khi payout verified.

## Dieu kien go production

- Staging pass smoke test va full manual business test.
- PayOS webhook duplicate/idempotent va amount mismatch da test tren staging.
- Cart multi-hold rollback da test tren staging.
- Tracking missing HMAC bi reject tren staging.
- Refund payout disabled => `PendingManual` da test tren staging.
- DB password da rotate, secret khong con trong repo/history public.
- Co backup DB + backup site + rollback owner trong khung deploy.

## Rui ro con lai can lam o vong sau

- Refactor triệt de moi flow admin/driver/manual qua `BookingTransitionService`.
- Them UI/refund bank info day du cho khach/admin.
- Kiem chung PayOS payout signature va state mapping voi provider truoc khi bat live.
- Long-term rental can chuyen tiep thanh inquiry/quote + hold/deposit production-grade.
- Snapshot/backup noi bo can ma hoa/retention rieng.
- CSP con `unsafe-inline`/`unsafe-eval`, nen giam dan sau khi UI on dinh.
