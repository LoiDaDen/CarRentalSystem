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

    function Assert-Evidence {
        param([string]$Path)
        Assert-Exists $Path "$Path is missing."
        if ($RequireCompletedEvidence) {
            Assert-NotContains $Path "Status:\s*TEMPLATE" "$Path is still a template."
            Assert-NotContains $Path "\bTBD\b" "$Path still contains TBD placeholders."
        }
    }

    Write-Host "[wave3] Checking docs and evidence templates..."
    Assert-Exists "docs\wave3-load-test-plan.md" "Wave 3 load test plan is missing."
    Assert-Contains "docs\wave3-load-test-plan.md" "P95 < 500ms" "Wave 3 load test plan must define P95 target."
    Assert-Evidence "docs\wave3-evidence\load-test-report.md"
    Assert-Evidence "docs\wave3-evidence\coverage-report.md"
    Assert-Evidence "docs\wave3-evidence\privacy-controls.md"

    Write-Host "[wave3] Checking runtime privacy controls..."
    Assert-Exists "Models\PrivacyRequest.cs" "PrivacyRequest model is missing."
    Assert-Exists "Services\PrivacyRequestService.cs" "Privacy request service is missing."
    Assert-Contains "Program.cs" "AddScoped<IPrivacyRequestService, PrivacyRequestService>" "Privacy request service must be registered."
    Assert-Contains "Services\PrivacyRequestService.cs" "PreviewRetentionAsync" "Retention preview must exist."
    Assert-Contains "Services\PrivacyRequestService.cs" "IAuditTrailService" "Privacy actions must use hash-chain audit."

    Write-Host "[wave3] Checking sensitive audit guard..."
    Assert-NotContains "Controllers\SecureFileController.cs" "_context\.AuditLogs\.Add" "Secure file audit must use IAuditTrailService."
    Assert-NotContains "Services\BookingTransitionService.cs" "_context\.AuditLogs\.Add" "Booking transition audit must use IAuditTrailService."
    Assert-NotContains "Services\PayOSPaymentService.cs" "_context\.AuditLogs\.Add" "PayOS sensitive audit must use IAuditTrailService."
    Assert-Contains "docs\enterprise-production-readiness-gates.md" "Wave 3 implemented gates" "Readiness gates must include Wave 3 implemented gates."
    Assert-Contains "docs\enterprise-completion-roadmap.md" "Wave 3 implementation status" "Completion roadmap must show Wave 3 implementation status."

    Write-Host "[wave3] Production safety checks passed."
}
finally {
    Pop-Location
}
