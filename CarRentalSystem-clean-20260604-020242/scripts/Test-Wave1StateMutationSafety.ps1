param(
    [string]$RepoRoot = "."
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

    Write-Host "[wave1] Checking transition service surface..."
    Assert-Exists "Services\PaymentTransitionService.cs" "PaymentTransitionService is missing."
    Assert-Contains "Program.cs" "AddScoped<IPaymentTransitionService, PaymentTransitionService>" "IPaymentTransitionService must be registered in DI."
    Assert-Contains "Services\PaymentTransitionService.cs" "ConfirmManualPaymentAsync" "Manual payment transition is missing."
    Assert-Contains "Services\PaymentTransitionService.cs" "MarkPayOSExistingBookingPaidAsync" "PayOS paid transition is missing."
    Assert-Contains "Services\PaymentTransitionService.cs" "MarkPayOSFailedAsync" "PayOS failed transition is missing."
    Assert-Contains "Services\PaymentTransitionService.cs" "SetBookingDepositStatusAsync" "Booking deposit transition is missing."
    Assert-Contains "Services\PaymentTransitionService.cs" "IAuditTrailService" "Payment transitions must use hash-chain audit service."
    Assert-Contains "Services\PaymentTransitionService.cs" "IdempotencyKey" "Payment transitions must create idempotent ledger entries."

    Write-Host "[wave1] Checking controllers no longer mutate audited payment/deposit states..."
    Assert-NotContains "Areas\Admin\Controllers\PaymentController.cs" "PaymentStatus\s*=" "Admin PaymentController must not set PaymentStatus directly."
    Assert-NotContains "Areas\Admin\Controllers\PaymentController.cs" "DepositStatus\s*=" "Admin PaymentController must not set DepositStatus directly."
    Assert-Contains "Areas\Admin\Controllers\PaymentController.cs" "ConfirmManualPaymentAsync" "Manual confirm must delegate to IPaymentTransitionService."

    Assert-NotContains "Controllers\BookingController.cs" "booking\.DepositStatus\s*=\s*`"PendingRefund`"" "Booking return must not directly set PendingRefund."
    Assert-NotContains "Controllers\BookingController.cs" "booking\.DepositStatus\s*=\s*`"UnderReview`"" "Booking return must not directly set UnderReview."
    Assert-NotContains "Controllers\BookingController.cs" "CarStatusID\s*=\s*availableStatus\.CarStatusID" "Booking return must not directly reset car status."
    Assert-Contains "Controllers\BookingController.cs" "SetBookingDepositStatusAsync" "Booking return must delegate deposit state."
    Assert-Contains "Controllers\BookingController.cs" "CreateAsync\(" "Booking return with violation must create a settlement review."

    Write-Host "[wave1] Checking PayOS webhook transition delegation..."
    Assert-Contains "Services\PayOSPaymentService.cs" "MarkPayOSExistingBookingPaidAsync" "PayOS existing booking paid path must delegate to domain transition."
    Assert-Contains "Services\PayOSPaymentService.cs" "MarkPayOSFailedAsync" "PayOS failed path must delegate to domain transition."
    Assert-Contains "Services\PayOSPaymentService.cs" "MarkCartPaymentUnderReviewAsync" "PayOS cart under-review path must delegate to domain transition."
    Assert-NotContains "Services\PayOSPaymentService.cs" "transaction\.Booking\.DepositStatus\s*=" "PayOS service must not directly mutate existing booking deposit status."
    Assert-NotContains "Services\PayOSPaymentService.cs" "transaction\.Booking\.BookingStatusID\s*=" "PayOS service must not directly mutate existing booking status."

    Write-Host "[wave1] Checking snapshot production governance..."
    Assert-Contains "Areas\Admin\Controllers\SnapshotController.cs" "SnapshotDetailsMetadataOnly" "Snapshot details must be metadata-only in production by default."
    Assert-Contains "Areas\Admin\Controllers\SnapshotController.cs" "AllowUnsafeDetails" "Snapshot details unsafe flag must be checked."
    Assert-Contains "Areas\Admin\Controllers\SnapshotController.cs" "IAuditTrailService" "Snapshot audit must use hash-chain audit service."
    Assert-Contains "Services\ConfigurationGuard.cs" "Snapshot:AllowUnsafeDetails" "ConfigurationGuard must reject unsafe snapshot details in production."
    Assert-Contains "appsettings.Production.example.json" '"AllowUnsafeDetails"\s*:\s*false' "Production example must keep unsafe snapshot details disabled."

    Write-Host "[wave1] Checking tests and docs..."
    Assert-Exists "..\CarRentalSystem.Tests\UnitTests\Services\EnterpriseWave1Tests.cs" "Wave 1 regression tests are missing."
    Assert-Contains "..\CarRentalSystem.Tests\UnitTests\Services\EnterpriseWave1Tests.cs" "ConfirmManualPayment_CreatesLedgerAndHashAudit" "Manual payment regression test is missing."
    Assert-Contains "..\CarRentalSystem.Tests\UnitTests\Services\EnterpriseWave1Tests.cs" "SettlementReview_RequiresApprovalBeforeDeductionLedger" "Settlement approval regression test is missing."
    Assert-Contains "docs\enterprise-production-readiness-gates.md" "Wave 1 implemented gates" "Readiness gates must include Wave 1 implemented gates."
    Assert-Contains "docs\enterprise-completion-roadmap.md" "Wave 1 implementation status" "Completion roadmap must show Wave 1 implementation status."

    Write-Host "[wave1] State mutation safety checks passed."
}
finally {
    Pop-Location
}
