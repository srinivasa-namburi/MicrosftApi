param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$')]
    [string]$ClientId,

    [Parameter(Mandatory = $true, Position = 1)]
    [ValidatePattern('^https?://')]
    [string]$DeploymentUrl
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RedirectUrl = "$DeploymentUrl/signin-oidc"

# Function to display usage
function Show-Usage {
    Write-Host "Usage: $($MyInvocation.MyCommand.Name) <client-id> <deployment-url>"
    Write-Host ""
    Write-Host "Parameters:"
    Write-Host "  client-id       The ClientId from PVICO_ENTRA_CREDENTIALS (Entra ID app registration)"
    Write-Host "  deployment-url  The base URL of your deployment (without /signin-oidc)"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\$($MyInvocation.MyCommand.Name) ""12345678-1234-1234-1234-123456789012"" ""https://20.124.45.67"""
    Write-Host "  .\$($MyInvocation.MyCommand.Name) ""12345678-1234-1234-1234-123456789012"" ""https://myapp.example.com"""
    Write-Host ""
    exit 1
}

Write-Host "🔧 Attempting to update Entra ID app registration redirect URL..."
Write-Host "   Client ID: $ClientId"
Write-Host "   Redirect URL: $RedirectUrl"

# Check if Azure CLI is available
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Error: Azure CLI is not installed or not in PATH" -ForegroundColor Red
    Write-Host "   Please install Azure CLI and authenticate before running this script"
    exit 1
}

# Check if we're authenticated to Azure
try {
    az account show 2>$null | Out-Null
} catch {
    Write-Host "❌ Error: Not authenticated to Azure" -ForegroundColor Red
    Write-Host "   Please run 'az login' to authenticate before running this script"
    exit 1
}

# Attempt to update the app registration
Write-Host "🔄 Attempting to add redirect URL to app registration..."

# Try to get current redirect URIs
try {
    $currentRedirectsJson = az ad app show --id $ClientId --query "web.redirectUris" --output json 2>$null
    if ($LASTEXITCODE -ne 0) { throw }

    Write-Host "✅ Successfully retrieved current app registration" -ForegroundColor Green

    $currentRedirects = $currentRedirectsJson | ConvertFrom-Json

    # Check if the redirect URL already exists
    if ($currentRedirects -contains $RedirectUrl) {
        Write-Host "✅ Redirect URL already configured: $RedirectUrl" -ForegroundColor Green
        Write-Host "   No changes needed"
        exit 0
    }

    # Add the new redirect URL to existing ones
    $newRedirects = $currentRedirects + $RedirectUrl

    # Attempt to update the app registration
    try {
        az ad app update --id $ClientId --web-redirect-uris $newRedirects 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { throw }

        Write-Host "✅ Successfully added redirect URL to app registration!" -ForegroundColor Green
        Write-Host "   Added: $RedirectUrl"
        Write-Host "   Your application should now be able to authenticate users"
    } catch {
        Write-Host "⚠️  Failed to update app registration (insufficient permissions)" -ForegroundColor Yellow
        Write-Host "   The deployment service principal does not have sufficient permissions to modify the app registration"
        Write-Host ""
        Write-Host "🔧 Manual Configuration Required:" -ForegroundColor Cyan
        Write-Host "   1. Navigate to Azure Portal → Azure Active Directory → App registrations"
        Write-Host "   2. Search for app with Client ID: $ClientId"
        Write-Host "   3. Go to Authentication → Platform configurations → Web"
        Write-Host "   4. Add redirect URI: $RedirectUrl"
        Write-Host "   5. Save the configuration"
        Write-Host ""
        Write-Host "   Alternative using Azure CLI (with appropriate permissions):"
        Write-Host "   az ad app update --id ""$ClientId"" --web-redirect-uris $RedirectUrl"

        # Don't exit with error - this is expected in many deployment scenarios
        exit 0
    }
} catch {
    Write-Host "⚠️  Failed to retrieve app registration (app not found or insufficient permissions)" -ForegroundColor Yellow
    Write-Host "   Client ID: $ClientId"
    Write-Host ""
    Write-Host "🔧 Manual Configuration Required:" -ForegroundColor Cyan
    Write-Host "   1. Verify the Client ID is correct (from PVICO_ENTRA_CREDENTIALS)"
    Write-Host "   2. Navigate to Azure Portal → Azure Active Directory → App registrations"
    Write-Host "   3. Search for app with Client ID: $ClientId"
    Write-Host "   4. Go to Authentication → Platform configurations → Web"
    Write-Host "   5. Add redirect URI: $RedirectUrl"
    Write-Host "   6. Save the configuration"
    Write-Host ""
    Write-Host "   If the app registration doesn't exist, run the sp-create.ps1 script first:"
    Write-Host "   .\build\scripts\sp-create.ps1"

    # Don't exit with error - provide guidance instead
    exit 0
}

Write-Host "✅ App registration redirect URL configuration completed!" -ForegroundColor Green