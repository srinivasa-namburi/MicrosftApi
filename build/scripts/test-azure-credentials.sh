#!/usr/bin/env bash
set -euo pipefail

# Script to test Azure service principal credentials
# Usage:
#   1. Set environment variables:
#      export AZURE_APP_ID="your-app-id"
#      export AZURE_PASSWORD="your-password"
#      export AZURE_TENANT="your-tenant-id"
#      ./test-azure-credentials.sh
#
#   2. Or provide JSON credentials:
#      echo '{"appId":"...","password":"...","tenant":"..."}' | ./test-azure-credentials.sh
#

echo "================================================"
echo "Azure Service Principal Credential Tester"
echo "================================================"
echo ""

# Check if credentials are provided via stdin (JSON format)
if [ ! -t 0 ]; then
    echo "Reading credentials from stdin (JSON format)..."
    CREDS_JSON=$(cat)
    export AZURE_APP_ID=$(echo "$CREDS_JSON" | jq -r '.appId // .clientId')
    export AZURE_PASSWORD=$(echo "$CREDS_JSON" | jq -r '.password // .clientSecret')
    export AZURE_TENANT=$(echo "$CREDS_JSON" | jq -r '.tenant // .tenantId')
fi

# Validate required variables are set
if [ -z "${AZURE_APP_ID:-}" ] || [ -z "${AZURE_PASSWORD:-}" ] || [ -z "${AZURE_TENANT:-}" ]; then
    echo "❌ ERROR: Required credentials not provided"
    echo ""
    echo "Please provide credentials either by:"
    echo "  1. Setting environment variables: AZURE_APP_ID, AZURE_PASSWORD, AZURE_TENANT"
    echo "  2. Piping JSON: echo '{\"appId\":\"...\",\"password\":\"...\",\"tenant\":\"...\"}' | ./test-azure-credentials.sh"
    exit 1
fi

echo "Testing credentials for:"
echo "  App ID: ${AZURE_APP_ID:0:8}***"
echo "  Tenant: ${AZURE_TENANT:0:8}***"
echo ""

# Test 1: Check if az CLI is installed
if ! command -v az >/dev/null 2>&1; then
    echo "❌ Azure CLI (az) is not installed"
    echo "   Install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi
echo "✅ Azure CLI is installed: $(az version --query '\"azure-cli\"' -o tsv)"
echo ""

# Test 2: Attempt login with service principal
echo "Attempting to login with service principal..."
set +e
LOGIN_OUTPUT=$(az login \
    --service-principal \
    --username "$AZURE_APP_ID" \
    --password "$AZURE_PASSWORD" \
    --tenant "$AZURE_TENANT" \
    2>&1)
LOGIN_EXIT_CODE=$?
set -e

if [ $LOGIN_EXIT_CODE -eq 0 ]; then
    echo "✅ Login successful!"
    echo ""

    # Show subscription info
    echo "Available subscriptions:"
    az account list --query '[].{Name:name, ID:id, State:state}' -o table
    echo ""

    # Show current subscription
    CURRENT_SUB=$(az account show --query name -o tsv 2>/dev/null || echo "None")
    echo "Current subscription: $CURRENT_SUB"
    echo ""

    # Test 3: Try to list resource groups (permission check)
    echo "Testing permissions (listing resource groups)..."
    if az group list --query '[0].name' -o tsv >/dev/null 2>&1; then
        echo "✅ Can list resource groups (has read permissions)"
        RG_COUNT=$(az group list --query 'length(@)' -o tsv)
        echo "   Found $RG_COUNT resource group(s)"
    else
        echo "⚠️  Cannot list resource groups (may have limited permissions)"
    fi
    echo ""

    echo "================================================"
    echo "✅ CREDENTIALS ARE VALID"
    echo "================================================"

    # Logout to clean up
    az logout >/dev/null 2>&1 || true

    exit 0
else
    echo "❌ Login failed!"
    echo ""
    echo "Error details:"
    echo "$LOGIN_OUTPUT"
    echo ""

    # Check for specific error patterns
    if echo "$LOGIN_OUTPUT" | grep -q "AADSTS7000222"; then
        echo "================================================"
        echo "❌ CLIENT SECRET HAS EXPIRED"
        echo "================================================"
        echo ""
        echo "The client secret for this service principal has expired."
        echo ""
        echo "To fix this, you need to generate a new client secret:"
        echo ""
        echo "Option 1: Using Azure Portal"
        echo "  1. Go to: https://portal.azure.com"
        echo "  2. Navigate to: Azure Active Directory > App registrations"
        echo "  3. Find your app: $AZURE_APP_ID"
        echo "  4. Go to: Certificates & secrets > Client secrets"
        echo "  5. Click 'New client secret' and set an expiration"
        echo "  6. Copy the new secret value (you can't see it again!)"
        echo "  7. Update your GitHub secret with the new credentials"
        echo ""
        echo "Option 2: Using Azure CLI"
        echo "  az ad app credential reset --id $AZURE_APP_ID --append"
        echo ""
    elif echo "$LOGIN_OUTPUT" | grep -q "AADSTS700016"; then
        echo "❌ Application not found in tenant"
        echo "   Check that the App ID and Tenant ID are correct"
    elif echo "$LOGIN_OUTPUT" | grep -q "AADSTS7000215"; then
        echo "❌ Invalid client secret"
        echo "   The password may be incorrect or has special characters that need escaping"
    fi

    echo "================================================"
    echo "❌ CREDENTIALS ARE INVALID"
    echo "================================================"

    exit 1
fi