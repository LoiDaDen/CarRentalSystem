param(
    [string]$Config = "config.yaml",
    [switch]$Once,
    [switch]$PreviewOnly
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Python = Join-Path $ScriptDir ".venv\Scripts\python.exe"

if (-not (Test-Path $Python)) {
    throw "Python virtual environment not found. Run: python -m venv .venv; .\.venv\Scripts\python.exe -m pip install -r requirements.txt"
}

$ArgsList = @("-m", "camera_bridge", "--config", $Config)
if ($Once) {
    $ArgsList += "--once"
}
if ($PreviewOnly) {
    $ArgsList += "--preview-only"
}

Push-Location $ScriptDir
try {
    $env:PYTHONPATH = Join-Path $ScriptDir "src"
    $env:PYTHONUTF8 = "1"
    $env:PYTHONIOENCODING = "utf-8"
    & $Python @ArgsList
}
finally {
    Pop-Location
}
