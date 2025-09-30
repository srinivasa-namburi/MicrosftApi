# Copyright (c) Microsoft Corporation. All rights reserved.
# Development Environment Setup Script for Windows
# Installs: Docker, .NET 9.0 SDK, VS Code, Dev Containers extension, wasm-tools workload, dotnet ef tools, Azure CLI
# Usage examples:
#   pwsh -ExecutionPolicy Bypass -File scripts/setup-dev-environment.ps1
#   pwsh scripts/setup-dev-environment.ps1 -SkipDocker -SkipVSCode
#   pwsh scripts/setup-dev-environment.ps1 -Force   # Reinstall even if present

param(
    [switch]$Force,
    [switch]$SkipDocker,
    [switch]$SkipDotNet,
    [switch]$SkipVSCode
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Microsoft Greenlight - Development Environment Setup" -ForegroundColor Magenta
Write-Host "=" * 60
Write-Host ""

# Check if running as administrator  
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
Write-Host "ℹ️  Administrator privileges: $(if ($isAdmin) { 'Enabled' } else { 'Not required for this setup' })" -ForegroundColor Cyan
Write-Host ""

function Test-Command {
    param(
        [Parameter(Mandatory=$true)][string]$Command
    )
    try {
        Get-Command -Name $Command -ErrorAction Stop | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

# Check if winget is available (early exit if missing)
if (Test-Command 'winget') {
    try {
        $wingetVersion = winget --version 2>$null
        if ([string]::IsNullOrWhiteSpace($wingetVersion)) {
            Write-Host "✅ Windows Package Manager detected" -ForegroundColor Green
        }
        else {
            Write-Host "✅ Windows Package Manager: $wingetVersion" -ForegroundColor Green
        }
    }
    catch {
        Write-Warning "⚠️  Winget present but version check failed: $($_.Exception.Message)"
    }
}
else {
    Write-Error "❌ Windows Package Manager (winget) is required but not found. Install from Microsoft Store or https://github.com/microsoft/winget-cli/releases and re-run."
    exit 1
}

Write-Host ""

# Preview actions that will be taken
Write-Host "📋 Setup Actions Preview" -ForegroundColor Magenta
Write-Host "=" * 30

$actions = @()
if (-not $SkipDocker) { $actions += "🐳 Install/Verify Docker Desktop" }
if (-not $SkipDotNet) { $actions += "🔧 Install/Verify .NET 9.0 SDK" }
if (-not $SkipVSCode) { $actions += "💻 Install/Verify VS Code + Dev Containers extension" }
$actions += "🛠️  Verify/Install wasm-tools workload"
$actions += "🔧 Verify/Install dotnet ef tools"
$actions += "☁️  Verify/Install Azure CLI"
$actions += "🔌 Configure MCP for Windows"
$actions += "⚙️  Setup AppHost development configuration"
$actions += "🔑 Optional: Auto-configure Azure AD settings"

foreach ($action in $actions) {
    Write-Host "   $action" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Press any key to continue or Ctrl+C to cancel..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
Write-Host ""
Write-Host "🚀 Starting setup..." -ForegroundColor Green
Write-Host ""

function Invoke-WingetInstall {
    param(
        [Parameter(Mandatory=$true)][string]$Id,
        [string]$DisplayName,
        [string]$AdditionalArgs
    )
    $pkg = if ($DisplayName) { $DisplayName } else { $Id }
    try {
        & winget install --id $Id --accept-source-agreements --accept-package-agreements $AdditionalArgs
        Write-Host "   ✅ $pkg installed" -ForegroundColor Green
        return $true
    }
    catch {
    Write-Warning ("   ❌ Failed to install {0} : {1}" -f $pkg, $_.Exception.Message)
        return $false
    }
}

function Confirm-AzureTenant {
    Write-Host "🔐 Azure Tenant Verification" -ForegroundColor Cyan
    Write-Host "-" * 30
    
    # Check if Azure CLI is available
    if (-not (Test-Command 'az')) {
        Write-Host "   ⚠️  Azure CLI not found - skipping tenant verification" -ForegroundColor Yellow
        return $true
    }
    
    # Check if user is logged in
    try {
        $currentAccount = az account show --query '{tenantId: tenantId, name: name, user: user.name, type: user.type}' --output json 2>$null | ConvertFrom-Json
        if (-not $currentAccount) {
            Write-Host "   ❌ Not logged into Azure CLI" -ForegroundColor Red
            Write-Host "      Please run: az login" -ForegroundColor Gray
            return $false
        }
    }
    catch {
        Write-Host "   ❌ Not logged into Azure CLI" -ForegroundColor Red
        Write-Host "      Please run: az login" -ForegroundColor Gray
        return $false
    }
    
    Write-Host "   ✅ Current Azure Login:" -ForegroundColor Green
    Write-Host "      User: $($currentAccount.user)" -ForegroundColor Gray
    Write-Host "      Tenant: $($currentAccount.name)" -ForegroundColor Gray
    Write-Host "      Tenant ID: $($currentAccount.tenantId)" -ForegroundColor Gray
    Write-Host "      Type: $($currentAccount.type)" -ForegroundColor Gray
    Write-Host ""
    
    # Warn about guest accounts
    if ($currentAccount.type -eq "guest") {
        Write-Host "   ⚠️  WARNING: You are logged in as a guest user" -ForegroundColor Yellow
        Write-Host "      This may limit your ability to manage app registrations" -ForegroundColor Gray
        Write-Host ""
    }
    
    $confirm = Read-Host "      Is this the correct tenant for your app registration? [Y/n]"
    if ($confirm -match '^[Nn]') {
        Write-Host ""
        Write-Host "   🔄 Available Tenants:" -ForegroundColor Cyan
        
        # Try to list available tenants
        try {
            $tenants = az account tenant list --query '[].{tenantId: tenantId, displayName: displayName, defaultDomain: defaultDomain}' --output json 2>$null | ConvertFrom-Json
            if ($tenants -and $tenants.Count -gt 0) {
                for ($i = 0; $i -lt $tenants.Count; $i++) {
                    $tenant = $tenants[$i]
                    $marker = if ($tenant.tenantId -eq $currentAccount.tenantId) { " (current)" } else { "" }
                    Write-Host "      [$($i + 1)] $($tenant.displayName) - $($tenant.defaultDomain)$marker" -ForegroundColor Gray
                    Write-Host "          Tenant ID: $($tenant.tenantId)" -ForegroundColor DarkGray
                }
                Write-Host ""
                
                $selection = Read-Host "      Select tenant number (1-$($tenants.Count)) or 'c' to continue with current"
                if ($selection -ne 'c' -and $selection -match '^\d+$') {
                    $selectedIndex = [int]$selection - 1
                    if ($selectedIndex -ge 0 -and $selectedIndex -lt $tenants.Count) {
                        $selectedTenant = $tenants[$selectedIndex]
                        Write-Host "   🔄 Switching to tenant: $($selectedTenant.displayName)" -ForegroundColor Yellow
                        
                        Write-Host "      This will open a browser window for authentication..." -ForegroundColor Gray
                        try {
                            # Set the tenant first
                            az account set --tenant $selectedTenant.tenantId
                            if ($LASTEXITCODE -ne 0) {
                                throw "Failed to set tenant"
                            }
                            
                            # Then login to the specific tenant
                            az login --tenant $selectedTenant.tenantId --only-show-errors
                            if ($LASTEXITCODE -ne 0) {
                                throw "Failed to login to tenant"
                            }
                            
                            Write-Host "   ✅ Successfully switched to tenant: $($selectedTenant.displayName)" -ForegroundColor Green
                        }
                        catch {
                            Write-Host "   ❌ Failed to switch tenants: $($_.Exception.Message)" -ForegroundColor Red
                            Write-Host "      Please run manually: az login --tenant $($selectedTenant.tenantId)" -ForegroundColor Gray
                            return $false
                        }
                    }
                }
            } else {
                Write-Host "      Could not retrieve tenant list" -ForegroundColor Gray
                Write-Host "      To switch tenants manually, run: az login --tenant <tenant-id>" -ForegroundColor Gray
            }
        }
        catch {
            Write-Host "      Could not retrieve tenant list" -ForegroundColor Gray
            Write-Host "      To switch tenants manually, run: az login --tenant <tenant-id>" -ForegroundColor Gray
        }
        
        Write-Host ""
        $finalConfirm = Read-Host "      Continue with setup? [Y/n]"
        if ($finalConfirm -match '^[Nn]') {
            Write-Host "   ⏹️  Setup cancelled. Please login to the correct tenant and re-run the script." -ForegroundColor Yellow
            return $false
        }
    }
    
    Write-Host ""
    return $true
}

function Configure-AzureOpenAISettings {
    param(
        [Parameter(Mandatory=$true)][string]$ConfigFilePath
    )
    
    try {
        Write-Host "   🤖 Configuring Azure OpenAI endpoint..." -ForegroundColor Yellow
        
        # Read current config
        $config = Get-Content -Path $ConfigFilePath -Raw | ConvertFrom-Json
        
        # Check if openai-planner connection string already exists and is valid (longer than 10 characters)
        $existingConnectionString = $null
        if ($config.ConnectionStrings -and $config.ConnectionStrings."openai-planner") {
            $existingConnectionString = $config.ConnectionStrings."openai-planner"
            if ($existingConnectionString.Length -gt 10) {
                Write-Host "   ✅ Found valid Azure OpenAI configuration" -ForegroundColor Green
                Write-Host "      Current: $existingConnectionString" -ForegroundColor Gray
                
                $updateChoice = Read-Host "      Update Azure OpenAI configuration? [y/N]"
                if (-not ($updateChoice -match '^[Yy]')) {
                    return
                }
            }
        }
        
        Write-Host "" 
        Write-Host "   📝 Azure OpenAI Configuration" -ForegroundColor Cyan
        Write-Host "      Example: https://your-resource.openai.azure.com/" -ForegroundColor Gray
        Write-Host ""
        
        do {
            $endpoint = Read-Host "      Enter Azure OpenAI Endpoint URL"
        } while ([string]::IsNullOrWhiteSpace($endpoint))
        
        # Ensure endpoint ends with /
        if (-not $endpoint.EndsWith("/")) {
            $endpoint += "/"
        }
        
        Write-Host "      Access Key (optional - press Enter to use Azure CLI identity):" -ForegroundColor Gray
        $accessKey = Read-Host "      Enter Access Key (optional)"
        
        # Format connection string based on whether key is provided
        if ([string]::IsNullOrWhiteSpace($accessKey)) {
            $connectionString = $endpoint
            Write-Host "   ✅ Configured for Azure CLI/Entra authentication" -ForegroundColor Green
        } else {
            $connectionString = "Endpoint=$endpoint;Key=$accessKey"
            Write-Host "   ✅ Configured with API key authentication" -ForegroundColor Green
        }
        
        # Ensure ConnectionStrings section exists
        if (-not $config.ConnectionStrings) {
            $config | Add-Member -Type NoteProperty -Name 'ConnectionStrings' -Value @{}
        }
        
        # Add the openai-planner connection string
        $config.ConnectionStrings | Add-Member -Type NoteProperty -Name 'openai-planner' -Value $connectionString -Force
        
        # Save updated config
        $config | ConvertTo-Json -Depth 10 | Set-Content -Path $ConfigFilePath
        
        Write-Host "   ✅ Azure OpenAI configuration updated successfully!" -ForegroundColor Green
        Write-Host "      Connection String: $(if ($accessKey) { $connectionString -replace 'Key=[^;]*', 'Key=[REDACTED]' } else { $connectionString })" -ForegroundColor Gray
        
    }
    catch {
        $errorMsg = "Azure OpenAI configuration failed: $($_.Exception.Message)"
        Write-Host "   ⚠️  $errorMsg" -ForegroundColor Yellow
    }
}

function Configure-AzureAdSettings {
    param(
        [Parameter(Mandatory=$true)][string]$ConfigFilePath
    )
    
    $logFile = Join-Path (Get-Location) "setup-azuread.log"
    $appName = "sp-ms-industrypermitting"
    
    try {
        Write-Host "   🔍 Checking Azure CLI login status..." -ForegroundColor Yellow
        $account = az account show --query '{tenantId: tenantId, name: name}' --output json 2>$null | ConvertFrom-Json
        
        if (-not $account) {
            Write-Host "   ⚠️  Please login to Azure CLI first: az login" -ForegroundColor Yellow
            return
        }
        
        Write-Host "   🔍 Tenant: $($account.name)" -ForegroundColor Gray
        $tenantId = $account.tenantId
        
        Write-Host "   🔍 Looking up app registration '$appName'..." -ForegroundColor Yellow
        $appInfo = az ad app list --display-name $appName --query '[0].{appId: appId, id: id}' --output json 2>$null | ConvertFrom-Json
        
        if (-not $appInfo -or -not $appInfo.appId) {
            Write-Host "   ⚠️  App registration '$appName' not found" -ForegroundColor Yellow
            "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'): App registration '$appName' not found" | Out-File -FilePath $logFile -Append
            return
        }
        
        $clientId = $appInfo.appId
        Write-Host "   🔑 Found app: $clientId" -ForegroundColor Gray
        
        # Check for existing dev secret for this machine/user first
        $secretDescription = "DevSetup-$env:COMPUTERNAME-$env:USERNAME"
        Write-Host "   🔍 Checking for existing development secret..." -ForegroundColor Yellow
        
        $existingSecrets = az ad app credential list --id $clientId --query '[].displayName' --output json 2>$null | ConvertFrom-Json
        $hasExistingSecret = $existingSecrets -contains $secretDescription
        
        if ($hasExistingSecret) {
            Write-Host "   ✅ Found existing development secret for this machine/user" -ForegroundColor Green
            Write-Host "   ⚠️  Using existing secret (not creating a new one)" -ForegroundColor Yellow
            # We can't retrieve the existing secret value, so we'll need to create a new one
            Write-Host "   🔄 Rotating existing development secret..." -ForegroundColor Yellow
        } else {
            Write-Host "   🔑 Creating new development secret..." -ForegroundColor Yellow
        }
        
        # Remove existing secret with same description if it exists, then create new one
        if ($hasExistingSecret) {
            $null = az ad app credential delete --id $clientId --key-id (az ad app credential list --id $clientId --query "[?displayName=='$secretDescription'].keyId" --output tsv) 2>$null
        }
        
        $secretInfo = az ad app credential reset --id $clientId --append --display-name $secretDescription --years 1 --query '{password: password}' --output json 2>$null | ConvertFrom-Json
        
        if (-not $secretInfo -or -not $secretInfo.password) {
            Write-Host "   ⚠️  Failed to create client secret" -ForegroundColor Yellow
            "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'): Failed to create client secret for $appName" | Out-File -FilePath $logFile -Append
            return
        }
        
        $clientSecret = $secretInfo.password
        
        # Get tenant domain from tenant ID
        $domain = $null
        
        # Try to get the default domain from Graph API
        $tenantInfo = az rest --method GET --url "https://graph.microsoft.com/v1.0/domains" --query 'value[?isDefault].id' --output tsv 2>$null
        if (-not [string]::IsNullOrWhiteSpace($tenantInfo) -and $tenantInfo.EndsWith(".onmicrosoft.com")) {
            $domain = $tenantInfo
        }
        
        # If no valid domain found, try organization endpoint
        if ([string]::IsNullOrWhiteSpace($domain)) {
            $tenantDetails = az rest --method GET --url "https://graph.microsoft.com/v1.0/organization" --query 'value[0].{displayName: displayName}' --output json 2>$null | ConvertFrom-Json
            if ($tenantDetails -and $tenantDetails.displayName -and $tenantDetails.displayName -notmatch '\s') {
                $domain = "$($tenantDetails.displayName).onmicrosoft.com"
            }
        }
        
        # Last resort: construct from tenant ID (most reliable fallback)
        if ([string]::IsNullOrWhiteSpace($domain)) {
            # Try to get initial domain from tenant info - this usually works
            $initialDomain = az rest --method GET --url "https://graph.microsoft.com/v1.0/organization" --query 'value[0].verifiedDomains[?isInitial].name' --output tsv 2>$null
            if (-not [string]::IsNullOrWhiteSpace($initialDomain) -and $initialDomain.EndsWith(".onmicrosoft.com")) {
                $domain = $initialDomain
            } else {
                # Final fallback - warn user this might be incorrect
                $domain = "$($account.name).onmicrosoft.com"
                Write-Host "   ⚠️  Could not determine tenant domain automatically" -ForegroundColor Yellow
                Write-Host "      Using fallback: $domain" -ForegroundColor Gray
                Write-Host "      Please verify this is correct and update manually if needed" -ForegroundColor Gray
            }
        }
        
        Write-Host "   📝 Updating configuration file..." -ForegroundColor Yellow
        
        # Read and update JSON
        $config = Get-Content -Path $ConfigFilePath -Raw | ConvertFrom-Json
        $config.AzureAd.TenantId = $tenantId
        $config.AzureAd.ClientId = $clientId  
        $config.AzureAd.ClientSecret = $clientSecret
        $config.AzureAd.Domain = $domain
        # Always update Scopes field even if config exists (to fix incorrect values)
        $existingScopes = $config.AzureAd.Scopes
        $requiredScope = "api://$clientId/access_as_user"
        
        # If existing scopes is just "access_user" (wrong), replace it entirely
        if ($existingScopes -eq "access_user") {
            $config.AzureAd.Scopes = $requiredScope
        }
        # If existing scopes doesn't contain the required scope, add it
        elseif (-not $existingScopes.Contains($requiredScope)) {
            if ([string]::IsNullOrWhiteSpace($existingScopes)) {
                $config.AzureAd.Scopes = $requiredScope
            } else {
                $config.AzureAd.Scopes = "$existingScopes $requiredScope"
            }
        }
        
        $config | ConvertTo-Json -Depth 10 | Set-Content -Path $ConfigFilePath
        
        Write-Host "   ✅ Azure AD configuration completed successfully!" -ForegroundColor Green
        Write-Host "      TenantId: $tenantId" -ForegroundColor Gray
        Write-Host "      ClientId: $clientId" -ForegroundColor Gray
        Write-Host "      Domain: $domain" -ForegroundColor Gray
        Write-Host "      Scopes: api://$clientId/access_as_user" -ForegroundColor Gray
        Write-Host "      Client Secret: [REDACTED] (12-month expiry, desc: $secretDescription)" -ForegroundColor Gray
        
    }
    catch {
        $errorMsg = "Azure AD configuration failed: $($_.Exception.Message)"
        Write-Host "   ⚠️  $errorMsg" -ForegroundColor Yellow
        Write-Host "      See $logFile for details" -ForegroundColor Gray
        "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'): $errorMsg" | Out-File -FilePath $logFile -Append
        "Full exception: $($_.Exception.ToString())" | Out-File -FilePath $logFile -Append
    }
}

# 1. Docker Desktop
if (-not $SkipDocker) {
    Write-Host "🐳 Docker Desktop" -ForegroundColor Cyan
    Write-Host "-" * 20

    if ((Test-Command 'docker') -and -not $Force) {
        try {
            $dockerVersion = docker --version 2>$null
            Write-Host "   ✅ Already installed: $dockerVersion" -ForegroundColor Green
        }
        catch {
            Write-Warning "   ⚠️  Docker command found but not responding - you may need to restart Docker Desktop" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "   📦 Installing Docker Desktop..." -ForegroundColor Yellow
        if (Invoke-WingetInstall -Id 'Docker.DockerDesktop' -DisplayName 'Docker Desktop') {
            Write-Host "   ⚠️  Please restart your computer and ensure Docker Desktop is running" -ForegroundColor Yellow
        }
    }

    Write-Host ""
}

# 2. .NET 9.0 SDK
if (-not $SkipDotNet) {
    Write-Host "🔧 .NET 9.0 SDK" -ForegroundColor Cyan
    Write-Host "-" * 20

    $needInstallDotNet = $true
    if ((Test-Command 'dotnet') -and -not $Force) {
        try {
            $dotnetVersion = dotnet --version 2>$null
            if ($dotnetVersion -match '^9\.') {
                Write-Host "   ✅ Already installed: .NET $dotnetVersion" -ForegroundColor Green
                $needInstallDotNet = $false
            }
            else {
                Write-Warning "   ⚠️  Found .NET $dotnetVersion, but .NET 9.x is required" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Warning "   ⚠️  Detected dotnet but version check failed: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    if ($needInstallDotNet) {
        Write-Host "   📦 Installing .NET 9.0 SDK..." -ForegroundColor Yellow
        Invoke-WingetInstall -Id 'Microsoft.DotNet.SDK.9' -DisplayName '.NET 9.0 SDK' | Out-Null
    }

    Write-Host ""
}

# 3. Visual Studio Code
if (-not $SkipVSCode) {
    Write-Host "💻 Visual Studio Code" -ForegroundColor Cyan
    Write-Host "-" * 20

    $needInstallVSCode = $true
    if ((Test-Command 'code') -and -not $Force) {
        try {
            $codeVersion = code --version 2>$null | Select-Object -First 1
            if (-not [string]::IsNullOrWhiteSpace($codeVersion)) {
                Write-Host "   ✅ Already installed: VS Code $codeVersion" -ForegroundColor Green
                $needInstallVSCode = $false
            }
        }
        catch {
            Write-Warning "   ⚠️  VS Code detected but version check failed: $($_.Exception.Message)"
        }
    }

    if ($needInstallVSCode) {
        Write-Host "   📦 Installing Visual Studio Code..." -ForegroundColor Yellow
        Invoke-WingetInstall -Id 'Microsoft.VisualStudioCode' -DisplayName 'Visual Studio Code' | Out-Null
    }

    if (Test-Command 'code') {
        Write-Host "   📦 Installing Dev Containers extension..." -ForegroundColor Yellow
        try {
            code --install-extension ms-vscode-remote.remote-containers --force 2>$null
            Write-Host "   ✅ Dev Containers extension installed" -ForegroundColor Green
        }
        catch {
            Write-Warning "   ⚠️  Failed to install Dev Containers extension - you may need to install it manually (ms-vscode-remote.remote-containers)"
        }
    }

    Write-Host ""
}

# Install/Verify wasm-tools workload
Write-Host "🛠️  .NET Workloads" -ForegroundColor Cyan
Write-Host "-" * 20

if (Test-Command 'dotnet') {
    try {
        Write-Host "   📦 Checking installed workloads..." -ForegroundColor Yellow
        $installedWorkloads = dotnet workload list 2>$null | Out-String
        
        # Check if wasm-tools is installed
        if ($installedWorkloads -match "wasm-tools") {
            Write-Host "   ✅ wasm-tools workload already installed" -ForegroundColor Green
        } else {
            Write-Host "   📦 Installing wasm-tools workload..." -ForegroundColor Yellow
            dotnet workload install wasm-tools 2>$null
            Write-Host "   ✅ wasm-tools workload installed" -ForegroundColor Green
        }
        
        # Note about Aspire 9.x change
        Write-Host "   ℹ️  Note: aspire workload no longer required with Aspire 9.x" -ForegroundColor Cyan
    }
    catch {
        Write-Warning "   ⚠️  Failed to manage workloads - you may need to open a new shell or restart after installing the .NET SDK" -ForegroundColor Yellow
    }
}
else {
    Write-Warning "   ⚠️  .NET CLI not found - skipping workload installation" -ForegroundColor Yellow
}

Write-Host ""

# Install/Verify Azure CLI
Write-Host "☁️  Azure CLI" -ForegroundColor Cyan
Write-Host "-" * 20

if ((Test-Command 'az') -and -not $Force) {
    try {
        $azVersion = az version --query '"azure-cli"' --output tsv 2>$null
        Write-Host "   ✅ Already installed: Azure CLI $azVersion" -ForegroundColor Green
    }
    catch {
        Write-Warning "   ⚠️  Azure CLI command found but not responding properly" -ForegroundColor Yellow
    }
}
else {
    Write-Host "   📦 Installing Azure CLI..." -ForegroundColor Yellow
    if (Invoke-WingetInstall -Id 'Microsoft.AzureCLI' -DisplayName 'Azure CLI') {
        Write-Host "   ✅ Azure CLI installed" -ForegroundColor Green
    }
}

Write-Host ""

# Verify Azure tenant before proceeding with setup
if (-not (Confirm-AzureTenant)) {
    exit 1
}

# Install/Verify dotnet ef tools
Write-Host "🔧 .NET Entity Framework Tools" -ForegroundColor Cyan
Write-Host "-" * 20

if (Test-Command 'dotnet') {
    try {
        Write-Host "   📦 Checking dotnet ef tools..." -ForegroundColor Yellow
        $efToolsCheck = dotnet tool list -g | Select-String "dotnet-ef" 2>$null
        
        if ($efToolsCheck) {
            Write-Host "   ✅ dotnet ef tools already installed" -ForegroundColor Green
        } else {
            Write-Host "   📦 Installing dotnet ef tools..." -ForegroundColor Yellow
            dotnet tool install --global dotnet-ef 2>$null
            Write-Host "   ✅ dotnet ef tools installed" -ForegroundColor Green
        }
    }
    catch {
        Write-Warning "   ⚠️  Failed to manage dotnet ef tools - you may need to install manually with: dotnet tool install --global dotnet-ef" -ForegroundColor Yellow
    }
}
else {
    Write-Warning "   ⚠️  .NET CLI not found - skipping dotnet ef tools installation" -ForegroundColor Yellow
}

Write-Host ""

# Setup MCP configuration
Write-Host "🔌 MCP Configuration" -ForegroundColor Cyan
Write-Host "-" * 20

try {
    # Get the directory where the script is located
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
    $rootDir = Split-Path -Parent $scriptDir
    $sourceFile = Join-Path $scriptDir "mcp-source.windows.json"
    $targetFile = Join-Path $rootDir ".mcp.json"
    
    Write-Host "   📦 Setting up MCP configuration for Windows..." -ForegroundColor Yellow
    
    # Copy the Windows configuration to root as .mcp.json
    Copy-Item -Path $sourceFile -Destination $targetFile -Force
    Write-Host "   ✅ MCP configuration installed" -ForegroundColor Green
}
catch {
    Write-Warning "   ⚠️  Failed to install MCP configuration: $($_.Exception.Message)"
    Write-Host "      Please copy scripts/mcp-source.windows.json to .mcp.json manually" -ForegroundColor Yellow
}

Write-Host ""

# Setup AppHost development configuration
Write-Host "⚙️  AppHost Configuration" -ForegroundColor Cyan
Write-Host "-" * 20

try {
    # Get the directory where the script is located
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
    $rootDir = Split-Path -Parent $scriptDir
    $templateFile = Join-Path $rootDir "src\Microsoft.Greenlight.AppHost\appsettings-template-development.json"
    $targetFile = Join-Path $rootDir "src\Microsoft.Greenlight.AppHost\appsettings.Development.json"
    
    if (-not (Test-Path $targetFile) -and (Test-Path $templateFile)) {
        Write-Host "   📦 Setting up AppHost development configuration..." -ForegroundColor Yellow
        Copy-Item -Path $templateFile -Destination $targetFile -Force
        Write-Host "   ✅ AppHost development configuration created" -ForegroundColor Green
        
        # Offer Azure AD auto-configuration
        $configureAzureAd = $false
        if (Test-Command 'az') {
            Write-Host "" 
            Write-Host "   🔑 Azure AD Configuration" -ForegroundColor Cyan
            Write-Host "      Auto-configure Azure AD settings using Azure CLI?" -ForegroundColor Gray
            Write-Host "      App Registration: sp-ms-industrypermitting" -ForegroundColor Gray
            $response = Read-Host "      Continue? [y/N]"
            if ($response -match '^[Yy]') {
                $configureAzureAd = $true
            }
        }
        
        if ($configureAzureAd) {
            Configure-AzureAdSettings -ConfigFilePath $targetFile
        } else {
            Write-Host "   ⚠️  MANUAL STEP REQUIRED: Configure AzureAD settings" -ForegroundColor Yellow
            Write-Host "      Edit: src\Microsoft.Greenlight.AppHost\appsettings.Development.json" -ForegroundColor Gray
            Write-Host "      Update AzureAd section: TenantId, ClientId, ClientSecret, Domain, Scopes" -ForegroundColor Gray
        }
        
        # Always prompt for Azure OpenAI configuration
        Write-Host ""
        Write-Host "   🤖 Azure OpenAI Configuration" -ForegroundColor Cyan
        Write-Host "      Configure Azure OpenAI endpoint for the application?" -ForegroundColor Gray
        $configureOpenAI = Read-Host "      Continue? [Y/n]"
        if (-not ($configureOpenAI -match '^[Nn]')) {
            Configure-AzureOpenAISettings -ConfigFilePath $targetFile
        }
    }
    elseif (Test-Path $targetFile) {
        Write-Host "   ✅ AppHost development configuration already exists" -ForegroundColor Green
        
        # Check and potentially update existing configuration
        $config = Get-Content -Path $targetFile -Raw | ConvertFrom-Json
        
        # Always check Azure AD Scopes field and fix if incorrect
        if ($config.AzureAd -and $config.AzureAd.ClientId) {
            $clientId = $config.AzureAd.ClientId
            $existingScopes = $config.AzureAd.Scopes
            $requiredScope = "api://$clientId/access_as_user"
            
            $needsScopeUpdate = $false
            if ($existingScopes -eq "access_user") {
                # Wrong scope format, replace entirely
                $config.AzureAd.Scopes = $requiredScope
                $needsScopeUpdate = $true
                Write-Host "   🔄 Fixed incorrect Azure AD scope" -ForegroundColor Yellow
            }
            elseif (-not [string]::IsNullOrWhiteSpace($existingScopes) -and -not $existingScopes.Contains($requiredScope)) {
                # Add required scope if missing
                $config.AzureAd.Scopes = "$existingScopes $requiredScope"
                $needsScopeUpdate = $true
                Write-Host "   🔄 Added missing Azure AD scope" -ForegroundColor Yellow
            }
            
            if ($needsScopeUpdate) {
                $config | ConvertTo-Json -Depth 10 | Set-Content -Path $targetFile
                Write-Host "      Updated Scopes: $($config.AzureAd.Scopes)" -ForegroundColor Gray
            }
            
            # Always check and fix domain - get the correct domain regardless of current value
            $existingDomain = $config.AzureAd.Domain
            Write-Host "   🔄 Verifying Azure AD domain..." -ForegroundColor Yellow

            # Get correct domain using Graph API
            $correctDomain = $null
            $tenantInfo = az rest --method GET --url "https://graph.microsoft.com/v1.0/domains" --query 'value[?isDefault].id' --output tsv 2>$null
            if (-not [string]::IsNullOrWhiteSpace($tenantInfo) -and $tenantInfo.EndsWith(".onmicrosoft.com")) {
                $correctDomain = $tenantInfo
            }

            if ([string]::IsNullOrWhiteSpace($correctDomain)) {
                $initialDomain = az rest --method GET --url "https://graph.microsoft.com/v1.0/organization" --query 'value[0].verifiedDomains[?isInitial].name' --output tsv 2>$null
                if (-not [string]::IsNullOrWhiteSpace($initialDomain) -and $initialDomain.EndsWith(".onmicrosoft.com")) {
                    $correctDomain = $initialDomain
                }
            }

            # Always update domain if we found a correct one and it's different
            if (-not [string]::IsNullOrWhiteSpace($correctDomain) -and $correctDomain -ne $existingDomain) {
                $config.AzureAd.Domain = $correctDomain
                $config | ConvertTo-Json -Depth 10 | Set-Content -Path $targetFile
                Write-Host "      Updated Domain: $existingDomain -> $correctDomain" -ForegroundColor Gray
            } elseif (-not [string]::IsNullOrWhiteSpace($correctDomain)) {
                Write-Host "      Domain is correct: $correctDomain" -ForegroundColor Green
            } else {
                Write-Host "      ⚠️  Could not determine correct domain automatically" -ForegroundColor Yellow
            }
        }

        # Always check and set DeveloperSetupExecuted flag
        $needsDeveloperSetupUpdate = $false
        if (-not $config.ServiceConfiguration) {
            $config | Add-Member -Type NoteProperty -Name 'ServiceConfiguration' -Value @{}
        }
        if (-not $config.ServiceConfiguration.GreenlightServices) {
            $config.ServiceConfiguration | Add-Member -Type NoteProperty -Name 'GreenlightServices' -Value @{}
        }
        if (-not $config.ServiceConfiguration.GreenlightServices.Global) {
            $config.ServiceConfiguration.GreenlightServices | Add-Member -Type NoteProperty -Name 'Global' -Value @{}
        }

        $existingDeveloperSetup = $config.ServiceConfiguration.GreenlightServices.Global.DeveloperSetupExecuted
        if (-not $existingDeveloperSetup -or $existingDeveloperSetup -ne $true) {
            $config.ServiceConfiguration.GreenlightServices.Global | Add-Member -Type NoteProperty -Name 'DeveloperSetupExecuted' -Value $true -Force
            $needsDeveloperSetupUpdate = $true
            Write-Host "   🔄 Set DeveloperSetupExecuted flag to true" -ForegroundColor Yellow
        }

        if ($needsDeveloperSetupUpdate) {
            $config | ConvertTo-Json -Depth 10 | Set-Content -Path $targetFile
            Write-Host "      ✅ DeveloperSetupExecuted flag updated" -ForegroundColor Green
        }
        
        # Check for Azure OpenAI configuration
        $openaiConnectionString = $null
        if ($config.ConnectionStrings -and $config.ConnectionStrings."openai-planner") {
            $openaiConnectionString = $config.ConnectionStrings."openai-planner"
        }
        
        if (-not $openaiConnectionString -or $openaiConnectionString.Length -le 10) {
            Write-Host ""
            Write-Host "   🤖 Azure OpenAI Configuration Missing/Invalid" -ForegroundColor Yellow
            Write-Host "      Configure Azure OpenAI endpoint for the application?" -ForegroundColor Gray
            $configureOpenAI = Read-Host "      Continue? [Y/n]"
            if (-not ($configureOpenAI -match '^[Nn]')) {
                Configure-AzureOpenAISettings -ConfigFilePath $targetFile
            }
        } else {
            Write-Host "   ✅ Valid Azure OpenAI configuration found" -ForegroundColor Green
            Write-Host ""
            Write-Host "   🤖 Azure OpenAI Configuration" -ForegroundColor Cyan
            Write-Host "      Update Azure OpenAI endpoint configuration?" -ForegroundColor Gray
            $updateOpenAI = Read-Host "      Continue? [y/N]"
            if ($updateOpenAI -match '^[Yy]') {
                Configure-AzureOpenAISettings -ConfigFilePath $targetFile
            }
        }
    }
    else {
        Write-Warning "   ⚠️  Template file not found - skipping AppHost configuration setup"
    }
}
catch {
    Write-Warning "   ⚠️  Failed to create AppHost development configuration: $($_.Exception.Message)"
    Write-Host "      Please copy appsettings-template-development.json to appsettings.Development.json manually" -ForegroundColor Yellow
}

Write-Host ""

# Summary
Write-Host "🎉 Installation Summary" -ForegroundColor Green
Write-Host "=" * 30

$components = @()
if (-not $SkipDocker) { $components += "Docker Desktop" }
if (-not $SkipDotNet) { $components += ".NET 9.0 SDK" }
if (-not $SkipVSCode) { $components += "VS Code + Dev Containers" }
$components += "wasm-tools Workload"
$components += "dotnet ef Tools"
$components += "Azure CLI"
$components += "MCP Configuration"
$components += "AppHost Configuration"

foreach ($component in $components) {
    Write-Host "   ✅ $component" -ForegroundColor Green
}

Write-Host ""
Write-Host "🚀 Next Steps:" -ForegroundColor Magenta
Write-Host "   1. Configure Azure AD settings in src\Microsoft.Greenlight.AppHost\appsettings.Development.json" -ForegroundColor Yellow
Write-Host "      (Required: TenantId, ClientId, ClientSecret, Domain, Scopes)" -ForegroundColor Gray
Write-Host "   2. Restart your computer if Docker was installed" -ForegroundColor Gray
Write-Host "   3. Start Docker Desktop" -ForegroundColor Gray
Write-Host "   4. Open this project in VS Code: code ." -ForegroundColor Gray
Write-Host "   5. Use Ctrl+Shift+P → 'Dev Containers: Reopen in Container'" -ForegroundColor Gray
Write-Host ""
Write-Host "📚 For more information, see the project documentation." -ForegroundColor Cyan