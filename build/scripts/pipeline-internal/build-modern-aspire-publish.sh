#!/usr/bin/env bash
set -euo pipefail

# Build and publish using Aspire CLI, outputting to the given directory (default: out/publish)
# Usage: build/scripts/build-modern-aspire-publish.sh [output_dir]

# Enable verbose output only if DEBUG is set
if [[ "${DEBUG:-}" == "true" ]]; then
  set -x
fi

OUT_DIR=${1:-out/publish}

# Check if aspire CLI is installed
if ! command -v aspire >/dev/null 2>&1; then
    echo "[modern] Error: Aspire CLI is not installed" >&2
    echo "[modern] Install using: curl -sSL https://aspire.dev/install.sh | bash" >&2
    echo "[modern] Or as .NET tool: dotnet tool install -g Aspire.Cli" >&2
    exit 1
fi

echo "[modern] Ensuring output directory: $OUT_DIR"
mkdir -p "$OUT_DIR"

echo "[modern] Current directory: $(pwd)"
echo "[modern] Aspire version: $(aspire --version)"
echo "[modern] Running aspire publish to $OUT_DIR..."

# --- RUN ASPIRE PUBLISH, CAPTURE EXIT CODE, AND DECIDE WHETHER TO CONTINUE ---
set +e
if [[ "${DEBUG:-}" == "true" ]]; then
  aspire publish -o "$OUT_DIR" --verbosity diagnostic \
    --parameter workloadIdentityName="${WORKLOAD_IDENTITY_NAME:-}" \
    --parameter workloadIdentityResourceGroup="${AZURE_RESOURCE_GROUP:-}"
else
  aspire publish -o "$OUT_DIR" \
    --parameter workloadIdentityName="${WORKLOAD_IDENTITY_NAME:-}" \
    --parameter workloadIdentityResourceGroup="${AZURE_RESOURCE_GROUP:-}"
fi
rc=$?
set -e

# Summarize whatever we have so far
echo "[modern] Aspire publish exited with code: $rc"
if [[ -d "$OUT_DIR" ]]; then
  echo "[modern] Checking what was generated (top level):"
  ls -la "$OUT_DIR" 2>/dev/null || true
  echo "[modern] Sampling first 20 files under $OUT_DIR:"
  find "$OUT_DIR" -type f | head -20 || true
else
  echo "[modern] Output directory not created"
fi

# Count artifacts to decide if we can continue on non-zero rc (e.g., warnings)
bicep_count=$(find "$OUT_DIR" -type f -name "*.bicep" 2>/dev/null | wc -l | tr -d '[:space:]')
yaml_count=$(find "$OUT_DIR" -type f -name "*.yaml"  2>/dev/null | wc -l | tr -d '[:space:]')
json_count=$(find "$OUT_DIR" -type f -name "*.json"  2>/dev/null | wc -l | tr -d '[:space:]')

# If rc == 0, great. If rc != 0 but we have artifacts, proceed (treat as warnings).
# If rc != 0 and no artifacts, fail with the original rc.
if [[ $rc -ne 0 ]]; then
  if [[ $rc -eq 2 || $((bicep_count + yaml_count + json_count)) -gt 0 ]]; then
    echo "[modern] Non-zero exit ($rc), but artifacts exist (bicep=$bicep_count yaml=$yaml_count json=$json_count). Continuing."
  else
    echo "[modern] ERROR: aspire publish failed (rc=$rc) and produced no artifacts. Aborting."
    exit "$rc"
  fi
fi

echo ""
echo "[modern] Aspire publish complete: $OUT_DIR"
echo "[modern] Checking generated output:"
ls -la "$OUT_DIR" | head -20 || true

echo ""
echo "[modern] Generated directories:"
find "$OUT_DIR" -maxdepth 1 -type d | sort || true

echo ""
echo "[modern] Generated files summary:"
echo "  Bicep files: $bicep_count"
echo "  YAML files:  $yaml_count"
echo "  JSON files:  $json_count"

# Apply fixes to the generated Aspire output
echo ""
echo "[modern] Applying fixes to Aspire-generated output..."

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
echo "[modern] Script directory: $SCRIPT_DIR"

# Try multiple possible locations for the fix script
FIX_SCRIPT=""
if [[ -f "$SCRIPT_DIR/build-modern-fix-aspire-outputs.sh" ]]; then
  FIX_SCRIPT="$SCRIPT_DIR/build-modern-fix-aspire-outputs.sh"
elif [[ -f "$(pwd)/build/scripts/pipeline-internal/build-modern-fix-aspire-outputs.sh" ]]; then
  FIX_SCRIPT="$(pwd)/build/scripts/pipeline-internal/build-modern-fix-aspire-outputs.sh"
elif [[ -f "${BUILD_SOURCESDIRECTORY:-$(pwd)}/build/scripts/pipeline-internal/build-modern-fix-aspire-outputs.sh" ]]; then
  FIX_SCRIPT="${BUILD_SOURCESDIRECTORY:-$(pwd)}/build/scripts/pipeline-internal/build-modern-fix-aspire-outputs.sh"
fi

if [[ -n "$FIX_SCRIPT" ]] && [[ -f "$FIX_SCRIPT" ]]; then
  echo "[modern] Found fix script at: $FIX_SCRIPT"
  # Ensure script is executable
  chmod +x "$FIX_SCRIPT" 2>/dev/null || true

  # Run the fix script
  echo "[modern] Running fix script on directory: $OUT_DIR"
  if "$FIX_SCRIPT" "$OUT_DIR"; then
    echo "[modern] Fixes applied successfully"

    # Verify the fix was applied
    echo "[modern] Verifying YAML fixes..."
    if grep -q '"ConnectionStrings__blob-docing"' "$OUT_DIR/templates/web-docgen/config.yaml" 2>/dev/null; then
      echo "[modern] YAML keys successfully quoted"
    else
      echo "[modern] WARNING: YAML keys may not be properly quoted"
    fi
  else
    echo "[modern] ERROR: Fix script failed with exit code $?"
    echo "[modern] Continuing anyway but deployment may fail"
  fi
else
  echo "[modern] ERROR: Fix script not found!"
  echo "[modern] Tried locations:"
  echo "[modern]   - $SCRIPT_DIR/build-modern-fix-aspire-outputs.sh"
  echo "[modern]   - $(pwd)/build/scripts/pipeline-internal/build-modern-fix-aspire-outputs.sh"
  echo "[modern]   - ${BUILD_SOURCESDIRECTORY:-$(pwd)}/build/scripts/pipeline-internal/build-modern-fix-aspire-outputs.sh"
  echo "[modern] Current directory: $(pwd)"
  echo "[modern] Directory listing of $SCRIPT_DIR:"
  ls -la "$SCRIPT_DIR/" 2>/dev/null | head -20 || echo "[modern] Could not list directory"
  echo "[modern] This will cause deployment failures!"
fi
