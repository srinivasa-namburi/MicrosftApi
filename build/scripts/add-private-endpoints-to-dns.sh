#!/usr/bin/env bash
set -euo pipefail

if ! command -v az >/dev/null 2>&1; then
  echo "[dns-link] Azure CLI is required" >&2
  exit 1
fi

read -rp "Subscription ID for private endpoints (leave blank to use current context): " pe_subscription_id
if [[ -z "${pe_subscription_id}" ]]; then
  pe_subscription_id=$(az account show --query id -o tsv 2>/dev/null || true)
  if [[ -z "$pe_subscription_id" ]]; then
    echo "[dns-link] Could not determine current subscription; please provide one explicitly." >&2
    exit 1
  fi
fi

az account set --subscription "$pe_subscription_id" >/dev/null
printf '[dns-link] Using private endpoint subscription: %s\n' "$pe_subscription_id"

read -rp "Resource group containing the private endpoints: " pe_resource_group
if [[ -z "${pe_resource_group}" ]]; then
  echo "[dns-link] Resource group is required." >&2
  exit 1
fi

read -rp "Subscription ID for private DNS zones (leave blank to reuse $pe_subscription_id): " dns_subscription_id
if [[ -z "${dns_subscription_id}" ]]; then
  dns_subscription_id="$pe_subscription_id"
fi

read -rp "Resource group containing the private DNS zones: " dns_resource_group
if [[ -z "${dns_resource_group}" ]]; then
  echo "[dns-link] Private DNS zone resource group is required." >&2
  exit 1
fi

printf '[dns-link] Discovering private DNS zones in %s (subscription %s)...\n' "$dns_resource_group" "$dns_subscription_id"
zones_json=$(az network private-dns zone list -g "$dns_resource_group" --subscription "$dns_subscription_id" -o json)

zone_count=$(python3 -c "import json,sys; print(len(json.load(sys.stdin)))" <<<"$zones_json")
if [[ "$zone_count" -eq 0 ]]; then
  echo "[dns-link] No private DNS zones found. Nothing to link." >&2
  exit 0
fi

declare -A expected_zones=(
  [sqlServer]="privatelink.database.windows.net"
  [blob]="privatelink.blob.core.windows.net"
  [table]="privatelink.table.core.windows.net"
  [queue]="privatelink.queue.core.windows.net"
  [searchService]="privatelink.search.windows.net"
  [namespace]="privatelink.servicebus.windows.net"
)

declare -A zone_ids
for key in "${!expected_zones[@]}"; do
  zone_name="${expected_zones[$key]}"
  zone_id=$(python3 -c "import json,sys; zones=json.load(sys.stdin); name=sys.argv[1]; match=[z['id'] for z in zones if z.get('name')==name]; print(match[0] if match else '')" <<<"$zones_json" "$zone_name")
  if [[ -n "$zone_id" ]]; then
    zone_ids[$key]="$zone_id"
    printf '[dns-link] Found private DNS zone for %s: %s\n' "$key" "$zone_name"
  else
    printf '[dns-link] No private DNS zone named %s found; links for %s will be skipped.\n' "$zone_name" "$key"
  fi
done

echo "[dns-link] Discovering private endpoints in $pe_resource_group..."
pe_json=$(az network private-endpoint list -g "$pe_resource_group" --subscription "$pe_subscription_id" -o json)

if [[ $(python3 -c "import json,sys; print(len(json.load(sys.stdin)))" <<<"$pe_json") -eq 0 ]]; then
  echo "[dns-link] No private endpoints found in resource group $pe_resource_group"
  exit 0
fi

zone_entries=()
for key in "${!zone_ids[@]}"; do
  zone_entries+=("\"$key\":\"${zone_ids[$key]}\"")
done

if [[ ${#zone_entries[@]} -gt 0 ]]; then
  zone_map_json="{"$(IFS=,; echo "${zone_entries[*]}")"}"
else
  zone_map_json="{}"
fi

python3 - <<'PY' "$pe_json" "$pe_resource_group" "${pe_subscription_id}" "$zone_map_json"
import json
import subprocess
import sys

pe_data = json.loads(sys.argv[1])
resource_group = sys.argv[2]
subscription = sys.argv[3]
zone_map = json.loads(sys.argv[4])

link_total = 0
skip_total = 0

expected = {
    "sqlServer": "privatelink.database.windows.net",
    "blob": "privatelink.blob.core.windows.net",
    "table": "privatelink.table.core.windows.net",
    "queue": "privatelink.queue.core.windows.net",
    "searchService": "privatelink.search.windows.net",
    "namespace": "privatelink.servicebus.windows.net",
}

def link_zone(pe_name: str, group_id: str, zone_id: str) -> bool:
    zone_name = zone_id.split("/")[-1]
    group_label = zone_name.replace('.', '-').replace('_', '-')
    group_name = f"manual-{group_id}-{group_label}"

    list_cmd = [
        "az", "network", "private-endpoint", "dns-zone-group", "list",
        "--resource-group", resource_group,
        "--endpoint-name", pe_name,
        "--subscription", subscription,
        "-o", "json",
    ]
    existing = json.loads(subprocess.run(list_cmd, check=False, capture_output=True, text=True).stdout or "[]")
    for zone_group in existing:
        for config in zone_group.get("privateDnsZoneConfigs", []):
            if config.get("privateDnsZoneId", "").lower() == zone_id.lower():
                print(f"[dns-link] Private endpoint {pe_name}: zone {zone_name} already linked via {zone_group['name']}")
                return False

    create_cmd = [
        "az", "network", "private-endpoint", "dns-zone-group", "create",
        "--name", group_name,
        "--resource-group", resource_group,
        "--endpoint-name", pe_name,
        "--private-dns-zone", zone_id,
        "--zone-name", zone_name,
        "--subscription", subscription,
        "--only-show-errors",
    ]
    result = subprocess.run(create_cmd, check=False, text=True, capture_output=True)
    if result.returncode != 0:
        sys.stderr.write(f"[dns-link] WARNING: Failed to link {zone_name} for {pe_name}: {result.stderr}\n")
        return False

    print(f"[dns-link] Linked {pe_name} -> {zone_name} via zone group {group_name}")
    return True

for endpoint in pe_data:
    pe_name = endpoint.get("name")
    connections = endpoint.get("privateLinkServiceConnections", [])
    if not connections:
        print(f"[dns-link] Skipping {pe_name}: no service connections found")
        skip_total += 1
        continue

    group_ids = connections[0].get("groupIds", [])
    if not group_ids:
        print(f"[dns-link] Skipping {pe_name}: no groupIds present")
        skip_total += 1
        continue

    group_id = group_ids[0]
    zone_id = zone_map.get(group_id)
    if not zone_id:
        expected_name = expected.get(group_id, group_id)
        print(f"[dns-link] Skipping {pe_name}: no DNS zone configured for group {group_id} ({expected_name})")
        skip_total += 1
        continue

    if link_zone(pe_name, group_id, zone_id):
        link_total += 1
    else:
        skip_total += 1

print(f"[dns-link] Summary: created {link_total} DNS zone group(s); skipped {skip_total} endpoint(s)")
PY
