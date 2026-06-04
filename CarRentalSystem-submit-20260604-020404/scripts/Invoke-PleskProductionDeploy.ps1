[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDir,

    [Parameter(Mandatory = $true)]
    [string]$SiteName,

    [Parameter(Mandatory = $true)]
    [string]$SitePath,

    [Parameter(Mandatory = $true)]
    [string]$AppPoolName,

    [string]$AppPoolIdentity = "",
    [string]$EnvFile = "C:\CarRentalSystem\secrets\.env.production",
    [string]$DataProtectionKeysPath = "C:\CarRentalSystem\keys",
    [string]$BackupRoot = "C:\CarRentalSystem\backups",
    [string]$AppCmd = (Join-Path $env:windir "System32\inetsrv\appcmd.exe"),
    [string]$SqlServer = ".",
    [string]$DatabaseName = "",
    [PSCredential]$SqlCredential,
    [switch]$TrustServerCertificate,
    [switch]$ApplyMigration,
    [switch]$BackupDatabase,
    [string]$DatabaseBackupPath = "",
    [switch]$SkipDatabaseBackup,
    [string]$BaseUrl = "",
    [switch]$SkipSmokeTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$SetEnvScript = Join-Path $PSScriptRoot "Set-IisEnvironmentVariables.ps1"
$PublishDir = Join-Path $PackageDir "publish"
$MigrationScript = Join-Path $PackageDir "database-migration-idempotent.sql"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

if ([string]::IsNullOrWhiteSpace($AppPoolIdentity)) {
    $AppPoolIdentity = "IIS AppPool\$AppPoolName"
}

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

function Get-EnvValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    foreach ($rawLine in Get-Content $EnvFile) {
        $line = $rawLine.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
            continue
        }

        $parts = $line.Split("=", 2)
        if ($parts.Count -eq 2 -and $parts[0].Trim() -eq $Name) {
            return $parts[1].Trim().Trim('"').Trim("'")
        }
    }

    return ""
}

function Grant-DirectoryAccess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Identity,

        [Parameter(Mandatory = $true)]
        [System.Security.AccessControl.FileSystemRights]$Rights
    )

    if ($PSCmdlet.ShouldProcess($Path, "Grant $Rights to $Identity")) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
        $acl = Get-Acl $Path
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $Identity,
            $Rights,
            "ContainerInherit,ObjectInherit",
            "None",
            "Allow")
        $acl.SetAccessRule($rule)
        Set-Acl -Path $Path -AclObject $acl
    }
}

function Invoke-SqlCmdFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputFile,

        [string]$Database = ""
    )

    $sqlArgs = @("-b", "-S", $SqlServer, "-i", $InputFile)
    if (-not [string]::IsNullOrWhiteSpace($Database)) {
        $sqlArgs += @("-d", $Database)
    }
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

Write-Host "==> Preflight"
if (-not (Test-Path $PublishDir)) {
    throw "Publish folder not found: $PublishDir. Run scripts\New-ProductionMvpRelease.ps1 first."
}
if (-not (Test-Path (Join-Path $PublishDir "CarRentalSystem.dll"))) {
    throw "Published app DLL not found in $PublishDir."
}
if (-not (Test-Path (Join-Path $PublishDir "web.config"))) {
    throw "Published web.config not found in $PublishDir."
}
if (-not ((Get-Content -Raw (Join-Path $PublishDir "web.config")) -match 'ASPNETCORE_ENVIRONMENT"\s+value="Production"')) {
    throw "publish\web.config must keep ASPNETCORE_ENVIRONMENT=Production."
}
if (-not (Test-Path $EnvFile)) {
    throw "Production env file not found: $EnvFile. Create it with scripts\New-ProductionEnvFile.ps1 and replace CHANGE_ME values."
}
if ((Get-Content -Raw $EnvFile) -match 'CHANGE_ME') {
    throw "Production env file still contains CHANGE_ME placeholders: $EnvFile"
}
if (-not (Test-Path $SetEnvScript)) {
    throw "Set-IisEnvironmentVariables.ps1 not found: $SetEnvScript"
}
if (-not (Test-Path $AppCmd)) {
    throw "IIS appcmd.exe not found. Run this script on the IIS server."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet is not available on PATH. Install the .NET 10 ASP.NET Core Hosting Bundle."
}
$runtimes = & dotnet --list-runtimes
if (-not ($runtimes | Where-Object { $_ -match '^Microsoft\.AspNetCore\.App\s+10\.' })) {
    throw ".NET 10 ASP.NET Core runtime is not installed. Install the .NET 10 ASP.NET Core Hosting Bundle, then restart IIS."
}
$moduleRoots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$moduleCandidates = $moduleRoots |
    ForEach-Object { Join-Path $_ "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll" }
if (-not ($moduleCandidates | Where-Object { Test-Path $_ })) {
    throw "AspNetCoreModuleV2 was not found. Install the .NET 10 ASP.NET Core Hosting Bundle, then restart IIS."
}
Invoke-NativeChecked $AppCmd list site /name:$SiteName | Out-Null
Invoke-NativeChecked $AppCmd list apppool /name:$AppPoolName | Out-Null
if ($ApplyMigration) {
    if ([string]::IsNullOrWhiteSpace($DatabaseName)) {
        throw "DatabaseName is required when ApplyMigration is set."
    }
    if (-not (Test-Path $MigrationScript)) {
        throw "Migration script not found: $MigrationScript"
    }
    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        throw "sqlcmd is required for database backup/migration."
    }
    if (-not $BackupDatabase -and -not $SkipDatabaseBackup) {
        throw "ApplyMigration requires either -BackupDatabase with -DatabaseBackupPath or an explicit -SkipDatabaseBackup."
    }
}
if ($BackupDatabase -and [string]::IsNullOrWhiteSpace($DatabaseBackupPath)) {
    $DatabaseBackupPath = Join-Path $BackupRoot "$DatabaseName-before-$Timestamp.bak"
}

Write-Host "==> Preparing server folders and permissions"
foreach ($dir in @((Split-Path -Parent $EnvFile), $DataProtectionKeysPath, $BackupRoot, $SitePath)) {
    if ($PSCmdlet.ShouldProcess($dir, "Create directory")) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}
Grant-DirectoryAccess -Path $SitePath -Identity $AppPoolIdentity -Rights "ReadAndExecute"
Grant-DirectoryAccess -Path $DataProtectionKeysPath -Identity $AppPoolIdentity -Rights "Modify"

Write-Host "==> Applying IIS production environment variables"
& $SetEnvScript -SiteName $SiteName -EnvFile $EnvFile -WhatIf:$WhatIfPreference
if ($LASTEXITCODE -ne 0) {
    throw "Failed to apply IIS environment variables."
}

if ($BackupDatabase) {
    Write-Host "==> Backing up database"
    $safeDatabaseName = $DatabaseName.Replace("]", "]]")
    $safeBackupPath = $DatabaseBackupPath.Replace("'", "''")
    $backupSqlPath = Join-Path $BackupRoot "backup-$DatabaseName-$Timestamp.sql"
    if (-not $WhatIfPreference) {
        "BACKUP DATABASE [$safeDatabaseName] TO DISK = N'$safeBackupPath' WITH INIT, CHECKSUM;" |
            Set-Content -Path $backupSqlPath -Encoding UTF8
    }
    if ($PSCmdlet.ShouldProcess($DatabaseName, "Backup database to $DatabaseBackupPath")) {
        Invoke-SqlCmdFile -InputFile $backupSqlPath
    }
}
elseif ($ApplyMigration -and $SkipDatabaseBackup) {
    Write-Warning "Database backup was explicitly skipped."
}

if ($ApplyMigration) {
    Write-Host "==> Applying idempotent EF migration script"
    if ($PSCmdlet.ShouldProcess($DatabaseName, "Apply $MigrationScript")) {
        Invoke-SqlCmdFile -InputFile $MigrationScript -Database $DatabaseName
    }
}

$SiteBackupDir = Join-Path $BackupRoot "site-$Timestamp"
Write-Host "==> Backing up current site files"
if ($PSCmdlet.ShouldProcess($SitePath, "Backup current site files to $SiteBackupDir")) {
    New-Item -ItemType Directory -Path $SiteBackupDir -Force | Out-Null
    if (Test-Path $SitePath) {
        Copy-Item -Path (Join-Path $SitePath "*") -Destination $SiteBackupDir -Recurse -Force
    }
}

Write-Host "==> Deploying publish output"
if ($PSCmdlet.ShouldProcess($AppPoolName, "Stop IIS app pool")) {
    & $AppCmd stop apppool /apppool.name:$AppPoolName
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Could not stop app pool $AppPoolName. Continuing with file copy."
    }
}
try {
    if ($PSCmdlet.ShouldProcess($SitePath, "Copy publish output from $PublishDir")) {
        New-Item -ItemType Directory -Path $SitePath -Force | Out-Null
        Copy-Item -Path (Join-Path $PublishDir "*") -Destination $SitePath -Recurse -Force
    }
}
finally {
    if ($PSCmdlet.ShouldProcess($AppPoolName, "Start IIS app pool")) {
        Invoke-NativeChecked $AppCmd start apppool /apppool.name:$AppPoolName
    }
}

if (-not $SkipSmokeTest -and -not $WhatIfPreference) {
    if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
        $BaseUrl = Get-EnvValue -Name "App__PublicBaseUrl"
    }
    if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
        throw "BaseUrl was not supplied and App__PublicBaseUrl was not found in $EnvFile."
    }

    $BaseUrl = $BaseUrl.TrimEnd("/")
    Write-Host "==> Smoke testing $BaseUrl"
    foreach ($path in @("/health", "/ready", "/", "/Auth/Login")) {
        $url = "$BaseUrl$path"
        Write-Host "Checking $url"
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30
        if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) {
            throw "Smoke check failed for ${url}: HTTP $($response.StatusCode)"
        }
    }
}

Write-Host ""
Write-Host "Production deploy finished."
Write-Host "Site backup: $SiteBackupDir"
if ($BackupDatabase) {
    Write-Host "Database backup: $DatabaseBackupPath"
}
