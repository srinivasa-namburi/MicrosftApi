#!/bin/bash
# Copyright (c) Microsoft Corporation. All rights reserved.
# Development Environment Setup Script for macOS/Linux
# Installs: Docker, .NET 9.0 SDK, VS Code, Dev Containers extension, wasm-tools workload, dotnet ef tools, Azure CLI

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Parse command line arguments
FORCE=false
SKIP_DOCKER=false
SKIP_DOTNET=false
SKIP_VSCODE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --force)
            FORCE=true
            shift
            ;;
        --skip-docker)
            SKIP_DOCKER=true
            shift
            ;;
        --skip-dotnet)
            SKIP_DOTNET=true
            shift
            ;;
        --skip-vscode)
            SKIP_VSCODE=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [--force] [--skip-docker] [--skip-dotnet] [--skip-vscode]"
            exit 0
            ;;
        *)
            echo "Unknown option $1"
            exit 1
            ;;
    esac
done

echo -e "${MAGENTA}🚀 Microsoft Greenlight - Development Environment Setup${NC}"
echo "============================================================"
echo ""

# Detect platform
if [[ "$OSTYPE" == "darwin"* ]]; then
    PLATFORM="macOS"
    PACKAGE_MANAGER="brew"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    PLATFORM="Linux"
    if command -v apt-get >/dev/null 2>&1; then
        PACKAGE_MANAGER="apt"
    elif command -v yum >/dev/null 2>&1; then
        PACKAGE_MANAGER="yum"
    elif command -v dnf >/dev/null 2>&1; then
        PACKAGE_MANAGER="dnf"
    elif command -v pacman >/dev/null 2>&1; then
        PACKAGE_MANAGER="pacman"
    else
        echo -e "${RED}❌ Unsupported Linux distribution${NC}"
        exit 1
    fi
else
    echo -e "${RED}❌ Unsupported platform: $OSTYPE${NC}"
    exit 1
fi

echo -e "${GRAY}Platform: $PLATFORM${NC}"
echo -e "${GRAY}Package Manager: $PACKAGE_MANAGER${NC}"
echo ""

# Preview actions that will be taken
echo -e "${MAGENTA}📋 Setup Actions Preview${NC}"
echo "=============================="

ACTIONS=()
if [[ "$SKIP_DOCKER" == false ]]; then ACTIONS+=("🐳 Install/Verify Docker"); fi
if [[ "$SKIP_DOTNET" == false ]]; then ACTIONS+=("🔧 Install/Verify .NET 9.0 SDK"); fi
if [[ "$SKIP_VSCODE" == false ]]; then ACTIONS+=("💻 Install/Verify VS Code + Dev Containers extension"); fi
ACTIONS+=("🛠️  Verify/Install wasm-tools workload")
ACTIONS+=("🔧 Verify/Install dotnet ef tools")
ACTIONS+=("☁️  Verify/Install Azure CLI")
ACTIONS+=("🔌 Configure MCP for Unix/Linux")
ACTIONS+=("⚙️  Setup AppHost development configuration")
ACTIONS+=("🔑 Optional: Auto-configure Azure AD settings")

for action in "${ACTIONS[@]}"; do
    echo -e "   ${GRAY}$action${NC}"
done

echo ""
echo -e "${YELLOW}Press Enter to continue or Ctrl+C to cancel...${NC}"
read -r
echo ""
echo -e "${GREEN}🚀 Starting setup...${NC}"
echo ""

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Confirm Azure tenant is correct
confirm_azure_tenant() {
    echo -e "${CYAN}🔐 Azure Tenant Verification${NC}"
    echo "=============================="
    
    # Check if Azure CLI is available
    if ! command_exists az; then
        echo -e "   ${YELLOW}⚠️  Azure CLI not found - skipping tenant verification${NC}"
        return 0
    fi
    
    # Check if user is logged in
    local current_account
    if ! current_account=$(az account show --query '{tenantId: tenantId, name: name, user: user.name, type: user.type}' --output json 2>/dev/null); then
        echo -e "   ${RED}❌ Not logged into Azure CLI${NC}"
        echo -e "      ${GRAY}Please run: az login${NC}"
        return 1
    fi
    
    local tenant_id user_name tenant_name user_type
    tenant_id=$(echo "$current_account" | jq -r '.tenantId')
    user_name=$(echo "$current_account" | jq -r '.user')
    tenant_name=$(echo "$current_account" | jq -r '.name')
    user_type=$(echo "$current_account" | jq -r '.type')
    
    echo -e "   ${GREEN}✅ Current Azure Login:${NC}"
    echo -e "      ${GRAY}User: $user_name${NC}"
    echo -e "      ${GRAY}Tenant: $tenant_name${NC}"
    echo -e "      ${GRAY}Tenant ID: $tenant_id${NC}"
    echo -e "      ${GRAY}Type: $user_type${NC}"
    echo ""
    
    # Warn about guest accounts
    if [[ "$user_type" == "guest" ]]; then
        echo -e "   ${YELLOW}⚠️  WARNING: You are logged in as a guest user${NC}"
        echo -e "      ${GRAY}This may limit your ability to manage app registrations${NC}"
        echo ""
    fi
    
    read -p "      Is this the correct tenant for your app registration? [Y/n]: " -n 1 -r confirm
    echo
    if [[ $confirm =~ ^[Nn]$ ]]; then
        echo ""
        echo -e "   ${CYAN}🔄 Available Tenants:${NC}"
        
        # Try to list available tenants
        local tenants
        if tenants=$(az account tenant list --query '[].{tenantId: tenantId, displayName: displayName, defaultDomain: defaultDomain}' --output json 2>/dev/null); then
            local tenant_count
            tenant_count=$(echo "$tenants" | jq length)
            
            if [[ $tenant_count -gt 0 ]]; then
                for ((i=0; i<tenant_count; i++)); do
                    local tenant_info tenant_display_name tenant_domain tenant_tenant_id marker
                    tenant_info=$(echo "$tenants" | jq -r ".[$i]")
                    tenant_display_name=$(echo "$tenant_info" | jq -r '.displayName')
                    tenant_domain=$(echo "$tenant_info" | jq -r '.defaultDomain')
                    tenant_tenant_id=$(echo "$tenant_info" | jq -r '.tenantId')
                    
                    if [[ "$tenant_tenant_id" == "$tenant_id" ]]; then
                        marker=" (current)"
                    else
                        marker=""
                    fi
                    
                    echo -e "      ${GRAY}[$((i+1))] $tenant_display_name - $tenant_domain$marker${NC}"
                    echo -e "          ${GRAY}Tenant ID: $tenant_tenant_id${NC}"
                done
                echo ""
                
                read -p "      Select tenant number (1-$tenant_count) or 'c' to continue with current: " selection
                if [[ "$selection" != "c" && "$selection" =~ ^[0-9]+$ ]] && [[ $selection -ge 1 && $selection -le $tenant_count ]]; then
                    local selected_index=$((selection - 1))
                    local selected_tenant selected_tenant_name selected_tenant_id
                    selected_tenant=$(echo "$tenants" | jq -r ".[$selected_index]")
                    selected_tenant_name=$(echo "$selected_tenant" | jq -r '.displayName')
                    selected_tenant_id=$(echo "$selected_tenant" | jq -r '.tenantId')
                    
                    echo -e "   ${YELLOW}🔄 Switching to tenant: $selected_tenant_name${NC}"
                    echo -e "      ${GRAY}This will open a browser window for authentication...${NC}"
                    
                    # Set the tenant first
                    if az account set --tenant "$selected_tenant_id"; then
                        # Then login to the specific tenant
                        if az login --tenant "$selected_tenant_id" --only-show-errors; then
                            echo -e "   ${GREEN}✅ Successfully switched to tenant: $selected_tenant_name${NC}"
                        else
                            echo -e "   ${RED}❌ Failed to login to tenant${NC}"
                            echo -e "      ${GRAY}Please run manually: az login --tenant $selected_tenant_id${NC}"
                            return 1
                        fi
                    else
                        echo -e "   ${RED}❌ Failed to set tenant${NC}"
                        echo -e "      ${GRAY}Please run manually: az login --tenant $selected_tenant_id${NC}"
                        return 1
                    fi
                fi
            else
                echo -e "      ${GRAY}Could not retrieve tenant list${NC}"
                echo -e "      ${GRAY}To switch tenants manually, run: az login --tenant <tenant-id>${NC}"
            fi
        else
            echo -e "      ${GRAY}Could not retrieve tenant list${NC}"
            echo -e "      ${GRAY}To switch tenants manually, run: az login --tenant <tenant-id>${NC}"
        fi
        
        echo ""
        read -p "      Continue with setup? [Y/n]: " -n 1 -r final_confirm
        echo
        if [[ $final_confirm =~ ^[Nn]$ ]]; then
            echo -e "   ${YELLOW}⏹️  Setup cancelled. Please login to the correct tenant and re-run the script.${NC}"
            return 1
        fi
    fi
    
    echo ""
    return 0
}

# Configure Azure OpenAI settings
configure_azure_openai_settings() {
    local config_file="$1"
    
    echo -e "   ${YELLOW}🤖 Configuring Azure OpenAI endpoint...${NC}"
    
    # Check if openai-planner connection string already exists and is valid (longer than 10 characters)
    local existing_connection_string
    if existing_connection_string=$(jq -r '.ConnectionStrings."openai-planner" // empty' "$config_file" 2>/dev/null) && [[ -n "$existing_connection_string" && ${#existing_connection_string} -gt 10 ]]; then
        echo -e "   ${GREEN}✅ Found valid Azure OpenAI configuration${NC}"
        echo -e "      ${GRAY}Current: $existing_connection_string${NC}"
        
        read -p "      Update Azure OpenAI configuration? [y/N]: " -n 1 -r update_choice
        echo
        if [[ ! $update_choice =~ ^[Yy]$ ]]; then
            return 0
        fi
    fi
    
    echo ""
    echo -e "   ${CYAN}📝 Azure OpenAI Configuration${NC}"
    echo -e "      ${GRAY}Example: https://your-resource.openai.azure.com/${NC}"
    echo ""
    
    local endpoint
    while [[ -z "$endpoint" ]]; do
        read -p "      Enter Azure OpenAI Endpoint URL: " endpoint
    done
    
    # Ensure endpoint ends with /
    if [[ "$endpoint" != */ ]]; then
        endpoint="$endpoint/"
    fi
    
    echo -e "      ${GRAY}Access Key (optional - press Enter to use Azure CLI identity):${NC}"
    read -p "      Enter Access Key (optional): " access_key
    
    # Format connection string based on whether key is provided
    local connection_string
    if [[ -z "$access_key" ]]; then
        connection_string="$endpoint"
        echo -e "   ${GREEN}✅ Configured for Azure CLI/Entra authentication${NC}"
    else
        connection_string="Endpoint=$endpoint;Key=$access_key"
        echo -e "   ${GREEN}✅ Configured with API key authentication${NC}"
    fi
    
    # Update config with connection string using jq
    if ! jq --arg connectionString "$connection_string" \
           '.ConnectionStrings = (.ConnectionStrings // {}) | .ConnectionStrings."openai-planner" = $connectionString' \
           "$config_file" > "${config_file}.tmp" && mv "${config_file}.tmp" "$config_file"; then
        echo -e "   ${YELLOW}⚠️  Failed to update configuration file${NC}"
        rm -f "${config_file}.tmp"
        return 1
    fi
    
    echo -e "   ${GREEN}✅ Azure OpenAI configuration updated successfully!${NC}"
    local redacted_string="$connection_string"
    if [[ -n "$access_key" ]]; then
        redacted_string=$(echo "$connection_string" | sed 's/Key=[^;]*/Key=[REDACTED]/')
    fi
    echo -e "      ${GRAY}Connection String: $redacted_string${NC}"
    
    return 0
}

# Configure Azure AD settings automatically
configure_azure_ad_settings() {
    local config_file="$1"
    local log_file="$(pwd)/setup-azuread.log"
    local app_name="sp-ms-industrypermitting"
    
    echo -e "   ${YELLOW}🔍 Checking Azure CLI login status...${NC}"
    
    if ! account_info=$(az account show --query '{tenantId: tenantId, name: name}' --output json 2>/dev/null); then
        echo -e "   ${YELLOW}⚠️  Please login to Azure CLI first: az login${NC}"
        return 1
    fi
    
    local tenant_id=$(echo "$account_info" | jq -r '.tenantId // empty')
    local tenant_name=$(echo "$account_info" | jq -r '.name // empty')
    
    if [[ -z "$tenant_id" ]]; then
        echo -e "   ${YELLOW}⚠️  Could not get tenant information${NC}"
        return 1
    fi
    
    echo -e "   ${GRAY}🔍 Tenant: $tenant_name${NC}"
    
    echo -e "   ${YELLOW}🔍 Looking up app registration '$app_name'...${NC}"
    
    if ! app_info=$(az ad app list --display-name "$app_name" --query '[0].{appId: appId, id: id}' --output json 2>/dev/null); then
        echo -e "   ${YELLOW}⚠️  Failed to query app registrations${NC}"
        echo "$(date '+%Y-%m-%d %H:%M:%S'): Failed to query app registrations for $app_name" >> "$log_file"
        return 1
    fi
    
    local client_id=$(echo "$app_info" | jq -r '.appId // empty')
    
    if [[ -z "$client_id" || "$client_id" == "null" ]]; then
        echo -e "   ${YELLOW}⚠️  App registration '$app_name' not found${NC}"
        echo "$(date '+%Y-%m-%d %H:%M:%S'): App registration '$app_name' not found" >> "$log_file"
        return 1
    fi
    
    echo -e "   ${GRAY}🔑 Found app: $client_id${NC}"
    
    # Check for existing dev secret for this machine/user first
    local secret_description="DevSetup-$(hostname)-$USER"
    echo -e "   ${YELLOW}🔍 Checking for existing development secret...${NC}"
    
    local existing_secrets
    if existing_secrets=$(az ad app credential list --id "$client_id" --query '[].displayName' --output json 2>/dev/null); then
        if echo "$existing_secrets" | jq -r '.[]' | grep -q "^$secret_description$"; then
            echo -e "   ${GREEN}✅ Found existing development secret for this machine/user${NC}"
            echo -e "   ${YELLOW}⚠️  Rotating existing secret...${NC}"
            # Remove existing secret with same description
            local key_id
            if key_id=$(az ad app credential list --id "$client_id" --query "[?displayName=='$secret_description'].keyId" --output tsv 2>/dev/null); then
                az ad app credential delete --id "$client_id" --key-id "$key_id" >/dev/null 2>&1 || true
            fi
        else
            echo -e "   ${YELLOW}🔑 Creating new development secret...${NC}"
        fi
    fi
    
    if ! secret_info=$(az ad app credential reset --id "$client_id" --append --display-name "$secret_description" --years 1 --query '{password: password}' --output json 2>/dev/null); then
        echo -e "   ${YELLOW}⚠️  Failed to create client secret${NC}"
        echo "$(date '+%Y-%m-%d %H:%M:%S'): Failed to create client secret for $app_name" >> "$log_file"
        return 1
    fi
    
    local client_secret=$(echo "$secret_info" | jq -r '.password // empty')
    
    if [[ -z "$client_secret" || "$client_secret" == "null" ]]; then
        echo -e "   ${YELLOW}⚠️  Failed to get client secret${NC}"
        return 1
    fi
    
    # Get tenant domain from tenant ID with validation
    local domain=""
    
    # Try to get the default domain from Graph API
    local tenant_domain
    if tenant_domain=$(az rest --method GET --url "https://graph.microsoft.com/v1.0/domains" --query 'value[?isDefault].id' --output tsv 2>/dev/null) && [[ -n "$tenant_domain" && "$tenant_domain" == *.onmicrosoft.com ]]; then
        domain="$tenant_domain"
    fi
    
    # If no valid domain found, try organization endpoint
    if [[ -z "$domain" ]]; then
        local tenant_display_name
        if tenant_display_name=$(az rest --method GET --url "https://graph.microsoft.com/v1.0/organization" --query 'value[0].displayName' --output tsv 2>/dev/null) && [[ -n "$tenant_display_name" && ! "$tenant_display_name" =~ [[:space:]] ]]; then
            domain="${tenant_display_name}.onmicrosoft.com"
        fi
    fi
    
    # Last resort: try to get initial domain from tenant info
    if [[ -z "$domain" ]]; then
        local initial_domain
        if initial_domain=$(az rest --method GET --url "https://graph.microsoft.com/v1.0/organization" --query 'value[0].verifiedDomains[?isInitial].name' --output tsv 2>/dev/null) && [[ -n "$initial_domain" && "$initial_domain" == *.onmicrosoft.com ]]; then
            domain="$initial_domain"
        else
            # Final fallback - warn user this might be incorrect
            domain="${tenant_name}.onmicrosoft.com"
            echo -e "   ${YELLOW}⚠️  Could not determine tenant domain automatically${NC}"
            echo -e "      ${GRAY}Using fallback: $domain${NC}"
            echo -e "      ${GRAY}Please verify this is correct and update manually if needed${NC}"
        fi
    fi
    
    echo -e "   ${YELLOW}📝 Updating configuration file...${NC}"
    
    # Update JSON using jq - always update Scopes field even if config exists (to fix incorrect values)
    local required_scope="api://$client_id/access_as_user"
    local existing_scopes=$(jq -r '.AzureAd.Scopes // empty' "$config_file" 2>/dev/null || echo "")
    local final_scopes
    
    # If existing scopes is just "access_user" (wrong), replace it entirely
    if [[ "$existing_scopes" == "access_user" ]]; then
        final_scopes="$required_scope"
    elif [[ -n "$existing_scopes" ]] && [[ "$existing_scopes" != *"$required_scope"* ]]; then
        # Add required scope if missing
        final_scopes="$existing_scopes $required_scope"
    else
        final_scopes="$required_scope"
    fi
    
    if ! jq --arg tenantId "$tenant_id" \
           --arg clientId "$client_id" \
           --arg clientSecret "$client_secret" \
           --arg domain "$domain" \
           --arg scopes "$final_scopes" \
           '.AzureAd.TenantId = $tenantId | .AzureAd.ClientId = $clientId | .AzureAd.ClientSecret = $clientSecret | .AzureAd.Domain = $domain | .AzureAd.Scopes = $scopes' \
           "$config_file" > "${config_file}.tmp" && mv "${config_file}.tmp" "$config_file"; then
        echo -e "   ${YELLOW}⚠️  Failed to update configuration file${NC}"
        rm -f "${config_file}.tmp"
        return 1
    fi
    
    echo -e "   ${GREEN}✅ Azure AD configuration completed successfully!${NC}"
    echo -e "      ${GRAY}TenantId: $tenant_id${NC}"
    echo -e "      ${GRAY}ClientId: $client_id${NC}"
    echo -e "      ${GRAY}Domain: $domain${NC}"
    echo -e "      ${GRAY}Scopes: $final_scopes${NC}"
    echo -e "      ${GRAY}Client Secret: [REDACTED] (12-month expiry, desc: $secret_description)${NC}"
    
    return 0
}

# Function to install package manager if needed
install_package_manager() {
    if [[ "$PLATFORM" == "macOS" ]] && ! command_exists brew; then
        echo -e "${YELLOW}📦 Installing Homebrew...${NC}"
        /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
        echo -e "${GREEN}   ✅ Homebrew installed${NC}"
    elif [[ "$PLATFORM" == "Linux" ]] && [[ "$PACKAGE_MANAGER" == "apt" ]]; then
        sudo apt-get update >/dev/null 2>&1
    fi
}

# Install Docker
install_docker() {
    if [[ "$SKIP_DOCKER" == true ]]; then
        return
    fi
    
    echo -e "${CYAN}🐳 Docker${NC}"
    echo "--------------------"
    
    if command_exists docker && [[ "$FORCE" == false ]]; then
        DOCKER_VERSION=$(docker --version 2>/dev/null || echo "Unknown")
        echo -e "   ${GREEN}✅ Already installed: $DOCKER_VERSION${NC}"
    else
        echo -e "   ${YELLOW}📦 Installing Docker...${NC}"
        case $PACKAGE_MANAGER in
            brew)
                brew install --cask docker
                echo -e "   ${GREEN}✅ Docker Desktop installed${NC}"
                echo -e "   ${YELLOW}⚠️  Please start Docker Desktop from Applications${NC}"
                ;;
            apt)
                # Install Docker using official Docker repository
                sudo apt-get remove docker docker-engine docker.io containerd runc >/dev/null 2>&1 || true
                sudo apt-get update
                sudo apt-get install -y ca-certificates curl gnupg lsb-release
                sudo mkdir -p /etc/apt/keyrings
                curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
                echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
                sudo apt-get update
                sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
                sudo usermod -aG docker $USER
                echo -e "   ${GREEN}✅ Docker installed${NC}"
                echo -e "   ${YELLOW}⚠️  Please log out and back in to use Docker without sudo${NC}"
                ;;
            yum|dnf)
                if [[ "$PACKAGE_MANAGER" == "dnf" ]]; then
                    PKG_CMD="sudo dnf"
                else
                    PKG_CMD="sudo yum"
                fi
                $PKG_CMD install -y yum-utils
                $PKG_CMD config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
                $PKG_CMD install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
                sudo systemctl start docker
                sudo systemctl enable docker
                sudo usermod -aG docker $USER
                echo -e "   ${GREEN}✅ Docker installed${NC}"
                ;;
            pacman)
                sudo pacman -S --noconfirm docker docker-compose
                sudo systemctl start docker
                sudo systemctl enable docker
                sudo usermod -aG docker $USER
                echo -e "   ${GREEN}✅ Docker installed${NC}"
                ;;
        esac
    fi
    echo ""
}

# Install .NET 9.0 SDK
install_dotnet() {
    if [[ "$SKIP_DOTNET" == true ]]; then
        return
    fi
    
    echo -e "${CYAN}🔧 .NET 9.0 SDK${NC}"
    echo "--------------------"
    
    if command_exists dotnet && [[ "$FORCE" == false ]]; then
        DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "Unknown")
        if [[ "$DOTNET_VERSION" == 9.* ]]; then
            echo -e "   ${GREEN}✅ Already installed: .NET $DOTNET_VERSION${NC}"
        else
            echo -e "   ${YELLOW}⚠️  Found .NET $DOTNET_VERSION, but .NET 9.0 is required${NC}"
            echo -e "   ${YELLOW}📦 Installing .NET 9.0 SDK...${NC}"
            install_dotnet_package
        fi
    else
        echo -e "   ${YELLOW}📦 Installing .NET 9.0 SDK...${NC}"
        install_dotnet_package
    fi
    echo ""
}

install_dotnet_package() {
    case $PACKAGE_MANAGER in
        brew)
            brew install --cask dotnet-sdk
            echo -e "   ${GREEN}✅ .NET 9.0 SDK installed${NC}"
            ;;
        apt)
            # Install Microsoft package repository
            wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
            sudo dpkg -i packages-microsoft-prod.deb
            rm packages-microsoft-prod.deb
            sudo apt-get update
            sudo apt-get install -y dotnet-sdk-9.0
            echo -e "   ${GREEN}✅ .NET 9.0 SDK installed${NC}"
            ;;
        yum|dnf)
            if [[ "$PACKAGE_MANAGER" == "dnf" ]]; then
                PKG_CMD="sudo dnf"
            else
                PKG_CMD="sudo yum"
            fi
            $PKG_CMD install -y https://packages.microsoft.com/config/centos/8/packages-microsoft-prod.rpm
            $PKG_CMD install -y dotnet-sdk-9.0
            echo -e "   ${GREEN}✅ .NET 9.0 SDK installed${NC}"
            ;;
        pacman)
            sudo pacman -S --noconfirm dotnet-sdk
            echo -e "   ${GREEN}✅ .NET SDK installed${NC}"
            ;;
    esac
}

# Install Visual Studio Code
install_vscode() {
    if [[ "$SKIP_VSCODE" == true ]]; then
        return
    fi
    
    echo -e "${CYAN}💻 Visual Studio Code${NC}"
    echo "--------------------"
    
    if command_exists code && [[ "$FORCE" == false ]]; then
        CODE_VERSION=$(code --version 2>/dev/null | head -n1 || echo "Unknown")
        echo -e "   ${GREEN}✅ Already installed: VS Code $CODE_VERSION${NC}"
    else
        echo -e "   ${YELLOW}📦 Installing Visual Studio Code...${NC}"
        case $PACKAGE_MANAGER in
            brew)
                brew install --cask visual-studio-code
                echo -e "   ${GREEN}✅ Visual Studio Code installed${NC}"
                ;;
            apt)
                wget -qO- https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > packages.microsoft.gpg
                sudo install -o root -g root -m 644 packages.microsoft.gpg /etc/apt/trusted.gpg.d/
                echo "deb [arch=amd64,arm64,armhf signed-by=/etc/apt/trusted.gpg.d/packages.microsoft.gpg] https://packages.microsoft.com/repos/code stable main" | sudo tee /etc/apt/sources.list.d/vscode.list > /dev/null
                sudo apt-get update
                sudo apt-get install -y code
                echo -e "   ${GREEN}✅ Visual Studio Code installed${NC}"
                ;;
            yum|dnf)
                if [[ "$PACKAGE_MANAGER" == "dnf" ]]; then
                    PKG_CMD="sudo dnf"
                else
                    PKG_CMD="sudo yum"
                fi
                $PKG_CMD install -y https://packages.microsoft.com/yumrepos/vscode/code-*.rpm || {
                    sudo rpm --import https://packages.microsoft.com/keys/microsoft.asc
                    echo -e "[code]\nname=Visual Studio Code\nbaseurl=https://packages.microsoft.com/yumrepos/vscode\nenabled=1\ngpgcheck=1\ngpgkey=https://packages.microsoft.com/keys/microsoft.asc" | sudo tee /etc/yum.repos.d/vscode.repo > /dev/null
                    $PKG_CMD check-update
                    $PKG_CMD install -y code
                }
                echo -e "   ${GREEN}✅ Visual Studio Code installed${NC}"
                ;;
            pacman)
                # Use AUR helper if available, otherwise manual installation
                if command_exists yay; then
                    yay -S --noconfirm visual-studio-code-bin
                elif command_exists paru; then
                    paru -S --noconfirm visual-studio-code-bin
                else
                    echo -e "   ${YELLOW}⚠️  Please install VS Code manually from AUR or use an AUR helper${NC}"
                fi
                echo -e "   ${GREEN}✅ Visual Studio Code installed${NC}"
                ;;
        esac
    fi
    
    # Install Dev Containers extension
    if command_exists code; then
        echo -e "   ${YELLOW}📦 Installing Dev Containers extension...${NC}"
        code --install-extension ms-vscode-remote.remote-containers --force >/dev/null 2>&1 || {
            echo -e "   ${YELLOW}⚠️  Failed to install Dev Containers extension - you may need to install it manually${NC}"
            echo -e "      Extension ID: ms-vscode-remote.remote-containers"
        }
        echo -e "   ${GREEN}✅ Dev Containers extension installed${NC}"
    fi
    
    echo ""
}

# Install/Verify wasm-tools workload
install_workloads() {
    echo -e "${CYAN}🛠️  .NET Workloads${NC}"
    echo "--------------------"
    
    if command_exists dotnet; then
        echo -e "   ${YELLOW}📦 Checking installed workloads...${NC}"
        INSTALLED_WORKLOADS=$(dotnet workload list 2>/dev/null || echo "")
        
        # Check if wasm-tools is installed
        if echo "$INSTALLED_WORKLOADS" | grep -q "wasm-tools"; then
            echo -e "   ${GREEN}✅ wasm-tools workload already installed${NC}"
        else
            echo -e "   ${YELLOW}📦 Installing wasm-tools workload...${NC}"
            if dotnet workload install wasm-tools >/dev/null 2>&1; then
                echo -e "   ${GREEN}✅ wasm-tools workload installed${NC}"
            else
                echo -e "   ${YELLOW}⚠️  Failed to install wasm-tools workload - may need elevated privileges${NC}"
                if [[ "$EUID" -ne 0 ]] && [[ "$PLATFORM" == "Linux" ]]; then
                    echo -e "   ${YELLOW}   Try running with sudo if permission issues persist${NC}"
                fi
            fi
        fi
        
        # Note about Aspire 9.x change
        echo -e "   ${CYAN}ℹ️  Note: aspire workload no longer required with Aspire 9.x${NC}"
    else
        echo -e "   ${YELLOW}⚠️  .NET CLI not found - skipping workload installation${NC}"
    fi
    echo ""
}

# Install/Verify Azure CLI
install_azure_cli() {
    echo -e "${CYAN}☁️  Azure CLI${NC}"
    echo "--------------------"
    
    if command_exists az && [[ "$FORCE" == false ]]; then
        AZ_VERSION=$(az version --query '"azure-cli"' --output tsv 2>/dev/null || echo "Unknown")
        echo -e "   ${GREEN}✅ Already installed: Azure CLI $AZ_VERSION${NC}"
    else
        echo -e "   ${YELLOW}📦 Installing Azure CLI...${NC}"
        case $PACKAGE_MANAGER in
            brew)
                brew install azure-cli jq
                echo -e "   ${GREEN}✅ Azure CLI and jq installed${NC}"
                ;;
            apt)
                curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
                # Install jq for JSON processing
                sudo apt-get update && sudo apt-get install -y jq
                echo -e "   ${GREEN}✅ Azure CLI and jq installed${NC}"
                ;;
            yum|dnf)
                if [[ "$PACKAGE_MANAGER" == "dnf" ]]; then
                    PKG_CMD="sudo dnf"
                else
                    PKG_CMD="sudo yum"
                fi
                sudo rpm --import https://packages.microsoft.com/keys/microsoft.asc
                echo -e "[azure-cli]\nname=Azure CLI\nbaseurl=https://packages.microsoft.com/yumrepos/azure-cli\nenabled=1\ngpgcheck=1\ngpgkey=https://packages.microsoft.com/keys/microsoft.asc" | sudo tee /etc/yum.repos.d/azure-cli.repo
                $PKG_CMD install azure-cli jq
                echo -e "   ${GREEN}✅ Azure CLI and jq installed${NC}"
                ;;
            pacman)
                # Use AUR helper if available
                if command_exists yay; then
                    yay -S --noconfirm azure-cli jq
                elif command_exists paru; then
                    paru -S --noconfirm azure-cli jq
                else
                    echo -e "   ${YELLOW}⚠️  Please install Azure CLI and jq manually from AUR or use an AUR helper${NC}"
                fi
                echo -e "   ${GREEN}✅ Azure CLI and jq installed${NC}"
                ;;
        esac
    fi
    echo ""
}

# Install/Verify dotnet ef tools
install_ef_tools() {
    echo -e "${CYAN}🔧 .NET Entity Framework Tools${NC}"
    echo "--------------------"
    
    if command_exists dotnet; then
        echo -e "   ${YELLOW}📦 Checking dotnet ef tools...${NC}"
        
        if dotnet tool list -g 2>/dev/null | grep -q "dotnet-ef"; then
            echo -e "   ${GREEN}✅ dotnet ef tools already installed${NC}"
        else
            echo -e "   ${YELLOW}📦 Installing dotnet ef tools...${NC}"
            if dotnet tool install --global dotnet-ef >/dev/null 2>&1; then
                echo -e "   ${GREEN}✅ dotnet ef tools installed${NC}"
            else
                echo -e "   ${YELLOW}⚠️  Failed to install dotnet ef tools - you may need to install manually with: dotnet tool install --global dotnet-ef${NC}"
            fi
        fi
    else
        echo -e "   ${YELLOW}⚠️  .NET CLI not found - skipping dotnet ef tools installation${NC}"
    fi
    echo ""
}

# Setup MCP configuration
setup_mcp_config() {
    echo -e "${CYAN}🔌 MCP Configuration${NC}"
    echo "--------------------"
    
    # Get the directory where the script is located
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    ROOT_DIR="$(dirname "$SCRIPT_DIR")"
    
    echo -e "   ${YELLOW}📦 Setting up MCP configuration for Unix/Linux...${NC}"
    
    # Copy the Unix configuration to root as .mcp.json
    if cp "$SCRIPT_DIR/mcp-source.unix.json" "$ROOT_DIR/.mcp.json" 2>/dev/null; then
        echo -e "   ${GREEN}✅ MCP configuration installed${NC}"
    else
        echo -e "   ${YELLOW}⚠️  Failed to install MCP configuration - you may need to copy manually${NC}"
        echo -e "      Source: scripts/mcp-source.unix.json -> .mcp.json${NC}"
    fi
    
    echo ""
}

# Setup AppHost development configuration
setup_apphost_config() {
    echo -e "${CYAN}⚙️  AppHost Configuration${NC}"
    echo "--------------------"
    
    # Get the directory where the script is located
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    ROOT_DIR="$(dirname "$SCRIPT_DIR")"
    
    TEMPLATE_FILE="$ROOT_DIR/src/Microsoft.Greenlight.AppHost/appsettings-template-development.json"
    TARGET_FILE="$ROOT_DIR/src/Microsoft.Greenlight.AppHost/appsettings.Development.json"
    
    if [[ ! -f "$TARGET_FILE" && -f "$TEMPLATE_FILE" ]]; then
        echo -e "   ${YELLOW}📦 Setting up AppHost development configuration...${NC}"
        
        if cp "$TEMPLATE_FILE" "$TARGET_FILE" 2>/dev/null; then
            echo -e "   ${GREEN}✅ AppHost development configuration created${NC}"
            
            # Offer Azure AD auto-configuration
            local configure_azure_ad=false
            if command_exists az && command_exists jq; then
                echo ""
                echo -e "   ${CYAN}🔑 Azure AD Configuration${NC}"
                echo -e "      ${GRAY}Auto-configure Azure AD settings using Azure CLI?${NC}"
                echo -e "      ${GRAY}App Registration: sp-ms-industrypermitting${NC}"
                read -p "      Continue? [y/N]: " -n 1 -r response
                echo
                if [[ $response =~ ^[Yy]$ ]]; then
                    configure_azure_ad=true
                fi
            elif command_exists az; then
                echo -e "   ${YELLOW}⚠️  jq is required for Azure AD auto-configuration${NC}"
            fi
            
            if [[ "$configure_azure_ad" == true ]]; then
                if configure_azure_ad_settings "$TARGET_FILE"; then
                    echo -e "   ${GREEN}✓ Azure AD configured successfully${NC}"
                else
                    echo -e "   ${YELLOW}⚠️  Azure AD auto-configuration failed, manual setup required${NC}"
                    echo -e "      Edit: src/Microsoft.Greenlight.AppHost/appsettings.Development.json${NC}"
                    echo -e "      Update AzureAd section: TenantId, ClientId, ClientSecret, Domain, Scopes${NC}"
                fi
            else
                echo -e "   ${YELLOW}⚠️  MANUAL STEP REQUIRED: Configure AzureAD settings${NC}"
                echo -e "      Edit: src/Microsoft.Greenlight.AppHost/appsettings.Development.json${NC}"
                echo -e "      Update AzureAd section: TenantId, ClientId, ClientSecret, Domain, Scopes${NC}"
            fi
            
            # Always prompt for Azure OpenAI configuration
            echo ""
            echo -e "   ${CYAN}🤖 Azure OpenAI Configuration${NC}"
            echo -e "      ${GRAY}Configure Azure OpenAI endpoint for the application?${NC}"
            read -p "      Continue? [Y/n]: " -n 1 -r configure_openai_response
            echo
            if [[ ! $configure_openai_response =~ ^[Nn]$ ]]; then
                configure_azure_openai_settings "$TARGET_FILE"
            fi
        else
            echo -e "   ${YELLOW}⚠️  Failed to create AppHost development configuration${NC}"
            echo -e "      Please copy appsettings-template-development.json to appsettings.Development.json manually${NC}"
        fi
    elif [[ -f "$TARGET_FILE" ]]; then
        echo -e "   ${GREEN}✅ AppHost development configuration already exists${NC}"
        
        # Check and potentially update existing configuration
        if command_exists jq; then
            # Always check Azure AD Scopes field and fix if incorrect
            local client_id
            if client_id=$(jq -r '.AzureAd.ClientId // empty' "$TARGET_FILE" 2>/dev/null) && [[ -n "$client_id" && "$client_id" != "null" ]]; then
                local existing_scopes
                existing_scopes=$(jq -r '.AzureAd.Scopes // empty' "$TARGET_FILE" 2>/dev/null || echo "")
                local required_scope="api://$client_id/access_as_user"
                
                local needs_scope_update=false
                local final_scopes
                
                if [[ "$existing_scopes" == "access_user" ]]; then
                    # Wrong scope format, replace entirely
                    final_scopes="$required_scope"
                    needs_scope_update=true
                    echo -e "   ${YELLOW}🔄 Fixed incorrect Azure AD scope${NC}"
                elif [[ -n "$existing_scopes" && "$existing_scopes" != *"$required_scope"* ]]; then
                    # Add required scope if missing
                    final_scopes="$existing_scopes $required_scope"
                    needs_scope_update=true
                    echo -e "   ${YELLOW}🔄 Added missing Azure AD scope${NC}"
                fi
                
                if [[ "$needs_scope_update" == true ]]; then
                    if jq --arg scopes "$final_scopes" '.AzureAd.Scopes = $scopes' "$TARGET_FILE" > "${TARGET_FILE}.tmp" && mv "${TARGET_FILE}.tmp" "$TARGET_FILE"; then
                        echo -e "      ${GRAY}Updated Scopes: $final_scopes${NC}"
                    else
                        rm -f "${TARGET_FILE}.tmp"
                        echo -e "   ${YELLOW}⚠️  Failed to update scopes${NC}"
                    fi
                fi
                
                # Always check and fix domain - get the correct domain regardless of current value
                local existing_domain
                existing_domain=$(jq -r '.AzureAd.Domain // empty' "$TARGET_FILE" 2>/dev/null || echo "")
                echo -e "   ${YELLOW}🔄 Verifying Azure AD domain...${NC}"
                
                # Get correct domain using Graph API
                local correct_domain=""
                local tenant_domain
                if tenant_domain=$(az rest --method GET --url "https://graph.microsoft.com/v1.0/domains" --query 'value[?isDefault].id' --output tsv 2>/dev/null) && [[ -n "$tenant_domain" && "$tenant_domain" == *.onmicrosoft.com ]]; then
                    correct_domain="$tenant_domain"
                fi
                
                if [[ -z "$correct_domain" ]]; then
                    local initial_domain
                    if initial_domain=$(az rest --method GET --url "https://graph.microsoft.com/v1.0/organization" --query 'value[0].verifiedDomains[?isInitial].name' --output tsv 2>/dev/null) && [[ -n "$initial_domain" && "$initial_domain" == *.onmicrosoft.com ]]; then
                        correct_domain="$initial_domain"
                    fi
                fi
                
                # Always update domain if we found a correct one and it's different
                if [[ -n "$correct_domain" && "$correct_domain" != "$existing_domain" ]]; then
                    if jq --arg domain "$correct_domain" '.AzureAd.Domain = $domain' "$TARGET_FILE" > "${TARGET_FILE}.tmp" && mv "${TARGET_FILE}.tmp" "$TARGET_FILE"; then
                        echo -e "      ${GRAY}Updated Domain: $existing_domain -> $correct_domain${NC}"
                    else
                        rm -f "${TARGET_FILE}.tmp"
                        echo -e "   ${YELLOW}⚠️  Failed to update domain${NC}"
                    fi
                elif [[ -n "$correct_domain" ]]; then
                    echo -e "      ${GREEN}Domain is correct: $correct_domain${NC}"
                else
                    echo -e "      ${YELLOW}⚠️  Could not determine correct domain automatically${NC}"
                fi
            fi

            # Always check and set DeveloperSetupExecuted flag
            local developer_setup_executed
            developer_setup_executed=$(jq -r '.ServiceConfiguration.GreenlightServices.Global.DeveloperSetupExecuted // false' "$TARGET_FILE" 2>/dev/null || echo "false")

            if [[ "$developer_setup_executed" != "true" ]]; then
                echo -e "   ${YELLOW}🔄 Setting DeveloperSetupExecuted flag to true${NC}"
                if jq '.ServiceConfiguration = (.ServiceConfiguration // {}) |
                       .ServiceConfiguration.GreenlightServices = (.ServiceConfiguration.GreenlightServices // {}) |
                       .ServiceConfiguration.GreenlightServices.Global = (.ServiceConfiguration.GreenlightServices.Global // {}) |
                       .ServiceConfiguration.GreenlightServices.Global.DeveloperSetupExecuted = true' \
                       "$TARGET_FILE" > "${TARGET_FILE}.tmp" && mv "${TARGET_FILE}.tmp" "$TARGET_FILE"; then
                    echo -e "      ${GREEN}✅ DeveloperSetupExecuted flag updated${NC}"
                else
                    rm -f "${TARGET_FILE}.tmp"
                    echo -e "   ${YELLOW}⚠️  Failed to update DeveloperSetupExecuted flag${NC}"
                fi
            fi
            
            # Check for Azure OpenAI configuration
            local openai_config
            openai_config=$(jq -r '.ConnectionStrings."openai-planner" // empty' "$TARGET_FILE" 2>/dev/null || echo "")
            
            if [[ -z "$openai_config" || "$openai_config" == "null" || ${#openai_config} -le 10 ]]; then
                echo ""
                echo -e "   ${YELLOW}🤖 Azure OpenAI Configuration Missing/Invalid${NC}"
                echo -e "      ${GRAY}Configure Azure OpenAI endpoint for the application?${NC}"
                read -p "      Continue? [Y/n]: " -n 1 -r configure_openai_response
                echo
                if [[ ! $configure_openai_response =~ ^[Nn]$ ]]; then
                    configure_azure_openai_settings "$TARGET_FILE"
                fi
            else
                echo -e "   ${GREEN}✅ Valid Azure OpenAI configuration found${NC}"
                echo ""
                echo -e "   ${CYAN}🤖 Azure OpenAI Configuration${NC}"
                echo -e "      ${GRAY}Update Azure OpenAI endpoint configuration?${NC}"
                read -p "      Continue? [y/N]: " -n 1 -r update_openai_response
                echo
                if [[ $update_openai_response =~ ^[Yy]$ ]]; then
                    configure_azure_openai_settings "$TARGET_FILE"
                fi
            fi
        fi
    else
        echo -e "   ${YELLOW}⚠️  Template file not found - skipping AppHost configuration setup${NC}"
    fi
    
    echo ""
}

# Main execution
install_package_manager
install_docker
install_dotnet
install_vscode
install_workloads
install_ef_tools
install_azure_cli

# Verify Azure tenant before proceeding with configuration
if ! confirm_azure_tenant; then
    exit 1
fi

setup_mcp_config
setup_apphost_config

# Summary
echo -e "${GREEN}🎉 Installation Summary${NC}"
echo "=============================="

COMPONENTS=()
[[ "$SKIP_DOCKER" == false ]] && COMPONENTS+=("Docker")
[[ "$SKIP_DOTNET" == false ]] && COMPONENTS+=(".NET 9.0 SDK")
[[ "$SKIP_VSCODE" == false ]] && COMPONENTS+=("VS Code + Dev Containers")
COMPONENTS+=("wasm-tools Workload")
COMPONENTS+=("dotnet ef Tools")
COMPONENTS+=("Azure CLI")
COMPONENTS+=("MCP Configuration")
COMPONENTS+=("AppHost Configuration")

for component in "${COMPONENTS[@]}"; do
    echo -e "   ${GREEN}✅ $component${NC}"
done

echo ""
echo -e "${MAGENTA}🚀 Next Steps:${NC}"
echo -e "   ${YELLOW}1. Configure Azure AD settings in src/Microsoft.Greenlight.AppHost/appsettings.Development.json${NC}"
echo -e "      ${GRAY}(Required: TenantId, ClientId, ClientSecret, Domain, Scopes)${NC}"
echo -e "   ${GRAY}2. Start Docker (if installed)${NC}"
if [[ "$PLATFORM" == "Linux" ]]; then
    echo -e "   ${GRAY}3. Log out and back in to use Docker without sudo${NC}"
    echo -e "   ${GRAY}4. Open this project in VS Code: code .${NC}"
    echo -e "   ${GRAY}5. Use Ctrl+Shift+P → 'Dev Containers: Reopen in Container'${NC}"
else
    echo -e "   ${GRAY}3. Open this project in VS Code: code .${NC}"
    echo -e "   ${GRAY}4. Use Ctrl+Shift+P → 'Dev Containers: Reopen in Container'${NC}"
fi
echo ""
echo -e "${CYAN}📚 For more information, see the project documentation.${NC}"