#!/usr/bin/env bash
set -euo pipefail
# Align role assignment principalId references in Aspire publish output to use proper principal.
# Usage: build-modern-align-role-principals.sh <publish-output-dir>
OUT_DIR=${1:-out/publish}

# Check if we have a workload identity principal ID
if [[ -n "${WORKLOAD_IDENTITY_PRINCIPAL_ID:-}" ]]; then
  echo "[modern-role-align] Using workload identity principal: $WORKLOAD_IDENTITY_PRINCIPAL_ID"
  PRINCIPAL_ID="$WORKLOAD_IDENTITY_PRINCIPAL_ID"
  PRINCIPAL_TYPE="ServicePrincipal"
  # For SQL Server, we need the managed identity name
  if [[ -z "${WORKLOAD_IDENTITY_NAME:-}" ]]; then
    echo "[modern-role-align] ERROR: WORKLOAD_IDENTITY_NAME is required for SQL Server role assignments"
    exit 1
  fi
  PRINCIPAL_NAME="$WORKLOAD_IDENTITY_NAME"
else
  echo "[modern-role-align] WORKLOAD_IDENTITY_PRINCIPAL_ID not set - role assignments will use deployment principal"
  # Don't exit - we still need to fix the main.bicep to use the principalId parameter
fi

# Fix main.bicep to pass principalId parameter to role modules
MAIN_BICEP="$OUT_DIR/main.bicep"
if [[ -f "$MAIN_BICEP" ]]; then
  echo "[modern-role-align] Fixing main.bicep to pass principalId to role modules"

  # Create backup
  cp "$MAIN_BICEP" "${MAIN_BICEP}.bak" || true

  # Replace empty principalId with parameter reference in role module calls
  sed -i "s/principalId: ''/principalId: principalId/g" "$MAIN_BICEP"
  sed -i "s/principalType: ''/principalType: 'ServicePrincipal'/g" "$MAIN_BICEP"

  # For SQL modules, we need to handle principalName specially
  # SQL modules expect a managed identity name, not just the principal ID
  # Always add the principalName parameter to avoid deployment errors
  if ! grep -q "param principalName string" "$MAIN_BICEP"; then
    # Add principalName parameter after principalId parameter (no default value)
    sed -i "/param principalId string/a\\\\nparam principalName string" "$MAIN_BICEP"
    echo "[modern-role-align] Added principalName parameter to main.bicep"
  fi

  # Update SQL role module calls to use principalName parameter
  if [[ -n "${WORKLOAD_IDENTITY_PRINCIPAL_ID:-}" ]]; then
    sed -i "s/principalName: ''/principalName: principalName/g" "$MAIN_BICEP"
  else
    # If not using workload identity, use principalId as fallback for principalName
    sed -i "s/principalName: ''/principalName: principalId/g" "$MAIN_BICEP"
  fi

  echo "[modern-role-align] Updated main.bicep to use principalId parameter"
fi

# Also fix individual role module files if needed
ROLE_FILES=$(find "$OUT_DIR" -name "*-roles.bicep" 2>/dev/null || true)
if [[ -n "$ROLE_FILES" ]]; then
  for file in $ROLE_FILES; do
    echo "[modern-role-align] Checking role module: $file"

    # Check if this is a SQL role file that needs principalName
    if grep -q "userAssignedIdentities" "$file" 2>/dev/null; then
      echo "[modern-role-align] SQL role module detected - requires managed identity"
      # SQL modules need special handling - they use managed identity name
      # We'll need to ensure the managed identity is passed correctly
    fi
  done
fi

echo "[modern-role-align] Role assignment alignment complete"
