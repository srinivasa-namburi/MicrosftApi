#!/usr/bin/env bash
set -euo pipefail

# One-shot publish + deploy wrapper with provider-aware env parsing
# Usage:
#   build/scripts/build-modern-publish-deploy.sh \
#     --provider [ado|github] \
#     --env-file build/environment-variables-ADO-<env>.yml|build/environment-variables-github-<env>.yml \
#     [--out out/publish]

PROVIDER=""
ENV_FILE=""
OUT_DIR="out/publish"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --provider) PROVIDER="$2"; shift 2 ;;
    --env-file) ENV_FILE="$2"; shift 2 ;;
    --out) OUT_DIR="$2"; shift 2 ;;
    *) echo "Unknown arg: $1" >&2; exit 1 ;;
  esac
done

if [[ -z "$PROVIDER" || -z "$ENV_FILE" ]]; then
  echo "Missing --provider or --env-file" >&2
  exit 1
fi

command -v yq >/dev/null || { echo "yq is required" >&2; exit 1; }

# Parse variables by provider
ENV_NAME=$(yq -r '.environment.name' "$ENV_FILE")
if [[ "$PROVIDER" == "ado" ]]; then
  AZURE_SUBSCRIPTION_ID=$(yq -r '.variableGroup.variables.AZURE_SUBSCRIPTION_ID' "$ENV_FILE")
  AZURE_RESOURCE_GROUP=$(yq -r '.variableGroup.variables.AZURE_RESOURCE_GROUP' "$ENV_FILE")
  AZURE_LOCATION=$(yq -r '.variableGroup.variables.AZURE_LOCATION' "$ENV_FILE")
  AKS_NAMESPACE=$(yq -r '.variableGroup.variables.AKS_NAMESPACE' "$ENV_FILE")
  HELM_RELEASE=$(yq -r '.variableGroup.variables.HELM_RELEASE' "$ENV_FILE")
elif [[ "$PROVIDER" == "github" ]]; then
  AZURE_SUBSCRIPTION_ID=$(yq -r '.variables.AZURE_SUBSCRIPTION_ID // empty' "$ENV_FILE")
  AZURE_RESOURCE_GROUP=$(yq -r '.variables.AZURE_RESOURCE_GROUP' "$ENV_FILE")
  AZURE_LOCATION=$(yq -r '.variables.AZURE_LOCATION' "$ENV_FILE")
  AKS_NAMESPACE=$(yq -r '.variables.AKS_NAMESPACE' "$ENV_FILE")
  HELM_RELEASE=$(yq -r '.variables.HELM_RELEASE' "$ENV_FILE")
else
  echo "Unknown provider: $PROVIDER (expected ado|github)" >&2
  exit 1
fi

echo "[modern] Provider=$PROVIDER Env=$ENV_NAME Out=$OUT_DIR"

# Optional: set subscription if available
if command -v az >/dev/null && [[ -n "${AZURE_SUBSCRIPTION_ID:-}" ]]; then
  echo "[modern] Setting Azure subscription $AZURE_SUBSCRIPTION_ID"
  az account set --subscription "$AZURE_SUBSCRIPTION_ID" || true
fi

# Publish (legacy patching disabled for consistent Aspire naming)
build/scripts/build-modern-aspire-publish.sh "$OUT_DIR"
# build/scripts/build-modern-patch-azure-names.sh "$OUT_DIR"  # DISABLED: Use consistent Aspire naming

# Deploy Azure and Helm
build/scripts/build-modern-deploy-azure.sh "$OUT_DIR" "$AZURE_RESOURCE_GROUP" "$AZURE_LOCATION"
build/scripts/build-modern-helm-deploy.sh "$OUT_DIR" "$HELM_RELEASE" "$AKS_NAMESPACE"

echo "[modern] Completed publish + deploy for $ENV_NAME ($PROVIDER)"

