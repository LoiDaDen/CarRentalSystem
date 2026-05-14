[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$BaseUrl = $BaseUrl.TrimEnd("/")
$checks = @(
    @{ Name = "health"; Url = "$BaseUrl/health" },
    @{ Name = "home"; Url = "$BaseUrl/" },
    @{ Name = "login"; Url = "$BaseUrl/Auth/Login" }
)

foreach ($check in $checks) {
    Write-Host "==> Checking $($check.Name): $($check.Url)"
    $response = Invoke-WebRequest -Uri $check.Url -UseBasicParsing -TimeoutSec 20
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) {
        throw "Smoke check failed for $($check.Name): HTTP $($response.StatusCode)"
    }
}

Write-Host "Staging smoke checks passed."
