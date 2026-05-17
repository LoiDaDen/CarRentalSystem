param(
    [string]$RepoRoot = ".",
    [switch]$RequireCompletedEvidence
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

    function Assert-EvidenceTemplate {
        param([string]$Path, [string[]]$RequiredPatterns)
        Assert-Exists $Path "$Path is missing."
        foreach ($pattern in $RequiredPatterns) {
            Assert-Contains $Path $pattern "$Path is missing required evidence field: $pattern"
        }

        if ($RequireCompletedEvidence) {
            Assert-NotContains $Path "Status:\s*TEMPLATE" "$Path is still a template."
            Assert-NotContains $Path "\bTBD\b" "$Path still contains TBD placeholders."
        }
    }

    Write-Host "[wave2] Checking staging config shape..."
    Assert-Exists "appsettings.Staging.example.json" "Staging example config is missing."
    Assert-Contains "appsettings.Staging.example.json" '"RequirePrivilegedMfa"\s*:\s*true' "Staging must require privileged MFA."
    Assert-Contains "appsettings.Staging.example.json" '"RequireRecaptcha"\s*:\s*true' "Staging must require reCAPTCHA."
    Assert-Contains "appsettings.Staging.example.json" '"HashChainEnabled"\s*:\s*true' "Staging must enable audit hash chain."
    Assert-Contains "appsettings.Staging.example.json" '"AllowUnsafeDownload"\s*:\s*false' "Staging must block unsafe snapshot download."
    Assert-Contains "appsettings.Staging.example.json" '"AllowUnsafeRestore"\s*:\s*false' "Staging must block unsafe snapshot restore."
    Assert-Contains "appsettings.Staging.example.json" '"AllowUnsafeDetails"\s*:\s*false' "Staging must block unsafe snapshot details."
    Assert-Contains "appsettings.Staging.example.json" '"OpenTelemetry"\s*:\s*\{[\s\S]*"Enabled"\s*:\s*true' "Wave 2 staging must send OpenTelemetry to a real backend."
    Assert-Contains "appsettings.Staging.example.json" '"ApiDocs"\s*:\s*\{[\s\S]*"Enabled"\s*:\s*true[\s\S]*"RequireAuth"\s*:\s*true' "Staging API docs may be enabled only with auth."
    Assert-NotContains "appsettings.Staging.example.json" '"AllowedOrigins"\s*:\s*\[[\s\S]*"\*"' "Staging CORS must not use wildcard origins."

    Write-Host "[wave2] Checking runbook..."
    Assert-Exists "docs\wave2-production-operations-runbook.md" "Wave 2 operations runbook is missing."
    Assert-Contains "docs\wave2-production-operations-runbook.md" "ENT-INFRA-01" "Wave 2 runbook must cover staging infra."
    Assert-Contains "docs\wave2-production-operations-runbook.md" "ENT-SECRET-01" "Wave 2 runbook must cover secret rotation."
    Assert-Contains "docs\wave2-production-operations-runbook.md" "ENT-DR-01" "Wave 2 runbook must cover restore drill."
    Assert-Contains "docs\wave2-production-operations-runbook.md" "ENT-OBS-01" "Wave 2 runbook must cover observability."
    Assert-Contains "docs\wave2-production-operations-runbook.md" "ENT-RELEASE-01" "Wave 2 runbook must cover release rollback."
    Assert-Contains "docs\wave2-production-operations-runbook.md" "RTO <= 4" "Wave 2 runbook must keep RTO target."
    Assert-Contains "docs\wave2-production-operations-runbook.md" "RPO <= 1" "Wave 2 runbook must keep RPO target."

    Write-Host "[wave2] Checking evidence templates..."
    Assert-EvidenceTemplate "docs\wave2-evidence\staging-topology.md" @(
        "Resource Inventory",
        "/live",
        "/ready",
        "PayOS test webhook",
        "Admin SLO page"
    )
    Assert-EvidenceTemplate "docs\wave2-evidence\secret-rotation.md" @(
        "Secret scan result",
        "Old version revoked",
        "Stored outside repo",
        "PayOS client ID/API key/checksum"
    )
    Assert-EvidenceTemplate "docs\wave2-evidence\restore-drill.md" @(
        "RTO target",
        "RPO target",
        "SQL PITR Drill",
        "Secure Storage Drill",
        "Dispute export"
    )
    Assert-EvidenceTemplate "docs\wave2-evidence\observability-alert-drill.md" @(
        "Dashboard Links",
        "Alert Drill Results",
        "PayOS webhook failure",
        "Hangfire backlog"
    )
    Assert-EvidenceTemplate "docs\wave2-evidence\release-rollback-drill.md" @(
        "Deployment strategy",
        "Rollback target",
        "Measured rollback duration",
        "Post-rollback smoke"
    )

    Write-Host "[wave2] Checking docs integration..."
    Assert-Contains "docs\enterprise-completion-roadmap.md" "Wave 2 implementation status" "Completion roadmap must show Wave 2 implementation status."
    Assert-Contains "docs\enterprise-production-readiness-gates.md" "Wave 2 implemented gates" "Readiness gates must include Wave 2 implemented gates."
    Assert-Contains "docs\enterprise-production-readiness-gates.md" "Test-Wave2OperationsSafety.ps1" "Readiness gates must reference the Wave 2 safety script."

    Write-Host "[wave2] Operations safety checks passed."
}
finally {
    Pop-Location
}
