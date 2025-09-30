#!/usr/bin/env bash
set -euo pipefail

# Restore Workload Identity Variables to ADO Variable Group
# This script restores workload identity variables that should have been created during AKS provisioning
# Usage: ./restore-workload-identity-variables.sh <resource-group> <aks-cluster-name> <variable-group-name> <ado-org> <ado-project>

RESOURCE_GROUP=${1:-rg-greenlight-adodev}
AKS_CLUSTER_NAME=${2:-aks-greenlight-adodev}
VARIABLE_GROUP_NAME=${3:-greenlight-modern-dev}
ADO_ORG=${4:-"https://dev.azure.com/your-org"}
ADO_PROJECT=${5:-industry-permitting}

echo "[restore-wi] Restoring workload identity variables to ADO variable group"
echo "[restore-wi] Resource Group: $RESOURCE_GROUP"
echo "[restore-wi] AKS Cluster: $AKS_CLUSTER_NAME"
echo "[restore-wi] Variable Group: $VARIABLE_GROUP_NAME"
echo "[restore-wi] ADO Project: $ADO_PROJECT"

# Check prerequisites
if ! command -v az >/dev/null 2>&1; then
  echo "[restore-wi] ERROR: Azure CLI not installed" >&2
  exit 1
fi

# Check if az devops extension is installed
if ! az extension show --name azure-devops >/dev/null 2>&1; then
  echo "[restore-wi] Installing Azure DevOps CLI extension..."
  az extension add --name azure-devops
fi

# Configure Azure DevOps defaults
az devops configure --defaults organization="$ADO_ORG" project="$ADO_PROJECT" >/dev/null 2>&1 || true

# Get variable group ID
VG_ID=$(az pipelines variable-group list --query "[?name=='$VARIABLE_GROUP_NAME'].id | [0]" -o tsv 2>/dev/null || echo "")
if [[ -z "$VG_ID" ]]; then
  echo "[restore-wi] ERROR: Variable group '$VARIABLE_GROUP_NAME' not found in project '$ADO_PROJECT'"
  echo "[restore-wi] Available variable groups:"
  az pipelines variable-group list --query "[].name" -o tsv
  exit 1
fi

echo "[restore-wi] Found variable group ID: $VG_ID"

# Function to update or create a variable
update_or_create_var() {
  local key=$1
  local value=$2
  local secret=${3:-false}

  if [[ -z "$value" ]]; then
    echo "[restore-wi] WARNING: No value for $key - skipping"
    return
  fi

  # Check if variable exists
  existing=$(az pipelines variable-group variable list --group-id "$VG_ID" --query "$key.value" -o tsv 2>/dev/null || echo "")

  if [[ -n "$existing" ]]; then
    # Update existing variable
    if [[ "$existing" != "$value" ]]; then
      az pipelines variable-group variable update \
        --group-id "$VG_ID" \
        --name "$key" \
        --value "$value" \
        --secret "$secret" \
        --output none
      echo "[restore-wi] Updated: $key"
    else
      echo "[restore-wi] Unchanged: $key"
    fi
  else
    # Create new variable
    az pipelines variable-group variable create \
      --group-id "$VG_ID" \
      --name "$key" \
      --value "$value" \
      --secret "$secret" \
      --output none
    echo "[restore-wi] Created: $key"
  fi
}

# Step 1: Get the workload identity that was created during AKS provisioning
# Following the pattern from provision-aks-cluster.sh: WI_IDENTITY_NAME="uami-$CLUSTER_NAME"
WI_IDENTITY_NAME="uami-${AKS_CLUSTER_NAME}"
echo "[restore-wi] Looking for workload identity: $WI_IDENTITY_NAME"

# Get the managed identity details
IDENTITY=$(az identity show \
  --name "$WI_IDENTITY_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --output json 2>/dev/null || echo "{}")

if [[ "$IDENTITY" == "{}" ]]; then
  echo "[restore-wi] ERROR: Workload identity '$WI_IDENTITY_NAME' not found in resource group '$RESOURCE_GROUP'"
  echo "[restore-wi] Available identities:"
  az identity list --resource-group "$RESOURCE_GROUP" --query "[].name" -o tsv
  exit 1
fi

# Extract values from the identity
UAMI_CLIENT_ID=$(echo "$IDENTITY" | jq -r '.clientId // empty')
UAMI_PRINCIPAL_ID=$(echo "$IDENTITY" | jq -r '.principalId // empty')
UAMI_RESOURCE_ID=$(echo "$IDENTITY" | jq -r '.id // empty')

echo "[restore-wi] Found workload identity:"
echo "  Client ID: $UAMI_CLIENT_ID"
echo "  Principal ID: $UAMI_PRINCIPAL_ID"
echo "  Resource ID: $UAMI_RESOURCE_ID"

# Step 2: Get AKS OIDC issuer
echo "[restore-wi] Getting AKS OIDC issuer..."
OIDC_ISSUER=$(az aks show \
  --name "$AKS_CLUSTER_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "oidcIssuerProfile.issuerUrl" \
  -o tsv 2>/dev/null || echo "")

if [[ -z "$OIDC_ISSUER" ]]; then
  echo "[restore-wi] WARNING: Could not get OIDC issuer for AKS cluster"
else
  echo "[restore-wi] OIDC Issuer: $OIDC_ISSUER"
fi

# Step 3: Get federated credential details
echo "[restore-wi] Getting federated credential details..."
FED_CREDS=$(az identity federated-credential list \
  --identity-name "$WI_IDENTITY_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --output json 2>/dev/null || echo "[]")

# Find the greenlight-app service account federated credential
FED_SUBJECT=$(echo "$FED_CREDS" | jq -r '.[] | select(.subject | contains("greenlight-app")) | .subject' | head -1)
if [[ -z "$FED_SUBJECT" ]]; then
  # Use default if not found
  FED_SUBJECT="system:serviceaccount:greenlight-dev:greenlight-app"
  echo "[restore-wi] Using default federated subject: $FED_SUBJECT"
else
  echo "[restore-wi] Found federated subject: $FED_SUBJECT"
fi

# Step 4: Update the variable group with all workload identity variables
echo ""
echo "[restore-wi] Updating variable group: $VARIABLE_GROUP_NAME"
echo "========================================="

# Core workload identity variables
update_or_create_var "WORKLOAD_IDENTITY_NAME" "$WI_IDENTITY_NAME"
update_or_create_var "WORKLOAD_IDENTITY_CLIENT_ID" "$UAMI_CLIENT_ID"
update_or_create_var "WORKLOAD_IDENTITY_PRINCIPAL_ID" "$UAMI_PRINCIPAL_ID"
update_or_create_var "WORKLOAD_IDENTITY_RESOURCE_ID" "$UAMI_RESOURCE_ID"
update_or_create_var "WORKLOAD_IDENTITY_SERVICE_ACCOUNT" "greenlight-app"

# AKS OIDC configuration
update_or_create_var "AKS_OIDC_ISSUER" "$OIDC_ISSUER"
update_or_create_var "WORKLOAD_IDENTITY_FEDERATED_SUBJECT" "$FED_SUBJECT"

# Also ensure AKS cluster name is set correctly
update_or_create_var "AKS_CLUSTER_NAME" "$AKS_CLUSTER_NAME"

# Step 5: Verify all variables were set
echo ""
echo "[restore-wi] Verifying variables in group..."
echo "========================================="

# List all workload identity related variables
VARS=$(az pipelines variable-group variable list \
  --group-id "$VG_ID" \
  --output json 2>/dev/null || echo "{}")

echo "$VARS" | jq -r 'to_entries | .[] | select(.key | startswith("WORKLOAD_IDENTITY") or startswith("AKS_OIDC")) | "\(.key): \(.value.value // "***SECRET***")"'

echo ""
echo "========================================="
echo "[restore-wi] Workload identity variables restored successfully!"
echo "========================================="
echo ""
echo "Summary:"
echo "  Variable Group: $VARIABLE_GROUP_NAME"
echo "  Identity Name: $WI_IDENTITY_NAME"
echo "  Client ID: $UAMI_CLIENT_ID"
echo "  Principal ID: $UAMI_PRINCIPAL_ID"
echo ""
echo "These variables will be used by the pipeline during deployment to:"
echo "  1. Configure workload identity for pods"
echo "  2. Grant permissions to Azure resources"
echo "  3. Enable DefaultAzureCredential authentication"
echo ""
echo "Next steps:"
echo "  1. Run the pipeline to deploy with workload identity"
echo "  2. Verify pods are using the correct service account"
echo "  3. Check that Orleans and SQL can authenticate"