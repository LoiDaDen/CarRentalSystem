# Developer Onboarding

Updated: 2026-05-16

## Prerequisites

- .NET SDK 10.x.
- SQL Server LocalDB or SQL Server developer instance.
- PowerShell 7 or Windows PowerShell.
- Optional: Redis for distributed cache testing.

## Setup

1. Restore packages:

```powershell
dotnet restore CarRentalSystem.csproj
dotnet restore ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj
dotnet tool restore
```

2. Configure local secrets through environment variables or `appsettings.Development.json`.

Required for full local flows:

- `ConnectionStrings:DefaultConnection`
- SMTP settings for email flows, or use local/test doubles.
- PayOS sandbox values for payment flows.
- FPT AI sandbox key for eKYC flows.

3. Apply migrations to local database:

```powershell
dotnet tool run dotnet-ef database update --project CarRentalSystem.csproj --startup-project CarRentalSystem.csproj
```

4. Build and test:

```powershell
dotnet build CarRentalSystem.csproj --configuration Release --no-restore /p:UseAppHost=false
dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --configuration Release --no-restore /p:UseAppHost=false
```

5. Run safety gates:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Test-TrackedSecrets.ps1 -RepoRoot .
powershell -ExecutionPolicy Bypass -File scripts\Test-Phase1ProductionSafety.ps1
powershell -ExecutionPolicy Bypass -File scripts\Test-Phase2ProductionSafety.ps1
```

## Common Issues

- If EF tool version warnings appear, run `dotnet tool restore` and use `dotnet tool run dotnet-ef`.
- Do not put production secrets in tracked JSON files.
- Production-like staging must use explicit CORS origins, reCAPTCHA, privileged MFA, and audit hash chain.
- If `bin\Debug\net10.0\CarRentalSystem.dll` is locked by a running app, stop the local app or build to a temporary output path for compile verification, then re-run the normal build once the lock is gone.
- For Pass 2 audit work, update both `CAR_RENTAL_SYSTEM_TASKS.md` and `CAR_RENTAL_SYSTEM_FULL_AUDIT_100_PERCENT.md` with task status, evidence, and any blocker reason before handing off.
