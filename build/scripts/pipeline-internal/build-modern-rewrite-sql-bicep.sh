#!/usr/bin/env bash
set -euo pipefail

# Rewrite SQL Bicep templates to use workload identity as admin directly
# This eliminates deployment scripts and storage account policy issues
# Usage: build/scripts/build-modern-rewrite-sql-bicep.sh <publish-output-dir>

# Enable verbose output only if DEBUG is set
if [[ "${DEBUG:-}" == "true" ]]; then set -x; fi

OUT_DIR=${1:-out/publish}

echo ""
echo "[modern-sql-rewrite] Rewriting SQL Bicep templates to use workload identity as admin"
echo "[modern-sql-rewrite] Output directory: $OUT_DIR"

# Find SQL server Bicep files
SQL_SERVER_FILES=$(find "$OUT_DIR" -name "*sql*.bicep" -path "*/sql*" -o -name "sqldocgen.bicep" 2>/dev/null || true)

if [[ -z "$SQL_SERVER_FILES" ]]; then
    echo "[modern-sql-rewrite] No SQL server Bicep files found"
    exit 0
fi

echo "[modern-sql-rewrite] Found SQL server files to rewrite:"
echo "$SQL_SERVER_FILES"

for sql_file in $SQL_SERVER_FILES; do
    if [[ "$(basename "$sql_file")" == "sqldocgen.bicep" ]]; then
        echo "[modern-sql-rewrite] Rewriting SQL server template: $sql_file"

        # Determine publicNetworkAccess setting based on DEPLOYMENT_MODEL
        DEPLOYMENT_MODEL_LOWER=$(echo "${DEPLOYMENT_MODEL:-public}" | tr '[:upper:]' '[:lower:]')
        if [[ "$DEPLOYMENT_MODEL_LOWER" == "private" ]] || [[ "$DEPLOYMENT_MODEL_LOWER" == "hybrid" ]]; then
            PUBLIC_NETWORK_ACCESS="Disabled"
            echo "[modern-sql-rewrite] Setting publicNetworkAccess to Disabled for $DEPLOYMENT_MODEL_LOWER deployment"
        else
            PUBLIC_NETWORK_ACCESS="Enabled"
            echo "[modern-sql-rewrite] Setting publicNetworkAccess to Enabled for $DEPLOYMENT_MODEL_LOWER deployment"
        fi

        # Create the new SQL server template that uses workload identity directly as Azure AD admin
        cat > "$sql_file" <<EOF
@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

@description('The principal ID of the workload identity to set as SQL Server admin')
param principalId string

@description('The principal name of the workload identity to set as SQL Server admin')
param principalName string

var publicNetworkAccess = '$PUBLIC_NETWORK_ACCESS'

resource sqldocgen 'Microsoft.Sql/servers@2023-08-01' = {
  name: take('sqldocgen-\${uniqueString(resourceGroup().id)}', 63)
  location: location
  properties: {
    administrators: {
      login: principalName
      sid: principalId
      tenantId: subscription().tenantId
      principalType: 'Application'
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: publicNetworkAccess
    version: '12.0'
  }
  tags: {
    'aspire-resource-name': 'sqldocgen'
  }
}

resource sqlFirewallRule_AllowAllAzureIps 'Microsoft.Sql/servers/firewallRules@2023-08-01' = if (publicNetworkAccess == 'Enabled') {
  name: 'AllowAllAzureIps'
  properties: {
    endIpAddress: '0.0.0.0'
    startIpAddress: '0.0.0.0'
  }
  parent: sqldocgen
}

resource ProjectVicoDB 'Microsoft.Sql/servers/databases@2023-08-01' = {
  name: 'ProjectVicoDB'
  location: location
  parent: sqldocgen
}

output sqlServerFqdn string = sqldocgen.properties.fullyQualifiedDomainName
output name string = sqldocgen.name
output sqlServerAdminName string = principalName
output resourceId string = sqldocgen.id
EOF

        echo "[modern-sql-rewrite] Rewrote SQL server template to use workload identity as admin"
    fi
done

# Find and remove SQL roles files that contain deployment scripts
SQL_ROLES_FILES=$(find "$OUT_DIR" -name "*sql*-roles.bicep" -o -name "sqldocgen-roles" -type d 2>/dev/null || true)

if [[ -n "$SQL_ROLES_FILES" ]]; then
    echo "[modern-sql-rewrite] Removing SQL roles files with deployment scripts:"
    echo "$SQL_ROLES_FILES"

    for roles_item in $SQL_ROLES_FILES; do
        if [[ -d "$roles_item" ]]; then
            echo "[modern-sql-rewrite] Removing directory: $roles_item"
            rm -rf "$roles_item"
        elif [[ -f "$roles_item" ]]; then
            echo "[modern-sql-rewrite] Removing file: $roles_item"
            rm -f "$roles_item"
        fi
    done
else
    echo "[modern-sql-rewrite] No SQL roles files found"
fi

# Update main.bicep to remove references to the SQL roles module
MAIN_BICEP="$OUT_DIR/main.bicep"
if [[ -f "$MAIN_BICEP" ]]; then
    echo "[modern-sql-rewrite] Updating main.bicep to remove SQL roles module reference"

    # Remove the sqldocgen-roles module call entirely and fix parameter passing
    echo "[modern-sql-rewrite] Removing sqldocgen-roles module references from main.bicep"

    # Create a backup
    cp "$MAIN_BICEP" "${MAIN_BICEP}.backup" 2>/dev/null || true

    # Ensure principalName parameter exists before we add it to module calls
    if ! grep -q "param principalName string" "$MAIN_BICEP"; then
        echo "[modern-sql-rewrite] Adding principalName parameter to main.bicep"
        sed -i "/param principalId string/a\\\\nparam principalName string" "$MAIN_BICEP"
    fi

    # Use a comprehensive Python script to fix main.bicep
    cat > /tmp/fix_main_bicep.py << 'PYTHON_EOF'
import sys
import re

def fix_main_bicep(file_path):
    with open(file_path, 'r') as f:
        content = f.read()

    # First check if sqldocgen module already has the parameters we need
    # Look for sqldocgen module block and check if it has the parameters
    sqldocgen_match = re.search(r'module sqldocgen[^{]*\{[^}]*params:\s*\{[^}]*\}[^}]*\}', content, re.DOTALL)
    sqldocgen_has_principal_id = sqldocgen_match and 'principalId:' in sqldocgen_match.group(0)
    sqldocgen_has_principal_name = sqldocgen_match and 'principalName:' in sqldocgen_match.group(0)

    # If both parameters already exist, we just need to remove sqldocgen-roles references
    if sqldocgen_has_principal_id and sqldocgen_has_principal_name:
        # Remove sqldocgen-roles module references
        content = re.sub(r'module\s+[^{]*sqldocgen-roles[^{]*\{[^}]*(?:\{[^}]*\}[^}]*)*\}', '', content, flags=re.DOTALL)
        content = re.sub(r'.*sqldocgen-roles.*\n', '', content)
        with open(file_path, 'w') as f:
            f.write(content)
        return

    # Otherwise, do the full processing
    lines = content.splitlines(True)
    output_lines = []
    in_sqldocgen_roles_module = False
    in_sqldocgen_module = False
    sqldocgen_module_depth = 0
    sqldocgen_params_section = False

    i = 0
    while i < len(lines):
        line = lines[i]

        # Skip sqldocgen-roles module blocks entirely
        if 'sqldocgen-roles' in line and 'module' in line:
            in_sqldocgen_roles_module = True
            brace_count = line.count('{') - line.count('}')
            i += 1

            # Skip until we close the module block
            while i < len(lines) and (in_sqldocgen_roles_module or brace_count > 0):
                line = lines[i]
                brace_count += line.count('{') - line.count('}')
                if brace_count <= 0:
                    in_sqldocgen_roles_module = False
                i += 1
            continue

        # Handle sqldocgen module to add required parameters
        if 'module sqldocgen' in line and not in_sqldocgen_module:
            in_sqldocgen_module = True
            sqldocgen_module_depth = line.count('{') - line.count('}')  # Start counting from first line
            output_lines.append(line)
        elif in_sqldocgen_module:
            # Update depth first
            sqldocgen_module_depth += line.count('{') - line.count('}')

            if 'params:' in line and '{' in line:
                sqldocgen_params_section = True
                output_lines.append(line)
            elif sqldocgen_params_section and 'location:' in line:
                # Add location line
                output_lines.append(line)
                # Add the required parameters after location (only if not already present)
                if not sqldocgen_has_principal_id:
                    indent = '    '  # Match existing indentation
                    output_lines.append(f'{indent}principalId: principalId\n')
                if not sqldocgen_has_principal_name:
                    indent = '    '  # Match existing indentation
                    output_lines.append(f'{indent}principalName: principalName\n')
            elif sqldocgen_params_section and '}' in line and sqldocgen_module_depth > 1:
                # End of params section (but not module)
                sqldocgen_params_section = False
                output_lines.append(line)
            else:
                # Regular line in module
                output_lines.append(line)

            # Check if module is complete
            if sqldocgen_module_depth <= 0:
                in_sqldocgen_module = False
        else:
            # Regular line - just copy it
            output_lines.append(line)

        i += 1

    # Write the fixed content
    with open(file_path, 'w') as f:
        f.writelines(output_lines)

if __name__ == "__main__":
    fix_main_bicep(sys.argv[1])
PYTHON_EOF

    python3 /tmp/fix_main_bicep.py "$MAIN_BICEP"
    rm -f /tmp/fix_main_bicep.py

    echo "[modern-sql-rewrite] Updated main.bicep"
fi

echo "[modern-sql-rewrite] SQL Bicep template rewriting complete"
echo "[modern-sql-rewrite] The workload identity is now set as SQL Server admin directly"
echo "[modern-sql-rewrite] No deployment scripts or storage accounts required"
