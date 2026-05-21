param(
    [string]$BaseUrl = "http://localhost:5168",
    [string]$ResetSecret = "local-cypress-reset",
    [string]$PublicBaseUrl = "",
    [ValidateSet("Mock", "Real")]
    [string]$VendorMode = "Mock"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$projectPath = Join-Path $repoRoot "CarRentalSystem\CarRentalSystem.csproj"
$testsPath = Join-Path $repoRoot "CarRentalSystem.Tests\CarRentalSystem.Tests.csproj"
$outLog = Join-Path $repoRoot "carrental-cypress.out.log"
$errLog = Join-Path $repoRoot "carrental-cypress.err.log"

function Stop-ProcessTree {
    param([Parameter(Mandatory = $true)][int]$ProcessId)

    Get-CimInstance Win32_Process -Filter "ParentProcessId=$ProcessId" -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-ProcessTree -ProcessId $_.ProcessId }

    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}

Push-Location $repoRoot
try {
    $env:ASPNETCORE_ENVIRONMENT = "Cypress"
    $env:DOTNET_ENVIRONMENT = "Cypress"
    $env:ASPNETCORE_URLS = $BaseUrl
    $env:Cypress__ResetSecret = $ResetSecret
    $useRealVendors = $VendorMode -eq "Real"
    $vendorFlag = if ($useRealVendors) { "true" } else { "false" }
    $env:Cypress__UsePayOSSandbox = $vendorFlag
    $env:Cypress__UseFptSandbox = $vendorFlag
    $env:CYPRESS_USE_PAYOS_SANDBOX = $vendorFlag
    $env:CYPRESS_USE_FPT_SANDBOX = $vendorFlag
    $env:CYPRESS_VENDOR_MODE = $VendorMode
    $env:CYPRESS_RESET_SECRET = $ResetSecret
    if (-not [string]::IsNullOrWhiteSpace($PublicBaseUrl)) {
        $env:App__PublicBaseUrl = $PublicBaseUrl
    }
    Remove-Item Env:ELECTRON_RUN_AS_NODE -ErrorAction SilentlyContinue

    Write-Host "Cypress vendor mode: $VendorMode"

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
            throw "Cypress app did not become ready at $BaseUrl. See $outLog and $errLog."
        }

        $statusResponse = Invoke-WebRequest "$BaseUrl/__test/sandbox/status" `
            -UseBasicParsing `
            -TimeoutSec 10 `
            -Headers @{ "X-Cypress-Reset-Secret" = $ResetSecret }
        $sandboxStatus = $statusResponse.Content | ConvertFrom-Json
        if (-not $sandboxStatus.isCypressDatabase) {
            throw "Cypress safety check failed: DefaultConnection must target CarRentalSystem_Cypress."
        }
        if ($useRealVendors -and -not $sandboxStatus.ready) {
            $missing = ($sandboxStatus.missing -join ", ")
            throw "Cypress real vendor sandbox is not ready. Missing: $missing"
        }

        npm.cmd run test:e2e:full
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
