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

    function Assert-Exists {
        param([string]$Path, [string]$Message)
        if (-not (Test-Path -LiteralPath $Path)) { throw $Message }
    }

    if (-not $SkipBuildAndTests) {
        Write-Host "[phase3] Running tracked secret scan..."
        & powershell -ExecutionPolicy Bypass -File "scripts\Test-TrackedSecrets.ps1" -RepoRoot "."

        Write-Host "[phase3] Running Release build..."
        & dotnet build "CarRentalSystem.csproj" --configuration Release --no-restore /p:UseAppHost=false

        Write-Host "[phase3] Running Release tests..."
        & dotnet test "..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj" --configuration Release --no-restore /p:UseAppHost=false

        Write-Host "[phase3] Generating idempotent migration script..."
        & dotnet tool restore
        $artifactDir = Join-Path $root "artifacts"
        if (-not (Test-Path -LiteralPath $artifactDir)) {
            New-Item -ItemType Directory -Path $artifactDir | Out-Null
        }
        $timestamp = Get-Date -Format "yyyyMMddHHmmss"
        & dotnet tool run dotnet-ef migrations script --idempotent --project "CarRentalSystem.csproj" --startup-project "CarRentalSystem.csproj" --no-build --configuration Release --output (Join-Path $artifactDir "phase3-migration-check-$timestamp.sql")
    }

    Write-Host "[phase3] Checking endpoint and localization gates..."
    Assert-Contains "Program.cs" 'MapGet\("/live"' "Program.cs must expose /live."
    Assert-Contains "Program.cs" 'MapGet\("/ready"' "Program.cs must expose /ready."
    Assert-Contains "Program.cs" 'UseRequestLocalization' "Request localization middleware must be enabled."
    Assert-Contains "Program.cs" 'AddOpenTelemetry' "OpenTelemetry registration must be present."
    Assert-Contains "Program.cs" 'AddSource\(EnterpriseTelemetryService\.ActivitySourceName\)' "Enterprise ActivitySource must be registered."

    Write-Host "[phase3] Checking production config guards..."
    Assert-Contains "appsettings.Production.example.json" '"OpenTelemetry"\s*:\s*\{[\s\S]*"Enabled"\s*:\s*false' "OpenTelemetry must be disabled by default in production example."
    Assert-Contains "appsettings.Production.example.json" '"Analytics"\s*:\s*\{[\s\S]*"Enabled"\s*:\s*false' "Analytics must be disabled by default in production example."
    Assert-Contains "appsettings.Production.example.json" '"ApiDocs"\s*:\s*\{[\s\S]*"Enabled"\s*:\s*false[\s\S]*"RequireAuth"\s*:\s*true' "API docs must be disabled by default and require auth when enabled."
    Assert-Contains "Services\ConfigurationGuard.cs" 'Observability:OpenTelemetry:OtlpEndpoint' "Configuration guard must enforce OTLP endpoint when OpenTelemetry is enabled."
    Assert-Contains "Services\ConfigurationGuard.cs" 'ApiDocs:RequireAuth' "Configuration guard must enforce API docs auth."

    Write-Host "[phase3] Checking PII-safe telemetry and analytics..."
    Assert-Contains "Services\EnterpriseTelemetryService.cs" 'SensitiveKeyFragments' "Enterprise telemetry must define sensitive key filtering."
    Assert-Contains "Services\EnterpriseTelemetryService.cs" 'email.*phone.*citizen.*license.*password.*token.*secret.*payload.*file.*path.*address' "Enterprise telemetry sensitive key list is incomplete."
    Assert-Contains "Services\ProductAnalyticsService.cs" 'NoopProductAnalyticsService' "Analytics must default to no-op provider."
    Assert-Contains "Services\ProductAnalyticsService.cs" 'AllowedEvents' "Analytics event taxonomy must be enforced."
    Assert-NotContains "Services\EnterpriseTelemetryService.cs" 'SetTag\([^,]+,\s*.*Email' "Telemetry must not directly tag email values."

    Write-Host "[phase3] Checking docs..."
    Assert-Exists "docs\phase3-sre-runbook.md" "Phase 3 SRE runbook is missing."
    Assert-Exists "docs\multi-az-failover-drill.md" "Multi-AZ failover drill doc is missing."
    Assert-Exists "docs\phase3-observability.md" "Phase 3 observability doc is missing."
    Assert-Exists "docs\accessibility-wcag-aa-checklist.md" "Accessibility checklist is missing."
    Assert-Exists "docs\product-analytics-taxonomy.md" "Product analytics taxonomy is missing."
    Assert-Exists "docs\developer-portal-readiness.md" "Developer portal readiness doc is missing."
    Assert-Contains "docs\enterprise-production-readiness-gates.md" 'Phase 3 implemented gates' "Readiness gates must include Phase 3 implemented gates."

    Write-Host "[phase3] Checking CI posture..."
    Assert-NotContains "..\.github\workflows\ci-cd-tests.yml" "exit-code:\s*'0'" "Trivy scans must remain blocking."

    Write-Host "[phase3] Production safety checks passed."
}
finally {
    Pop-Location
}
