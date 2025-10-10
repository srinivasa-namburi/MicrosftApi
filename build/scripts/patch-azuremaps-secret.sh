#!/usr/bin/env bash
set -euo pipefail

# Quick fix script to patch Azure Maps key into deployed secrets
# Usage: ./patch-azuremaps-secret.sh <namespace> <azure-maps-key>

NAMESPACE="${1:?Missing namespace parameter}"
AZUREMAPS_KEY="${2:?Missing Azure Maps key parameter}"

echo "[Patch] Patching Azure Maps key into all service secrets in namespace: $NAMESPACE"

# Base64-encode the key
AZUREMAPS_B64=$(printf '%s' "$AZUREMAPS_KEY" | base64 | tr -d '\n')

# Patch all service secrets
for app in api-main db-setupmanager mcpserver-core mcpserver-flow silo web-docgen; do
  secret="${app}-secrets"
  echo "[Patch] Patching $secret..."

  if kubectl -n "$NAMESPACE" get secret "$secret" >/dev/null 2>&1; then
    kubectl -n "$NAMESPACE" patch secret "$secret" --type='merge' \
      -p "{\"data\":{\"ServiceConfiguration__AzureMaps__Key\":\"$AZUREMAPS_B64\"}}"
    echo "[Patch] ✅ Patched $secret"
  else
    echo "[Patch] ⚠️  Secret $secret not found, skipping"
  fi
done

echo ""
echo "[Patch] Restarting deployments to pick up new secrets..."
for app in api-main mcp-server silo web-docgen; do
  deployment="${app}-deployment"
  if kubectl -n "$NAMESPACE" get deployment "$deployment" >/dev/null 2>&1; then
    kubectl -n "$NAMESPACE" rollout restart deployment "$deployment"
    echo "[Patch] ✅ Restarted $deployment"
  fi
done

echo ""
echo "[Patch] ✅ Azure Maps key patched successfully!"
echo "[Patch] Wait for pods to restart and verify the map component works"