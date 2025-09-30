#!/usr/bin/env bash
set -euo pipefail

# Fix ConfigMap/Secret separation for connection strings
# This script removes connection strings from ConfigMaps and ensures they're only in Secrets
# Usage: build/scripts/build-modern-fix-configmap-secrets.sh <publish-output-dir>

OUT_DIR=${1:-out/publish}

echo "[fix-secrets] Cleaning connection strings from ConfigMaps and moving to Secrets"
echo "[fix-secrets] Output directory: $OUT_DIR"

# Function to update deployment templates to use secretRef
update_deployments_for_secrets() {
    local template_dir="$OUT_DIR/templates"

    if [[ ! -d "$template_dir" ]]; then
        echo "[fix-secrets] No templates directory found, skipping"
        return 0
    fi

    echo "[fix-secrets] Updating deployment templates to use secretRef for connection strings..."

    # Process each deployment.yaml file
    find "$template_dir" -name "*.yaml" -type f | while read -r file; do
        # Check if this is a deployment file with env sections
        if ! grep -q "kind: Deployment" "$file" 2>/dev/null; then
            continue
        fi

        local service_name=$(basename "$file" .yaml | sed 's/-deployment//')
        echo "[fix-secrets] Processing deployment: $service_name"

        # Create a temporary file for modifications
        local temp_file="${file}.tmp"
        local in_env_section=false
        local found_connection_strings=false
        local added_secret_ref=false

        # Process the file line by line
        while IFS= read -r line; do
            # Detect env: section
            if [[ "$line" =~ ^[[:space:]]*env: ]]; then
                echo "$line" >> "$temp_file"
                in_env_section=true
                continue
            fi

            # Detect end of env section (when indentation decreases)
            if [[ "$in_env_section == true" ]] && [[ "$line" =~ ^[[:space:]]{0,10}[^[:space:]-] ]]; then
                # Before closing env section, add envFrom if we found connection strings
                if [[ "$found_connection_strings" == "true" ]] && [[ "$added_secret_ref" == "false" ]]; then
                    # Calculate proper indentation (should be at same level as 'env:')
                    local indent=$(echo "$line" | sed 's/[^[:space:]].*//')
                    echo "${indent}envFrom:" >> "$temp_file"
                    echo "${indent}  - secretRef:" >> "$temp_file"
                    echo "${indent}      name: greenlight-connection-strings" >> "$temp_file"
                    echo "${indent}      optional: true" >> "$temp_file"
                    added_secret_ref=true
                fi
                in_env_section=false
            fi

            # Skip ConnectionStrings__ entries in env section
            if [[ "$in_env_section" == "true" ]] && [[ "$line" =~ ConnectionStrings__ ]]; then
                found_connection_strings=true
                echo "[fix-secrets]   Removing connection string from ConfigMap: $(echo "$line" | sed 's/:.*//')"
                # Skip this line and the next line if it's the value continuation
                continue
            fi

            # Write all other lines
            echo "$line" >> "$temp_file"

        done < "$file"

        # Handle case where env section is at end of container spec
        if [[ "$found_connection_strings" == "true" ]] && [[ "$added_secret_ref" == "false" ]]; then
            # Add envFrom at the end
            echo "          envFrom:" >> "$temp_file"
            echo "            - secretRef:" >> "$temp_file"
            echo "                name: greenlight-connection-strings" >> "$temp_file"
            echo "                optional: true" >> "$temp_file"
        fi

        # Replace original file if changes were made
        if [[ "$found_connection_strings" == "true" ]]; then
            mv "$temp_file" "$file"
            echo "[fix-secrets]   Updated $file - removed connection strings from ConfigMap, added secretRef"
        else
            rm "$temp_file"
        fi
    done
}

# Function to clean connection strings from values.yaml
clean_values_yaml() {
    local values_file="$OUT_DIR/values.yaml"

    if [[ ! -f "$values_file" ]]; then
        echo "[fix-secrets] No values.yaml found, skipping"
        return 0
    fi

    echo "[fix-secrets] NOTE: Keeping connection strings in values.yaml for Helm deployment to process"
    echo "[fix-secrets]   Connection strings will be extracted to secrets during Helm deployment"
    # DO NOT remove connection strings from values.yaml - they're needed by the Helm deployment script
    # to replace placeholders and create the secret. They should only be removed from the
    # generated ConfigMaps in the templates, not from the source values.
}

# Function to ensure connection strings secret is properly referenced
add_secret_to_chart() {
    local chart_file="$OUT_DIR/Chart.yaml"

    if [[ ! -f "$chart_file" ]]; then
        echo "[fix-secrets] No Chart.yaml found, skipping"
        return 0
    fi

    # Create a secret template if it doesn't exist
    local secret_template="$OUT_DIR/templates/connection-strings-secret.yaml"

    if [[ ! -f "$secret_template" ]]; then
        echo "[fix-secrets] Creating connection strings secret template..."
        cat > "$secret_template" <<'EOF'
apiVersion: v1
kind: Secret
metadata:
  name: greenlight-connection-strings
  namespace: {{ .Release.Namespace }}
type: Opaque
stringData:
  {{- range $key, $value := .Values.secrets.connectionStrings }}
  {{ $key }}: {{ $value | quote }}
  {{- end }}
EOF
        echo "[fix-secrets]   Created secret template at $secret_template"
    fi
}

# Main execution
echo "[fix-secrets] Starting ConfigMap to Secret migration..."

# Step 1: Clean values.yaml
clean_values_yaml

# Step 2: Update deployment templates
update_deployments_for_secrets

# Step 3: Add secret template to chart
add_secret_to_chart

echo "[fix-secrets] ConfigMap to Secret migration complete"
echo "[fix-secrets] Connection strings will now be stored in 'greenlight-connection-strings' Secret"
echo "[fix-secrets] Deployments updated to use envFrom with secretRef"