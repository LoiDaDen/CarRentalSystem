param(
    [string]$Urls = "http://localhost:5168",
    [switch]$StopExisting,
    [switch]$NoBuild,
    [switch]$NoLaunchProfile
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "CarRentalSystem\CarRentalSystem.csproj"
$uri = [Uri]$Urls.Split(";")[0]
$port = $uri.Port

$listeners = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
if ($listeners.Count -gt 0) {
    $processIds = @($listeners | Select-Object -ExpandProperty OwningProcess -Unique)

    if (-not $StopExisting) {
        Write-Host "Port $port is already in use by process id(s): $($processIds -join ', ')." -ForegroundColor Yellow
        Write-Host "Inspect: Get-Process -Id $($processIds -join ',')" -ForegroundColor Yellow
        Write-Host "Stop and run: .\scripts\Start-Dev.ps1 -StopExisting" -ForegroundColor Yellow
        exit 1
    }

    foreach ($processId in $processIds) {
        Write-Host "Stopping process $processId that is listening on port $port..."
        Stop-Process -Id $processId -Force
    }
}

$dotnetArgs = @("run", "--project", $projectPath)
if ($NoBuild) {
    $dotnetArgs += "--no-build"
}
if ($NoLaunchProfile) {
    $dotnetArgs += "--no-launch-profile"
}
$dotnetArgs += @("--", "--urls", $Urls)

Write-Host "Starting CarRentalSystem at $Urls..."
& dotnet @dotnetArgs
