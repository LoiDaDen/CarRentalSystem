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

foreach ($item in $variables) {
    $section = "system.webServer/aspNetCore/environmentVariables"
    $path = "MACHINE/WEBROOT/APPHOST/$SiteName"
    $masked = if ($item.Name -match '(?i)(password|pass|secret|key|pin|connection)') { "***" } else { $item.Value }

    if ($PSCmdlet.ShouldProcess($SiteName, "Set IIS env var $($item.Name)=$masked")) {
        Invoke-NativeChecked $AppCmd set config $path `
            /section:$section `
            "/+[name='$($item.Name)',value='$($item.Value)']" `
            /commit:apphost
    }
}

Write-Host "IIS environment variables were applied for site: $SiteName"
Write-Host "Restart the IIS app pool after changing environment variables."
