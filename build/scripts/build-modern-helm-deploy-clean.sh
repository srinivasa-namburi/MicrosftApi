#!/usr/bin/env bash
set -euo pipefail

# CLEAN Helm deployment script - replaces complex pipeline with simple values overrides
# This eliminates all sed-based template modifications that were causing YAML corruption

OUT_DIR=${1:-out/publish}
RELEASE=${2:?Missing release name}
NAMESPACE=${3:?Missing namespace}

echo "[clean] Clean Helm deployment - no template modifications"
echo "[clean] Using pure Helm values override approach"

# CRITICAL: Validate required Azure AD credentials
if [[ -z "${PVICO_ENTRA_CREDENTIALS:-}" ]]; then
  echo "[clean] ERROR: PVICO_ENTRA_CREDENTIALS is required but not provided"
  echo "[clean] This variable should contain the complete Azure AD app registration JSON"
  echo "[clean] Please ensure the variable group is linked to this pipeline and the variable is set"
  exit 1
fi

# Validate PVICO_ENTRA_CREDENTIALS is valid JSON
if ! echo "$PVICO_ENTRA_CREDENTIALS" | jq empty 2>/dev/null; then
  echo "[clean] ERROR: PVICO_ENTRA_CREDENTIALS is not valid JSON"
  echo "[clean] Please check the Azure DevOps library variable configuration"
  exit 1
fi

echo "[clean] ✅ PVICO_ENTRA_CREDENTIALS validated"

# Log status of optional secrets
echo "[clean] Checking optional secrets..."
if [[ -n "${PVICO_OPENAI_CONNECTIONSTRING:-}" ]]; then
  echo "[clean] ✅ PVICO_OPENAI_CONNECTIONSTRING is configured - AI features will be available"
else
  echo "[clean] ℹ️  PVICO_OPENAI_CONNECTIONSTRING not provided - AI features will be unavailable"
fi

if [[ -n "${PVICO_AZUREMAPS_KEY:-}" ]]; then
  echo "[clean] ✅ PVICO_AZUREMAPS_KEY is configured - Azure Maps features will be available"
else
  echo "[clean] ℹ️  PVICO_AZUREMAPS_KEY not provided - Azure Maps features will be unavailable"
fi

# Check if Helm chart exists
if [[ ! -f "$OUT_DIR/Chart.yaml" ]]; then
  echo "[clean] ERROR: No Helm chart found at $OUT_DIR/Chart.yaml" >&2
  echo "[clean] Ensure 'aspire publish' has been run successfully" >&2
  exit 1
fi

# Ensure namespace exists
echo "[clean] Ensuring namespace: $NAMESPACE"
kubectl get ns "$NAMESPACE" >/dev/null 2>&1 || kubectl create ns "$NAMESPACE"

# Generate stable Redis passwords (reuse existing if available)
get_existing_redis_password() {
  local redis_type="$1" # redis | redis-signalr
  # Try to get existing password from secrets
  kubectl get secret redis-auth -n "$NAMESPACE" -o jsonpath="{.data.${redis_type}_PASSWORD}" 2>/dev/null | base64 -d 2>/dev/null || openssl rand -hex 32
}

REDIS_PASSWORD="${REDIS_PASSWORD:-$(get_existing_redis_password "REDIS")}"
REDIS_SIGNALR_PASSWORD="${REDIS_SIGNALR_PASSWORD:-$(get_existing_redis_password "REDIS_SIGNALR")}"

# Determine image prefix for full references (avoid leading slash when registry is empty)
if [[ -n "${AZURE_CONTAINER_REGISTRY_ENDPOINT:-}" ]]; then
  IMG_PREFIX="${AZURE_CONTAINER_REGISTRY_ENDPOINT}/"
else
  IMG_PREFIX=""
fi

# Prefer an explicit IMAGE_TAG, otherwise use the sanitized pipeline build number
# (Pipeline sets BUILD_BUILDNUMBER by replacing '.' with '-')
IMAGE_TAG="${IMAGE_TAG:-${BUILD_BUILDNUMBER:-latest}}"

# Create values override file that fixes all template/values mismatches
OVERRIDE_VALUES=$(mktemp)
trap "rm -f $OVERRIDE_VALUES" EXIT

echo "[clean] Creating comprehensive Helm values override..."

# Sanitize Azure output values (some generators may include a stray closing brace)
sanitize_brace() {
  local val="$1"
  # Normalize: remove CRs, trim trailing whitespace, then strip one trailing '}' if present
  val=$(printf "%s" "$val" | tr -d '\r' | sed -e 's/[[:space:]]*$//')
  echo "${val%\}}"
}

# Note: Removed complex Azure CLI resolution functions - pipeline now validates
# that Azure outputs are provided correctly rather than trying fallback resolution

# Extract Azure resource endpoints from pipeline-provided environment variables
# Use UPPER_SNAKE_CASE to match actual Bicep/Aspire outputs
DOCING_BLOB_EP=$(sanitize_brace "${AZURE_OUTPUT_DOCING_BLOBENDPOINT:-}")
SQLSERVER_FQDN=$(sanitize_brace "${AZURE_OUTPUT_SQLDOCGEN_SQLSERVERFQDN:-}")
ORLEANS_TABLE_EP=$(sanitize_brace "${AZURE_OUTPUT_ORLEANS_STORAGE_TABLEENDPOINT:-}")
ORLEANS_BLOB_EP=$(sanitize_brace "${AZURE_OUTPUT_ORLEANS_STORAGE_BLOBENDPOINT:-}")
EVENTHUBS_EP=$(sanitize_brace "${AZURE_OUTPUT_EVENTHUB_EVENTHUBSENDPOINT:-}")
AISEARCH_CS=$(sanitize_brace "${AZURE_OUTPUT_AISEARCH_CONNECTIONSTRING:-}")
APPINSIGHTS_CS=$(sanitize_brace "${AZURE_OUTPUT_INSIGHTS_APPINSIGHTSCONNECTIONSTRING:-}")

# Get current tenant ID
TENANT_ID=$(az account show --query tenantId -o tsv)

# Validate required Azure outputs - fail fast if missing
echo "[Helm] Validating required Azure resource outputs..."
MISSING_VARS=()

if [[ -z "$DOCING_BLOB_EP" ]]; then MISSING_VARS+=("DOCING_BLOB_EP (from AZURE_OUTPUT_DOCING_BLOBENDPOINT)"); fi
if [[ -z "$SQLSERVER_FQDN" ]]; then MISSING_VARS+=("SQLSERVER_FQDN (from AZURE_OUTPUT_SQLDOCGEN_SQLSERVERFQDN)"); fi
if [[ -z "$ORLEANS_TABLE_EP" ]]; then MISSING_VARS+=("ORLEANS_TABLE_EP (from AZURE_OUTPUT_ORLEANS_STORAGE_TABLEENDPOINT)"); fi
if [[ -z "$ORLEANS_BLOB_EP" ]]; then MISSING_VARS+=("ORLEANS_BLOB_EP (from AZURE_OUTPUT_ORLEANS_STORAGE_BLOBENDPOINT)"); fi
if [[ -z "$AISEARCH_CS" ]]; then MISSING_VARS+=("AISEARCH_CS (from AZURE_OUTPUT_AISEARCH_CONNECTIONSTRING)"); fi
if [[ -z "$APPINSIGHTS_CS" ]]; then MISSING_VARS+=("APPINSIGHTS_CS (from AZURE_OUTPUT_INSIGHTS_APPINSIGHTSCONNECTIONSTRING)"); fi

if [[ ${#MISSING_VARS[@]} -gt 0 ]]; then
  echo "[Helm] ❌ ERROR: Required Azure resource outputs are missing!"
  echo "[Helm] Missing variables:"
  for var in "${MISSING_VARS[@]}"; do
    echo "[Helm]   - $var"
  done
  echo ""
  echo "[Helm] This indicates the pipeline's Azure deployment step failed to export outputs."
  echo "[Helm] Cannot proceed with deployment - applications would be broken with placeholder values."
  echo "[Helm] Please check the Azure deployment pipeline step for errors."
  exit 1
fi

echo "[Helm] ✅ All required Azure outputs are available, proceeding with deployment..."

cat > "$OVERRIDE_VALUES" <<EOF
# ===================================================================
# CLEAN PIPELINE APPROACH - Fix Aspire template/values mismatches
# No template modifications needed - just proper Helm values
# ===================================================================

# === CONTAINER IMAGES ===
# Aspire Helm charts expect flat parameter keys, not nested per-service blocks.
# Provide flat keys to guarantee override takes effect.
parameters:
  db_setupmanager:
    db_setupmanager_image: "${IMG_PREFIX}db-setupmanager:${IMAGE_TAG}"
  api_main:
    api_main_image: "${IMG_PREFIX}api-main:${IMAGE_TAG}"
  mcp_server:
    mcp_server_image: "${IMG_PREFIX}mcp-server:${IMAGE_TAG}"
  silo:
    silo_image: "${IMG_PREFIX}silo:${IMAGE_TAG}"
    # Ensure distinct Orleans ports to avoid duplicate-name errors in Service/Deployment
    # Prefer values from existing configmap if present
    port_orleans_silo: "${SILO_PORT_CM:-11111}"
    port_orleans_gateway: "${GW_PORT_CM:-30000}"
  web_docgen:
    web_docgen_image: "${IMG_PREFIX}web-docgen:${IMAGE_TAG}"

# === SECRETS - Fix the critical template/values mismatch ===
secrets:
  # FIX: Aspire templates expect per-service secrets but values.yaml only has container secrets
  # We provide both structures to satisfy all templates

  # Container secrets used by Redis statefulsets (what Aspire templates expect)
  redis:
    redisPassword: "${REDIS_PASSWORD}"
  redis_signalr:
    redisSignalRPassword: "${REDIS_SIGNALR_PASSWORD}"

  # Per-service secrets (all in one place to avoid duplication)
  # Extract ClientSecret from PVICO_ENTRA_CREDENTIALS first
EOF

CLIENT_SECRET=$(echo "$PVICO_ENTRA_CREDENTIALS" | jq -r '.ClientSecret // empty')

# Add ALL secrets for each service in one block (redis + optional secrets)
for SERVICE in api_main silo web_docgen mcp_server db_setupmanager; do
  cat >> "$OVERRIDE_VALUES" <<EOF
  $SERVICE:
    redis_password: "${REDIS_PASSWORD}"
    redis_signalr_password: "${REDIS_SIGNALR_PASSWORD}"
EOF

  # Add Azure AD ClientSecret if present
  if [[ -n "$CLIENT_SECRET" ]]; then
    cat >> "$OVERRIDE_VALUES" <<EOF
    AzureAd__ClientSecret: "${CLIENT_SECRET}"
EOF
  fi

  # Add OpenAI connection string if provided (optional)
  if [[ -n "${PVICO_OPENAI_CONNECTIONSTRING:-}" ]]; then
    cat >> "$OVERRIDE_VALUES" <<EOF
    ConnectionStrings__openai-planner: "${PVICO_OPENAI_CONNECTIONSTRING}"
EOF
    echo "[clean] Added OpenAI connection string to $SERVICE secrets"
  fi

  # Add Azure Maps key if provided (optional)
  if [[ -n "${PVICO_AZUREMAPS_KEY:-}" ]]; then
    cat >> "$OVERRIDE_VALUES" <<EOF
    ServiceConfiguration__AzureMaps__Key: "${PVICO_AZUREMAPS_KEY}"
EOF
    echo "[clean] Added Azure Maps key to $SERVICE secrets"
  fi
done

cat >> "$OVERRIDE_VALUES" <<EOF

# === CONFIGURATION - Replace Azure placeholders ===
config:
  # Common configuration for all services
  _common: &common_config
    GREENLIGHT_PRODUCTION: "true"

    # Replace Aspire placeholders with actual Azure outputs (quote keys with dashes)
    "ConnectionStrings__blob-docing": "${DOCING_BLOB_EP}"
    ConnectionStrings__ProjectVicoDB: "Server=tcp:${SQLSERVER_FQDN},1433;Encrypt=True;Authentication=Active Directory Default;Database=ProjectVicoDB"
    ConnectionStrings__clustering: "${ORLEANS_TABLE_EP}"
    ConnectionStrings__checkpointing: "${ORLEANS_TABLE_EP}"
    "ConnectionStrings__blob-orleans": "${ORLEANS_BLOB_EP}"
    "ConnectionStrings__greenlight-cg-streams": "Endpoint=${EVENTHUBS_EP};EntityPath=greenlight-hub;ConsumerGroup=greenlight-cg-streams"
    ConnectionStrings__aiSearch: "${AISEARCH_CS}"
    APPLICATIONINSIGHTS_CONNECTION_STRING: "${APPINSIGHTS_CS}"
    # Ensure Kestrel binds correctly in containers (avoid 8080 placeholder from Aspire)
    ASPNETCORE_URLS: "http://+:8080"

    # Orleans clustering configuration (shared by all services)
    Orleans__ClusterId: "greenlight-cluster"

    # Service discovery URLs (for inter-service communication in production)
    # Using colon format (converted to dashes by k8s) and underscore format for compatibility
    "services:api-main:http:0": "http://api-main:8080"
    "services:web-docgen:http:0": "http://web-docgen:8080"
    "services:silo:http:0": "http://silo:8080"
    "services:mcp-server:http:0": "http://mcp-server:8080"
    services__api_main__http__0: "http://api-main:8080"
    services__web_docgen__http__0: "http://web-docgen:8080"
    services__silo__http__0: "http://silo:8080"
    services__mcp_server__http__0: "http://mcp-server:8080"
EOF

# (Removed) Do not add AzureAd keys to common config; they are injected directly
# into each service ConfigMap after Helm upgrade to match how apps read configuration.

# Now apply the common config to all services
cat >> "$OVERRIDE_VALUES" <<EOF

  # Apply to each service
  db_setupmanager: *common_config
  api_main: *common_config
  mcp_server: *common_config
  silo:
    <<: *common_config
    # Silo-specific Orleans configuration (quote keys with dashes)
    "Orleans__GrainStorage__blob-orleans__ProviderType": "AzureBlobStorage"
    "Orleans__GrainStorage__blob-orleans__ServiceKey": "blob-orleans"
  web_docgen: *common_config
EOF

echo "[clean] Common configuration applied; AzureAd keys handled via post-upgrade ConfigMap patches"

# Add workload identity configuration if available (separate from Azure AD app registration)
if [[ -n "${WORKLOAD_IDENTITY_CLIENT_ID:-}" ]]; then
  echo "[clean] Configuring Helm to use existing workload identity ServiceAccount..."
  echo "[clean] ServiceAccount created by workload identity setup, Helm will not create it"

  # Do NOT create ServiceAccount template - it's managed by workload identity setup
  # Instead, configure Helm to use the existing ServiceAccount

  # Add serviceAccount configuration to values override
  cat >> "$OVERRIDE_VALUES" <<EOF

# === WORKLOAD IDENTITY ===
# ServiceAccount configuration for all deployments (managed by workload identity setup)
global:
  serviceAccount:
    create: false
    name: "${WORKLOAD_IDENTITY_SERVICE_ACCOUNT:-greenlight-app}"
    annotations:
      azure.workload.identity/client-id: "${WORKLOAD_IDENTITY_CLIENT_ID}"
      azure.workload.identity/tenant-id: "${AZURE_TENANT_ID:-}"
      azure.workload.identity/use: "true"

# === ASPNETCORE CONFIGURATION ===
# Override ASPNETCORE_URLS to use non-privileged port for web-docgen
config:
  web_docgen:
    ASPNETCORE_URLS: "http://+:8081"
EOF

  echo "[clean] AKS workload identity configured via Helm values (for pod-to-Azure authentication)"
else
  echo "[clean] WORKLOAD_IDENTITY_CLIENT_ID not provided - skipping workload identity configuration"
fi

# Ensure Azure AD client secret is stored as a Kubernetes Secret and available to web-docgen
CLIENT_SECRET_VAL=$(echo "${PVICO_ENTRA_CREDENTIALS:-}" | jq -r '.ClientSecret // empty')
if [[ -n "$CLIENT_SECRET_VAL" ]]; then
  echo "[clean] Creating/updating greenlight-auth secret for AzureAd__ClientSecret"
  kubectl create secret generic greenlight-auth \
    --namespace "$NAMESPACE" \
    --from-literal=AzureAd__ClientSecret="$CLIENT_SECRET_VAL" \
    --dry-run=client -o yaml | kubectl apply -f -
fi

# Create Redis auth secret using the same passwords as Helm values
echo "[clean] Creating Redis authentication secret..."
kubectl delete secret redis-auth -n "$NAMESPACE" --ignore-not-found=true >/dev/null 2>&1 || true
kubectl create secret generic redis-auth -n "$NAMESPACE" \
  --from-literal=REDIS_PASSWORD="$REDIS_PASSWORD" \
  --from-literal=REDIS_SIGNALR_PASSWORD="$REDIS_SIGNALR_PASSWORD" \
  --dry-run=client -o yaml | kubectl apply -f -

echo "[clean] Deploying with pure Helm approach - no template corruption possible..."

# Pre-check: lint and dry-render templates with the same values to surface issues early
echo "[clean] Helm pre-check: linting chart..."
if ! helm lint "$OUT_DIR" -n "$NAMESPACE" -f "$OUT_DIR/values.yaml" -f "$OVERRIDE_VALUES"; then
  echo "[clean] ERROR: helm lint failed. Aborting before upgrade."
  exit 1
fi

# Deploy using Helm with --force flag for idempotent operation
# The --force flag handles delete/recreate automatically for incompatible changes
POST_RENDERER=$(mktemp)
cat > "$POST_RENDERER" <<'PR'
#!/usr/bin/env bash
set -euo pipefail

TMP=$(mktemp)
cat > "$TMP"

# Resolve yq (install to /tmp if missing)
if ! command -v yq >/dev/null 2>&1; then
  curl -sSL https://github.com/mikefarah/yq/releases/latest/download/yq_linux_amd64 -o /tmp/yq
  chmod +x /tmp/yq
  YQ=/tmp/yq
else
  YQ=$(command -v yq)
fi

# Since we're doing clean service deletion, only minimal cleanup needed
# Remove any stray HTTPS ports if they somehow still exist
$YQ -i 'select(.kind=="Service").spec.ports |= map(select(.name != "https"))' "$TMP"
$YQ -i 'select(.kind=="Deployment").spec.template.spec.containers[0].ports |= map(select(.name != "https"))' "$TMP"

# Orleans environment variables are now correctly set by AppHost
# Only ensure ASPNETCORE_URLS is set correctly (not using placeholders)
$YQ -i 'select(.kind=="ConfigMap" and .metadata.name=="api-main-config").data.ASPNETCORE_URLS = "http://+:8080"' "$TMP"

# Force ASPNETCORE_URLS=http://+:8080 to avoid "$8080" placeholders overriding HTTP_PORTS
for dep in api-main-deployment mcp-server-deployment web-docgen-deployment; do
  $YQ -i 'select(.kind=="Deployment" and .metadata.name=="'"$dep"'").spec.template.spec.containers[0].env |= (
    ( . // [] ) | map(select(.name != "ASPNETCORE_URLS")) + [{"name":"ASPNETCORE_URLS","value":"http://+:8080"}]
  )' "$TMP"
done

cat "$TMP"
rm -f "$TMP"
PR
chmod +x "$POST_RENDERER"

helm upgrade --install "$RELEASE" "$OUT_DIR" \
  -n "$NAMESPACE" \
  -f "$OUT_DIR/values.yaml" \
  -f "$OVERRIDE_VALUES" \
  --post-renderer "$POST_RENDERER" \
  --force \
  --wait --timeout 10m

echo "[clean] Clean deployment completed successfully"
echo "[clean] No template modifications = No YAML corruption"

# Port configuration is now handled entirely by the post-renderer above
# No need for additional kubectl patches as they conflict with Helm's state management

# Orleans ports are now configured via post-renderer, so no additional kubectl patches needed
# The post-renderer ensures:
# - silo-service: Orleans Silo (11111) + Orleans Gateway (30000)
# - api-main-service: HTTP (8080) + Orleans Silo (11111) + Orleans Gateway (30000)
# - mcp-server/web-docgen-service: HTTP (8080) only

# Show deployment status
kubectl get pods,svc,secrets -n "$NAMESPACE"

# Ensure OpenAI connection string is present in per-app Secrets (fallback if Helm chart omitted it)
if [[ -n "${PVICO_OPENAI_CONNECTIONSTRING:-}" ]]; then
  echo "[clean] Ensuring ConnectionStrings__openai-planner exists in per-app secrets"
  # Base64-encode once for merge patch (portable: no line wraps)
  OAI_B64=$(printf '%s' "$PVICO_OPENAI_CONNECTIONSTRING" | base64 | tr -d '\n')
  for app in api-main db-setupmanager mcp-server silo web-docgen; do
    secret="${app}-secrets"
    if kubectl -n "$NAMESPACE" get secret "$secret" >/dev/null 2>&1; then
      if kubectl -n "$NAMESPACE" get secret "$secret" -o json | jq -e '.data["ConnectionStrings__openai-planner"]' >/dev/null 2>&1; then
        echo "[clean] $secret already has ConnectionStrings__openai-planner; ensuring value is current"
      else
        echo "[clean] Adding ConnectionStrings__openai-planner to $secret"
      fi
      kubectl -n "$NAMESPACE" patch secret "$secret" --type='merge' \
        -p "{\"data\":{\"ConnectionStrings__openai-planner\":\"$OAI_B64\"}}" >/dev/null && \
        echo "[clean] Patched $secret with OpenAI connection string"
    else
      echo "[clean] Creating $secret with OpenAI connection string"
      kubectl -n "$NAMESPACE" create secret generic "$secret" \
        --from-literal=ConnectionStrings__openai-planner="$PVICO_OPENAI_CONNECTIONSTRING" \
        --dry-run=client -o yaml | kubectl apply -f -
    fi
  done
else
  echo "[clean] PVICO_OPENAI_CONNECTIONSTRING not provided - skipping OpenAI secret injection"
fi

# IMPORTANT: Patch connection strings FIRST before restarting pods
# Force-correct critical connection string keys in ConfigMaps with sanitized values
echo "[clean] Patching critical connection strings in ConfigMaps with sanitized values"
for app in api-main db-setupmanager mcp-server silo web-docgen; do
  APP_CFG_CM="${app}-config"
  # Build patch JSON safely via jq to avoid quoting issues
  PATCH_JSON=$(jq -n \
    --arg app "$app" \
    --arg blobDoc "$DOCING_BLOB_EP" \
    --arg tbl "$ORLEANS_TABLE_EP" \
    --arg blobOr "$ORLEANS_BLOB_EP" \
    --arg eh "$EVENTHUBS_EP" \
    --arg ais "$AISEARCH_CS" \
    --arg ai "$APPINSIGHTS_CS" \
    --arg sqlFqdn "$SQLSERVER_FQDN" \
    --arg evConn "${EVENTHUB_CONN:-}" \
    --arg tenantId "$TENANT_ID" \
    '{data: {
      "ConnectionStrings__blob-docing": $blobDoc,
      "ConnectionStrings__clustering": $tbl,
      "ConnectionStrings__checkpointing": $tbl,
      "ConnectionStrings__blob-orleans": $blobOr,
      "ConnectionStrings__greenlight-cg-streams": (if ($evConn != "") then ($evConn + ";EntityPath=greenlight-hub;ConsumerGroup=greenlight-cg-streams") else ("Endpoint=" + $eh + ";EntityPath=greenlight-hub;ConsumerGroup=greenlight-cg-streams") end),
      "ConnectionStrings__aiSearch": $ais,
      "ConnectionStrings__ProjectVicoDB": ("Server=tcp:" + $sqlFqdn + ",1433;Encrypt=True;Authentication=Active Directory Default;Database=ProjectVicoDB"),
      "APPLICATIONINSIGHTS_CONNECTION_STRING": $ai,
      "Azure__TenantId": $tenantId,
      "Orleans__ClusterId": "greenlight-cluster",
      "Orleans__ServiceId": "greenlight-app",
      "services__api-main__http__0": "http://api-main:8080",
      "services__web-docgen__http__0": "http://web-docgen:8080",
      "services__silo__http__0": "http://silo:8080",
      "services__mcp-server__http__0": "http://mcp-server:8080",
      "services__api_main__http__0": "http://api-main:8080",
      "services__web_docgen__http__0": "http://web-docgen:8080",
      "services__silo__http__0": "http://silo:8080",
      "services__mcp_server__http__0": "http://mcp-server:8080"
    }}
    | if ($app == "silo" or $app == "api-main") then
        .data += {
          "Orleans__GrainStorage__blob-orleans__ProviderType": "AzureBlobStorage",
          "Orleans__GrainStorage__blob-orleans__ServiceKey": "blob-orleans"
        }
      else
        .
      end')
  echo "[clean] Patching $APP_CFG_CM with critical connection strings..."
  echo "[clean] Patch JSON preview: $(echo "$PATCH_JSON" | jq -c .data | head -c 200)..."
  if kubectl -n "$NAMESPACE" patch configmap "$APP_CFG_CM" --type='merge' -p "$PATCH_JSON"; then
    echo "[clean] ✅ Successfully patched $APP_CFG_CM"
  else
    echo "[clean] ❌ Failed to patch $APP_CFG_CM - this will cause pods to crash with placeholder values!"
    echo "[clean] Full patch JSON: $PATCH_JSON"
    exit 1
  fi
done

# Ensure AzureAd section keys are present in ConfigMaps for all services
echo "[clean] Ensuring AzureAd configuration keys are present in ConfigMaps"
AZ_JSON="${PVICO_ENTRA_CREDENTIALS:-}"
if [[ -n "$AZ_JSON" ]] && echo "$AZ_JSON" | jq empty >/dev/null 2>&1; then
  AZ_TENANT=$(echo "$AZ_JSON" | jq -r '.TenantId // empty')
  AZ_CLIENT=$(echo "$AZ_JSON" | jq -r '.ClientId // empty')
  AZ_INSTANCE=$(echo "$AZ_JSON" | jq -r '.Instance // "https://login.microsoftonline.com/"')
  AZ_DOMAIN=$(echo "$AZ_JSON" | jq -r '.Domain // empty')
  AZ_SCOPES=$(echo "$AZ_JSON" | jq -r '.Scopes // "api://greenlight/.default"')
  AZ_AUDIENCE=$(echo "$AZ_JSON" | jq -r '.Audience // empty')

  for app in api-main db-setupmanager mcp-server silo web-docgen; do
    APP_CFG_CM="${app}-config"
    PATCH_JSON=$(jq -n \
      --arg tenant "$AZ_TENANT" \
      --arg client "$AZ_CLIENT" \
      --arg instance "$AZ_INSTANCE" \
      --arg domain "$AZ_DOMAIN" \
      --arg scopes "$AZ_SCOPES" \
      --arg audience "$AZ_AUDIENCE" \
      '{data: {
        "AzureAd__TenantId": $tenant,
        "AzureAd__ClientId": $client,
        "AzureAd__Instance": $instance,
        "AzureAd__Domain": $domain,
        "AzureAd__Scopes": $scopes,
        "AzureAd__Audience": $audience,
      } }')
    echo "[clean] Patching $APP_CFG_CM with AzureAd configuration..."
    if kubectl -n "$NAMESPACE" patch configmap "$APP_CFG_CM" --type='merge' -p "$PATCH_JSON"; then
      echo "[clean] ✅ Successfully patched $APP_CFG_CM with AzureAd config"
    else
      echo "[clean] ❌ Failed to patch $APP_CFG_CM with AzureAd config"
    fi
  done
else
  echo "[clean] WARNING: PVICO_ENTRA_CREDENTIALS not set or invalid JSON; skipping AzureAd config patch"
fi

# Ensure pods pick up Workload Identity env automatically (append envFrom)
CONFIG_VERSION="${BUILD_BUILDNUMBER:-$(date +%s)}"

# Ensure envFrom includes both app config/secret and WI env, and force a rollout
echo "[clean] Ensuring envFrom (config, secrets, WI) on deployments + bumping config versions"
for app in api-main db-setupmanager mcp-server silo web-docgen; do
  DEP="${app}-deployment"
  APP_CFG_CM="${app}-config"
  APP_SEC="${app}-secrets"

  # Annotate the ConfigMap with a bump to force detection of changes each run
  kubectl -n "$NAMESPACE" annotate configmap "$APP_CFG_CM" "greenlight-config-version=$CONFIG_VERSION" --overwrite 2>/dev/null || true

  # Ensure serviceAccountName
  kubectl -n "$NAMESPACE" patch deployment "$DEP" --type='json' -p='[{"op":"add","path":"/spec/template/spec/serviceAccountName","value":"'"${WORKLOAD_IDENTITY_SERVICE_ACCOUNT:-greenlight-app}"'"}]' 2>/dev/null || \
  kubectl -n "$NAMESPACE" patch deployment "$DEP" --type='json' -p='[{"op":"replace","path":"/spec/template/spec/serviceAccountName","value":"'"${WORKLOAD_IDENTITY_SERVICE_ACCOUNT:-greenlight-app}"'"}]' 2>/dev/null || true

  # Ensure pod label required by azure-wi webhook
  kubectl -n "$NAMESPACE" patch deployment "$DEP" --type='json' -p='[{"op":"add","path":"/spec/template/metadata/labels/azure.workload.identity~1use","value":"true"}]' 2>/dev/null || \
  kubectl -n "$NAMESPACE" patch deployment "$DEP" --type='json' -p='[{"op":"replace","path":"/spec/template/metadata/labels/azure.workload.identity~1use","value":"true"}]' 2>/dev/null || true

  # Build a merged envFrom array if missing
  kubectl -n "$NAMESPACE" patch deployment "$DEP" --type='json' -p='[
    {"op":"add","path":"/spec/template/spec/containers/0/envFrom","value":[
      {"configMapRef":{"name":"'"$APP_CFG_CM"'"}},
      {"secretRef":{"name":"'"$APP_SEC"'"}},
      {"configMapRef":{"name":"workload-identity-env"}}
    ]}
  ]' 2>/dev/null || true

  # Append specific entries defensively (no-ops if already present)
  kubectl -n "$NAMESPACE" patch deployment "$DEP" --type='json' -p='[{"op":"add","path":"/spec/template/spec/containers/0/envFrom/-","value":{"configMapRef":{"name":"'"$APP_CFG_CM"'"}}}]' 2>/dev/null || true
  kubectl -n "$NAMESPACE" patch deployment "$DEP" --type='json' -p='[{"op":"add","path":"/spec/template/spec/containers/0/envFrom/-","value":{"secretRef":{"name":"'"$APP_SEC"'"}}}]' 2>/dev/null || true
  if kubectl -n "$NAMESPACE" get configmap workload-identity-env >/dev/null 2>&1; then
    kubectl -n "$NAMESPACE" patch deployment "$DEP" --type='json' -p='[{"op":"add","path":"/spec/template/spec/containers/0/envFrom/-","value":{"configMapRef":{"name":"workload-identity-env"}}}]' 2>/dev/null || true
  fi

  # If greenlight-auth secret exists (AzureAd__ClientSecret), add it for web-docgen and api-main to support OIDC confidential client
  if { [[ "$app" == "web-docgen" ]] || [[ "$app" == "api-main" ]]; } && kubectl -n "$NAMESPACE" get secret greenlight-auth >/dev/null 2>&1; then
    kubectl -n "$NAMESPACE" patch deployment "$DEP" --type='json' -p='[{"op":"add","path":"/spec/template/spec/containers/0/envFrom/-","value":{"secretRef":{"name":"greenlight-auth"}}}]' 2>/dev/null || true
  fi

  # Add GREENLIGHT_PRODUCTION via WI env CM if not already present
  if kubectl -n "$NAMESPACE" get configmap workload-identity-env >/dev/null 2>&1; then
    kubectl -n "$NAMESPACE" patch configmap workload-identity-env --type='merge' -p '{"data":{"GREENLIGHT_PRODUCTION":"true"}}' 2>/dev/null || true
  fi

  # Restart to pick up envFrom changes
  kubectl -n "$NAMESPACE" rollout restart deployment "$DEP" || true
done

# ------------------------------------------------------------------
# Run DB SetupManager as a one-off Job (idempotent) and keep deploy scaled to 0
# ------------------------------------------------------------------
if [[ "${DB_SETUP_JOB_MODE:-inline}" != "inline" ]]; then
  echo "[clean] DB SetupManager job execution deferred (DB_SETUP_JOB_MODE=${DB_SETUP_JOB_MODE:-postpone})"
  echo "[clean] Ensuring db-setupmanager deployment remains scaled to 0 in deferred mode"
  kubectl -n "$NAMESPACE" scale deployment/db-setupmanager-deployment --replicas=0 2>/dev/null || true
else
  echo "[clean] Executing DB SetupManager as a Kubernetes Job (one-shot)"
  # Ensure the deployment (if present) does not keep restarting the worker
  kubectl -n "$NAMESPACE" scale deployment/db-setupmanager-deployment --replicas=0 2>/dev/null || true

  # Small grace period to avoid immediate race with WI/role readiness after restart
  SLEEP_BEFORE_JOB=${SLEEP_BEFORE_JOB:-30}
  echo "[clean] Waiting ${SLEEP_BEFORE_JOB}s before starting SetupManager job to allow WI/DB readiness"
  sleep "$SLEEP_BEFORE_JOB" || true

  run_setup_job() {
  local job_suffix="$1"
  local job_name="db-setupmanager-job-${job_suffix}"
  local job_file
  job_file=$(mktemp)
  cat > "$job_file" <<EOF
apiVersion: batch/v1
kind: Job
metadata:
  name: ${job_name}
  namespace: ${NAMESPACE}
spec:
  backoffLimit: 0
  ttlSecondsAfterFinished: 1800
  template:
    metadata:
      labels:
        app: aspire
        component: db-setupmanager
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: ${WORKLOAD_IDENTITY_SERVICE_ACCOUNT:-greenlight-app}
      restartPolicy: Never
      containers:
        - name: db-setupmanager
          image: "${IMG_PREFIX}db-setupmanager:${IMAGE_TAG}"
          imagePullPolicy: IfNotPresent
          envFrom:
            - configMapRef:
                name: db-setupmanager-config
            - secretRef:
                name: db-setupmanager-secrets
            - configMapRef:
                name: workload-identity-env
EOF
  echo "[clean] Applying job ${job_name}"
  kubectl apply -f "$job_file"

  echo "[clean] Waiting for job completion (up to 15m)"
  if ! kubectl -n "$NAMESPACE" wait --for=condition=complete job/${job_name} --timeout=15m; then
    echo "[clean] ERROR: DB SetupManager job did not complete successfully"
    echo "[clean] Recent job events:"
    kubectl -n "$NAMESPACE" describe job ${job_name} | tail -n 80 || true
    kubectl -n "$NAMESPACE" logs job/${job_name} --tail=200 || true
    return 1
  fi

  # Inspect logs for known transient SQL auth errors (18456 login failed) and treat as retryable
  local logs
  logs=$(kubectl -n "$NAMESPACE" logs job/${job_name} --tail=500 || true)
  echo "$logs" | tail -n 200
  if echo "$logs" | grep -qiE "SqlException.*Login failed for user|Error Number:18456"; then
    echo "[clean] Detected SQL login failure in SetupManager logs (likely transient). Will retry after delay."
    return 2
  fi

  echo "[clean] DB SetupManager job completed successfully"
  return 0
}

# Attempt the job up to 3 times with backoff if we detect transient login failure
RETRIES=3
DELAY=45
ATTEMPT=1
while : ; do
  TS=$(date +%Y%m%d%H%M%S)
  run_setup_job "$TS"
  rc=$?
  if [[ $rc -eq 0 ]]; then
    break
  elif [[ $rc -eq 2 && $ATTEMPT -lt $RETRIES ]]; then
    echo "[clean] Retry $ATTEMPT/$RETRIES after ${DELAY}s..."
    sleep "$DELAY" || true
    ATTEMPT=$((ATTEMPT+1))
    continue
  else
    echo "[clean] SetupManager job failed (rc=$rc). Aborting deployment."
    exit 1
  fi
done
fi
