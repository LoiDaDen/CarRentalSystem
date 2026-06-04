param(
    [string]$RepoRoot = "."
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

    Write-Host "[wave3-coverage] Checking critical-path test coverage surface..."
    Assert-Exists "..\CarRentalSystem.Tests\UnitTests\Services\EnterpriseWave1Tests.cs" "Wave 1 payment/settlement tests are missing."
    Assert-Exists "..\CarRentalSystem.Tests\UnitTests\Services\EnterpriseWave3Tests.cs" "Wave 3 privacy/retention tests are missing."
    Assert-Contains "..\CarRentalSystem.Tests\UnitTests\Services\EnterpriseWave1Tests.cs" "ConfirmManualPayment_CreatesLedgerAndHashAudit" "Payment transition regression coverage is missing."
    Assert-Contains "..\CarRentalSystem.Tests\UnitTests\Services\EnterpriseWave1Tests.cs" "SettlementReview_RequiresApprovalBeforeDeductionLedger" "Settlement regression coverage is missing."
    Assert-Contains "..\CarRentalSystem.Tests\UnitTests\Services\EnterpriseWave3Tests.cs" "PrivacyRequest_WorkflowCreatesHashChainAuditAndManifest" "Privacy workflow coverage is missing."
    Assert-Contains "..\CarRentalSystem.Tests\UnitTests\Services\EnterpriseWave3Tests.cs" "RetentionPreview_ReturnsCandidatesWithoutMutatingData" "Retention dry-run coverage is missing."
    Assert-Contains "..\CarRentalSystem.Tests\UnitTests\EnterpriseP0RegressionTests.cs" "EkycUpload_ExtensionMismatchMagicBytes_IsRejected" "Upload validation regression coverage is missing."

    Write-Host "[wave3-coverage] Critical-path coverage surface checks passed."
}
finally {
    Pop-Location
}
