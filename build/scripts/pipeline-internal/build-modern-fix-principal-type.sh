#!/usr/bin/env bash
set -euo pipefail

# Fix missing principalType values in Bicep templates.
# Aspire 9.4 generates principalType parameter but passes empty string instead of 'ServicePrincipal'
# This resolves "InvalidPrincipalId" errors when deploying with service principals.
# Usage: build/scripts/build-modern-fix-principal-type.sh <publish-output-dir>

# Enable verbose output only if DEBUG is set
if [[ "${DEBUG:-}" == "true" ]]; then set -x; fi

OUT_DIR=${1:-out/publish}

echo ""
echo "[modern-principal-fix] Starting principal type fix script"
echo "[modern-principal-fix] Working directory: $(pwd)"
echo "[modern-principal-fix] Target directory: $OUT_DIR"
echo "[modern-principal-fix] Script running as: $(whoami)"

# Fix main.bicep - replace empty principalType with 'ServicePrincipal'
MAIN_BICEP="$OUT_DIR/main.bicep"
echo "[modern-principal-fix] Looking for main.bicep at: $MAIN_BICEP"

if [[ -f "$MAIN_BICEP" ]]; then
  echo "[modern-principal-fix] Found main.bicep, checking for empty principalType values"

  # Show first few lines with principalType for debugging
  echo "[modern-principal-fix] Sample of principalType lines before fix:"
  grep "principalType:" "$MAIN_BICEP" | head -3 || echo "  (no principalType lines found)"

  # Count how many empty principalType values we have
  COUNT=$(grep -c "principalType: ''" "$MAIN_BICEP" 2>/dev/null | tr -d '\n' || echo "0")

  if [[ $COUNT -gt 0 ]]; then
    echo "[modern-principal-fix] Found $COUNT empty principalType values to fix"

    # Create a temporary file
    TMP_FILE="${MAIN_BICEP}.tmp$$"

    # Replace all empty principalType with 'ServicePrincipal' using a more portable approach
    if sed "s/principalType: ''/principalType: 'ServicePrincipal'/g" "$MAIN_BICEP" > "$TMP_FILE"; then
      # Move the temp file to the original
      mv "$TMP_FILE" "$MAIN_BICEP"
      echo "[modern-principal-fix] Successfully replaced $COUNT empty principalType values"

      # Verify the fix worked
      echo "[modern-principal-fix] Sample of principalType lines after fix:"
      grep "principalType:" "$MAIN_BICEP" | head -3 || echo "  (no principalType lines found)"

      # Check if any empty values remain
      REMAINING=$(grep -c "principalType: ''" "$MAIN_BICEP" 2>/dev/null | tr -d '\n' || echo "0")
      if [ "$REMAINING" -gt "0" ]; then
        echo "[modern-principal-fix] WARNING: $REMAINING empty principalType values still remain!" >&2
      fi
    else
      echo "[modern-principal-fix] ERROR: Failed to process main.bicep" >&2
      rm -f "$TMP_FILE"
      exit 1
    fi
  else
    echo "[modern-principal-fix] No empty principalType values found in main.bicep"
  fi

  # Fix empty principalId and principalName parameters for SQL deployment script
  echo "[modern-principal-fix] Fixing empty principal parameters for role modules..."

  # Count how many empty parameters we have
  COUNT_ID=$(grep -c "principalId: ''" "$MAIN_BICEP" 2>/dev/null | tr -d '\n' || echo "0")
  COUNT_NAME=$(grep -c "principalName: ''" "$MAIN_BICEP" 2>/dev/null | tr -d '\n' || echo "0")

  if [[ $COUNT_ID -gt 0 ]] || [[ $COUNT_NAME -gt 0 ]]; then
    echo "[modern-principal-fix] Found $COUNT_ID empty principalId and $COUNT_NAME empty principalName values to fix"

    # Create a temporary file
    TMP_FILE="${MAIN_BICEP}.tmpid$$"

    # Skip fixing principal parameters here - they will be handled by the role alignment script
    # The role alignment script runs after this and properly handles principalId/principalName parameters
    echo "[modern-principal-fix] Skipping principalId/principalName fixes - handled by role alignment script"
    echo "[modern-principal-fix] Found $COUNT_ID empty principalId and $COUNT_NAME empty principalName values (will be fixed later)"
  fi

  # SQL permissions are now handled by setting workload identity as SQL Server admin
  echo "[modern-principal-fix] SQL permissions are handled by simplified admin approach"
  echo "[modern-principal-fix] The workload identity will be set as SQL Server admin during deployment"
  echo "[modern-principal-fix] No deployment scripts or complex role assignments needed"
else
  echo "[modern-principal-fix] ERROR: main.bicep not found at $MAIN_BICEP" >&2
  echo "[modern-principal-fix] Directory contents:" >&2
  ls -la "$OUT_DIR" 2>/dev/null || echo "  (directory does not exist)" >&2
  exit 1
fi

echo "[modern-principal-fix] Principal type and ID fixing complete"
echo "[modern-principal-fix] Script finished successfully"