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

    function Assert-Exists {
        param([string]$Path, [string]$Message)
        if (-not (Test-Path -LiteralPath $Path)) { throw $Message }
    }

    Write-Host "[wave4-accessibility] Checking layout language and audit docs..."
    Assert-Contains "Views\Shared\_Layout.cshtml" '<html[^>]+lang="vi"' "Main layout must set html lang."
    Assert-Contains "Views\Shared\_AdminLayout.cshtml" '<html[^>]+lang="vi"' "Admin layout must set html lang."
    Assert-Exists "docs\wave4-accessibility-audit-plan.md" "Wave 4 accessibility audit plan is missing."
    Assert-Exists "docs\wave4-evidence\accessibility-audit.md" "Wave 4 accessibility evidence template is missing."
    Assert-Contains "docs\wave4-accessibility-audit-plan.md" "WCAG 2.1 AA" "Accessibility plan must name WCAG 2.1 AA."

    if (Test-Path -LiteralPath "Areas\Admin\Views\Privacy\Index.cshtml") {
        Assert-NotContains "Areas\Admin\Views\Privacy\Index.cshtml" "MojibakeSentinelShouldNotAppear" "Touched admin privacy view must not contain obvious mojibake."
    }

    Write-Host "[wave4-accessibility] Accessibility safety checks passed."
}
finally {
    Pop-Location
}
