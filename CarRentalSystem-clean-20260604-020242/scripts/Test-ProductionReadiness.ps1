[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [switch]$SkipTests,
    [switch]$SkipMigrationScript
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $ProjectRoot "CarRentalSystem.csproj"
$TestsPath = Join-Path (Split-Path -Parent $ProjectRoot) "CarRentalSystem.Tests\CarRentalSystem.Tests.csproj"
$ArtifactRoot = Join-Path $ProjectRoot "artifacts"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ReportPath = Join-Path $ArtifactRoot "readiness-$Timestamp.txt"
$MigrationCheckPath = Join-Path $ArtifactRoot "migration-check-$Timestamp.sql"

New-Item -ItemType Directory -Path $ArtifactRoot -Force | Out-Null

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

$results = New-Object System.Collections.Generic.List[string]
function Add-Result([string]$Message) {
    $results.Add("[$((Get-Date).ToString("HH:mm:ss"))] $Message")
    Write-Host $Message
}

Add-Result "Production readiness check started."

Add-Result "Parsing PowerShell scripts."
foreach ($script in Get-ChildItem -Path (Join-Path $ProjectRoot "scripts") -Filter "*.ps1") {
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$null, [ref]$errors) | Out-Null
    if ($errors) {
        throw "PowerShell parse failed for $($script.Name): $($errors[0].Message)"
    }
}
Add-Result "PowerShell scripts parse passed."

Add-Result "Running tracked secret scan."
& (Join-Path $ProjectRoot "scripts\Test-TrackedSecrets.ps1") -RepoRoot $ProjectRoot
if ($LASTEXITCODE -ne 0) {
    throw "Tracked secret scan failed."
}
Add-Result "Tracked secret scan passed."

Add-Result "Running dotnet build."
Invoke-NativeChecked dotnet build $ProjectPath --configuration $Configuration --no-restore /p:UseAppHost=false
Add-Result "dotnet build passed."

if (-not $SkipTests -and (Test-Path $TestsPath)) {
    Add-Result "Running dotnet test."
    Invoke-NativeChecked dotnet test $TestsPath --configuration $Configuration --no-restore /p:UseAppHost=false
    Add-Result "dotnet test passed."
}

if (-not $SkipMigrationScript) {
    Add-Result "Generating idempotent migration script."
    Invoke-NativeChecked dotnet ef migrations script --idempotent `
        --project $ProjectPath `
        --startup-project $ProjectPath `
        --configuration $Configuration `
        --no-build `
        --output $MigrationCheckPath
    Add-Result "Migration script check passed: $MigrationCheckPath"
}

Add-Result "Production readiness check completed."
$results | Set-Content -Path $ReportPath -Encoding UTF8

Write-Host ""
Write-Host "Readiness report:"
Write-Host $ReportPath
