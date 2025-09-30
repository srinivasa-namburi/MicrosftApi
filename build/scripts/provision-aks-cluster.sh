#!/bin/bash
set -e

# AKS Cluster Provisioning Script for Microsoft Greenlight
# This script provisions an AKS cluster with appropriate configuration for Aspire deployments
# Supports public, private, and hybrid deployment models

# Disable color output for CI/CD environments
export TERM=dumb
export NO_COLOR=1

# Function to print status output
print_info() { echo "[INFO] $1"; }
print_success() { echo "[OK] $1"; }
print_error() { echo "[ERROR] $1" >&2; }
print_warning() { echo "[WARNING] $1"; }

# Function to show usage
show_usage() {
    echo "Usage: $0 <resource-group> <location> <cluster-name> [options]"
    echo ""
    echo "Required parameters:"
    echo "  resource-group    Resource group name"
    echo "  location          Azure region (e.g., eastus, westus2)"
    echo "  cluster-name      AKS cluster name"
    echo ""
    echo "Optional parameters:"
    echo "  --node-count      Number of nodes (default: 3)"
    echo "  --node-size       VM size (default: Standard_D4s_v6)"
    echo "  --cluster-mode    Cluster mode: public|private (default: public)"
    echo "                    public = API server accessible from internet"
    echo "                    private = API server only accessible within VNET"
    echo "  --vnet-resource-group VNET resource group (for VNET-integrated clusters)"
    echo "  --vnet-name       VNET name (for VNET-integrated clusters)"
    echo "  --subnet-name     Subnet name (must NOT be delegated)"
    echo "  --subnet-id       Full subnet resource ID (alternative to vnet/subnet names)"
    echo "  --service-cidr    Service CIDR (default: 10.0.0.0/16)"
    echo "  --dns-service-ip  DNS service IP (default: 10.0.0.10)"
    echo "  --private-dns-zone Private DNS zone for private cluster (default: system)"
    echo "                    Options: system, none, or resource ID like:"
    echo "                    /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Network/privateDnsZones/privatelink.{region}.azmk8s.io"
    echo "  --ssh-key-path    Path to SSH public key (optional)"
    echo ""
    echo "Workload Identity parameters (NOT RECOMMENDED - deployments manage their own):"
    echo "  --wi-identity-name  Name of managed identity (only for single-tenant test clusters)"
    echo "  --wi-service-account K8s service account (only with CREATE_CLUSTER_WORKLOAD_IDENTITY=true)"
    echo "  --wi-namespace    K8s namespace (only with CREATE_CLUSTER_WORKLOAD_IDENTITY=true)"
    echo ""
    echo "Examples:"
    echo "  # Public cluster without VNET (internet accessible API)"
    echo "  $0 rg-greenlight-dev eastus aks-greenlight-dev"
    echo ""
    echo "  # Public cluster with VNET integration (internet accessible API, nodes in VNET)"
    echo "  $0 rg-greenlight-dev eastus aks-greenlight-dev \\"
    echo "    --vnet-resource-group rg-network \\"
    echo "    --vnet-name vnet-greenlight \\"
    echo "    --subnet-name snet-aks"
    echo ""
    echo "  # Private cluster (API only accessible within VNET)"
    echo "  $0 rg-greenlight-dev eastus aks-greenlight-dev \\"
    echo "    --cluster-mode private \\"
    echo "    --subnet-id /subscriptions/xxx/resourceGroups/rg-network/providers/Microsoft.Network/virtualNetworks/vnet-greenlight/subnets/snet-aks \\"
    echo "    --private-dns-zone /subscriptions/hub-sub/resourceGroups/rg-dns/providers/Microsoft.Network/privateDnsZones/privatelink.eastus.azmk8s.io"
    echo ""
    echo "Note: This script creates a multi-tenant cluster. Each deployment (dev, demo, prod)"
    echo "will manage its own workload identity during pipeline execution. Do NOT use the"
    echo "--wi-* parameters unless you're creating a single-tenant test cluster."
}

# Check minimum required parameters
if [ $# -lt 3 ]; then
    show_usage
    exit 1
fi

# Required parameters
RESOURCE_GROUP=$1
LOCATION=$2
CLUSTER_NAME=$3
shift 3

# Default values
NODE_COUNT=3
NODE_SIZE="Standard_D4s_v6"
CLUSTER_MODE="public"
VNET_RESOURCE_GROUP=""
VNET_NAME=""
SUBNET_NAME=""
SUBNET_ID=""
SERVICE_CIDR="10.0.0.0/16"
DNS_SERVICE_IP="10.0.0.10"
PRIVATE_DNS_ZONE="system"
SSH_KEY_PATH=""
WI_IDENTITY_NAME=""
WI_SERVICE_ACCOUNT="greenlight-app"
WI_NAMESPACE="greenlight-dev"

# Parse optional arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --node-count)
            NODE_COUNT="$2"
            shift 2
            ;;
        --node-size)
            NODE_SIZE="$2"
            shift 2
            ;;
        --cluster-mode)
            CLUSTER_MODE="$2"
            if [[ ! "$CLUSTER_MODE" =~ ^(public|private)$ ]]; then
                print_error "Invalid cluster mode: $CLUSTER_MODE (must be public or private)"
                exit 1
            fi
            shift 2
            ;;
        --deployment-model)
            # Legacy parameter - map to cluster-mode
            print_warning "Parameter --deployment-model is deprecated. Use --cluster-mode instead."
            case "$2" in
                public)
                    CLUSTER_MODE="public"
                    ;;
                private)
                    CLUSTER_MODE="private"
                    ;;
                hybrid)
                    print_warning "Hybrid is not a cluster mode. Using public cluster with VNET integration."
                    CLUSTER_MODE="public"
                    ;;
                *)
                    print_error "Invalid deployment model: $2"
                    exit 1
                    ;;
            esac
            shift 2
            ;;
        --vnet-resource-group)
            VNET_RESOURCE_GROUP="$2"
            shift 2
            ;;
        --vnet-name)
            VNET_NAME="$2"
            shift 2
            ;;
        --subnet-name)
            SUBNET_NAME="$2"
            shift 2
            ;;
        --subnet-id)
            SUBNET_ID="$2"
            shift 2
            ;;
        --service-cidr)
            SERVICE_CIDR="$2"
            shift 2
            ;;
        --dns-service-ip)
            DNS_SERVICE_IP="$2"
            shift 2
            ;;
        --private-dns-zone)
            PRIVATE_DNS_ZONE="$2"
            shift 2
            ;;
        --ssh-key-path)
            SSH_KEY_PATH="$2"
            shift 2
            ;;
        --wi-identity-name)
            WI_IDENTITY_NAME="$2"
            shift 2
            ;;
        --wi-service-account)
            WI_SERVICE_ACCOUNT="$2"
            shift 2
            ;;
        --wi-namespace)
            WI_NAMESPACE="$2"
            shift 2
            ;;
        --help)
            show_usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Additional configuration
KUBERNETES_VERSION="1.31"  # Use stable version
DNS_NAME_PREFIX=$(echo $CLUSTER_NAME | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9-]//g')

echo "AKS Cluster Provisioning"
echo "========================"
echo "Resource Group: $RESOURCE_GROUP"
echo "Location: $LOCATION"
echo "Cluster Name: $CLUSTER_NAME"
echo "Node Count: $NODE_COUNT"
echo "Node Size: $NODE_SIZE"
echo "Cluster Mode: $CLUSTER_MODE"

# Validate requirements for VNET-integrated clusters
# Note: Both public and private clusters can be in a VNET
if [[ "$CLUSTER_MODE" == "private" ]]; then
    # Private clusters MUST be in a VNET
    if [[ -z "$SUBNET_ID" && (-z "$VNET_NAME" || -z "$SUBNET_NAME") ]]; then
        print_error "Private clusters require VNET integration. Provide either --subnet-id or both --vnet-name and --subnet-name"
        echo ""
        echo "Usage for private cluster:"
        echo "  Option 1 - Using subnet resource ID:"
        echo "    $0 $RESOURCE_GROUP $LOCATION $CLUSTER_NAME --cluster-mode private --subnet-id \$AZURE_SUBNET_AKS"
        echo ""
        echo "  Option 2 - Using VNet/Subnet names:"
        echo "    $0 $RESOURCE_GROUP $LOCATION $CLUSTER_NAME --cluster-mode private --vnet-name vnet-greenlight --subnet-name snet-aks"
        exit 1
    fi
fi

# Display VNET info if provided
if [[ -n "$SUBNET_ID" || -n "$VNET_NAME" ]]; then

    if [[ -n "$SUBNET_ID" ]]; then
        echo "Subnet Resource ID: $SUBNET_ID"
    else
        [[ -z "$VNET_RESOURCE_GROUP" ]] && VNET_RESOURCE_GROUP="$RESOURCE_GROUP"
        echo "VNET Resource Group: $VNET_RESOURCE_GROUP"
        echo "VNET Name: $VNET_NAME"
        echo "Subnet Name: $SUBNET_NAME"
    fi
fi

echo ""

# Check if logged in to Azure
if ! az account show > /dev/null 2>&1; then
    print_error "Not logged in to Azure. Please run 'az login' first."
    exit 1
fi

# Determine target subscription - use subnet subscription if provided, otherwise current
if [[ -n "$SUBNET_ID" ]]; then
    TARGET_SUBSCRIPTION=$(echo "$SUBNET_ID" | cut -d'/' -f3)
    print_info "Using subscription from subnet ID: $TARGET_SUBSCRIPTION"
    if ! az account set --subscription "$TARGET_SUBSCRIPTION"; then
        print_error "Could not switch to target subscription: $TARGET_SUBSCRIPTION"
        exit 1
    fi
else
    TARGET_SUBSCRIPTION=$(az account show --query id -o tsv)
    print_info "Using current subscription: $TARGET_SUBSCRIPTION"
fi

SUBSCRIPTION_ID="$TARGET_SUBSCRIPTION"
print_success "Using subscription: $SUBSCRIPTION_ID"

# Check if resource group exists, create if not
if ! az group show --name $RESOURCE_GROUP --subscription "$SUBSCRIPTION_ID" > /dev/null 2>&1; then
    print_warning "Resource group $RESOURCE_GROUP does not exist. Creating..."
    az group create --name $RESOURCE_GROUP --location $LOCATION --subscription "$SUBSCRIPTION_ID" --output none
    print_success "Resource group created"
else
    print_success "Resource group exists"
fi

# Setup SSH key
if [[ -z "$SSH_KEY_PATH" ]]; then
    # Use a dedicated key for AKS clusters, NOT the default SSH key
    SSH_DIR="$HOME/.ssh"
    [[ ! -d "$SSH_DIR" ]] && mkdir -p "$SSH_DIR"
    SSH_KEY_PATH="$SSH_DIR/aks-$CLUSTER_NAME"
fi

# Generate SSH key if it doesn't exist
if [[ ! -f "$SSH_KEY_PATH.pub" ]]; then
    print_info "Generating dedicated SSH key for AKS cluster: $SSH_KEY_PATH"
    ssh-keygen -t rsa -b 4096 -f "$SSH_KEY_PATH" -N "" -C "aks-$CLUSTER_NAME" -q
    print_success "SSH key generated: $SSH_KEY_PATH"
else
    print_info "Using existing AKS SSH key: $SSH_KEY_PATH"
fi

# Auto-detect availability zones for the location
print_info "Detecting availability zones for $LOCATION..."
ZONES_JSON=$(az vm list-skus --location $LOCATION --size $NODE_SIZE --query "[?resourceType=='virtualMachines'] | [0].locationInfo[0].zones" --output json 2>/dev/null)
if [ ! -z "$ZONES_JSON" ] && [ "$ZONES_JSON" != "null" ]; then
    AVAILABLE_ZONES=$(echo $ZONES_JSON | jq -r '.[]' 2>/dev/null | sort -n | tr '\n' ' ')
    if [ ! -z "$AVAILABLE_ZONES" ]; then
        print_success "Detected availability zones: $AVAILABLE_ZONES"
        ZONE_PARAMS="--zones $AVAILABLE_ZONES"
    else
        print_warning "No availability zones detected for $NODE_SIZE in $LOCATION"
        ZONE_PARAMS=""
    fi
else
    print_warning "Could not detect availability zones for $LOCATION"
    ZONE_PARAMS=""
fi

# Check if AKS cluster already exists
if az aks show --resource-group $RESOURCE_GROUP --name $CLUSTER_NAME > /dev/null 2>&1; then
    print_warning "AKS cluster $CLUSTER_NAME already exists in $RESOURCE_GROUP"
    if [[ "${ACCEPT_DEFAULTS:-false}" != "true" ]]; then
        read -p "Do you want to update the existing cluster? (y/N): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            print_info "Skipping cluster creation"
            EXISTING_CLUSTER=true
        else
            print_info "Updating existing cluster configuration..."
        fi
    else
        print_info "Non-interactive mode (ACCEPT_DEFAULTS=true) - not modifying existing cluster"
        EXISTING_CLUSTER=true
    fi

    # Update node pool if needed
    az aks nodepool scale \
        --resource-group $RESOURCE_GROUP \
        --cluster-name $CLUSTER_NAME \
        --name nodepool1 \
        --node-count $NODE_COUNT \
        --output none

    print_success "Cluster node count updated"
elif [[ -z "${EXISTING_CLUSTER:-}" ]]; then
    print_info "Creating AKS cluster (this may take 10-15 minutes)..."

    # Build base AKS create command
    AKS_CREATE_CMD="az aks create \
        --resource-group $RESOURCE_GROUP \
        --name $CLUSTER_NAME \
        --location $LOCATION \
        --kubernetes-version $KUBERNETES_VERSION \
        --node-count $NODE_COUNT \
        --node-vm-size $NODE_SIZE \
        --enable-managed-identity \
        --enable-oidc-issuer \
        --enable-workload-identity \
        --ssh-key-value $SSH_KEY_PATH.pub \
        --enable-cluster-autoscaler \
        --min-count 2 \
        --max-count 10 \
        $ZONE_PARAMS \
        --node-osdisk-size 100 \
        --node-osdisk-type Managed \
        --tier standard \
        --tags ManagedBy=Aspire ClusterMode=$CLUSTER_MODE CostControl=ignore"

    # Configure networking based on cluster mode and VNET settings
    # Get subnet ID if VNET parameters provided
    if [[ -n "$VNET_NAME" || -n "$SUBNET_ID" ]]; then
        if [[ -z "$SUBNET_ID" ]]; then
            SUBNET_ID=$(az network vnet subnet show \
                --resource-group $VNET_RESOURCE_GROUP \
                --vnet-name $VNET_NAME \
                --name $SUBNET_NAME \
                --query id -o tsv 2>/dev/null)

            if [[ -z "$SUBNET_ID" ]]; then
                print_error "Subnet $SUBNET_NAME not found in VNET $VNET_NAME"
                exit 1
            fi
        fi
        VNET_PARAMS="--vnet-subnet-id $SUBNET_ID --service-cidr $SERVICE_CIDR --dns-service-ip $DNS_SERVICE_IP"
    else
        VNET_PARAMS=""
    fi

    case "$CLUSTER_MODE" in
        "public")
            # Public cluster - API server accessible from internet
            AKS_CREATE_CMD="$AKS_CREATE_CMD \
                --network-plugin azure \
                --network-policy azure \
                --enable-addons monitoring \
                --dns-name-prefix $DNS_NAME_PREFIX \
                $VNET_PARAMS"

            if [[ -n "$VNET_PARAMS" ]]; then
                print_info "Configuring public AKS cluster with VNET integration"
                print_info "API server will be internet accessible, nodes in VNET"
            else
                print_info "Configuring public AKS cluster without VNET"
            fi
            ;;

        "private")
            # Private cluster - API server not accessible from internet
            if [[ -z "$SUBNET_ID" ]]; then
                print_error "Private cluster requires VNET integration but no subnet specified"
                exit 1
            fi

            # Handle private DNS zone parameter
            CLUSTER_MI_PARAMS=""
            if [[ "$PRIVATE_DNS_ZONE" == "system" ]]; then
                PRIVATE_DNS_PARAM="--private-dns-zone system"
                print_info "Using system-managed private DNS zone"
            elif [[ "$PRIVATE_DNS_ZONE" == "none" ]]; then
                PRIVATE_DNS_PARAM="--private-dns-zone none"
                print_warning "No private DNS zone - you must manage DNS resolution manually"
            elif [[ "$PRIVATE_DNS_ZONE" =~ ^/subscriptions/.*/privateDnsZones/privatelink\..+\.azmk8s\.io$ ]]; then
                # Custom DNS zone requires user-assigned managed identity
                print_info "Custom private DNS zone requires user-assigned managed identity"

                # Extract DNS zone details
                DNS_ZONE_SUB=$(echo "$PRIVATE_DNS_ZONE" | cut -d'/' -f3)
                DNS_ZONE_RG=$(echo "$PRIVATE_DNS_ZONE" | cut -d'/' -f5)
                DNS_ZONE_NAME=$(echo "$PRIVATE_DNS_ZONE" | cut -d'/' -f9)
                DNS_REGION=$(echo "$DNS_ZONE_NAME" | sed -n 's/.*privatelink\.\([^.]*\)\.azmk8s\.io$/\1/p')

                if [[ "$DNS_REGION" != "$LOCATION" ]]; then
                    print_warning "DNS zone region ($DNS_REGION) doesn't match cluster location ($LOCATION)"
                    print_warning "Ensure you're using the correct regional DNS zone"
                fi

                # Create or get managed identity for the cluster
                CLUSTER_MI_NAME="mi-${CLUSTER_NAME}"
                print_info "Ensuring managed identity: $CLUSTER_MI_NAME"

                MI_EXISTS=$(az identity show --name "$CLUSTER_MI_NAME" --resource-group "$RESOURCE_GROUP" --query id -o tsv 2>/dev/null || echo "")

                if [[ -z "$MI_EXISTS" ]]; then
                    print_info "Creating managed identity for cluster control plane..."
                    MI_RESOURCE_ID=$(az identity create \
                        --name "$CLUSTER_MI_NAME" \
                        --resource-group "$RESOURCE_GROUP" \
                        --location "$LOCATION" \
                        --query id -o tsv)
                    print_success "Created managed identity: $CLUSTER_MI_NAME"
                else
                MI_RESOURCE_ID=$(echo "$MI_EXISTS" | tr -d '\r')
                    print_success "Using existing managed identity: $CLUSTER_MI_NAME"
                fi

                MI_PRINCIPAL_ID=$(az identity show --ids "$MI_RESOURCE_ID" --query principalId -o tsv | tr -d '\r')
                MI_CLIENT_ID=$(az identity show --ids "$MI_RESOURCE_ID" --query clientId -o tsv | tr -d '\r')

                # Check if we have permissions to assign roles on the DNS zone
                print_info "Checking permissions on private DNS zone..."
                CURRENT_USER_ID=$(az account show --query user.name -o tsv)

                # Try to check access (this might fail if we don't have permissions)
                CAN_ASSIGN_ROLE=$(az role assignment list --scope "$PRIVATE_DNS_ZONE" --query "[?principalName=='$CURRENT_USER_ID'].id" -o tsv 2>/dev/null || echo "")

                # Check if MI already has the role
                print_info "Checking for existing Private DNS Zone Contributor role..."
                print_info "  Managed Identity Principal ID: $MI_PRINCIPAL_ID"
                print_info "  Managed Identity Client ID: $MI_CLIENT_ID"
                print_info "  DNS Zone: $DNS_ZONE_NAME in subscription $DNS_ZONE_SUB"

                # Debug: Let's see what roles are actually assigned
                if [[ "${DEBUG:-}" == "true" ]]; then
                    print_info "DEBUG: Checking all role assignments for MI using clientId..."
                    az role assignment list --all --assignee "$MI_CLIENT_ID" --output table 2>/dev/null || true

                    print_info "DEBUG: Checking role assignments on DNS zone scope..."
                    az role assignment list --scope "$PRIVATE_DNS_ZONE" --subscription "$DNS_ZONE_SUB" --output table 2>/dev/null || true
                    print_info "DEBUG: Continuing with role check..."
                fi

                # Simple direct check - just check for the principalId which we know works
                print_info "Checking for role assignment using principalId (which should work)..."

                EXISTING_ROLE=$(az role assignment list \
                    --scope "$PRIVATE_DNS_ZONE" \
                    --subscription "$DNS_ZONE_SUB" \
                    --query "[?roleDefinitionName=='Private DNS Zone Contributor' && (principalId=='$MI_PRINCIPAL_ID' || principalId=='$MI_CLIENT_ID')].id | [0]" \
                    -o tsv 2>/dev/null || echo "")

                print_info "DEBUG: Role check result: '$EXISTING_ROLE'"

                if [[ -n "$EXISTING_ROLE" ]]; then
                    print_success "Managed identity already has Private DNS Zone Contributor role"
                    print_info "DEBUG: EXISTING_ROLE final value: '$EXISTING_ROLE'"
                else
                    # Try to assign the role
                    print_info "Attempting to grant Private DNS Zone Contributor role..."

                    # Try without suppressing errors first to see what happens
                    unset ROLE_ASSIGN_FAILED
                    ROLE_ASSIGN_OUTPUT=$(az role assignment create \
                        --assignee-object-id "$MI_PRINCIPAL_ID" \
                        --assignee-principal-type ServicePrincipal \
                        --role "Private DNS Zone Contributor" \
                        --scope "$PRIVATE_DNS_ZONE" \
                        --subscription "$DNS_ZONE_SUB" \
                        --output none 2>&1) || ROLE_ASSIGN_FAILED=$?

                    if [[ -z "${ROLE_ASSIGN_FAILED:-}" ]]; then
                        print_success "Granted Private DNS Zone Contributor role to managed identity"
                    else
                        # Check one more time - assignment may have succeeded despite error
                        print_info "Role assignment returned an error, verifying role status..."

                        sleep 2  # Brief wait for propagation

                        # Check using --subscription flag instead of switching context
                        EXISTING_ROLE=$(az role assignment list \
                            --scope "$PRIVATE_DNS_ZONE" \
                            --subscription "$DNS_ZONE_SUB" \
                            --query "[?roleDefinitionName=='Private DNS Zone Contributor' && (principalId=='$MI_PRINCIPAL_ID' || principalId=='$MI_CLIENT_ID')].id | [0]" \
                            -o tsv 2>/dev/null)

                        if [[ -n "$EXISTING_ROLE" ]]; then
                            print_success "Managed identity has Private DNS Zone Contributor role (assignment succeeded despite error)"
                            # Continue
                        else
                            print_error "Cannot grant permissions to managed identity on private DNS zone"
                            echo ""
                            echo "══════════════════════════════════════════════════════════════"
                            echo "ACTION REQUIRED: Manual DNS Zone Permission Grant"
                            echo "══════════════════════════════════════════════════════════════"
                            echo ""
                            echo "The managed identity needs permissions on the private DNS zone."
                            echo "Please ask someone with Owner/User Access Administrator role on the DNS zone to run:"
                            echo ""
                            printf "  az role assignment create \\\\\n"
                            printf "    --assignee %s \\\\\n" "$MI_CLIENT_ID"
                            printf "    --role \"Private DNS Zone Contributor\" \\\\\n"
                            printf "    --scope %s\n" "$PRIVATE_DNS_ZONE"
                            echo ""
                            echo "DNS Zone Details:"
                            echo "  Zone Name: $DNS_ZONE_NAME"
                            echo "  Resource Group: $DNS_ZONE_RG"
                            echo "  Subscription: $DNS_ZONE_SUB"
                            echo ""
                            echo "Managed Identity Details:"
                            echo "  Name: $CLUSTER_MI_NAME"
                            echo "  Principal ID: $MI_PRINCIPAL_ID"
                            echo "  Resource ID: $MI_RESOURCE_ID"
                            echo ""
                            echo "After permissions are granted, re-run this script to create the cluster."
                            echo "══════════════════════════════════════════════════════════════"
                            exit 1
                        fi
                    fi
                fi

                # Also need Network Contributor on the subnet
                print_info "Checking Network Contributor role on subnet..."
                SUBNET_RG=$(echo "$SUBNET_ID" | cut -d'/' -f5 | tr -d '\r')
                VNET_ID=$(echo "$SUBNET_ID" | sed 's#/subnets/.*$##' | tr -d '\r')

                # Use clientId for role checks (this is what appears in role assignments)
                EXISTING_NET_ROLE=$(az role assignment list \
                    --assignee "$MI_CLIENT_ID" \
                    --scope "$VNET_ID" \
                    --role "Network Contributor" \
                    --query "[0].id" -o tsv 2>/dev/null || echo "")

                if [[ -n "$EXISTING_NET_ROLE" ]]; then
                    print_success "Managed identity already has Network Contributor role on VNet"
                else
                    # Use clientId for role assignment
                    if az role assignment create \
                        --assignee "$MI_CLIENT_ID" \
                        --role "Network Contributor" \
                        --scope "$VNET_ID" \
                        --output none 2>/dev/null; then
                        print_success "Granted Network Contributor role on VNet"
                    else
                        print_warning "Could not grant Network Contributor on VNet - may already exist at subnet level"
                    fi
                fi

                PRIVATE_DNS_PARAM="--private-dns-zone \"$PRIVATE_DNS_ZONE\""
                CLUSTER_MI_PARAMS="--assign-identity \"$MI_RESOURCE_ID\""

                print_info "Using custom private DNS zone with managed identity"
                print_info "Waiting 30 seconds for role propagation..."
                sleep 30
            else
                print_error "Invalid private DNS zone format: $PRIVATE_DNS_ZONE"
                print_error "Expected: system, none, or /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Network/privateDnsZones/privatelink.{region}.azmk8s.io"
                exit 1
            fi

            # Note: Removed --docker-bridge-cidr as it's deprecated
            AKS_CREATE_CMD="$AKS_CREATE_CMD \
                --network-plugin azure \
                --network-policy azure \
                --enable-private-cluster \
                $PRIVATE_DNS_PARAM \
                --enable-addons monitoring \
                $CLUSTER_MI_PARAMS \
                $VNET_PARAMS"

            print_info "Configuring private AKS cluster"
            print_warning "Private cluster API server will not be accessible from public internet"
            print_warning "You'll need to access it from within the VNET or via VPN/ExpressRoute"
            ;;
    esac

    # Execute the AKS create command
    AKS_CREATE_CMD="$AKS_CREATE_CMD --output none"
    eval $AKS_CREATE_CMD

    if [ $? -ne 0 ]; then
        print_error "Failed to create AKS cluster"
        exit 1
    fi

    print_success "AKS cluster created successfully"

    # Configure diagnostic logs if requested via AKS_DIAGNOSTICLOGS variable
    if [[ "${AKS_DIAGNOSTICLOGS:-false}" == "true" ]]; then
        print_info "Enabling diagnostic logs for AKS cluster (AKS_DIAGNOSTICLOGS=true)"

        # Check if a Log Analytics workspace exists
        LOG_ANALYTICS_WORKSPACE=$(az monitor log-analytics workspace list \
            --resource-group $RESOURCE_GROUP \
            --query "[0].id" -o tsv 2>/dev/null || true)

        if [[ -z "$LOG_ANALYTICS_WORKSPACE" ]]; then
            # Create a new Log Analytics workspace
            print_info "Creating Log Analytics workspace for diagnostic logs..."
            WORKSPACE_NAME="log-${CLUSTER_NAME}-${RANDOM}"
            LOG_ANALYTICS_WORKSPACE=$(az monitor log-analytics workspace create \
                --resource-group $RESOURCE_GROUP \
                --workspace-name $WORKSPACE_NAME \
                --location $LOCATION \
                --query id -o tsv)
            print_success "Created Log Analytics workspace: $WORKSPACE_NAME"
        fi

        # Create diagnostic settings for the AKS cluster
        print_info "Configuring diagnostic settings..."
        az monitor diagnostic-settings create \
            --name "aks-diagnostics" \
            --resource "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.ContainerService/managedClusters/$CLUSTER_NAME" \
            --workspace "$LOG_ANALYTICS_WORKSPACE" \
            --logs '[{"category":"kube-apiserver","enabled":true},{"category":"kube-controller-manager","enabled":true},{"category":"kube-scheduler","enabled":true},{"category":"cluster-autoscaler","enabled":true}]' \
            --metrics '[{"category":"AllMetrics","enabled":true}]' \
            --output none 2>/dev/null || {
                print_warning "Could not configure diagnostic settings - may already exist"
            }

        print_success "Diagnostic logs enabled for AKS cluster"
    else
        print_info "Diagnostic logs not enabled (set AKS_DIAGNOSTICLOGS=true to enable)"
    fi

    # Workload Identity: create user-assigned managed identity + federated credential
    # WARNING: This is typically not recommended for multi-tenant clusters
    # Each deployment should create its own identity in its own resource group
    # Only use this for single-tenant test clusters

    if [[ "${CREATE_CLUSTER_WORKLOAD_IDENTITY:-false}" == "true" ]]; then
        print_warning "Creating cluster-level workload identity (not recommended for multi-tenant)"
        print_warning "Each deployment should manage its own workload identity"

        if [[ -z "$WI_IDENTITY_NAME" ]]; then
            WI_IDENTITY_NAME="uami-$CLUSTER_NAME"
        fi
        print_info "Ensuring user-assigned managed identity: $WI_IDENTITY_NAME"
        UAMI=$(az identity show -g "$RESOURCE_GROUP" -n "$WI_IDENTITY_NAME" -o json 2>/dev/null || true)
        if [[ -z "$UAMI" ]]; then
            UAMI=$(az identity create -g "$RESOURCE_GROUP" -n "$WI_IDENTITY_NAME" -o json)
            print_success "Created user-assigned managed identity"
        else
            print_success "User-assigned managed identity exists"
        fi
        UAMI_CLIENT_ID=$(echo "$UAMI" | jq -r '.clientId')
        UAMI_PRINCIPAL_ID=$(echo "$UAMI" | jq -r '.principalId')
        UAMI_RESOURCE_ID=$(echo "$UAMI" | jq -r '.id')
    else
        print_info "Skipping cluster-level workload identity creation (recommended)"
        print_info "Each deployment will create its own workload identity during pipeline execution"
        UAMI_CLIENT_ID=""
        UAMI_PRINCIPAL_ID=""
        UAMI_RESOURCE_ID=""
    fi

    # Get OIDC issuer URL (always needed for cluster info)
    OIDC_ISSUER=$(az aks show -g "$RESOURCE_GROUP" -n "$CLUSTER_NAME" --query "oidcIssuerProfile.issuerUrl" -o tsv)
    if [[ -z "$OIDC_ISSUER" || "$OIDC_ISSUER" == "null" ]]; then
        print_warning "OIDC issuer not returned yet; retrying in 10s..."
        sleep 10
        OIDC_ISSUER=$(az aks show -g "$RESOURCE_GROUP" -n "$CLUSTER_NAME" --query "oidcIssuerProfile.issuerUrl" -o tsv || echo "")
    fi
    if [[ -z "$OIDC_ISSUER" ]]; then
        print_warning "Could not obtain OIDC issuer URL; workload identity will be configured during deployment"
    else
        print_success "OIDC Issuer: $OIDC_ISSUER"
        print_info "This will be used by deployments to configure their workload identities"

        # Only create federated credential if we created the cluster-level identity
        if [[ "${CREATE_CLUSTER_WORKLOAD_IDENTITY:-false}" == "true" && -n "$WI_IDENTITY_NAME" ]]; then
            FED_SUBJECT="system:serviceaccount:$WI_NAMESPACE:$WI_SERVICE_ACCOUNT"
            print_info "Configuring federated credential for subject: $FED_SUBJECT"
            # Check if federated credential exists
            FC_NAME="fc-$WI_NAMESPACE-$WI_SERVICE_ACCOUNT"
            EXISTING_FC=$(az identity federated-credential list --identity-name "$WI_IDENTITY_NAME" --resource-group "$RESOURCE_GROUP" -o json 2>/dev/null | jq -r '.[] | select(.name=="'$FC_NAME'") | .name')
            if [[ -z "$EXISTING_FC" ]]; then
                az identity federated-credential create \
                    --identity-name "$WI_IDENTITY_NAME" \
                    --resource-group "$RESOURCE_GROUP" \
                    --name "$FC_NAME" \
                    --issuer "$OIDC_ISSUER" \
                    --subject "$FED_SUBJECT" \
                    --audiences "api://AzureADTokenExchange" \
                    --output none
                print_success "Created federated credential ($FC_NAME)"
            else
                print_success "Federated credential already exists ($FC_NAME)"
            fi
        fi
    fi

    # Emit export block for CI/CD variable group update (user copies values)
    echo "" >> "$SSH_KEY_PATH.pub" # no-op keep file touched to avoid set -e early exit on empty pipeline later

    if [[ "${CREATE_CLUSTER_WORKLOAD_IDENTITY:-false}" == "true" && -n "$UAMI_CLIENT_ID" ]]; then
        echo "WORKLOAD IDENTITY DETAILS (Cluster-level - not recommended):"
        echo "  WI_IDENTITY_NAME: $WI_IDENTITY_NAME"
        echo "  UAMI_CLIENT_ID: $UAMI_CLIENT_ID"
        echo "  UAMI_PRINCIPAL_ID: $UAMI_PRINCIPAL_ID"
        echo "  UAMI_RESOURCE_ID: $UAMI_RESOURCE_ID"
        if [[ -n "$OIDC_ISSUER" ]]; then
            echo "  AKS_OIDC_ISSUER: $OIDC_ISSUER"
            echo "  FEDERATED_SUBJECT: system:serviceaccount:$WI_NAMESPACE:$WI_SERVICE_ACCOUNT"
        fi
    else
        echo "CLUSTER OIDC CONFIGURATION:"
        if [[ -n "$OIDC_ISSUER" ]]; then
            echo "  AKS_OIDC_ISSUER: $OIDC_ISSUER"
            echo "  (Save this for your deployment pipeline configuration)"
        fi
    fi
fi

# Get credentials and configure cluster (for public mode)
if [[ "$CLUSTER_MODE" != "private" ]]; then
    # Get cluster credentials
    print_info "Retrieving cluster credentials..."
    az aks get-credentials \
        --resource-group $RESOURCE_GROUP \
        --name $CLUSTER_NAME \
        --overwrite-existing \
        --output none

    # Verify cluster connectivity
    print_info "Verifying cluster connectivity..."
    if kubectl cluster-info > /dev/null 2>&1; then
        print_success "Successfully connected to cluster"

        # Display cluster info
        echo ""
        echo "Cluster Information:"
        echo "===================="
        kubectl cluster-info
        echo ""
        echo "Nodes:"
        kubectl get nodes
    else
        print_warning "Could not verify cluster connectivity"
    fi

    # Create namespaces for different environments
    print_info "Creating standard namespaces..."
    for namespace in greenlight-dev greenlight-staging greenlight-prod; do
        if kubectl get namespace $namespace > /dev/null 2>&1; then
            print_success "Namespace $namespace already exists"
        else
            kubectl create namespace $namespace
            print_success "Created namespace: $namespace"
        fi
    done

    # Install Helm if not present
    if ! command -v helm &> /dev/null; then
        print_warning "Helm not found. Installing Helm..."
        curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
        print_success "Helm installed"
    else
        print_success "Helm is already installed"
    fi

    # Add nginx ingress controller
    print_info "Setting up NGINX Ingress Controller..."
    helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx 2>/dev/null
    helm repo update 2>/dev/null

    if helm list -n ingress-nginx 2>/dev/null | grep -q ingress-nginx; then
        print_success "NGINX Ingress Controller already installed"
    else
        # Configure based on VNET integration
        if [[ -z "$SUBNET_ID" ]]; then
            # No VNET: External load balancer with Azure DNS name
            helm install ingress-nginx ingress-nginx/ingress-nginx \
                --create-namespace \
                --namespace ingress-nginx \
                --set controller.service.externalTrafficPolicy=Local \
                --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-health-probe-request-path"=/healthz \
                --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-dns-label-name"="$DNS_NAME_PREFIX-ingress" \
                > /dev/null 2>&1
            print_success "NGINX Ingress Controller installed with Azure DNS name: $DNS_NAME_PREFIX-ingress.$LOCATION.cloudapp.azure.com"
            print_info "Additional services can use path-based routing (e.g., /api, /mcp) or separate load balancer services"
        else
            # VNET integrated: Internal load balancer by default
            helm install ingress-nginx ingress-nginx/ingress-nginx \
                --create-namespace \
                --namespace ingress-nginx \
                --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-internal"="true" \
                --set controller.service.externalTrafficPolicy=Local \
                > /dev/null 2>&1
            print_success "NGINX Ingress Controller installed (internal load balancer)"
            print_info "Configure Application Gateway or Azure Front Door for external access"
        fi
    fi

    # Get ingress IP status
    if [[ -z "$SUBNET_ID" ]]; then
        print_info "Waiting for Ingress Controller external IP..."
        for i in {1..30}; do
            EXTERNAL_IP=$(kubectl get svc -n ingress-nginx ingress-nginx-controller -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null)
            if [ ! -z "$EXTERNAL_IP" ]; then
                break
            fi
            echo -n "."
            sleep 10
        done
        echo ""

        if [ ! -z "$EXTERNAL_IP" ]; then
            print_success "Ingress Controller External IP: $EXTERNAL_IP"
        else
            print_warning "External IP not yet assigned. Check later with: kubectl get svc -n ingress-nginx"
        fi
    else
        print_info "VNET-integrated cluster uses internal load balancer for ingress"
        print_info "External access requires Application Gateway or Azure Front Door configuration"
    fi
else
    # Private cluster - no direct access
    print_warning "Private cluster created. To access:"
    echo "  1. Connect from a VM within the same VNET"
    echo "  2. Or set up VPN/ExpressRoute access"
    echo "  3. Then run: az aks get-credentials --resource-group $RESOURCE_GROUP --name $CLUSTER_NAME"
fi

# Output summary for pipeline configuration
echo ""
echo "========================================="
echo "AKS Cluster Provisioning Complete!"
echo "========================================="
echo ""
echo "Cluster Details:"
echo "  Name: $CLUSTER_NAME"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Location: $LOCATION"
echo "  Cluster Mode: $CLUSTER_MODE"
echo "  Node Size: $NODE_SIZE"
echo "  Node Count: $NODE_COUNT"
if [[ -n "$SUBNET_ID" ]]; then
    echo "  VNET Integration: Yes"
fi
echo ""

if [[ "$CLUSTER_MODE" != "private" ]]; then
    echo "Add these variables to your CI/CD pipeline:"
    echo "  AKS_CLUSTER_NAME: $CLUSTER_NAME"
    echo "  AKS_RESOURCE_GROUP: $RESOURCE_GROUP"
    echo "  AKS_NAMESPACE: greenlight-dev  # or staging/prod"
    echo ""
fi

echo "For cluster access, the service principal needs:"
echo "  - Azure Kubernetes Service Cluster User Role"
echo "  - Or Azure Kubernetes Service RBAC Cluster Admin (for full access)"
echo ""

case "$CLUSTER_MODE" in
    "public")
        if [[ -n "$SUBNET_ID" ]]; then
            echo "Public cluster with VNET integration:"
            echo "  - API server is accessible from the internet"
            echo "  - Nodes are deployed in your VNET"
            echo "  - Ingress uses internal load balancer"
            echo "  - Configure Application Gateway or Azure Front Door for external access"
        else
            echo "Public cluster without VNET:"
            echo "  - API server is accessible from the internet"
            echo "  - Ingress controller uses external load balancer"
            echo "  - Direct public access to services"
        fi
        ;;
    "private")
        echo "Private cluster:"
        echo "  - API server is NOT accessible from the internet"
        echo "  - Access requires VNET connectivity (VM, VPN, or ExpressRoute)"
        echo "  - All resources are isolated within your VNET"
        ;;
esac

echo ""
echo "Next steps:"
echo "  1. Update your CI/CD variable group with the cluster details"
echo "  2. Ensure your service connection has access to this cluster"
echo "  3. Run your deployment pipeline"
echo ""
echo "Workload Identity Variables (add to variable group):"
echo "  WORKLOAD_IDENTITY_CLIENT_ID: ${UAMI_CLIENT_ID:-<set-if-created>}"
echo "  WORKLOAD_IDENTITY_RESOURCE_ID: ${UAMI_RESOURCE_ID:-<set-if-created>}"
echo "  WORKLOAD_IDENTITY_PRINCIPAL_ID: ${UAMI_PRINCIPAL_ID:-<set-if-created>}"
echo "  AKS_OIDC_ISSUER: ${OIDC_ISSUER:-<pending>}"
echo "  WORKLOAD_IDENTITY_FEDERATED_SUBJECT: system:serviceaccount:${WI_NAMESPACE}:${WI_SERVICE_ACCOUNT}"

# Emit machine-readable JSON summary for pipeline automation
SUMMARY_FILE="aks-provision-summary.json"
cat > "$SUMMARY_FILE" <<EOF
{
    "clusterName": "$CLUSTER_NAME",
    "resourceGroup": "$RESOURCE_GROUP",
    "location": "$LOCATION",
    "clusterMode": "$CLUSTER_MODE",
    "vnetIntegrated": $([ -n "$SUBNET_ID" ] && echo "true" || echo "false"),
    "workloadIdentity": {
        "clientId": "${UAMI_CLIENT_ID:-}",
        "principalId": "${UAMI_PRINCIPAL_ID:-}",
        "resourceId": "${UAMI_RESOURCE_ID:-}",
        "oidcIssuer": "${OIDC_ISSUER:-}",
        "federatedSubject": "system:serviceaccount:${WI_NAMESPACE}:${WI_SERVICE_ACCOUNT}"
    }
}
EOF
print_success "Wrote summary: $SUMMARY_FILE"
