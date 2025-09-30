#!/usr/bin/env bash
set -euo pipefail

# Validates that existing AKS cluster's networking mode is compatible with DEPLOYMENT_MODEL
# Usage: validate-aks-networking.sh <resource_group> <cluster_name> <deployment_model>
# Returns: 0 if compatible, 1 if incompatible, 2 if cluster doesn't exist

RG=${1:?Missing resource group}
CLUSTER=${2:?Missing cluster name}
DEPLOYMENT_MODEL=${3:-public}

echo "[validate-aks] Checking AKS cluster networking compatibility..."
echo "[validate-aks] Resource Group: $RG"
echo "[validate-aks] Cluster Name: $CLUSTER"
echo "[validate-aks] Target Deployment Model: $DEPLOYMENT_MODEL"

# Check if cluster exists
if ! az aks show -g "$RG" -n "$CLUSTER" >/dev/null 2>&1; then
  echo "[validate-aks] ✅ Cluster does not exist yet - will be created with $DEPLOYMENT_MODEL networking"
  exit 2
fi

# Get cluster networking configuration
echo "[validate-aks] Retrieving cluster networking configuration..."
CLUSTER_JSON=$(az aks show -g "$RG" -n "$CLUSTER" -o json)

# Check if it's a private cluster
IS_PRIVATE=$(echo "$CLUSTER_JSON" | jq -r '.apiServerAccessProfile.enablePrivateCluster // false')
PRIVATE_CLUSTER_ENABLED=$(echo "$CLUSTER_JSON" | jq -r '.privateClusterEnabled // false')

# Determine actual cluster mode
if [[ "$IS_PRIVATE" == "true" ]] || [[ "$PRIVATE_CLUSTER_ENABLED" == "true" ]]; then
  CLUSTER_MODE="private"
  echo "[validate-aks] Cluster is configured as: PRIVATE (API server not publicly accessible)"
else
  # Check if cluster is in a VNET
  VNET_SUBNET=$(echo "$CLUSTER_JSON" | jq -r '.agentPoolProfiles[0].vnetSubnetId // empty')
  if [[ -n "$VNET_SUBNET" ]]; then
    CLUSTER_MODE="hybrid"
    echo "[validate-aks] Cluster is configured as: HYBRID (public API, VNET integrated)"
    echo "[validate-aks]   Subnet: $VNET_SUBNET"
  else
    CLUSTER_MODE="public"
    echo "[validate-aks] Cluster is configured as: PUBLIC (no VNET integration)"
  fi
fi

# Check compatibility matrix
echo ""
echo "[validate-aks] Validating compatibility..."

case "$DEPLOYMENT_MODEL" in
  public)
    if [[ "$CLUSTER_MODE" != "public" ]]; then
      echo "[validate-aks] ❌ ERROR: Cannot deploy PUBLIC workloads to $CLUSTER_MODE cluster!"
      echo "[validate-aks]    Cluster networking mode: $CLUSTER_MODE"
      echo "[validate-aks]    Requested deployment model: $DEPLOYMENT_MODEL"
      echo ""
      echo "[validate-aks] INCOMPATIBLE: Public deployments require a public cluster."
      echo "[validate-aks] Options:"
      echo "[validate-aks]   1. Change DEPLOYMENT_MODEL to 'hybrid' or 'private' in your variable group"
      echo "[validate-aks]   2. Or provision a new public cluster for this deployment"
      exit 1
    fi
    echo "[validate-aks] ✅ Compatible: PUBLIC deployment to PUBLIC cluster"
    ;;

  hybrid)
    if [[ "$CLUSTER_MODE" == "public" ]]; then
      echo "[validate-aks] ❌ ERROR: Cannot deploy HYBRID workloads to PUBLIC cluster!"
      echo "[validate-aks]    Cluster networking mode: $CLUSTER_MODE"
      echo "[validate-aks]    Requested deployment model: $DEPLOYMENT_MODEL"
      echo ""
      echo "[validate-aks] INCOMPATIBLE: Hybrid deployments require VNET integration."
      echo "[validate-aks] Options:"
      echo "[validate-aks]   1. Change DEPLOYMENT_MODEL to 'public' in your variable group"
      echo "[validate-aks]   2. Or provision a new hybrid cluster with VNET integration"
      exit 1
    fi
    # Both hybrid and private clusters can host hybrid deployments
    echo "[validate-aks] ✅ Compatible: HYBRID deployment to $CLUSTER_MODE cluster"
    ;;

  private)
    if [[ "$CLUSTER_MODE" == "public" ]]; then
      echo "[validate-aks] ❌ ERROR: Cannot deploy PRIVATE workloads to PUBLIC cluster!"
      echo "[validate-aks]    Cluster networking mode: $CLUSTER_MODE"
      echo "[validate-aks]    Requested deployment model: $DEPLOYMENT_MODEL"
      echo ""
      echo "[validate-aks] INCOMPATIBLE: Private deployments require VNET integration."
      echo "[validate-aks] Options:"
      echo "[validate-aks]   1. Change DEPLOYMENT_MODEL to 'public' in your variable group"
      echo "[validate-aks]   2. Or provision a new private cluster with VNET integration"
      exit 1
    fi
    # Both hybrid and private clusters can host private deployments
    echo "[validate-aks] ✅ Compatible: PRIVATE deployment to $CLUSTER_MODE cluster"
    ;;

  *)
    echo "[validate-aks] ❌ ERROR: Unknown deployment model: $DEPLOYMENT_MODEL"
    echo "[validate-aks]    Valid values: public, hybrid, private"
    exit 1
    ;;
esac

# Additional validation for private clusters
if [[ "$CLUSTER_MODE" == "private" ]]; then
  echo ""
  echo "[validate-aks] ⚠️  Note: Private cluster detected - ensure your ADO agents can reach it"
  PRIVATE_FQDN=$(echo "$CLUSTER_JSON" | jq -r '.privateFqdn // empty')
  if [[ -n "$PRIVATE_FQDN" ]]; then
    echo "[validate-aks]    Private API endpoint: $PRIVATE_FQDN"
  fi
fi

echo ""
echo "[validate-aks] Validation completed successfully"
exit 0