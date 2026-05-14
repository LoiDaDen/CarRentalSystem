[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$TestConfiguration = "Debug",
    [string]$ArtifactRoot = "artifacts",
    [switch]$SkipTests,
    [switch]$SkipMigrationScript
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $ProjectRoot "CarRentalSystem.csproj"
$TestsPath = Join-Path (Split-Path -Parent $ProjectRoot) "CarRentalSystem.Tests\CarRentalSystem.Tests.csproj"
$DefaultObjDir = Join-Path $ProjectRoot "obj"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ReleaseRoot = Join-Path $ProjectRoot $ArtifactRoot
$ReleaseDir = Join-Path $ReleaseRoot "release-$Timestamp"
$PublishDir = Join-Path $ReleaseDir "publish"
$ScratchDir = Join-Path $ReleaseDir "scratch"
$ScratchObjDir = (Join-Path $ScratchDir "obj") + [IO.Path]::DirectorySeparatorChar
$ScratchBinDir = (Join-Path $ScratchDir "bin") + [IO.Path]::DirectorySeparatorChar
$IsolatedBuildProps = @(
    "/p:UseAppHost=false",
    "/p:BaseIntermediateOutputPath=$ScratchObjDir",
    "/p:BaseOutputPath=$ScratchBinDir"
)

New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null

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

function Copy-RestoreMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDir,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDir
    )

    if (-not (Test-Path (Join-Path $SourceDir "project.assets.json"))) {
        throw "Restore metadata missing in $SourceDir. Run dotnet restore first."
    }

    New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
    Get-ChildItem -Path $SourceDir -File |
        Where-Object {
            $_.Name -eq "project.assets.json" -or
            $_.Name -eq "project.nuget.cache" -or
            $_.Name.EndsWith(".props", [StringComparison]::OrdinalIgnoreCase) -or
            $_.Name.EndsWith(".targets", [StringComparison]::OrdinalIgnoreCase)
        } |
        Copy-Item -Destination $DestinationDir -Force
}

Write-Host "==> Restoring packages"
Invoke-NativeChecked dotnet restore $ProjectPath
if (Test-Path $TestsPath) {
    Invoke-NativeChecked dotnet restore $TestsPath
}
Copy-RestoreMetadata -SourceDir $DefaultObjDir -DestinationDir $ScratchObjDir

Write-Host "==> Building app"
Invoke-NativeChecked dotnet build $ProjectPath --configuration $Configuration --no-restore @IsolatedBuildProps

if (-not $SkipTests -and (Test-Path $TestsPath)) {
    Write-Host "==> Running tests"
    Invoke-NativeChecked dotnet test $TestsPath --configuration $TestConfiguration --no-restore /p:UseAppHost=false
}

if (-not $SkipMigrationScript) {
    $MigrationScript = Join-Path $ReleaseDir "database-migration-idempotent.sql"
    Write-Host "==> Generating idempotent migration script"
    Invoke-NativeChecked dotnet build $ProjectPath --configuration Debug --no-restore /p:UseAppHost=false
    Invoke-NativeChecked dotnet ef migrations script --idempotent `
        --project $ProjectPath `
        --startup-project $ProjectPath `
        --configuration Debug `
        --no-build `
        --output $MigrationScript
}

Write-Host "==> Publishing app"
Invoke-NativeChecked dotnet publish $ProjectPath --configuration $Configuration --no-build --output $PublishDir @IsolatedBuildProps

$ManifestPath = Join-Path $ReleaseDir "release-manifest.txt"
@(
    "CarRentalSystem production MVP release"
    "CreatedAt=$((Get-Date).ToString("o"))"
    "Configuration=$Configuration"
    "TestConfiguration=$TestConfiguration"
    "ProjectPath=$ProjectPath"
    "TestsPath=$TestsPath"
    "PublishDir=$PublishDir"
    "ScratchDir=$ScratchDir"
    "MigrationScript=$([bool](-not $SkipMigrationScript))"
    "TestsRun=$([bool](-not $SkipTests))"
) | Set-Content -Path $ManifestPath -Encoding UTF8

Write-Host ""
Write-Host "Release package created:"
Write-Host $ReleaseDir
