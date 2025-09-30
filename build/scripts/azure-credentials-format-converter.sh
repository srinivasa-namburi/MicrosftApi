#!/usr/bin/env bash
set -euo pipefail

# Converts Azure service principal JSON from az ad sp create output format
# to the format required by GitHub Actions azure/login@v2 action
#
# Usage:
#   1. From stdin:
#      echo '{"appId":"...","password":"...","tenant":"..."}' | ./azure-credentials-format-converter.sh
#
#   2. With subscription ID parameter:
#      echo '{"appId":"...","password":"...","tenant":"..."}' | ./azure-credentials-format-converter.sh cf3f334c-49bb-4ae9-926d-430b65f1b862
#
#   3. Auto-detect current subscription:
#      echo '{"appId":"...","password":"...","tenant":"..."}' | ./azure-credentials-format-converter.sh auto

echo "================================================"
echo "Azure Credentials Format Converter"
echo "================================================"
echo ""
echo "Converting from 'az ad sp create' format to GitHub Actions format..."
echo ""

# Read input JSON
if [ ! -t 0 ]; then
    INPUT_JSON=$(cat)
else
    echo "❌ ERROR: No input provided"
    echo ""
    echo "Usage: echo '{\"appId\":\"...\",\"password\":\"...\",\"tenant\":\"...\"}' | $0 [subscription-id|auto]"
    exit 1
fi

# Parse input fields
APP_ID=$(echo "$INPUT_JSON" | jq -r '.appId // .clientId // empty')
PASSWORD=$(echo "$INPUT_JSON" | jq -r '.password // .clientSecret // empty')
TENANT=$(echo "$INPUT_JSON" | jq -r '.tenant // .tenantId // empty')
SUBSCRIPTION=$(echo "$INPUT_JSON" | jq -r '.subscriptionId // empty')

# Validate required fields
if [ -z "$APP_ID" ]; then
    echo "❌ ERROR: Missing 'appId' or 'clientId' in input JSON"
    exit 1
fi
if [ -z "$PASSWORD" ]; then
    echo "❌ ERROR: Missing 'password' or 'clientSecret' in input JSON"
    exit 1
fi
if [ -z "$TENANT" ]; then
    echo "❌ ERROR: Missing 'tenant' or 'tenantId' in input JSON"
    exit 1
fi

# Handle subscription ID
if [ "${1:-}" = "auto" ]; then
    echo "Auto-detecting subscription from Azure CLI..."
    if command -v az >/dev/null 2>&1; then
        SUBSCRIPTION=$(az account show --query id -o tsv 2>/dev/null || echo "")
        if [ -n "$SUBSCRIPTION" ]; then
            echo "✅ Using current subscription: $SUBSCRIPTION"
        else
            echo "⚠️  WARNING: Could not detect subscription (not logged in?)"
        fi
    else
        echo "⚠️  WARNING: Azure CLI not found, cannot auto-detect subscription"
    fi
elif [ -n "${1:-}" ]; then
    SUBSCRIPTION="$1"
    echo "Using provided subscription: $SUBSCRIPTION"
fi

# Warn if subscription is missing
if [ -z "$SUBSCRIPTION" ]; then
    echo ""
    echo "⚠️  WARNING: No subscription ID provided or detected"
    echo "    The secret will work but won't set a default subscription"
    echo "    You can add it later by providing a subscription ID parameter"
    echo ""
fi

# Create output JSON in GitHub Actions format
echo "Creating GitHub Actions formatted JSON..."
OUTPUT_JSON=$(jq -n \
    --arg clientId "$APP_ID" \
    --arg clientSecret "$PASSWORD" \
    --arg tenantId "$TENANT" \
    --arg subscriptionId "${SUBSCRIPTION:-}" \
    '{
        clientId: $clientId,
        clientSecret: $clientSecret,
        tenantId: $tenantId,
        subscriptionId: (if $subscriptionId != "" then $subscriptionId else null end)
    }')

# Remove null subscriptionId if not provided
if [ -z "$SUBSCRIPTION" ]; then
    OUTPUT_JSON=$(echo "$OUTPUT_JSON" | jq 'del(.subscriptionId)')
fi

echo ""
echo "================================================"
echo "✅ Conversion Complete"
echo "================================================"
echo ""
echo "Original format (az ad sp create):"
echo "  appId     -> clientId"
echo "  password  -> clientSecret"
echo "  tenant    -> tenantId"
echo ""
echo "GitHub Actions AZURE_CREDENTIALS secret format:"
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "$OUTPUT_JSON"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "Single line (for CLI/scripts):"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "$OUTPUT_JSON" | jq -c .
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "To set this secret in GitHub:"
echo ""
echo "Option 1: Using gh CLI"
echo "  gh secret set AZURE_CREDENTIALS --env dev --body '\$(echo '$OUTPUT_JSON' | jq -c .)'"
echo ""
echo "Option 2: Using GitHub Portal"
echo "  1. Go to: https://github.com/YOUR_ORG/YOUR_REPO/settings/environments"
echo "  2. Select environment: dev"
echo "  3. Add/Update secret: AZURE_CREDENTIALS"
echo "  4. Paste the single-line JSON shown above"
echo ""