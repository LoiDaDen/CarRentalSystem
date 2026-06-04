param(
    [string]$RepoRoot = ".",
    [switch]$SkipBuildAndTests
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path $RepoRoot).Path
Push-Location $root

try {
    function Assert-FileContains {
        param(
            [string]$Path,
            [string]$Pattern,
            [string]$Message
        )

        $content = Get-Content -LiteralPath $Path -Raw
        if ($content -notmatch $Pattern) {
            throw $Message
        }
    }

    function Assert-FileNotContains {
        param(
            [string]$Path,
            [string]$Pattern,
            [string]$Message
        )

        $content = Get-Content -LiteralPath $Path -Raw
        if ($content -match $Pattern) {
            throw $Message
        }
    }

    Write-Host "[phase1] Running tracked secret scan..."
    & powershell -ExecutionPolicy Bypass -File "scripts\Test-TrackedSecrets.ps1" -RepoRoot $root

    if (-not $SkipBuildAndTests) {
        Write-Host "[phase1] Running Release build..."
        & dotnet build "CarRentalSystem.csproj" --configuration Release --no-restore /p:UseAppHost=false

        Write-Host "[phase1] Running Release tests..."
        & dotnet test "..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj" --configuration Release --no-restore /p:UseAppHost=false

        Write-Host "[phase1] Generating idempotent migration script..."
        & dotnet tool restore
        $artifactDir = Join-Path $root "artifacts"
        if (-not (Test-Path -LiteralPath $artifactDir)) {
            New-Item -ItemType Directory -Path $artifactDir | Out-Null
        }

        $timestamp = Get-Date -Format "yyyyMMddHHmmss"
        $migrationOutput = Join-Path $artifactDir "phase1-migration-check-$timestamp.sql"
        & dotnet tool run dotnet-ef migrations script --idempotent --project "CarRentalSystem.csproj" --startup-project "CarRentalSystem.csproj" --no-build --configuration Release --output $migrationOutput
    }

    Write-Host "[phase1] Checking production example safety flags..."
    Assert-FileContains "appsettings.Production.example.json" '"RequireRecaptcha"\s*:\s*true' "Production example must enable Security:RequireRecaptcha."
    Assert-FileContains "appsettings.Production.example.json" '"AllowUnsafeDownload"\s*:\s*false' "Production example must disable unsafe snapshot download."
    Assert-FileContains "appsettings.Production.example.json" '"AllowUnsafeRestore"\s*:\s*false' "Production example must disable unsafe snapshot restore."
    Assert-FileNotContains "appsettings.Production.example.json" '"AllowUnsafeDownload"\s*:\s*true' "Production example cannot enable unsafe snapshot download."
    Assert-FileNotContains "appsettings.Production.example.json" '"AllowUnsafeRestore"\s*:\s*true' "Production example cannot enable unsafe snapshot restore."

    Write-Host "[phase1] Checking tracking gate documentation..."
    Assert-FileContains "docs\enterprise-production-readiness-gates.md" 'X-Nonce' "Readiness gates must document tracking X-Nonce."

    Write-Host "[phase1] Checking CSP production branch..."
    Assert-FileContains "Program.cs" 'app\.Environment\.IsProduction\(\)' "Program.cs must branch CSP by Production environment."
    Assert-FileContains "Program.cs" "unsafe-eval.*cdn\.jsdelivr|cdn\.jsdelivr.*unsafe-eval" "Development CSP should retain unsafe-eval only for local tooling."

    Write-Host "[phase1] Checking CI vulnerability gates..."
    Assert-FileContains "..\.github\workflows\ci.yml" 'NuGetAudit=true' "ci.yml must run NuGet audit."
    Assert-FileContains "..\.github\workflows\ci.yml" "exit-code:\s*'1'" "ci.yml Trivy scan must block critical/high findings."
    Assert-FileNotContains "..\.github\workflows\ci-cd-tests.yml" "exit-code:\s*'0'" "ci-cd-tests.yml cannot leave Trivy scans non-blocking."
    Assert-FileNotContains "..\.github\workflows\ci-cd-tests.yml" 'continue-on-error:\s*true' "ci-cd-tests.yml smoke tests cannot continue on error."

    Write-Host "[phase1] Production safety checks passed."
}
finally {
    Pop-Location
}
