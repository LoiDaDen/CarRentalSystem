[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$SiteName,

    [Parameter(Mandatory = $true)]
    [string]$EnvFile,

    [string]$AppCmd = (Join-Path $env:windir "System32\inetsrv\appcmd.exe")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $EnvFile)) {
    throw "Env file not found: $EnvFile"
}

if (-not (Test-Path $AppCmd)) {
    throw "IIS appcmd.exe not found. Run this script on the IIS server."
}

$AdministrationAssembly = Join-Path $env:windir "System32\inetsrv\Microsoft.Web.Administration.dll"
if (-not (Test-Path $AdministrationAssembly)) {
    throw "Microsoft.Web.Administration.dll not found. Install IIS Management Scripts and Tools."
}

$variables = @()
foreach ($rawLine in Get-Content $EnvFile) {
    $line = $rawLine.Trim()
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
        continue
    }

    $parts = $line.Split("=", 2)
    if ($parts.Count -ne 2 -or [string]::IsNullOrWhiteSpace($parts[0])) {
        throw "Invalid env line: $rawLine"
    }

    $variables += [pscustomobject]@{
        Name = $parts[0].Trim()
        Value = $parts[1].Trim().Trim('"').Trim("'")
    }
}

$duplicates = $variables |
    Group-Object -Property Name |
    Where-Object { $_.Count -gt 1 } |
    Select-Object -ExpandProperty Name
if ($duplicates) {
    throw "Duplicate env variables in ${EnvFile}: $($duplicates -join ', ')"
}

if (-not ($variables | Where-Object { $_.Name -eq "ASPNETCORE_ENVIRONMENT" -and $_.Value -eq "Production" })) {
    Write-Warning "ASPNETCORE_ENVIRONMENT=Production is not present in $EnvFile. web.config may still provide it, but keeping it in IIS env vars is recommended."
}

$serverManagerType = "Microsoft.Web.Administration.ServerManager" -as [type]
if ($null -eq $serverManagerType) {
    Add-Type -Path $AdministrationAssembly
}

$serverManager = $null
try {
    $serverManager = [Microsoft.Web.Administration.ServerManager]::new()
    $site = $serverManager.Sites[$SiteName]
    if ($null -eq $site) {
        throw "IIS site not found: $SiteName"
    }

    $configuration = $serverManager.GetApplicationHostConfiguration()
    $section = $configuration.GetSection("system.webServer/aspNetCore", $SiteName)
    $collection = $section.GetCollection("environmentVariables")

    foreach ($item in $variables) {
        $masked = if ($item.Name -match '(?i)(password|pass|secret|key|pin|connection)') { "***" } else { $item.Value }

        if ($PSCmdlet.ShouldProcess($SiteName, "Set IIS env var $($item.Name)=$masked")) {
            $existing = @($collection | Where-Object { [string]$_["name"] -eq $item.Name })
            foreach ($element in $existing) {
                [void]$collection.Remove($element)
            }

            $newElement = $collection.CreateElement("environmentVariable")
            $newElement["name"] = $item.Name
            $newElement["value"] = $item.Value
            [void]$collection.Add($newElement)
        }
    }

    if ($PSCmdlet.ShouldProcess($SiteName, "Commit IIS environment variable changes")) {
        $serverManager.CommitChanges()
    }
}
finally {
    if ($null -ne $serverManager) {
        $serverManager.Dispose()
    }
}

Write-Host "IIS environment variables were applied for site: $SiteName"
Write-Host "Restart the IIS app pool after changing environment variables."
