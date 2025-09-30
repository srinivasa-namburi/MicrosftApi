#!/usr/bin/env bash
set -euo pipefail

# Comprehensive post-publish fixes for Aspire-generated output
# Combines multiple fix operations that were previously separate scripts
# Usage: build/scripts/build-modern-post-publish-fixes.sh <publish-output-dir> [fix-stage]
#   fix-stage: "publish" (default) or "deploy"
#     - publish: Runs naming patches and Helm template fixes
#     - deploy: Runs principal type, subscription scope, and role alignment fixes

# Enable verbose output only if DEBUG is set
if [[ "${DEBUG:-}" == "true" ]]; then
  set -x
fi

OUT_DIR=${1:-out/publish}
FIX_STAGE=${2:-publish}

echo ""
echo "[post-fix] Running post-publish fixes for stage: $FIX_STAGE"
echo "[post-fix] Output directory: $OUT_DIR"

# Function to patch Azure resource names for compatibility
patch_azure_names() {
    echo "[post-fix] Applying Azure resource naming conventions"

    # Check if any Bicep files exist
    if ! find "$OUT_DIR" -name "*.bicep" -o -name "*.json" | grep -q .; then
        echo "[post-fix] No Bicep/ARM templates found - skipping Azure name patching"
        return 0
    fi

    # Legacy names patching disabled for consistent Aspire naming across all deployments
    # local NAMES_SCRIPT="$(dirname "${BASH_SOURCE%/*}")/build-modern-patch-azure-names.sh"
    # if [[ -f "$NAMES_SCRIPT" ]]; then
    #     echo "[post-fix] Running Azure names patching script"
    #     bash "$NAMES_SCRIPT" "$OUT_DIR"
    # else
    #     echo "[post-fix] WARNING: Azure names patching script not found at $NAMES_SCRIPT"
    # fi
    echo "[post-fix] Legacy Azure names patching disabled - using consistent Aspire naming"
}

# Function to fix port values to be integers (removing quotes from template values)
fix_port_types() {
    echo "[post-fix] Fixing port types to be integers for Helm compatibility"

    # Fix service and deployment port values to be integers
    find "$OUT_DIR/templates" -name "service.yaml" -o -name "deployment.yaml" 2>/dev/null | while read -r file; do
        # Remove quotes around port template values to make them integers
        sed -E -i 's/(port:[[:space:]]*)"(\{\{[^}]+\}\})"/\1\2/g' "$file"
        sed -E -i 's/(targetPort:[[:space:]]*)"(\{\{[^}]+\}\})"/\1\2/g' "$file"
        sed -E -i 's/(containerPort:[[:space:]]*)"(\{\{[^}]+\}\})"/\1\2/g' "$file"
        echo "[post-fix] Fixed port types in: $file"
    done

    # Fix values.yaml port values if needed
    if [[ -f "$OUT_DIR/values.yaml" ]]; then
        sed -E -i 's/(port_[^:]+:[[:space:]]*)"([0-9]+)"/\1\2/g' "$OUT_DIR/values.yaml"
        # Fix ASPNETCORE_URLS that have $8080 instead of 8080
        sed -i 's/ASPNETCORE_URLS: "http:\/\/+:\$8080"/ASPNETCORE_URLS: "http:\/\/+:8080"/g' "$OUT_DIR/values.yaml"
        sed -i 's/ASPNETCORE_URLS: http:\/\/+:\$8080/ASPNETCORE_URLS: "http:\/\/+:8080"/g' "$OUT_DIR/values.yaml"
        # Also fix escaped quotes in SQL connection strings that Aspire generates
        sed -i 's/Authentication=\\"Active Directory Default\\"/Authentication=Active Directory Default/g' "$OUT_DIR/values.yaml"
        echo "[post-fix] Fixed ASPNETCORE_URLS port references and SQL authentication quotes"
    fi
}

# Function to fix Helm template YAML issues
fix_helm_templates() {
    echo "[post-fix] Fixing Helm template YAML issues"

    # Check if templates directory exists
    if [[ ! -d "$OUT_DIR/templates" ]]; then
        echo "[post-fix] No templates directory found - skipping Helm fixes"
        return 0
    fi

    # Check if Python is available
    local PYTHON_CMD=""
    if command -v python3 &> /dev/null; then
        PYTHON_CMD="python3"
    elif command -v python &> /dev/null; then
        # Check if it's Python 3
        if python --version 2>&1 | grep -q "Python 3"; then
            PYTHON_CMD="python"
        fi
    fi

    if [[ -n "$PYTHON_CMD" ]]; then
        echo "[post-fix] Using Python for Helm template fixes"

        # Create temporary Python script
        local PYTHON_SCRIPT=$(mktemp /tmp/fix-helm.XXXXXX.py)
        cat > "$PYTHON_SCRIPT" << 'PYTHON_EOF'
#!/usr/bin/env python3
import os, sys, re
from pathlib import Path

def fix_config_yaml(file_path):
    with open(file_path, 'r') as f:
        lines = f.readlines()

    fixed_lines = []
    in_data = False

    for line in lines:
        if line.strip() == 'data:':
            in_data = True
            fixed_lines.append(line)
            continue

        if in_data and line and line[0] not in ' \t':
            in_data = False

        if in_data and ':' in line:
            match = re.match(r'^(\s*)([^:]+):\s*(.*)$', line)
            if match:
                indent, key, value = match.groups()

                # Quote keys with hyphens
                if '-' in key and not (key.startswith('"') and key.endswith('"')):
                    key = f'"{key}"'

                # Fix template references with hyphens
                if '{{' in value and '-' in value:
                    pattern = r'\{\{\s*\.Values\.config\.([^}]+)\.([^}]*-[^}]*)\s*\}\}'
                    value = re.sub(pattern, r'{{ index .Values.config.\1 "\2" }}', value)

                    if 'index .Values.config' in value and value.startswith('"'):
                        value = "'" + value[1:-1] + "'"

                fixed_lines.append(f'{indent}{key}: {value}\n')
            else:
                fixed_lines.append(line)
        else:
            fixed_lines.append(line)

    with open(file_path, 'w') as f:
        f.writelines(fixed_lines)

def fix_values_yaml(file_path):
    with open(file_path, 'r') as f:
        content = f.read()

    # Fix escaped quotes in connection strings
    content = re.sub(r'(Authentication=)\\"([^"]+)\\"', r"\1'\2'", content)

    with open(file_path, 'w') as f:
        f.write(content)

output_dir = Path(sys.argv[1])
values_file = output_dir / 'values.yaml'
if values_file.exists():
    fix_values_yaml(values_file)

templates_dir = output_dir / 'templates'
if templates_dir.exists():
    for config_file in templates_dir.glob('*/config.yaml'):
        fix_config_yaml(config_file)
        print(f"[post-fix] Fixed: {config_file}")
PYTHON_EOF

        $PYTHON_CMD "$PYTHON_SCRIPT" "$OUT_DIR" 2>/dev/null || {
            echo "[post-fix] Python failed, using bash fallback"
            fix_helm_templates_bash
        }
        rm -f "$PYTHON_SCRIPT"
    else
        fix_helm_templates_bash
    fi
}

# Bash fallback for Helm template fixes
fix_helm_templates_bash() {
    echo "[post-fix] Using bash/sed fallback for Helm fixes"

    # Fix values.yaml
    if [[ -f "$OUT_DIR/values.yaml" ]]; then
        sed -i.bak 's/Authentication=\\"/Authentication='\''/g; s/\\";Database=/'\'';\Database=/g' "$OUT_DIR/values.yaml"
        rm -f "$OUT_DIR/values.yaml.bak"
    fi

    # Fix config.yaml files
    find "$OUT_DIR/templates" -name "config.yaml" 2>/dev/null | while read -r file; do
        # Quote keys with hyphens and fix template references
        awk '
            BEGIN { in_data = 0 }
            /^data:/ { in_data = 1; print; next }
            /^[a-zA-Z]/ && in_data { in_data = 0 }
            in_data && /^  [^" ][^:]*-[^:]*:/ {
                match($0, /^(  )([^:]+)(:.*$)/, parts)
                if (parts[2] && parts[2] ~ /-/) {
                    print parts[1] "\"" parts[2] "\"" parts[3]
                } else {
                    print
                }
                next
            }
            { print }
        ' "$file" > "$file.tmp"

        # Fix template references
        sed -E -i 's/\{\{[[:space:]]*\.Values\.config\.([^}]*)\.(([^}[:space:]]*-[^}[:space:]]*)+)[[:space:]]*\}\}/{{ index .Values.config.\1 "\2" }}/g' "$file.tmp"
        sed -E -i 's/^([[:space:]]*"?[^":]+:"?[[:space:]]*)"(\{\{ index .* ".+" \}\})"$/\1'\''\2'\''/g' "$file.tmp"

        mv "$file.tmp" "$file"
        echo "[post-fix] Fixed: $file"
    done
}

# Function to fix missing principalType in role assignments
fix_principal_type() {
    echo "[post-fix] Fixing missing principalType in role assignments"

    local SCRIPT="${BASH_SOURCE%/*}/build-modern-fix-principal-type.sh"
    if [[ -f "$SCRIPT" ]]; then
        bash "$SCRIPT" "$OUT_DIR"
    else
        echo "[post-fix] WARNING: Principal type fix script not found"
    fi
}

# Function to fix subscription scope issues
fix_subscription_scope() {
    echo "[post-fix] Fixing subscription-scoped Bicep templates"

    local SCRIPT="${BASH_SOURCE%/*}/build-modern-fix-subscription-scope.sh"
    if [[ -f "$SCRIPT" ]]; then
        bash "$SCRIPT" "$OUT_DIR"
    else
        echo "[post-fix] WARNING: Subscription scope fix script not found"
    fi
}

# Function to add CostControl tags if COSTCONTROL_IGNORE is true
add_costcontrol_tags() {
    echo "[post-fix] Checking if CostControl tags should be added"

    if [[ "${COSTCONTROL_IGNORE:-false}" != "true" ]]; then
        echo "[post-fix] COSTCONTROL_IGNORE is not true, skipping CostControl tags"
        return 0
    fi

    # Safety change: Do NOT mutate Bicep anymore. Tagging will be applied post-deployment
    # using Azure CLI with merge semantics to avoid duplicate 'tags' blocks and unsupported
    # properties. See: az tag update --operation Merge (MS Docs).
    echo "[post-fix] Deferring CostControl tagging to post-deployment (Azure CLI merge)."
    # Emit a marker file so deploy script knows to apply tags post-deploy
    echo "COSTCONTROL_IGNORE=true" > "$OUT_DIR/.apply_costcontrol_tags_post_deploy"
}

# Function to add SecurityControl tags and enable storage network access if SECURITYCONTROL_IGNORE is true
add_securitycontrol_overrides() {
    if [[ "${SECURITYCONTROL_IGNORE:-false}" != "true" ]]; then
        return 0
    fi

    # Emit a marker file so deploy script knows to apply storage network overrides post-deploy
    echo "SECURITYCONTROL_IGNORE=true" > "$OUT_DIR/.apply_securitycontrol_overrides_post_deploy"
}


# Function to expose module outputs in main.bicep
expose_module_outputs() {
    echo "[post-fix] Exposing module outputs in main.bicep"

    local MAIN_BICEP="$OUT_DIR/main.bicep"
    if [[ ! -f "$MAIN_BICEP" ]]; then
        echo "[post-fix] No main.bicep found - skipping output exposure"
        return 0
    fi

    # Check if outputs are already added
    if grep -q "output sqlServerFqdn" "$MAIN_BICEP"; then
        echo "[post-fix] Outputs already exposed in main.bicep"
        return 0
    fi

    # Add outputs at the end of main.bicep
    cat >> "$MAIN_BICEP" << 'EOF'

// Expose module outputs for Helm deployment
output sqlServerFqdn string = sqldocgen.outputs.sqlServerFqdn
// Redis is now containerized - no Azure Managed Redis outputs
// SignalR is now self-hosted with Redis backplane - no Azure SignalR outputs
output aiSearchConnectionString string = aiSearch.outputs.connectionString
output docingBlobEndpoint string = docing.outputs.blobEndpoint
output docingTableEndpoint string = docing.outputs.tableEndpoint
output docingQueueEndpoint string = docing.outputs.queueEndpoint
output orleansStorageBlobEndpoint string = orleans_storage.outputs.blobEndpoint
output orleansStorageTableEndpoint string = orleans_storage.outputs.tableEndpoint
output orleansStorageQueueEndpoint string = orleans_storage.outputs.queueEndpoint
output appInsightsConnectionString string = insights.outputs.appInsightsConnectionString
output eventHubsEndpoint string = eventhub.outputs.eventHubsEndpoint
EOF

    echo "[post-fix] Added module outputs to main.bicep"
}

# Function to align role principals
align_role_principals() {
    echo "[post-fix] Aligning role assignment principals"

    local SCRIPT="${BASH_SOURCE%/*}/build-modern-align-role-principals.sh"
    if [[ -f "$SCRIPT" ]]; then
        WORKLOAD_IDENTITY_PRINCIPAL_ID="${WORKLOAD_IDENTITY_PRINCIPAL_ID:-}" \
        WORKLOAD_IDENTITY_NAME="${WORKLOAD_IDENTITY_NAME:-}" \
        bash "$SCRIPT" "$OUT_DIR"
    else
        echo "[post-fix] WARNING: Role alignment script not found"
    fi
}

# Function to fix ConfigMap/Secret separation
fix_configmap_secrets() {
    echo "[post-fix] Fixing ConfigMap/Secret separation for connection strings"

    local SCRIPT="${BASH_SOURCE%/*}/build-modern-fix-configmap-secrets.sh"
    if [[ -f "$SCRIPT" ]]; then
        bash "$SCRIPT" "$OUT_DIR"
    else
        echo "[post-fix] WARNING: ConfigMap/Secret fix script not found at $SCRIPT"
    fi
}

# Function to rewrite SQL Bicep templates to use workload identity as admin
rewrite_sql_bicep() {
    echo "[post-fix] Rewriting SQL Bicep templates to use workload identity as admin directly"
    local SCRIPT="${BASH_SOURCE%/*}/build-modern-rewrite-sql-bicep.sh"
    if [[ -f "$SCRIPT" ]]; then
        bash "$SCRIPT" "$OUT_DIR"
    else
        echo "[post-fix] WARNING: SQL Bicep rewrite script not found at $SCRIPT"
    fi
}

# Main execution based on stage
case "$FIX_STAGE" in
    publish)
        echo "[post-fix] Running publish stage fixes"
        patch_azure_names
        fix_helm_templates
        fix_port_types
        expose_module_outputs
        fix_configmap_secrets  # Add ConfigMap/Secret cleanup
        ;;
    deploy)
        echo "[post-fix] Running deploy stage fixes"
        add_costcontrol_tags
        add_securitycontrol_overrides
        fix_principal_type
        fix_subscription_scope
        rewrite_sql_bicep
        align_role_principals
        ;;
    all)
        echo "[post-fix] Running all fixes"
        patch_azure_names
        fix_helm_templates
        fix_port_types
        fix_principal_type
        fix_subscription_scope
        rewrite_sql_bicep
        align_role_principals
        fix_configmap_secrets  # Add ConfigMap/Secret cleanup
        ;;
    *)
        echo "[post-fix] ERROR: Unknown stage: $FIX_STAGE"
        echo "[post-fix] Valid stages: publish, deploy, all"
        exit 1
        ;;
esac

echo "[post-fix] Completed post-publish fixes for stage: $FIX_STAGE"
