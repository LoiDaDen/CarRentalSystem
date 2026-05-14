# Deploy runbook - Windows IIS + SQL Server

Ngay cap nhat: 2026-05-13

Muc tieu: deploy CarRentalSystem len staging/production theo cach co backup, co migration script, co health check va co rollback.

## Nguyen tac

- Khong commit secret vao repo.
- Production khong tu chay migration luc startup.
- Production khong seed demo/reference data bang endpoint runtime.
- PayOS return URL chi hien thi trang dang xac nhan; webhook moi la diem ghi nhan da thu tien.
- PayOS payout chi bat khi da test live/sandbox thanh cong voi merchant account that.

## 1. Dieu kien truoc deploy

- DB password da rotate neu tung nam trong config tracked.
- IIS server da cai .NET Hosting Bundle dung version.
- SQL Server co backup job rieng.
- App pool identity co quyen doc thu muc publish, ghi logs neu can, doc/ghi DataProtection key ring.
- Secret duoc cau hinh bang IIS environment variables hoac secret manager.
- `appsettings.Production.json` khong commit vao repo.

Bien moi truong toi thieu:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection`
- `Cors__AllowedOrigins__0`
- `Tracking__IngestSecret`
- `DataProtection__KeysPath`
- `PayOS__ClientId`
- `PayOS__ApiKey`
- `PayOS__ChecksumKey`
- `PayOS__PayoutEnabled=false` cho den khi payout duoc verify
- `SmtpSettings__Host`
- `SmtpSettings__User`
- `SmtpSettings__Pass`
- `Recaptcha__SecretKey`
- `FptAi__ApiKey`

Co the tao mot file `.env.staging` nam ngoai repo theo format `.env.example`, sau do ap bien moi truong vao IIS bang:

```powershell
.\scripts\Set-IisEnvironmentVariables.ps1 `
  -SiteName "CarRentalSystem" `
  -EnvFile C:\CarRentalSystem\secrets\.env.staging
```

Script se mask ten bien nhay cam khi in ra console, nhung file `.env.staging` van phai duoc luu ngoai repo va phan quyen chi cho admin/deploy user.

## 2. Tao release package

Chay tu thu muc project `CarRentalSystem`:

```powershell
.\scripts\New-ProductionMvpRelease.ps1
```

Ket qua nam trong:

```text
artifacts\release-yyyyMMdd-HHmmss\
```

Ben trong co:

- `publish\`: output de copy vao IIS.
- `database-migration-idempotent.sql`: script migration an toan de chay nhieu lan.
- `release-manifest.txt`: thong tin build/test.

## 3. Backup truoc deploy

Backup SQL Server:

```sql
BACKUP DATABASE [CarRentalDB]
TO DISK = N'D:\Backups\CarRentalDB_before_release.bak'
WITH INIT, COMPRESSION;
```

Backup file site IIS:

```powershell
Copy-Item C:\inetpub\CarRentalSystem C:\CarRentalSystem\backups\site-before-release -Recurse -Force
```

## 4. Apply migration

Staging:

```powershell
sqlcmd -b -S YOUR_SQL_SERVER -d CarRentalDB -i .\artifacts\release-yyyyMMdd-HHmmss\database-migration-idempotent.sql -C
```

Production:

- Chay script tren maintenance window.
- Luu output terminal vao change ticket.
- Neu migration fail, dung deploy va rollback DB tu backup neu da co thay doi partial.

## 5. Deploy IIS

Staging co the dung script:

```powershell
.\scripts\Deploy-IisStaging.ps1 `
  -PackageDir .\artifacts\release-yyyyMMdd-HHmmss `
  -SitePath C:\inetpub\CarRentalSystem `
  -AppPoolName CarRentalSystem `
  -WhatIf
```

Neu ket qua `-WhatIf` dung, chay that:

```powershell
.\scripts\Deploy-IisStaging.ps1 `
  -PackageDir .\artifacts\release-yyyyMMdd-HHmmss `
  -SitePath C:\inetpub\CarRentalSystem `
  -AppPoolName CarRentalSystem
```

## 6. Smoke test sau deploy

```powershell
.\scripts\Test-StagingSmoke.ps1 -BaseUrl https://staging.your-domain.example
```

Kiem tra thu cong them:

- Login/logout.
- Register + email verification.
- eKYC test account.
- Cart checkout 1 xe.
- Cart checkout nhieu xe.
- PayOS webhook duplicate.
- PayOS amount mismatch.
- Refund payout disabled => `PendingManual`.
- Tracking request thieu HMAC => reject.
- Admin API anonymous => unauthorized.

## 7. Rollback

Rollback app:

```powershell
%windir%\System32\inetsrv\appcmd.exe stop apppool /apppool.name:CarRentalSystem
Remove-Item C:\inetpub\CarRentalSystem\* -Recurse -Force
Copy-Item C:\CarRentalSystem\backups\site-before-release\* C:\inetpub\CarRentalSystem -Recurse -Force
%windir%\System32\inetsrv\appcmd.exe start apppool /apppool.name:CarRentalSystem
```

Rollback DB:

- Chi restore DB neu migration/data change gay loi va da thong nhat downtime.
- Khong rollback DB tuy tien neu da co giao dich thanh toan moi sau deploy.

## 8. Go/no-go

Duoc go production khi:

- Build/test pass.
- Migration script chay thanh cong tren staging.
- Health check OK.
- Smoke test OK.
- PayOS webhook verified tren staging.
- Secrets da rotate va khong con trong repo.
- Co nguoi truc rollback trong khung deploy.
