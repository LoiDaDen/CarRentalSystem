# Production Deployment Checklist

Updated: 2026-05-19

This checklist mirrors `ConfigurationGuard` and the audit backlog. Do not mark a release ready until every environment item has concrete evidence from staging or production.

## Build and Test Gates

- `dotnet build .\CarRentalSystem.csproj` returns 0 warnings and 0 errors.
- `dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --no-restore` passes.
- No tracked secret appears in appsettings, `.env.example`, scripts, docs, or committed test artifacts.

## Required Production Configuration

- `ConnectionStrings:DefaultConnection` points to managed SQL.
- `Security:RequireRecaptcha=true`.
- `Recaptcha:SecretKey` is set through environment or secret manager.
- CORS origins are explicit and not wildcarded.
- DataProtection keys path is mounted on persistent storage.
- Audit hash chain is enabled.
- Snapshot raw download/restore unsafe flags are disabled.
- Privileged MFA is enabled for Admin, Manager, and Staff.

## Provider Evidence

- PayOS live webhook URL reaches `/Payment/Webhook` over HTTPS.
- PayOS valid signature, duplicate webhook, amount mismatch, and late payment after hold expiry are tested.
- FPT eKYC sandbox/live keys are configured outside source control.
- SMTP can send OTP and operational alerts.
- Google/Microsoft OAuth callback URLs match the deployed host if enabled.

## Operational Evidence

- `/live` and `/ready` pass from an external monitor.
- Hangfire dashboard is reachable only by privileged roles.
- Backup and restore drill covers SQL plus secure uploads/contracts/evidence.
- Rollback procedure is tested with version and owner recorded.
- Load test evidence supports the target SLO before real traffic.

## Current Blockers

- Chờ deploy T3 + staging evidence — không thể verify từ máy dev.
- External PayOS/eKYC/OAuth/SMTP checks must not be simulated as production evidence.
