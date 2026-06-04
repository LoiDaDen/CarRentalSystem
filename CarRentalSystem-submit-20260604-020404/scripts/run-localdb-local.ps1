param(
    [string]$Database = "CarRentalSystem_Local",
    [string]$Url = "http://localhost:5168"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = $Url
$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=$Database;Trusted_Connection=True;MultipleActiveResultSets=false;TrustServerCertificate=True"
$env:App__PublicBaseUrl = $Url
$env:Security__RunMigrationsOnStartup = "true"
$env:Security__SeedReferenceDataOnStartup = "true"
$env:Security__EnableRecurringJobs = "false"
$env:Security__RequireRecaptcha = "false"

Write-Host "Starting CarRentalSystem at $Url with LocalDB database $Database"
dotnet run --no-launch-profile
