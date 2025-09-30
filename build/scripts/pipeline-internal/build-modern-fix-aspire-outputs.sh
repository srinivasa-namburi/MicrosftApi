#!/usr/bin/env bash
set -euo pipefail

# Fix Aspire 9.4 generated outputs for pipeline compatibility
# This script:
# 1. Adds output declarations to main.bicep for Azure resource endpoints
# 2. Fixes YAML syntax issues in Helm templates (quotes keys with dashes)

OUT_DIR=${1:-out/publish}

echo "[aspire-fix] Fixing Aspire 9.4 generated outputs for pipeline compatibility"
echo "[aspire-fix] Script location: $0"
echo "[aspire-fix] Working directory: $(pwd)"
echo "[aspire-fix] Output directory: $OUT_DIR"

# Verify directory exists
if [[ ! -d "$OUT_DIR" ]]; then
  echo "[aspire-fix] ERROR: Output directory does not exist: $OUT_DIR"
  exit 1
fi

# List what's in the directory
echo "[aspire-fix] Contents of output directory:"
ls -la "$OUT_DIR" | head -10 || true

# Fix 1: Add resourceId outputs to individual resource Bicep modules
echo "[aspire-fix] Adding resourceId outputs to resource Bicep modules..."

# Find all resource Bicep files (excluding roles and main.bicep)
find "$OUT_DIR" -type f -name "*.bicep" 2>/dev/null | while read -r bicep_file; do
  # Skip main.bicep, roles files, and private-endpoints
  [[ "$(basename "$bicep_file")" == "main.bicep" ]] && continue
  [[ "$bicep_file" == *"-roles.bicep" ]] && continue
  [[ "$bicep_file" == *"private-endpoints"* ]] && continue

  # Extract resource type from file (look for resource declarations)
  resource_var=$(grep "^resource [a-zA-Z_][a-zA-Z0-9_]* " "$bicep_file" | head -1 | awk '{print $2}')

  # Skip if no resource declaration found or resourceId output already exists
  [[ -z "$resource_var" ]] && continue
  grep -q "^output resourceId string" "$bicep_file" && continue

  echo "[aspire-fix]   Adding resourceId output to: $(basename $(dirname $bicep_file))/$(basename $bicep_file)"

  # Add resourceId output after existing outputs
  echo "" >> "$bicep_file"
  echo "output resourceId string = ${resource_var}.id" >> "$bicep_file"
done

# Fix 2: Add output declarations to main.bicep if it exists
if [[ -f "$OUT_DIR/main.bicep" ]]; then
  echo "[aspire-fix] Adding output declarations to main.bicep..."

  # Check if outputs already exist (to make script idempotent)
  if ! grep -q "^output " "$OUT_DIR/main.bicep"; then
    cat >> "$OUT_DIR/main.bicep" <<'EOF'

// Output declarations - expose module outputs for pipeline consumption
// Using UPPER_SNAKE_CASE to match pipeline expectations

// Storage endpoints
output DOCING_BLOBENDPOINT string = docing.outputs.blobEndpoint
output DOCING_NAME string = docing.outputs.name
output ORLEANS_STORAGE_BLOBENDPOINT string = orleans_storage.outputs.blobEndpoint
output ORLEANS_STORAGE_TABLEENDPOINT string = orleans_storage.outputs.tableEndpoint
output ORLEANS_STORAGE_NAME string = orleans_storage.outputs.name

// SQL Server
output SQLDOCGEN_SQLSERVERFQDN string = sqldocgen.outputs.sqlServerFqdn
output SQLDOCGEN_NAME string = sqldocgen.outputs.name

// AI Search
output AISEARCH_CONNECTIONSTRING string = aiSearch.outputs.connectionString
output AISEARCH_NAME string = aiSearch.outputs.name

// Application Insights
output INSIGHTS_APPINSIGHTSCONNECTIONSTRING string = insights.outputs.appInsightsConnectionString
output INSIGHTS_NAME string = insights.outputs.name

// Event Hub
output EVENTHUB_EVENTHUBSENDPOINT string = eventhub.outputs.eventHubsEndpoint
output EVENTHUB_NAME string = eventhub.outputs.name
EOF
    echo "[aspire-fix] Added Bicep output declarations"
  else
    echo "[aspire-fix] Bicep outputs already present, skipping"
  fi
fi

# Fix 3: Quote YAML keys containing dashes in all ConfigMap files
echo "[aspire-fix] Fixing YAML syntax in Helm templates..."

# Find all config.yaml files in templates
find "$OUT_DIR/templates" -name "config.yaml" -type f 2>/dev/null | while read -r config_file; do
  echo "[aspire-fix] Processing: $config_file"

  # Create a temporary file
  temp_file=$(mktemp)

  # Process the file line by line to quote keys with dashes
  while IFS= read -r line; do
    # Check if line contains a key with dash in format: "  ConnectionStrings__blob-docing: ..."
    if [[ "$line" =~ ^([[:space:]]*)([A-Za-z_][A-Za-z0-9_-]*):(.*)$ ]]; then
      indent="${BASH_REMATCH[1]}"
      key="${BASH_REMATCH[2]}"
      rest="${BASH_REMATCH[3]}"

      # If key contains a dash and isn't already quoted, quote it
      if [[ "$key" == *"-"* ]] && [[ ! "$key" =~ ^\".*\"$ ]]; then
        line="${indent}\"${key}\":${rest}"
      fi
    fi
    echo "$line"
  done < "$config_file" > "$temp_file"

  # Replace original file
  mv "$temp_file" "$config_file"
done

# Count fixes applied
CONFIG_FILES_FIXED=$(find "$OUT_DIR/templates" -name "config.yaml" -type f 2>/dev/null | wc -l)
echo "[aspire-fix] Fixed YAML syntax in $CONFIG_FILES_FIXED config files"

# Fix 4: Rewrite Go template expressions referencing hyphenated keys to use index
# Example: {{ .Values.config.web_docgen.ConnectionStrings__blob-docing }}
#   becomes: {{ index .Values.config.web_docgen "ConnectionStrings__blob-docing" }}
echo "[aspire-fix] Rewriting Go template references to hyphenated keys (using index ...)"
find "$OUT_DIR/templates" -name "*.yaml" -type f 2>/dev/null | while read -r yaml_file; do
  # Only require that the file contains any template; perl will no-op if no match
  if grep -q '{{' "$yaml_file"; then
    tmpfile=$(mktemp)
    # Use perl to rewrite occurrences within template expressions
    # Regex breakdown:
    #  (\{\{[^}]*?)   => start of template up to before the offending identifier
    #  (\.\w+(?:\.\w+)*) => dot-qualified prefix like .Values.config.web_docgen
    #  \.([A-Za-z0-9_]+-[A-Za-z0-9_\-]+) => the hyphenated key segment
    #  ([^}]*\}\})   => rest of the template until closing braces
    perl -0777 -pe '
      s/(\{\{[^}]*?)(\.\w+(?:\.\w+)*?)\.([A-Za-z0-9_]+-[A-Za-z0-9_\-]+)([^}]*\}\})/$1 . " index " . $2 . " \"" . $3 . "\"" . $4/eg;
    ' "$yaml_file" > "$tmpfile"
    mv "$tmpfile" "$yaml_file"
  fi
done

# Fix 4: Use Helm's quote filter for config values to avoid YAML parsing issues
echo "[aspire-fix] Ensuring all ConfigMap values use Helm | quote for safe YAML"
find "$OUT_DIR/templates" -name "config.yaml" -type f 2>/dev/null | while read -r cfg; do
  # Two-pass sed to avoid issues with inner quotes inside the template expression
  # 1) key: "{{ EXPR }}"  -> key: {{ (EXPR }}
  sed -E -i 's/(:\s*)"\{\{\s*/\1{{ (/g' "$cfg"
  # 2) ... }}" -> ...) | quote }}
  sed -E -i 's/\s*\}\}\"\s*$/) | quote }}/' "$cfg"
done

# Fix 5: Ensure Service/Deployment ports are integers, not strings
echo "[aspire-fix] Normalizing Service/Deployment port fields to integers"
# Service ports: port / targetPort should be ints
find "$OUT_DIR/templates" -name "service.yaml" -type f 2>/dev/null | while read -r svc; do
  # port: "{{ EXPR }}" -> port: {{ (EXPR) | int }}
  sed -E -i 's/(^\s*port:\s*)"\{\{\s*(.*?)\s*\}\}"/\1{{ (\2) | int }}/' "$svc"
  # targetPort: "{{ EXPR }}" -> targetPort: {{ (EXPR) | int }}
  sed -E -i 's/(^\s*targetPort:\s*)"\{\{\s*(.*?)\s*\}\}"/\1{{ (\2) | int }}/' "$svc"
done

# Deployment containerPort fields should be ints
find "$OUT_DIR/templates" -name "deployment.yaml" -type f 2>/dev/null | while read -r dep; do
  # containerPort: "{{ EXPR }}" -> containerPort: {{ (EXPR) | int }}
  sed -E -i 's/(^\s*containerPort:\s*)"\{\{\s*(.*?)\s*\}\}"/\1{{ (\2) | int }}/' "$dep"
done

# Ensure private networking scaffolding exists and tighten public network access when required
deploy_model="${DEPLOYMENT_MODEL:-public}"
deploy_model="${deploy_model,,}"
main_bicep="$OUT_DIR/main.bicep"

if [[ -f "$main_bicep" ]]; then
  template_source="$(pwd)/build/bicep/private-endpoints.bicep"
  template_dir="$OUT_DIR/private-endpoints"
  if [[ -f "$template_source" ]]; then
    mkdir -p "$template_dir"
    cp "$template_source" "$template_dir/private-endpoints.bicep"
  else
    echo "[aspire-fix] WARNING: private-endpoints template not found at $template_source"
  fi

  if ! grep -q "param peSubnet string" "$main_bicep"; then
    python3 - "$main_bicep" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text()
marker = "param principalId string"
insertion = "param principalId string\n\nparam peSubnet string = ''\n"
if marker in text:
    text = text.replace(marker, insertion, 1)
else:
    text += "\nparam peSubnet string = ''\n"
path.write_text(text)
PY
    echo "[aspire-fix] Added peSubnet parameter to main.bicep"
  fi

  if ! grep -q "private-endpoints/private-endpoints.bicep" "$main_bicep"; then
    python3 - "$main_bicep" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text()
module_block = """
module private_endpoints 'private-endpoints/private-endpoints.bicep' = if (!empty(peSubnet)) {
  name: 'private-endpoints'
  scope: rg
  params: {
    location: location
    peSubnet: peSubnet
    sqldocgenId: sqldocgen.outputs.resourceId
    sqldocgenName: sqldocgen.outputs.name
    docingId: docing.outputs.resourceId
    docingName: docing.outputs.name
    orleansStorageId: orleans_storage.outputs.resourceId
    orleansStorageName: orleans_storage.outputs.name
    eventhubId: eventhub.outputs.resourceId
    eventhubName: eventhub.outputs.name
    aiSearchId: aiSearch.outputs.resourceId
    aiSearchName: aiSearch.outputs.name
  }
}
""".strip()

marker = "// Output declarations"
if marker in text:
    text = text.replace(marker, module_block + "\n\n" + marker, 1)
else:
    text = text.rstrip() + "\n\n" + module_block + "\n"

path.write_text(text)
PY
    echo "[aspire-fix] Injected private endpoints module into main.bicep"
  fi

  if [[ "$deploy_model" == "private" || "$deploy_model" == "hybrid" ]]; then
    echo "[aspire-fix] DEPLOYMENT_MODEL=$deploy_model - disabling public network access for Azure PaaS resources"

    if [[ -f "$OUT_DIR/sqldocgen/sqldocgen.bicep" ]]; then
      sed -i "s/publicNetworkAccess: 'Enabled'/publicNetworkAccess: 'Disabled'/" "$OUT_DIR/sqldocgen/sqldocgen.bicep"
    fi

    for storage_file in "$OUT_DIR/docing/docing.bicep" "$OUT_DIR/orleans-storage/orleans-storage.bicep"; do
      if [[ -f "$storage_file" ]] && ! grep -q "publicNetworkAccess" "$storage_file"; then
        python3 - "$storage_file" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text()
needle = "minimumTlsVersion: 'TLS1_2'\n    "
replacement = "minimumTlsVersion: 'TLS1_2'\n    publicNetworkAccess: 'Disabled'\n    "
if needle in text:
    text = text.replace(needle, replacement, 1)
else:
    text = text.replace("properties: {\n", "properties: {\n    publicNetworkAccess: 'Disabled'\n", 1)
path.write_text(text)
PY
      fi
    done

    if [[ -f "$OUT_DIR/eventhub/eventhub.bicep" ]] && ! grep -q "publicNetworkAccess" "$OUT_DIR/eventhub/eventhub.bicep"; then
      python3 - "$OUT_DIR/eventhub/eventhub.bicep" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text()
needle = "disableLocalAuth: true\n  }"
replacement = "disableLocalAuth: true\n    publicNetworkAccess: 'Disabled'\n  }"
if needle in text:
    text = text.replace(needle, replacement, 1)
else:
    text = text.replace("properties: {\n", "properties: {\n    publicNetworkAccess: 'Disabled'\n", 1)
path.write_text(text)
PY
    fi

    if [[ -f "$OUT_DIR/aiSearch/aiSearch.bicep" ]] && ! grep -q "publicNetworkAccess" "$OUT_DIR/aiSearch/aiSearch.bicep"; then
      python3 - "$OUT_DIR/aiSearch/aiSearch.bicep" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text()
needle = "disableLocalAuth: true\n    partitionCount: 1"
replacement = "disableLocalAuth: true\n    publicNetworkAccess: 'disabled'\n    partitionCount: 1"
if needle in text:
    text = text.replace(needle, replacement, 1)
else:
    text = text.replace("properties: {\n", "properties: {\n    publicNetworkAccess: 'disabled'\n", 1)
path.write_text(text)
PY
    fi
  else
    echo "[aspire-fix] DEPLOYMENT_MODEL=$deploy_model - retaining default public networking"
  fi
fi

echo "[aspire-fix] Aspire output fixes complete"
