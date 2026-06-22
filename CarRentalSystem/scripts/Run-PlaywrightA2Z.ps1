param(
    [string]$BaseUrl = "http://localhost:5168",
    [string]$ResetSecret = "local-playwright-reset",
    [string]$PublicBaseUrl = "",
    [string]$TestcaseXlsx = "",
    [ValidateSet("Mock", "Real")]
    [string]$VendorMode = "Mock"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$projectPath = Join-Path $repoRoot "CarRentalSystem\CarRentalSystem.csproj"
$testsPath = Join-Path $repoRoot "CarRentalSystem.Tests\CarRentalSystem.Tests.csproj"
$outLog = Join-Path $repoRoot "carrental-playwright.out.log"
$errLog = Join-Path $repoRoot "carrental-playwright.err.log"

function Stop-ProcessTree {
    param([Parameter(Mandatory = $true)][int]$ProcessId)

    Get-CimInstance Win32_Process -Filter "ParentProcessId=$ProcessId" -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-ProcessTree -ProcessId $_.ProcessId }

    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}

Push-Location $repoRoot
try {
    $env:ASPNETCORE_ENVIRONMENT = "Playwright"
    $env:DOTNET_ENVIRONMENT = "Playwright"
    $env:ASPNETCORE_URLS = $BaseUrl
    $env:PLAYWRIGHT_BASE_URL = $BaseUrl
    $env:Playwright__ResetSecret = $ResetSecret
    $useRealVendors = $VendorMode -eq "Real"
    $vendorFlag = if ($useRealVendors) { "true" } else { "false" }
    $env:Playwright__UsePayOSSandbox = $vendorFlag
    $env:Playwright__UseFptSandbox = $vendorFlag
    $env:PLAYWRIGHT_USE_PAYOS_SANDBOX = $vendorFlag
    $env:PLAYWRIGHT_USE_FPT_SANDBOX = $vendorFlag
    $env:PLAYWRIGHT_VENDOR_MODE = $VendorMode
    $env:PLAYWRIGHT_RESET_SECRET = $ResetSecret
    if (-not [string]::IsNullOrWhiteSpace($PublicBaseUrl)) {
        $env:App__PublicBaseUrl = $PublicBaseUrl
    }
    Remove-Item Env:ELECTRON_RUN_AS_NODE -ErrorAction SilentlyContinue

    Write-Host "Playwright vendor mode: $VendorMode"
    if (-not [string]::IsNullOrWhiteSpace($TestcaseXlsx)) {
        $resolvedWorkbook = Resolve-Path $TestcaseXlsx -ErrorAction Stop
        $env:TESTCASE_XLSX = $resolvedWorkbook.Path
        Write-Host "Playwright testcase source: workbook $($resolvedWorkbook.Path)"
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:TESTCASE_XLSX)) {
        if (-not (Test-Path $env:TESTCASE_XLSX)) {
            throw "TESTCASE_XLSX does not exist: $env:TESTCASE_XLSX"
        }
        Write-Host "Playwright testcase source: workbook $env:TESTCASE_XLSX"
    }
    else {
        Write-Host "Playwright testcase source: committed fixture/specs"
    }

    dotnet build $projectPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet test $testsPath --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    npm.cmd run testcases:extract
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if (Test-Path $outLog) { Remove-Item $outLog -Force }
    if (Test-Path $errLog) { Remove-Item $errLog -Force }

    $app = Start-Process dotnet `
        -ArgumentList @("run", "--project", $projectPath, "--no-launch-profile") `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $outLog `
        -RedirectStandardError $errLog

    try {
        $deadline = (Get-Date).AddSeconds(90)
        do {
            try {
                $response = Invoke-WebRequest "$BaseUrl/live" -UseBasicParsing -TimeoutSec 3
                if ($response.StatusCode -eq 200) { break }
            } catch {
                Start-Sleep -Seconds 2
            }
        } while ((Get-Date) -lt $deadline)

        if ((Get-Date) -ge $deadline) {
            throw "Playwright app did not become ready at $BaseUrl. See $outLog and $errLog."
        }

        $statusResponse = Invoke-WebRequest "$BaseUrl/__test/sandbox/status" `
            -UseBasicParsing `
            -TimeoutSec 10 `
            -Headers @{ "X-Playwright-Reset-Secret" = $ResetSecret }
        $sandboxStatus = $statusResponse.Content | ConvertFrom-Json
        if (-not $sandboxStatus.isPlaywrightDatabase) {
            throw "Playwright safety check failed: DefaultConnection must target CarRentalSystem_Playwright."
        }
        if ($useRealVendors -and -not $sandboxStatus.ready) {
            $missing = ($sandboxStatus.missing -join ", ")
            throw "Playwright real vendor sandbox is not ready. Missing: $missing"
        }

        npm.cmd run test:e2e:full -- --project=chromium
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        npm.cmd run testcases:report
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    finally {
        if ($app) {
            Stop-ProcessTree -ProcessId $app.Id
        }
    }
}
finally {
    Pop-Location
}
