#!/usr/bin/env bash
set -euo pipefail

# Setup Workload Identity for Orleans and SQL Authentication
# This script configures workload identity for all Azure resources access
# Usage: build/scripts/pipeline-internal/build-modern-setup-workload-identity.sh <environment-resource-group> <aks-resource-group> <aks-name> <namespace> [service-account]

ENV_RESOURCE_GROUP=${1:?Missing environment resource group}
AKS_RESOURCE_GROUP=${2:?Missing AKS resource group}
AKS_NAME=${3:?Missing AKS cluster name}
NAMESPACE=${4:-greenlight-dev}
SERVICE_ACCOUNT=${5:-greenlight-app}
ENV_LOCATION=${AZURE_LOCATION:-}

sanitize_identity_name() {
  local raw="$1"
  raw=$(echo "$raw" | tr '[:upper:]' '[:lower:]')
  raw=$(echo "$raw" | tr -c 'a-z0-9-' '-')
  raw=$(echo "$raw" | sed 's/-\{2,\}/-/g; s/^-//; s/-$//')
  echo "${raw:0:120}"
}

# Use the workload identity name from pipeline or fall back to resource-group-based name
if [[ -z "${WORKLOAD_IDENTITY_NAME:-}" ]]; then
  BASE_NAME="$ENV_RESOURCE_GROUP"
  if [[ -z "$BASE_NAME" ]]; then
    BASE_NAME="$NAMESPACE"
  fi
  IDENTITY_NAME="$(sanitize_identity_name "uami-${BASE_NAME}")"
else
  IDENTITY_NAME="$(sanitize_identity_name "$WORKLOAD_IDENTITY_NAME")"
fi

echo "[workload-identity] Setting up workload identity for Azure resources"
echo "[workload-identity] Environment resource group: $ENV_RESOURCE_GROUP"
echo "[workload-identity] AKS resource group: $AKS_RESOURCE_GROUP"
echo "[workload-identity] AKS cluster: $AKS_NAME"
echo "[workload-identity] Namespace: $NAMESPACE"
echo "[workload-identity] Service account: $SERVICE_ACCOUNT"
echo "[workload-identity] Identity name: $IDENTITY_NAME"

# Check for required tools
if ! command -v az >/dev/null 2>&1; then
  echo "[workload-identity] Error: Azure CLI is not installed" >&2
  exit 1
fi

if ! command -v kubectl >/dev/null 2>&1; then
  echo "[workload-identity] Error: kubectl is not installed" >&2
  exit 1
fi

# Get AKS OIDC issuer
echo "[workload-identity] Getting AKS OIDC issuer..."
OIDC_ISSUER=$(az aks show --name "$AKS_NAME" --resource-group "$AKS_RESOURCE_GROUP" --query "oidcIssuerProfile.issuerUrl" -o tsv)

if [[ -z "$OIDC_ISSUER" ]]; then
  echo "[workload-identity] ERROR: OIDC issuer not found. Ensure workload identity is enabled on the cluster."
  exit 1
fi

echo "[workload-identity] OIDC Issuer: $OIDC_ISSUER"

# Ensure the environment resource group exists (needed for identity creation)
if ! az group show --name "$ENV_RESOURCE_GROUP" >/dev/null 2>&1; then
  if [[ -z "$ENV_LOCATION" ]]; then
    echo "[workload-identity] ERROR: AZURE_LOCATION not provided and resource group $ENV_RESOURCE_GROUP does not exist" >&2
    exit 1
  fi
  echo "[workload-identity] Creating resource group: $ENV_RESOURCE_GROUP in $ENV_LOCATION"
  az group create --name "$ENV_RESOURCE_GROUP" --location "$ENV_LOCATION" --output none
fi

# Check if the identity already exists
IDENTITY_EXISTS=$(az identity show --name "$IDENTITY_NAME" --resource-group "$ENV_RESOURCE_GROUP" --query "id" -o tsv 2>/dev/null || echo "")

if [[ -z "$IDENTITY_EXISTS" ]]; then
  echo "[workload-identity] Creating managed identity: $IDENTITY_NAME"
  LOCATION=$(az group show --name "$ENV_RESOURCE_GROUP" --query location -o tsv)
  az identity create --name "$IDENTITY_NAME" --resource-group "$ENV_RESOURCE_GROUP" --location "$LOCATION"
else
  echo "[workload-identity] Managed identity already exists: $IDENTITY_NAME"
fi

# Get identity details
CLIENT_ID=$(az identity show --name "$IDENTITY_NAME" --resource-group "$ENV_RESOURCE_GROUP" --query "clientId" -o tsv)
PRINCIPAL_ID=$(az identity show --name "$IDENTITY_NAME" --resource-group "$ENV_RESOURCE_GROUP" --query "principalId" -o tsv)

echo "[workload-identity] Client ID: $CLIENT_ID"
echo "[workload-identity] Principal ID: $PRINCIPAL_ID"

if [[ -z "$PRINCIPAL_ID" ]]; then
  echo "[workload-identity] ERROR: Unable to resolve managed identity principal ID" >&2
  exit 1
fi

# Role assignments and SQL admin configuration are handled by Bicep deployment modules
echo "[workload-identity] Skipping direct RBAC and SQL admin configuration (handled by Bicep templates)"

# Get AKS credentials for kubectl
echo "[workload-identity] Getting AKS credentials..."
az aks get-credentials --resource-group "$AKS_RESOURCE_GROUP" --name "$AKS_NAME" --overwrite-existing

# Create or update Kubernetes service account (only if needed)
echo "[workload-identity] Configuring Kubernetes service account..."
kubectl get namespace "$NAMESPACE" >/dev/null 2>&1 || kubectl create namespace "$NAMESPACE"

# Get tenant ID once
TENANT_ID=$(az account show --query tenantId -o tsv)

# Check if service account exists and has correct annotations
CURRENT_SA=$(kubectl get serviceaccount "$SERVICE_ACCOUNT" -n "$NAMESPACE" -o json 2>/dev/null || echo "")
CURRENT_CLIENT_ID=""
CURRENT_TENANT_ID=""
if [[ -n "$CURRENT_SA" ]]; then
  CURRENT_CLIENT_ID=$(echo "$CURRENT_SA" | jq -r '.metadata.annotations["azure.workload.identity/client-id"] // ""')
  CURRENT_TENANT_ID=$(echo "$CURRENT_SA" | jq -r '.metadata.annotations["azure.workload.identity/tenant-id"] // ""')
fi

# Only create/update if service account doesn't exist or has incorrect annotations
if [[ -z "$CURRENT_SA" ]]; then
  echo "[workload-identity] Creating new service account: $SERVICE_ACCOUNT"
  cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ServiceAccount
metadata:
  name: $SERVICE_ACCOUNT
  namespace: $NAMESPACE
  annotations:
    azure.workload.identity/client-id: "$CLIENT_ID"
    azure.workload.identity/tenant-id: "$TENANT_ID"
    azure.workload.identity/use: "true"
EOF
  echo "[workload-identity] ✅ Created service account: $SERVICE_ACCOUNT"
elif [[ "$CURRENT_CLIENT_ID" != "$CLIENT_ID" || "$CURRENT_TENANT_ID" != "$TENANT_ID" ]]; then
  echo "[workload-identity] Updating service account annotations (Client ID: $CURRENT_CLIENT_ID -> $CLIENT_ID, Tenant ID: $CURRENT_TENANT_ID -> $TENANT_ID)"
  cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ServiceAccount
metadata:
  name: $SERVICE_ACCOUNT
  namespace: $NAMESPACE
  annotations:
    azure.workload.identity/client-id: "$CLIENT_ID"
    azure.workload.identity/tenant-id: "$TENANT_ID"
    azure.workload.identity/use: "true"
EOF
  echo "[workload-identity] ✅ Updated service account: $SERVICE_ACCOUNT"
else
  echo "[workload-identity] ✅ Service account already exists with correct configuration: $SERVICE_ACCOUNT"
fi

# Create federated identity credential (only if needed)
echo "[workload-identity] Checking federated identity credential..."
# Use the naming pattern that matches existing credentials: fc-{namespace}-{serviceaccount}
FEDERATED_IDENTITY_NAME="fc-${NAMESPACE}-${SERVICE_ACCOUNT}"
EXPECTED_SUBJECT="system:serviceaccount:${NAMESPACE}:${SERVICE_ACCOUNT}"

# Check if federated credential already exists with correct configuration
# Retry the query to handle Azure's eventual consistency
RETRIES=3
for ((i=1; i<=RETRIES; i++)); do
  EXISTING_FC=$(az identity federated-credential list \
    --identity-name "$IDENTITY_NAME" \
    --resource-group "$ENV_RESOURCE_GROUP" \
    --query "[?name=='$FEDERATED_IDENTITY_NAME']" \
    -o json 2>/dev/null || echo "[]")

  EXISTING_FC_COUNT=$(echo "$EXISTING_FC" | jq length)

  # If we found the credential, break out of retry loop
  if [[ "$EXISTING_FC_COUNT" -gt 0 ]]; then
    break
  fi

  # Brief delay for eventual consistency if this isn't the last attempt
  if [[ $i -lt $RETRIES ]]; then
    sleep 2
  fi
done

if [[ "$EXISTING_FC_COUNT" -eq 0 ]]; then
  echo "[workload-identity] Creating federated identity credential: $FEDERATED_IDENTITY_NAME"
  if az identity federated-credential create \
    --name "$FEDERATED_IDENTITY_NAME" \
    --identity-name "$IDENTITY_NAME" \
    --resource-group "$ENV_RESOURCE_GROUP" \
    --issuer "$OIDC_ISSUER" \
    --subject "$EXPECTED_SUBJECT" \
    --audience "api://AzureADTokenExchange" \
    --output none 2>/dev/null; then
    echo "[workload-identity] ✅ Created federated credential: $FEDERATED_IDENTITY_NAME"
  else
    echo "[workload-identity] ⚠️ Failed to create federated credential (likely already exists due to timing)"
    # Double-check if it was created by another process
    echo "[workload-identity] Verifying credential existence after failed creation..."
    sleep 5

    # First, list all credentials for debugging
    ALL_FC=$(az identity federated-credential list \
      --identity-name "$IDENTITY_NAME" \
      --resource-group "$ENV_RESOURCE_GROUP" \
      -o json 2>/dev/null || echo "[]")
    echo "[workload-identity] All existing federated credentials: $(echo "$ALL_FC" | jq -r '.[].name // "none"' | tr '\n' ' ')"

    # Then check specifically for ours
    VERIFY_FC=$(echo "$ALL_FC" | jq "[.[] | select(.name == \"$FEDERATED_IDENTITY_NAME\")]")
    if [[ $(echo "$VERIFY_FC" | jq length) -gt 0 ]]; then
      echo "[workload-identity] ✅ Federated credential exists after all: $FEDERATED_IDENTITY_NAME"
      EXISTING_FC="$VERIFY_FC"
      EXISTING_FC_COUNT=1
    else
      echo "[workload-identity] ❌ Federated credential creation failed and not found"
      echo "[workload-identity] Expected name: $FEDERATED_IDENTITY_NAME"
      echo "[workload-identity] This may cause authentication issues - manual intervention may be required"
      echo "[workload-identity] Continuing with deployment but monitoring required..."
      # Set a flag to indicate this issue for later steps
      EXISTING_FC_COUNT=0
    fi
  fi
else
  # Verify the existing credential has the correct configuration
  EXISTING_SUBJECT=$(echo "$EXISTING_FC" | jq -r '.[0].subject // ""')
  EXISTING_ISSUER=$(echo "$EXISTING_FC" | jq -r '.[0].issuer // ""')

  if [[ "$EXISTING_SUBJECT" == "$EXPECTED_SUBJECT" && "$EXISTING_ISSUER" == "$OIDC_ISSUER" ]]; then
    echo "[workload-identity] ✅ Federated credential already exists with correct configuration: $FEDERATED_IDENTITY_NAME"
  else
    echo "[workload-identity] ⚠️ Federated credential exists but has different configuration"
    echo "[workload-identity]   Expected subject: $EXPECTED_SUBJECT"
    echo "[workload-identity]   Actual subject: $EXISTING_SUBJECT"
    echo "[workload-identity]   Expected issuer: $OIDC_ISSUER"
    echo "[workload-identity]   Actual issuer: $EXISTING_ISSUER"
    echo "[workload-identity] Consider manually updating or recreating the federated credential"
  fi
fi

# Create or update ConfigMap (only if needed)
echo "[workload-identity] Checking workload identity ConfigMap..."
CURRENT_CM=$(kubectl get configmap workload-identity-env -n "$NAMESPACE" -o json 2>/dev/null || echo "")

if [[ -z "$CURRENT_CM" ]]; then
  echo "[workload-identity] Creating workload identity ConfigMap"
  cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ConfigMap
metadata:
  name: workload-identity-env
  namespace: $NAMESPACE
data:
  AZURE_CLIENT_ID: "$CLIENT_ID"
  AZURE_TENANT_ID: "$TENANT_ID"
  AZURE_AUTHORITY_HOST: "https://login.microsoftonline.com/"
  AZURE_USE_WORKLOAD_IDENTITY: "true"
  GREENLIGHT_PRODUCTION: "true"
  WORKLOAD_IDENTITY_NAME: "$IDENTITY_NAME"
EOF
  echo "[workload-identity] ✅ Created workload identity ConfigMap"
else
  # Check all configurable fields that might differ
  CURRENT_CLIENT_ID=$(echo "$CURRENT_CM" | jq -r '.data.AZURE_CLIENT_ID // ""')
  CURRENT_TENANT_ID=$(echo "$CURRENT_CM" | jq -r '.data.AZURE_TENANT_ID // ""')
  CURRENT_WI_NAME=$(echo "$CURRENT_CM" | jq -r '.data.WORKLOAD_IDENTITY_NAME // ""')

  # Handle missing WORKLOAD_IDENTITY_NAME field from older ConfigMaps
  # If the field is missing but other values match, only update if WI_NAME is actually different
  NEEDS_UPDATE=false
  if [[ "$CURRENT_CLIENT_ID" != "$CLIENT_ID" ]]; then
    NEEDS_UPDATE=true
  elif [[ "$CURRENT_TENANT_ID" != "$TENANT_ID" ]]; then
    NEEDS_UPDATE=true
  elif [[ -z "$CURRENT_WI_NAME" && -n "$IDENTITY_NAME" ]]; then
    # Field missing from older ConfigMap - add it silently
    echo "[workload-identity] Adding missing WORKLOAD_IDENTITY_NAME field to existing ConfigMap"
    NEEDS_UPDATE=true
  elif [[ -n "$CURRENT_WI_NAME" && "$CURRENT_WI_NAME" != "$IDENTITY_NAME" ]]; then
    NEEDS_UPDATE=true
  fi

  # Only update if any of the key values have changed
  if [[ "$NEEDS_UPDATE" == "true" ]]; then
    echo "[workload-identity] Updating ConfigMap values:"
    echo "[workload-identity]   Client ID: $CURRENT_CLIENT_ID -> $CLIENT_ID"
    echo "[workload-identity]   Tenant ID: $CURRENT_TENANT_ID -> $TENANT_ID"
    echo "[workload-identity]   WI Name: $CURRENT_WI_NAME -> $IDENTITY_NAME"
    cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ConfigMap
metadata:
  name: workload-identity-env
  namespace: $NAMESPACE
data:
  AZURE_CLIENT_ID: "$CLIENT_ID"
  AZURE_TENANT_ID: "$TENANT_ID"
  AZURE_AUTHORITY_HOST: "https://login.microsoftonline.com/"
  AZURE_USE_WORKLOAD_IDENTITY: "true"
  GREENLIGHT_PRODUCTION: "true"
  WORKLOAD_IDENTITY_NAME: "$IDENTITY_NAME"
EOF
    echo "[workload-identity] ✅ Updated workload identity ConfigMap"
  else
    echo "[workload-identity] ✅ ConfigMap already exists with correct configuration"
  fi
fi

# Output summary for logging
echo ""
echo "========================================"
echo "Workload Identity Setup Complete"
echo "========================================"
echo "  Managed Identity: $IDENTITY_NAME"
echo "  Client ID: $CLIENT_ID"
echo "  Principal ID: $PRINCIPAL_ID"
echo "  Service Account: $SERVICE_ACCOUNT"
echo "  Namespace: $NAMESPACE"
echo ""
echo "Permissions Managed by Bicep Deployment:"
echo "  - Storage: Blob/Table/Queue Data Contributor"
echo "  - SQL: Azure AD admin + db_owner role grants"
echo ""
echo "Next Steps:"
echo "1. Ensure all pods use serviceAccountName: $SERVICE_ACCOUNT"
echo "2. Add workload-identity-env ConfigMap to pod envFrom"
echo "3. Connection strings should use endpoint-only format"
echo "4. DefaultAzureCredential will handle authentication"
echo "========================================"

# Brief pause to allow workload identity token propagation
echo "[workload-identity] Allowing time for token propagation..."
sleep 10
echo "[workload-identity] ✅ Setup complete - tokens should be ready"

# Export variables for other scripts
export WORKLOAD_IDENTITY_CLIENT_ID="$CLIENT_ID"
export WORKLOAD_IDENTITY_PRINCIPAL_ID="$PRINCIPAL_ID"
export WORKLOAD_IDENTITY_NAME="$IDENTITY_NAME"
export WORKLOAD_IDENTITY_SERVICE_ACCOUNT="$SERVICE_ACCOUNT"

# Write to file for pipeline persistence
OUTPUT_FILE="${OUTPUT_FILE:-workload-identity-config.env}"
cat > "$OUTPUT_FILE" <<EOF
WORKLOAD_IDENTITY_CLIENT_ID=$CLIENT_ID
WORKLOAD_IDENTITY_PRINCIPAL_ID=$PRINCIPAL_ID
WORKLOAD_IDENTITY_NAME=$IDENTITY_NAME
WORKLOAD_IDENTITY_SERVICE_ACCOUNT=$SERVICE_ACCOUNT
AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)
EOF

echo "[workload-identity] Configuration saved to: $OUTPUT_FILE"
