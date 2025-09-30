#!/usr/bin/env bash
set -euo pipefail

# -----------------------------------------------------------------------------
# Bootstrap Azure DevOps variable GROUP *for a single environment* plus the ADO
# Environment resource itself, based on a repo-local YAML descriptor.
#
# IMPORTANT:
#   • Each environment (dev / staging / prod, etc.) MUST have its *own* YAML and
#     its *own* variable group. This script never shares a variable group across
#     environments. The YAML's `.variableGroup.name` is the authoritative name.
#   • We persist three metadata variables inside the variable group:
#       - VARIABLE_GROUP_NAME
#       - ADO_ORG_URL
#       - ADO_PROJECT
#     These are convenience markers so later pipeline/jobs (e.g. workload
#     identity synchronization) can discover the correct variable group without
#     you re‑supplying org/project context. They are *environment‑scoped*, not
#     global. Having the same trio in several groups is expected.
#   • Placeholder workload identity variables are created empty so that later
#     pipeline runs can fill them in idempotently after the AKS/UAMI provisioning
#     step. This avoids conditional logic sprinkled everywhere.
#
# Usage: build/scripts/build-modern-bootstrap-ado.sh <env-yaml> <org-url> <project>
# Requires: az devops extension, and yq installed for YAML parsing.
# -----------------------------------------------------------------------------

ENV_YAML=${1:?Missing env yaml}
ADO_ORG=${2:?Missing ADO org URL}
ADO_PROJECT=${3:?Missing ADO project}

command -v yq >/dev/null || { echo "yq is required" >&2; exit 1; }

echo "[modern][ADO] Using org=$ADO_ORG project=$ADO_PROJECT"
az devops configure --defaults organization="$ADO_ORG" project="$ADO_PROJECT"

ENV_NAME=$(yq -r '.environment.name' "$ENV_YAML")
VG_NAME=$(yq -r '.variableGroup.name' "$ENV_YAML")

echo "[modern][ADO] Ensuring environment: $ENV_NAME"
az devops invoke --route-parameters project=$ADO_PROJECT --area distributedtask --resource environments \
  --http-method GET >/dev/null 2>&1 || true
az pipelines env create --name "$ENV_NAME" >/dev/null 2>&1 || true

echo "[modern][ADO] Ensuring variable group: $VG_NAME"
VG_ID=$(az pipelines variable-group list --query "[?name=='$VG_NAME'].id | [0]" -o tsv || echo "")
if [[ -z "$VG_ID" ]]; then
  VG_ID=$(az pipelines variable-group create --name "$VG_NAME" --authorize true --variables placeholder=1 --query id -o tsv)
fi

echo "[modern][ADO] Populating variables"
vars=$(yq -r '.variableGroup.variables | to_entries[] | "\(.key)=\(.value)"' "$ENV_YAML")
while IFS='=' read -r key val; do
  [[ -z "$key" ]] && continue
  az pipelines variable-group variable create --group-id "$VG_ID" --name "$key" --value "$val" >/dev/null
done <<< "$vars"

# Ensure placeholder keys for workload identity if not already present
# Note: These are populated during deployment, not cluster creation
# Each deployment (dev, demo, prod) manages its own workload identity in its resource group
for wi_var in WORKLOAD_IDENTITY_NAME WORKLOAD_IDENTITY_CLIENT_ID WORKLOAD_IDENTITY_RESOURCE_ID WORKLOAD_IDENTITY_PRINCIPAL_ID WORKLOAD_IDENTITY_SERVICE_ACCOUNT AKS_OIDC_ISSUER WORKLOAD_IDENTITY_FEDERATED_SUBJECT; do
  existing=$(az pipelines variable-group variable list --group-id "$VG_ID" --query "$wi_var" -o tsv 2>/dev/null || true)
  if [[ -z "$existing" ]]; then
    az pipelines variable-group variable create --group-id "$VG_ID" --name "$wi_var" --value "" >/dev/null || true
  fi
done

# Ensure automation toggle for AKS provisioning exists (default false)
DEPLOY_AKS_VAR=$(az pipelines variable-group variable list --group-id "$VG_ID" --query "DEPLOY_AKS" -o tsv 2>/dev/null || true)
if [[ -z "$DEPLOY_AKS_VAR" ]]; then
  az pipelines variable-group variable create --group-id "$VG_ID" --name DEPLOY_AKS --value "false" >/dev/null || true
fi

# Ensure cost control toggle exists (default false)
COSTCONTROL_VAR=$(az pipelines variable-group variable list --group-id "$VG_ID" --query "COSTCONTROL_IGNORE" -o tsv 2>/dev/null || true)
if [[ -z "$COSTCONTROL_VAR" ]]; then
  az pipelines variable-group variable create --group-id "$VG_ID" --name COSTCONTROL_IGNORE --value "false" >/dev/null || true
fi

# Ensure security control toggle exists (default false)
SECURITYCONTROL_VAR=$(az pipelines variable-group variable list --group-id "$VG_ID" --query "SECURITYCONTROL_IGNORE" -o tsv 2>/dev/null || true)
if [[ -z "$SECURITYCONTROL_VAR" ]]; then
  az pipelines variable-group variable create --group-id "$VG_ID" --name SECURITYCONTROL_IGNORE --value "false" >/dev/null || true
fi

# Persist variable group & org/project metadata for pipeline scripts (idempotent)
VG_NAME_VAR=$(az pipelines variable-group variable list --group-id "$VG_ID" --query "VARIABLE_GROUP_NAME" -o tsv 2>/dev/null || true)
if [[ -z "$VG_NAME_VAR" ]]; then
  az pipelines variable-group variable create --group-id "$VG_ID" --name VARIABLE_GROUP_NAME --value "$VG_NAME" >/dev/null || true
fi
ORG_VAR=$(az pipelines variable-group variable list --group-id "$VG_ID" --query "ADO_ORG_URL" -o tsv 2>/dev/null || true)
if [[ -z "$ORG_VAR" ]]; then
  az pipelines variable-group variable create --group-id "$VG_ID" --name ADO_ORG_URL --value "$ADO_ORG" >/dev/null || true
fi
PROJ_VAR=$(az pipelines variable-group variable list --group-id "$VG_ID" --query "ADO_PROJECT" -o tsv 2>/dev/null || true)
if [[ -z "$PROJ_VAR" ]]; then
  az pipelines variable-group variable create --group-id "$VG_ID" --name ADO_PROJECT --value "$ADO_PROJECT" >/dev/null || true
fi

echo "[modern][ADO] Bootstrap complete for $ENV_NAME"

# Extract key variables for display
RESOURCE_GROUP=$(yq -r '.variableGroup.variables.AZURE_RESOURCE_GROUP // "not-set"' "$ENV_YAML")
LOCATION=$(yq -r '.variableGroup.variables.AZURE_LOCATION // "not-set"' "$ENV_YAML")
AKS_CLUSTER_NAME=$(yq -r '.variableGroup.variables.AKS_CLUSTER_NAME // ""' "$ENV_YAML")
AKS_RESOURCE_GROUP=$(yq -r '.variableGroup.variables.AKS_RESOURCE_GROUP // ""' "$ENV_YAML")
DEPLOYMENT_MODEL=$(yq -r '.variableGroup.variables.DEPLOYMENT_MODEL // "public"' "$ENV_YAML")

# Display AKS requirements and next steps
echo ""
echo "========================================="
echo "AKS Cluster Requirements"
echo "========================================="
echo ""

if [[ -z "$AKS_CLUSTER_NAME" ]]; then
  # Use default naming convention
  AKS_CLUSTER_NAME="aks-${RESOURCE_GROUP}"
  AKS_RESOURCE_GROUP="${RESOURCE_GROUP}"
  echo "⚠️  AKS_CLUSTER_NAME not configured. Using default naming convention:"
  echo "   Cluster Name: $AKS_CLUSTER_NAME"
  echo "   Resource Group: $AKS_RESOURCE_GROUP"
else
  echo "✓ AKS cluster configuration detected:"
  echo "   Cluster Name: $AKS_CLUSTER_NAME"
  echo "   Resource Group: ${AKS_RESOURCE_GROUP:-$RESOURCE_GROUP}"
fi

echo ""
echo "Deployment Model: $DEPLOYMENT_MODEL"
echo ""

if [[ "$DEPLOYMENT_MODEL" == "public" ]]; then
  echo "To provision the AKS cluster for PUBLIC deployment, run:"
  echo ""
  echo "  # Bash (Linux/macOS):"
  echo "  ./build/scripts/provision-aks-cluster.sh \\"
  echo "    $RESOURCE_GROUP \\"
  echo "    $LOCATION \\"
  echo "    $AKS_CLUSTER_NAME"
  echo ""
  echo "  # PowerShell (Windows):"
  echo "  .\\build\\scripts\\provision-aks-cluster.ps1 \`"
  echo "    -ResourceGroup $RESOURCE_GROUP \`"
  echo "    -Location $LOCATION \`"
  echo "    -ClusterName $AKS_CLUSTER_NAME"

elif [[ "$DEPLOYMENT_MODEL" == "hybrid" ]] || [[ "$DEPLOYMENT_MODEL" == "private" ]]; then
  echo "⚠️  $DEPLOYMENT_MODEL deployment requires pre-existing networking resources:"
  echo ""
  echo "Required subnets:"
  echo "  • AKS subnet (snet-aks): /20 or larger, no delegation"
  echo "  • Private endpoints subnet (snet-pe): /24, no delegation"
  echo "  • PostgreSQL subnet (snet-postgres): /24, delegated to Microsoft.DBforPostgreSQL/flexibleServers (if using postgres)"
  if [[ "$DEPLOYMENT_MODEL" == "hybrid" ]]; then
    echo "  • Application Gateway subnet (snet-appgw): /24, no delegation"
  fi
  echo ""
  echo "Required DNS zones (can be in different subscription):"
  echo "  • privatelink.database.windows.net"
  echo "  • privatelink.redis.cache.windows.net"
  echo "  • privatelink.blob.core.windows.net"
  echo "  • privatelink.search.windows.net"
  echo "  • privatelink.service.signalr.net (if using SignalR)"
  echo "  • privatelink.postgres.database.azure.com (if using postgres)"
  echo ""

  AZURE_SUBNET_AKS=$(yq -r '.variableGroup.variables.AZURE_SUBNET_AKS // ""' "$ENV_YAML")
  if [[ -z "$AZURE_SUBNET_AKS" ]]; then
    echo "❌ AZURE_SUBNET_AKS not configured. To provision AKS cluster:"
    echo ""
    echo "  1. Create the required networking resources (see above)"
    echo "  2. Add AZURE_SUBNET_AKS variable to your environment YAML"
    echo "  3. Run PowerShell provisioning script:"
    echo ""
    echo "  .\\build\\scripts\\provision-aks-cluster.ps1 \`"
    echo "    -ResourceGroup $RESOURCE_GROUP \`"
    echo "    -Location $LOCATION \`"
    echo "    -ClusterName $AKS_CLUSTER_NAME \`"
    echo "    -DeploymentModel $DEPLOYMENT_MODEL \`"
    echo "    -SubnetResourceId \$env:AZURE_SUBNET_AKS"
  else
    echo "✓ AZURE_SUBNET_AKS configured"
    echo ""
    echo "To provision the AKS cluster for $DEPLOYMENT_MODEL deployment:"
    echo ""
    echo "  .\\build\\scripts\\provision-aks-cluster.ps1 \`"
    echo "    -ResourceGroup $RESOURCE_GROUP \`"
    echo "    -Location $LOCATION \`"
    echo "    -ClusterName $AKS_CLUSTER_NAME \`"
    echo "    -DeploymentModel $DEPLOYMENT_MODEL \`"
    echo "    -SubnetResourceId \"$AZURE_SUBNET_AKS\""
  fi

  if [[ "$DEPLOYMENT_MODEL" == "private" ]]; then
    echo ""
    echo "⚠️  IMPORTANT for private deployment:"
    echo "  • API server won't be accessible from public internet"
    echo "  • Requires self-hosted ADO agents within the VNET"
    echo "  • Or Azure DevOps private agents with VNET connectivity"
  fi
fi

echo ""
echo "========================================="
echo "Variable Configuration Summary"
echo "========================================="
echo ""
echo "Key variables configured in $VG_NAME:"
echo "  AZURE_RESOURCE_GROUP: $RESOURCE_GROUP"
echo "  AZURE_LOCATION: $LOCATION"
echo "  AKS_CLUSTER_NAME: ${AKS_CLUSTER_NAME:-<using default naming>}"
echo "  AKS_RESOURCE_GROUP: ${AKS_RESOURCE_GROUP:-<same as AZURE_RESOURCE_GROUP>}"
echo "  DEPLOYMENT_MODEL: $DEPLOYMENT_MODEL"

if [[ "$DEPLOYMENT_MODEL" != "public" ]]; then
  AZURE_SUBNET_PE=$(yq -r '.variableGroup.variables.AZURE_SUBNET_PE // ""' "$ENV_YAML")
  echo ""
  echo "Networking variables:"
  echo "  AZURE_SUBNET_AKS: ${AZURE_SUBNET_AKS:-❌ NOT SET - Required for $DEPLOYMENT_MODEL}"
  echo "  AZURE_SUBNET_PE: ${AZURE_SUBNET_PE:-❌ NOT SET - Required for $DEPLOYMENT_MODEL}"
fi

echo ""
echo "Next steps:"
echo "  1. Provision AKS cluster using the commands above"
echo "  2. Grant service connection permissions to the cluster"
echo "  3. Configure any missing variables in ADO Library > Variable Groups > $VG_NAME"
echo "  4. Add secrets (PVICO_ENTRA_CREDENTIALS, PVICO_AZUREMAPS_KEY, etc.)"
echo "  5. Run the deployment pipeline"

