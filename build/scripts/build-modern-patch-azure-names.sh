#!/usr/bin/env bash
set -euo pipefail

# Patch generated Azure Bicep names from Aspire publish to match legacy naming conventions.
# Usage: build/scripts/build-modern-patch-azure-names.sh <publish-output-dir>

# Enable verbose output only if DEBUG is set
if [[ "${DEBUG:-}" == "true" ]]; then
  set -x
fi

# Make ** recursive and ignore non-matches
shopt -s nullglob
shopt -s globstar

OUT_DIR=${1:-out/publish}

echo ""
echo "[modern-patch] Checking for Azure resources to patch in: $OUT_DIR"

# Check if we have any bicep files to patch
mapfile -t BICEP_FILES < <(find "$OUT_DIR" -type f -name "*.bicep" 2>/dev/null | sort)
BICEP_COUNT=${#BICEP_FILES[@]}

if [[ $BICEP_COUNT -eq 0 ]]; then
  echo "[modern-patch] No Bicep files found to patch."
  echo "[modern-patch] This is expected when using external Azure resources or AKS-only deployment."
  echo "[modern-patch] Skipping Azure name patching."
  exit 0
fi

echo "[modern-patch] Found $BICEP_COUNT Bicep files to potentially patch"

patch_file() {
  local file="$1"
  if [[ ! -r "$file" ]]; then
    echo "[modern-patch] WARNING: Skipping unreadable file: $file" >&2
    return 0
  fi

  local tmp
  tmp="$(mktemp "${file}.XXXXXX")"

  # Run awk; if awk fails, keep original file and report warning.
  if ! awk '
  function replace_name(new){
    sub(/^[[:space:]]*name:[[:space:]]*.*/, "  name: " new)
    state=""; patched=1
  }
  {
    if ($0 ~ /resource[[:space:]]+[^[:space:]]+[[:space:]]+.Microsoft\.Search\/searchServices@/) { state="search" }
    else if ($0 ~ /resource[[:space:]]+[^[:space:]]+[[:space:]]+.Microsoft\.Storage\/storageAccounts@/) { state="storage" }
    else if ($0 ~ /resource[[:space:]]+[^[:space:]]+[[:space:]]+.Microsoft\.Cache\/redis@/) { state="redis" }
    else if ($0 ~ /resource[[:space:]]+[^[:space:]]+[[:space:]]+.Microsoft\.SignalRService\/signalR@/) { state="signalr" }
    else if ($0 ~ /resource[[:space:]]+[^[:space:]]+[[:space:]]+.Microsoft\.Sql\/servers@/) { state="sqlserver" }
    else if ($0 ~ /resource[[:space:]]+[^[:space:]]+[[:space:]]+.Microsoft\.EventHub\/namespaces@/) { state="eventhubns" }

    if (state != "" && $0 ~ /^[[:space:]]*name:[[:space:]]*./) {
      uniqueStr="${uniqueString(resourceGroup().id)}"
      if (state == "search")         { replace_name("take('\''aisearch" uniqueStr "'\'', 60)") }
      else if (state == "storage")   { replace_name("take('\''docing"   uniqueStr "'\'', 24)") }
      else if (state == "redis")     { replace_name("take('\''redis"    uniqueStr "'\'', 63)") }
      else if (state == "signalr")   { replace_name("take('\''signalr"  uniqueStr "'\'', 63)") }
      else if (state == "sqlserver") { replace_name("take('\''sqldocgen" uniqueStr "'\'', 63)") }
      else if (state == "eventhubns"){ replace_name("'\''eventhub-" uniqueStr "'\''") }
    }
    print $0
  }' "$file" > "$tmp"; then
    echo "[modern-patch] WARNING: awk failed for $file; leaving file unchanged" >&2
    rm -f "$tmp"
    return 0
  fi

  # If awk succeeded, replace the file atomically
  if ! mv "$tmp" "$file"; then
    echo "[modern-patch] WARNING: Failed to replace $file (mv). Leaving original intact." >&2
    rm -f "$tmp"
    return 0
  fi

  return 0
}

patched_count=0
for file in "$OUT_DIR"/**/*.bicep "$OUT_DIR"/*.bicep; do
  # The glob may expand to nothing; nullglob handles that.
  [[ -e "$file" ]] || continue
  echo "[modern-patch] Patching: $file"
  if patch_file "$file"; then
    patched_count=$((patched_count + 1))  # Changed this line
  else
    # patch_file is resilient and should always return 0; this is defensive.
    echo "[modern-patch] WARNING: Failed to patch $file (unexpected error)"
  fi
done

if [[ $patched_count -gt 0 ]]; then
  echo "[modern-patch] Processed $patched_count Bicep files in: $OUT_DIR"
else
  echo "[modern-patch] No Bicep files processed"
fi
