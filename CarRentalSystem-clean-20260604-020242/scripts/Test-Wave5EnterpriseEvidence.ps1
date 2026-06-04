param(
    [string]$RepoRoot = ".",
    [switch]$RequireCompletedEvidence
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path $RepoRoot).Path
Push-Location $root

try {
    function Assert-Exists {
        param([string]$Path, [string]$Message)
        if (-not (Test-Path -LiteralPath $Path)) { throw $Message }
    }

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

    function Assert-Evidence {
        param([string]$Path)
        Assert-Exists $Path "$Path is missing."
        if ($RequireCompletedEvidence) {
            Assert-NotContains $Path "Status:\s*TEMPLATE" "$Path is still a template."
            Assert-NotContains $Path "\bTBD\b" "$Path still contains TBD placeholders."
            Assert-NotContains $Path "\|\s*(Critical|High)\s*\|\s*(Open|InProgress|TEMPLATE)\s*\|" "$Path has unresolved Critical/High security findings."
        }
    }

    Write-Host "[wave5] Checking prior wave gate scripts..."
    $priorScripts = @(
        "scripts\Test-Wave1StateMutationSafety.ps1",
        "scripts\Test-Wave2OperationsSafety.ps1",
        "scripts\Test-Wave3ProductionSafety.ps1",
        "scripts\Test-Wave3PerformanceSafety.ps1",
        "scripts\Test-Wave3CoverageGate.ps1",
        "scripts\Test-Wave4ProductionSafety.ps1",
        "scripts\Test-Wave4AccessibilitySafety.ps1",
        "scripts\Test-Wave4FleetSafety.ps1"
    )
    foreach ($script in $priorScripts) {
        Assert-Exists $script "$script is missing; Wave 5 certification cannot skip prior waves."
    }

    Write-Host "[wave5] Checking certification docs..."
    Assert-Contains "docs\multi-az-failover-drill.md" "PayOS webhook|Hangfire|secret manager|/ready" "Failover runbook must cover app, jobs, secrets, readiness and payment webhook."
    Assert-Exists "docs\soc2-ready-control-matrix.md" "SOC2-ready control matrix is missing."
    Assert-Contains "docs\soc2-ready-control-matrix.md" "Access control|Change management|Incident response|Backup/DR|Vulnerability management|Privacy/retention" "SOC2 matrix must cover required control families."
    Assert-Contains "docs\security-review-playbook.md" "Release-Blocking Rules" "Security playbook must define release-blocking rules."
    Assert-Contains "docs\enterprise-production-readiness-gates.md" "Wave 5 implemented gates" "Readiness gates must include Wave 5 implemented gates."
    Assert-Contains "docs\enterprise-completion-roadmap.md" "Wave 5 implementation status" "Completion roadmap must show Wave 5 implementation status."
    Assert-Contains "docs\roadmap.md" "Completion Wave 5" "Main roadmap must reference Wave 5 completion evidence."

    Write-Host "[wave5] Checking evidence templates..."
    Assert-Evidence "docs\wave5-evidence\multi-az-failover-report.md"
    Assert-Evidence "docs\wave5-evidence\soc2-evidence-index.md"
    Assert-Evidence "docs\wave5-evidence\pentest-remediation-tracker.md"
    Assert-Evidence "docs\wave5-final-enterprise-readiness-review.md"

    if ($RequireCompletedEvidence) {
        Write-Host "[wave5] Checking strict go-live evidence..."
        Assert-Contains "docs\wave5-evidence\multi-az-failover-report.md" "RTO result\s*\|\s*Pass" "Strict gate requires RTO pass."
        Assert-Contains "docs\wave5-evidence\multi-az-failover-report.md" "RPO result\s*\|\s*Pass" "Strict gate requires RPO pass."
        Assert-Contains "docs\wave5-final-enterprise-readiness-review.md" "Final decision\s*\|\s*Go" "Strict gate requires final Go decision."
        Assert-Contains "docs\wave5-final-enterprise-readiness-review.md" "Sign-off\s*\|\s*(Signed|Approved)" "Strict gate requires signed approval."
    }

    Write-Host "[wave5] Enterprise evidence checks passed."
}
finally {
    Pop-Location
}
