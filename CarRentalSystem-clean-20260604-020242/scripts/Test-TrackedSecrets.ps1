[CmdletBinding()]
param(
    [string]$RepoRoot = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) { (Get-Location).Path } else { $PSScriptRoot }
    $RepoRoot = Split-Path -Parent $scriptRoot
}

function Get-TrackedFiles {
    param([string]$Root)

    Push-Location $Root
    try {
        $files = & git ls-files --cached --others --exclude-standard
        if ($LASTEXITCODE -ne 0) {
            throw "git ls-files failed."
        }
        return $files
    }
    finally {
        Pop-Location
    }
}

function Test-IsAllowedPlaceholder {
    param([string]$Value)

    $trimmed = $Value.Trim().Trim('"').Trim("'")
    return [string]::IsNullOrWhiteSpace($trimmed) `
        -or $trimmed -match '^(YOUR_|CHANGE_TO_|localhost|127\.0\.0\.1|\*)' `
        -or $trimmed -match '^(true|false|[0-9]+)$' `
        -or $trimmed -match '^Server=YOUR_' `
        -or $trimmed -match '^Server=\(localdb\)'
}

$suspicious = New-Object System.Collections.Generic.List[string]
$candidateExtensions = @(".json", ".config", ".cs", ".cshtml", ".ps1", ".yml", ".yaml", ".env", ".txt", ".md")
$skipPathPatterns = @(
    '^Migrations/',
    '^docs/',
    '^wwwroot/lib/',
    '^bin/',
    '^obj/',
    '^artifacts/'
)

$files = Get-TrackedFiles -Root $RepoRoot
foreach ($relativePath in $files) {
    $normalized = $relativePath -replace '\\', '/'
    if ($skipPathPatterns | Where-Object { $normalized -match $_ }) {
        continue
    }

    $extension = [IO.Path]::GetExtension($relativePath)
    if ($candidateExtensions -notcontains $extension -and -not $relativePath.EndsWith(".env.example")) {
        continue
    }

    $path = Join-Path $RepoRoot $relativePath
    if (-not (Test-Path $path)) {
        continue
    }

    $lineNumber = 0
    foreach ($line in Get-Content -Path $path) {
        $lineNumber++

        if ($line -match '(?i)(password|pwd)\s*=(?!>)\s*([^;\s]+)') {
            $value = $Matches[2]
            if ($value -match '^\$' -or $value -match '^\[' -or $value -eq 'Read-Host') {
                continue
            }

            if ($value -match '^[A-Za-z_][A-Za-z0-9_.]*,?$') {
                continue
            }

            if (-not (Test-IsAllowedPlaceholder $value)) {
                $suspicious.Add("${relativePath}:${lineNumber}: possible password/connection secret")
            }
        }

        if ($line -match '(?i)"(ApiKey|SecretKey|ChecksumKey|ClientId|IngestSecret|Pass|Pin)"\s*:\s*"([^"]+)"') {
            $value = $Matches[2]
            if (-not (Test-IsAllowedPlaceholder $value)) {
                $suspicious.Add("${relativePath}:${lineNumber}: possible JSON secret value")
            }
        }

        if ($line -match '(?i)^(PayOS|FptAi|Groq|Gemini|Recaptcha|SmtpSettings|Tracking|Admin)__(ApiKey|SecretKey|ChecksumKey|ClientId|IngestSecret|Pass|Pin|ConnectionString)=(.+)$') {
            $value = $Matches[3]
            if (-not (Test-IsAllowedPlaceholder $value)) {
                $suspicious.Add("${relativePath}:${lineNumber}: possible env secret value")
            }
        }
    }
}

if ($suspicious.Count -gt 0) {
    $suspicious | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    throw "Tracked secret scan failed with $($suspicious.Count) finding(s)."
}

Write-Host "Tracked secret scan passed."
