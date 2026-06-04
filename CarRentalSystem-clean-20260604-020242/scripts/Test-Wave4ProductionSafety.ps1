param(
    [string]$RepoRoot = ".",
    [switch]$RequireCompletedEvidence
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path $RepoRoot).Path
Push-Location $root

try {
    function Assert-Contains {
        param([string]$Path, [string]$Pattern, [string]$Message)
        $content = Get-Content -LiteralPath $Path -Raw
        if ($content -notmatch $Pattern) { throw $Message }
    }
    function Assert-NotContains {
        param([string]$Path, [string]$Pattern, [string]$Message)
        $content = Get-Content -LiteralPath $Path -Raw
        if ($content -match $Pattern) { throw $Message }
    }
    function Assert-Exists {
        param([string]$Path, [string]$Message)
        if (-not (Test-Path -LiteralPath $Path)) { throw $Message }
    }
    function Assert-Evidence {
        param([string]$Path)
        Assert-Exists $Path "$Path is missing."
        if ($RequireCompletedEvidence) {
            Assert-NotContains $Path "Status:\s*TEMPLATE" "$Path is still a template."
            Assert-NotContains $Path "\bTBD\b" "$Path still contains TBD placeholders."
        }
    }

    Write-Host "[wave4] Checking runtime support/API/fleet surfaces..."
    Assert-Exists "Models\SupportTicket.cs" "SupportTicket model is missing."
    Assert-Exists "Models\ApiKey.cs" "ApiKey model is missing."
    Assert-Exists "Services\SupportTicketService.cs" "Support ticket service is missing."
    Assert-Exists "Services\ApiKeyService.cs" "API key service is missing."
    Assert-Exists "Services\WebhookSigningService.cs" "Webhook signing service is missing."
    Assert-Exists "Areas\Admin\Controllers\FleetOpsController.cs" "Fleet ops dashboard controller is missing."
    Assert-Contains "Program.cs" "AddScoped<ISupportTicketService, SupportTicketService>" "SupportTicketService must be registered."
    Assert-Contains "Program.cs" "AddScoped<IApiKeyService, ApiKeyService>" "ApiKeyService must be registered."
    Assert-Contains "Program.cs" "AddScoped<IWebhookSigningService, WebhookSigningService>" "WebhookSigningService must be registered."

    Write-Host "[wave4] Checking API safety config..."
    Assert-Contains "Services\ConfigurationGuard.cs" "PublicApi:RequireApiKey" "Production guard must require API keys when PublicApi is enabled."
    Assert-Contains "Services\ConfigurationGuard.cs" "PublicApi:RequireWebhookSigning" "Production guard must require webhook signing when PublicApi is enabled."
    Assert-Contains "appsettings.Production.example.json" '"PublicApi"\s*:\s*\{[\s\S]*"Enabled"\s*:\s*false' "Public API must be disabled by default."

    Write-Host "[wave4] Checking evidence docs..."
    Assert-Exists "docs\wave4-accessibility-audit-plan.md" "Accessibility audit plan is missing."
    Assert-Exists "docs\wave4-developer-portal-readiness.md" "Developer portal readiness doc is missing."
    Assert-Evidence "docs\wave4-evidence\accessibility-audit.md"
    Assert-Evidence "docs\wave4-evidence\support-sla.md"
    Assert-Evidence "docs\wave4-evidence\fleet-ops.md"
    Assert-Evidence "docs\wave4-evidence\api-readiness.md"
    Assert-Contains "docs\enterprise-production-readiness-gates.md" "Wave 4 implemented gates" "Readiness gates must include Wave 4 implemented gates."
    Assert-Contains "docs\enterprise-completion-roadmap.md" "Wave 4 implementation status" "Completion roadmap must show Wave 4 implementation status."

    Write-Host "[wave4] Production safety checks passed."
}
finally { Pop-Location }
