#!/bin/bash
set -euo pipefail

# Script to update Entra ID app registration with deployment redirect URL
# This script attempts to automatically add the signin-oidc redirect URL
# If it fails due to insufficient permissions, it provides guidance for manual configuration

# Usage: ./update-app-registration-redirect.sh <client-id> <deployment-url>
# Example: ./update-app-registration-redirect.sh "12345678-1234-1234-1234-123456789012" "https://myapp.example.com"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Function to display usage
usage() {
    echo "Usage: $0 <client-id> <deployment-url>"
    echo ""
    echo "Parameters:"
    echo "  client-id       The ClientId from PVICO_ENTRA_CREDENTIALS (Entra ID app registration)"
    echo "  deployment-url  The base URL of your deployment (without /signin-oidc)"
    echo ""
    echo "Examples:"
    echo "  $0 \"12345678-1234-1234-1234-123456789012\" \"https://20.124.45.67\""
    echo "  $0 \"12345678-1234-1234-1234-123456789012\" \"https://myapp.example.com\""
    echo ""
    exit 1
}

# Check parameters
if [ $# -ne 2 ]; then
    echo "Error: Incorrect number of parameters"
    usage
fi

CLIENT_ID="$1"
DEPLOYMENT_URL="$2"
REDIRECT_URL="${DEPLOYMENT_URL}/signin-oidc"

# Validate parameters
if [[ ! $CLIENT_ID =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$ ]]; then
    echo "Error: CLIENT_ID must be a valid GUID format"
    usage
fi

if [[ ! $DEPLOYMENT_URL =~ ^https?:// ]]; then
    echo "Error: DEPLOYMENT_URL must start with http:// or https://"
    usage
fi

echo "🔧 Attempting to update Entra ID app registration redirect URL..."
echo "   Client ID: $CLIENT_ID"
echo "   Redirect URL: $REDIRECT_URL"

# Check if Azure CLI is available and authenticated
if ! command -v az &> /dev/null; then
    echo "❌ Error: Azure CLI is not installed or not in PATH"
    echo "   Please install Azure CLI and authenticate before running this script"
    exit 1
fi

# Check if we're authenticated to Azure
if ! az account show &>/dev/null; then
    echo "❌ Error: Not authenticated to Azure"
    echo "   Please run 'az login' to authenticate before running this script"
    exit 1
fi

# Attempt to update the app registration
echo "🔄 Attempting to add redirect URL to app registration..."

# Try to get current redirect URIs
if CURRENT_REDIRECTS=$(az ad app show --id "$CLIENT_ID" --query "web.redirectUris" --output json 2>/dev/null); then
    echo "✅ Successfully retrieved current app registration"
    
    # Check if the redirect URL already exists
    if echo "$CURRENT_REDIRECTS" | jq -r '.[]' | grep -q "^${REDIRECT_URL}$"; then
        echo "✅ Redirect URL already configured: $REDIRECT_URL"
        echo "   No changes needed"
        exit 0
    fi
    
    # Add the new redirect URL to existing ones
    NEW_REDIRECTS=$(echo "$CURRENT_REDIRECTS" | jq --arg url "$REDIRECT_URL" '. + [$url]')
    
    # Attempt to update the app registration
    if az ad app update --id "$CLIENT_ID" --web-redirect-uris $(echo "$NEW_REDIRECTS" | jq -r '.[]') &>/dev/null; then
        echo "✅ Successfully added redirect URL to app registration!"
        echo "   Added: $REDIRECT_URL"
        echo "   Your application should now be able to authenticate users"
    else
        echo "⚠️  Failed to update app registration (insufficient permissions)"
        echo "   The deployment service principal does not have sufficient permissions to modify the app registration"
        echo ""
        echo "🔧 Manual Configuration Required:"
        echo "   1. Navigate to Azure Portal → Azure Active Directory → App registrations"
        echo "   2. Search for app with Client ID: $CLIENT_ID"
        echo "   3. Go to Authentication → Platform configurations → Web"
        echo "   4. Add redirect URI: $REDIRECT_URL"
        echo "   5. Save the configuration"
        echo ""
        echo "   Alternative using Azure CLI (with appropriate permissions):"
        echo "   az ad app update --id \"$CLIENT_ID\" --web-redirect-uris $REDIRECT_URL"
        
        # Don't exit with error - this is expected in many deployment scenarios
        exit 0
    fi
else
    echo "⚠️  Failed to retrieve app registration (app not found or insufficient permissions)"
    echo "   Client ID: $CLIENT_ID"
    echo ""
    echo "🔧 Manual Configuration Required:"
    echo "   1. Verify the Client ID is correct (from PVICO_ENTRA_CREDENTIALS)"
    echo "   2. Navigate to Azure Portal → Azure Active Directory → App registrations" 
    echo "   3. Search for app with Client ID: $CLIENT_ID"
    echo "   4. Go to Authentication → Platform configurations → Web"
    echo "   5. Add redirect URI: $REDIRECT_URL"
    echo "   6. Save the configuration"
    echo ""
    echo "   If the app registration doesn't exist, run the sp-create.ps1 script first:"
    echo "   ./build/scripts/sp-create.ps1"
    
    # Don't exit with error - provide guidance instead
    exit 0
fi

echo "✅ App registration redirect URL configuration completed!"