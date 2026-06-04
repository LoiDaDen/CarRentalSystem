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

    Write-Host "[wave3-performance] Checking admin aggregate posture..."
    Assert-Contains "Areas\Admin\Controllers\ReportsController.cs" "SumAsync|CountAsync|GroupBy" "ReportsController must use DB-side aggregate APIs."
    Assert-Contains "Areas\Admin\Controllers\DashboardController.cs" "SumAsync|CountAsync|GroupBy" "DashboardController must use DB-side aggregate APIs."
    Assert-Contains "Areas\Admin\Controllers\AreaDashboardController.cs" "SumAsync|CountAsync" "AreaDashboardController must use DB-side aggregate APIs."
    Assert-Contains "Areas\Admin\Controllers\AreaStatsController.cs" "SumAsync|CountAsync" "AreaStatsController must use DB-side aggregate APIs."
    Assert-NotContains "Areas\Admin\Controllers\DashboardController.cs" "GetAllBookingsAsync\\(\\)|GetAllCarsAsync\\(\\)" "Dashboard must not load all bookings/cars through services for KPI aggregates."
    Assert-NotContains "Areas\Admin\Controllers\ReportsController.cs" "Take\\(5000\\)[\\s\\S]*completedBookings" "Reports must not rely on a bounded in-memory booking sample for KPI aggregates."

    Write-Host "[wave3-performance] Performance safety checks passed."
}
finally {
    Pop-Location
}
