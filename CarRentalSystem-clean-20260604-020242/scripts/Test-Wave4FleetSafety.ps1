param([string]$RepoRoot = ".")

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

    Write-Host "[wave4-fleet] Checking fleet dashboard query posture..."
    Assert-Contains "Areas\Admin\Controllers\FleetOpsController.cs" 'GroupBy|Count\(|Take\(50\)|AsNoTracking' "FleetOps dashboard must use DB-side aggregate/paging."
    Assert-NotContains "Areas\Admin\Controllers\FleetOpsController.cs" 'ToListAsync\(\)\s*[\r\n\s.]*GroupBy' "FleetOps dashboard must not load then group large datasets in memory."
    Assert-NotContains "Areas\Admin\Controllers\FleetOpsController.cs" 'VehicleTrackings[\s\S]*Distinct\(\)\s*[\r\n\s.]*ToListAsync\(\)' "FleetOps tracking checks must not materialize all recently tracked car ids before filtering."

    Write-Host "[wave4-fleet] Fleet safety checks passed."
}
finally { Pop-Location }
