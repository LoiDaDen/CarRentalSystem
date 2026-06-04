[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$OutputPath = "C:\CarRentalSystem\secrets\.env.production",

    [Parameter(Mandatory = $true)]
    [string]$Domain,

    [Parameter(Mandatory = $true)]
    [string]$SqlConnectionString
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Domain = $Domain.Trim().TrimEnd("/")
if ([string]::IsNullOrWhiteSpace($Domain)) {
    throw "Domain is required."
}

if ($Domain.StartsWith("http://", [StringComparison]::OrdinalIgnoreCase) -or
    $Domain.StartsWith("https://", [StringComparison]::OrdinalIgnoreCase)) {
    $BaseUrl = $Domain
    $HostName = ([Uri]$Domain).Host
}
else {
    $BaseUrl = "https://$Domain"
    $HostName = $Domain
}

$SecretDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $SecretDir)) {
    New-Item -ItemType Directory -Path $SecretDir -Force | Out-Null
}

function New-RandomSecret([int]$Bytes = 48) {
    $buffer = New-Object byte[] $Bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($buffer)
    [Convert]::ToBase64String($buffer)
}

$content = @(
    "# CarRentalSystem production environment variables"
    "# Fill every CHANGE_ME value with the real provider secret before applying this file to IIS."
    "# Keep this file outside the repository."
    ""
    "ASPNETCORE_ENVIRONMENT=Production"
    "AllowedHosts=$HostName"
    "App__PublicBaseUrl=$BaseUrl"
    "ConnectionStrings__DefaultConnection=$SqlConnectionString"
    ""
    "Cors__AllowedOrigins__0=$BaseUrl"
    "DataProtection__KeysPath=C:\CarRentalSystem\keys"
    "Tracking__IngestSecret=$(New-RandomSecret)"
    "Auth__LoginTimeoutSeconds=15"
    "Auth__MfaQueueTimeoutSeconds=5"
    "Auth__EmailSendTimeoutSeconds=12"
    ""
    "Security__RunMigrationsOnStartup=false"
    "Security__SeedReferenceDataOnStartup=false"
    "Security__EnableRecurringJobs=true"
    "Security__RequirePrivilegedMfa=true"
    "Security__RequireRecaptcha=true"
    "Security__RequirePrivilegedSso=false"
    "Security__MfaExemptEmails__0=mgr.north@carrental.com"
    "Security__MfaExemptEmails__1=mgr.central@carrental.com"
    "Security__MfaExemptEmails__2=mgr.south@carrental.com"
    "Security__MfaExemptEmails__3=mgr.southeast@carrental.com"
    "Security__MfaExemptEmails__4=mgr.centralhighlands@carrental.com"
    "Security__MfaExemptEmails__5=staff.north@carrental.com"
    "Security__MfaExemptEmails__6=staff.central@carrental.com"
    "Security__MfaExemptEmails__7=staff.south@carrental.com"
    "Security__MfaExemptEmails__8=staff.southeast@carrental.com"
    "Security__MfaExemptEmails__9=staff.centralhighlands@carrental.com"
    "Security__AdminMfaExemptEmails__0=hloi78274@gmail.com"
    ""
    "PayOS__ClientId=CHANGE_ME"
    "PayOS__ApiKey=CHANGE_ME"
    "PayOS__ChecksumKey=CHANGE_ME"
    "PayOS__PayoutEnabled=false"
    "PayOS__PartnerCode="
    ""
    "SmtpSettings__Host=CHANGE_ME"
    "SmtpSettings__Port=587"
    "SmtpSettings__User=CHANGE_ME"
    "SmtpSettings__Pass=CHANGE_ME"
    ""
    "FptAi__ApiKey=CHANGE_ME"
    "FptAi__MaxThrottlingRetries=3"
    ""
    "Recaptcha__SiteKey=CHANGE_ME"
    "Recaptcha__SecretKey=CHANGE_ME"
    ""
    "Ai__Enabled=false"
    "Redis__ConnectionString="
)

if ($PSCmdlet.ShouldProcess($OutputPath, "Write production env template")) {
    $content | Set-Content -Path $OutputPath -Encoding UTF8
}

Write-Host "Production env file template written:"
Write-Host $OutputPath
Write-Host "Replace every CHANGE_ME value before running Set-IisEnvironmentVariables.ps1 or Invoke-PleskProductionDeploy.ps1."
