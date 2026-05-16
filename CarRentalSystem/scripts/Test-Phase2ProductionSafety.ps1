param(
    [string]$RepoRoot = ".",
    [switch]$SkipBuildAndTests
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path $RepoRoot).Path
Push-Location $root

try {
    function Assert-Contains {
        param([string]$Path, [string]$Pattern, [string]$Message)
        $content = Get-Content -LiteralPath $Path -Raw
        if ($content -notmatch $Pattern) { throw $Message }
    }

    function Assert-NotContains {
        param([string]$Path, [string]$Pattern, [string]$Message)
        $content = Get-Content -LiteralPath $Path -Raw
        if ($content -match $Pattern) { throw $Message }
    }

    if (-not $SkipBuildAndTests) {
        Write-Host "[phase2] Running Release build..."
        & dotnet build "CarRentalSystem.csproj" --configuration Release --no-restore /p:UseAppHost=false

        Write-Host "[phase2] Running Release tests..."
        & dotnet test "..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj" --configuration Release --no-restore /p:UseAppHost=false

        Write-Host "[phase2] Generating idempotent migration script..."
        & dotnet tool restore
        $artifactDir = Join-Path $root "artifacts"
        if (-not (Test-Path -LiteralPath $artifactDir)) {
            New-Item -ItemType Directory -Path $artifactDir | Out-Null
        }
        $timestamp = Get-Date -Format "yyyyMMddHHmmss"
        & dotnet tool run dotnet-ef migrations script --idempotent --project "CarRentalSystem.csproj" --startup-project "CarRentalSystem.csproj" --no-build --configuration Release --output (Join-Path $artifactDir "phase2-migration-check-$timestamp.sql")
    }

    Write-Host "[phase2] Checking config gates..."
    Assert-Contains "appsettings.Production.example.json" '"RequirePrivilegedSso"\s*:\s*false' "Production example must default privileged SSO to false until providers are configured."
    Assert-Contains "appsettings.Production.example.json" '"HashChainEnabled"\s*:\s*true' "Audit hash chain must be enabled in production example."
    Assert-Contains "appsettings.Production.example.json" '"IncludeSensitiveDefault"\s*:\s*false' "Dispute export must default to redacted output."

    Write-Host "[phase2] Checking implementation surface..."
    Assert-Contains "Services\AuditTrailService.cs" 'ComputeRecordHash' "AuditTrailService must compute record hashes."
    Assert-Contains "Services\DisputeExportService.cs" 'manifest\.json' "Dispute export must include a manifest."
    Assert-Contains "Services\SettlementReviewService.cs" 'settlement-review:' "Settlement approval must use idempotent ledger keys."
    Assert-Contains "Services\CatalogCacheService.cs" 'catalog:v1' "Catalog cache keys must be versioned."
    Assert-Contains "Data\ApplicationDbContext.cs" 'SettlementReviews' "DbContext must expose SettlementReviews."
    Assert-Contains "Data\ApplicationDbContext.cs" 'HasIndex\(a => new \{ a\.Timestamp, a\.EntityType, a\.Action \}\)' "AuditLog phase 2 index is missing."

    Write-Host "[phase2] Checking docs..."
    Assert-Contains "docs\enterprise-production-readiness-gates.md" 'Phase 2 implemented gates' "Readiness gates must include Phase 2 implemented gates."
    Assert-Contains "docs\developer-onboarding.md" 'dotnet build' "Developer onboarding must include build instructions."
    Assert-Contains "docs\security-review-playbook.md" 'Critical' "Security review playbook must define severity handling."
    Assert-Contains "docs\data-inventory-classification.md" 'PII' "Data inventory must classify PII."

    Write-Host "[phase2] Checking CI posture..."
    Assert-NotContains "..\.github\workflows\ci-cd-tests.yml" "exit-code:\s*'0'" "Trivy scans must remain blocking."

    Write-Host "[phase2] Production safety checks passed."
}
finally {
    Pop-Location
}
