#!/usr/bin/env bash
set -euo pipefail

# Bootstrap GitHub environment variables/secrets from a repo-local YAML using gh CLI.
# Usage: build/scripts/build-modern-bootstrap-github.sh <env-yaml> <owner/repo>
# Requires: gh CLI and yq

ENV_YAML=${1:?Missing env yaml}
REPO=${2:?Missing owner/repo}

command -v yq >/dev/null || { echo "yq is required" >&2; exit 1; }
command -v gh >/dev/null || { echo "gh is required" >&2; exit 1; }

ENV_NAME=$(yq -r '.environment.name' "$ENV_YAML")

echo "[modern][GH] Ensuring environment: $ENV_NAME"
gh api -X PUT repos/$REPO/environments/$ENV_NAME >/dev/null

echo "[modern][GH] Populating vars"
vars=$(yq -r '.variables | to_entries[] | "\(.key)=\(.value)"' "$ENV_YAML")
while IFS='=' read -r key val; do
  [[ -z "$key" ]] && continue
  gh variable set "$key" --env "$ENV_NAME" --repo "$REPO" --body "$val"
done <<< "$vars"

echo "[modern][GH] Populating secrets"
secs=$(yq -r '.secrets | to_entries[] | "\(.key)=\(.value)"' "$ENV_YAML")
while IFS='=' read -r key val; do
  [[ -z "$key" ]] && continue
  gh secret set "$key" --env "$ENV_NAME" --repo "$REPO" --body "$val"
done <<< "$secs"

echo "[modern][GH] Bootstrap complete for $ENV_NAME"

# Extract key variables for display
RESOURCE_GROUP=$(yq -r '.variables.AZURE_RESOURCE_GROUP // "not-set"' "$ENV_YAML")
LOCATION=$(yq -r '.variables.AZURE_LOCATION // "not-set"' "$ENV_YAML")
AKS_CLUSTER_NAME=$(yq -r '.variables.AKS_CLUSTER_NAME // ""' "$ENV_YAML")
AKS_RESOURCE_GROUP=$(yq -r '.variables.AKS_RESOURCE_GROUP // ""' "$ENV_YAML")
DEPLOYMENT_MODEL=$(yq -r '.variables.DEPLOYMENT_MODEL // "public"' "$ENV_YAML")

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

  AZURE_SUBNET_AKS=$(yq -r '.variables.AZURE_SUBNET_AKS // ""' "$ENV_YAML")
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
    echo "  • Requires self-hosted GitHub runners within the VNET"
  fi
fi

echo ""
echo "========================================="
echo "Variable Configuration Summary"
echo "========================================="
echo ""
echo "Key variables configured in $ENV_NAME environment:"
echo "  AZURE_RESOURCE_GROUP: $RESOURCE_GROUP"
echo "  AZURE_LOCATION: $LOCATION"
echo "  AKS_CLUSTER_NAME: ${AKS_CLUSTER_NAME:-<using default naming>}"
echo "  AKS_RESOURCE_GROUP: ${AKS_RESOURCE_GROUP:-<same as AZURE_RESOURCE_GROUP>}"
echo "  DEPLOYMENT_MODEL: $DEPLOYMENT_MODEL"

if [[ "$DEPLOYMENT_MODEL" != "public" ]]; then
  AZURE_SUBNET_PE=$(yq -r '.variables.AZURE_SUBNET_PE // ""' "$ENV_YAML")
  echo ""
  echo "Networking variables:"
  echo "  AZURE_SUBNET_AKS: ${AZURE_SUBNET_AKS:-❌ NOT SET - Required for $DEPLOYMENT_MODEL}"
  echo "  AZURE_SUBNET_PE: ${AZURE_SUBNET_PE:-❌ NOT SET - Required for $DEPLOYMENT_MODEL}"
fi

echo ""
echo "Next steps:"
echo "  1. Provision AKS cluster using the commands above"
echo "  2. Grant service principal permissions to the cluster"
echo "  3. Configure any missing secrets in GitHub Settings > Secrets > Environment secrets"
echo "  4. Add secrets (PVICO_ENTRA_CREDENTIALS, PVICO_AZUREMAPS_KEY, etc.)"
echo "  5. Run the deployment workflow"

