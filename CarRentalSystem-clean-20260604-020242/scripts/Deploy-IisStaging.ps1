[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDir,

    [Parameter(Mandatory = $true)]
    [string]$SitePath,

    [Parameter(Mandatory = $true)]
    [string]$AppPoolName,

    [string]$BackupRoot = "C:\CarRentalSystem\backups",
    [string]$SqlServer = ".",
    [string]$DatabaseName = "",
    [PSCredential]$SqlCredential,
    [switch]$TrustServerCertificate,
    [switch]$ApplyMigration
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$PublishDir = Join-Path $PackageDir "publish"
$MigrationScript = Join-Path $PackageDir "database-migration-idempotent.sql"
$AppCmd = Join-Path $env:windir "System32\inetsrv\appcmd.exe"

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

if (-not (Test-Path $PublishDir)) {
    throw "Publish folder not found: $PublishDir"
}

if (-not (Test-Path $AppCmd)) {
    throw "IIS appcmd.exe not found. Run this on the IIS server."
}

if ($ApplyMigration) {
    if ([string]::IsNullOrWhiteSpace($DatabaseName)) {
        throw "DatabaseName is required when ApplyMigration is set."
    }
    if (-not (Test-Path $MigrationScript)) {
        throw "Migration script not found: $MigrationScript"
    }
    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        throw "sqlcmd is required to apply migrations."
    }
}

$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$BackupDir = Join-Path $BackupRoot "site-$Timestamp"
New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null

if ($PSCmdlet.ShouldProcess($SitePath, "Backup current IIS site files")) {
    if (Test-Path $SitePath) {
        Copy-Item -Path (Join-Path $SitePath "*") -Destination $BackupDir -Recurse -Force
    }
}

if ($ApplyMigration -and $PSCmdlet.ShouldProcess("SQL Server", "Apply idempotent EF migration script")) {
    $sqlArgs = @("-b", "-S", $SqlServer, "-d", $DatabaseName, "-i", $MigrationScript)
    if ($TrustServerCertificate) {
        $sqlArgs += "-C"
    }
    if ($SqlCredential) {
        $sqlArgs += @("-U", $SqlCredential.UserName)
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlCredential.Password)
        try {
            $sqlArgs += @("-P", [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr))
        }
        finally {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }
    Invoke-NativeChecked sqlcmd @sqlArgs
}

if ($PSCmdlet.ShouldProcess($AppPoolName, "Stop IIS app pool")) {
    Invoke-NativeChecked $AppCmd stop apppool /apppool.name:$AppPoolName
}

try {
    if ($PSCmdlet.ShouldProcess($SitePath, "Copy new publish output")) {
        New-Item -ItemType Directory -Path $SitePath -Force | Out-Null
        Copy-Item -Path (Join-Path $PublishDir "*") -Destination $SitePath -Recurse -Force
    }
}
finally {
    if ($PSCmdlet.ShouldProcess($AppPoolName, "Start IIS app pool")) {
        Invoke-NativeChecked $AppCmd start apppool /apppool.name:$AppPoolName
    }
}

Write-Host ""
Write-Host "Staging deploy finished."
Write-Host "Backup: $BackupDir"
