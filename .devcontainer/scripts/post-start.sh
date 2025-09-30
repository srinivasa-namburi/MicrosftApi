#!/usr/bin/env bash
# Use simpler flags to allow fallback execution environments
set -eu

echo "[post-start] Trusting dev HTTPS certificate (idempotent)"
 dotnet dev-certs https --trust >/dev/null 2>&1 || true

# Normalize ownership of workspace files if they were previously seeded as root (legacy onCreateCommand behavior)
if command -v sudo >/dev/null 2>&1; then
  # Quick heuristic: look for root-owned marker file created by old seeding path
  if sudo test -f /workspaces/aismr/.devcontainer/.seed-complete; then
    # Count root-owned files (limit depth for speed first)
    ROOT_OWNED_COUNT=$(sudo find /workspaces/aismr -maxdepth 4 -user root -type f 2>/dev/null | head -n 200 | wc -l || echo 0)
    if [ "${ROOT_OWNED_COUNT}" -gt 0 ]; then
      echo "[post-start] Detected ${ROOT_OWNED_COUNT} root-owned files from legacy seed; normalizing ownership to vscode:vscode (this may take a moment)"
      sudo chown -R vscode:vscode /workspaces/aismr 2>/dev/null || true
    else
      echo "[post-start] No root-owned files detected needing ownership normalization"
    fi
  fi
fi

# Ensure jq is available (needed for setup scripts)
if ! command -v jq >/dev/null 2>&1; then
  echo "[post-start] Installing jq for JSON processing..."
  sudo apt-get update -qq >/dev/null 2>&1 || true
  sudo apt-get install -y jq >/dev/null 2>&1 || true
  if command -v jq >/dev/null 2>&1; then
    echo "[post-start] jq installed successfully"
  else
    echo "[post-start] WARNING: Failed to install jq - some setup scripts may not work"
  fi
else
  echo "[post-start] jq is available"
fi

###############################################
# Optional developer config directories (.claude / .codex)
# We no longer bind-mount these from the host (they may not exist there).
# Create container-local fallbacks with wide perms so user tools can write.
###############################################
ensure_local_opt_dir() {
  local dir="$1"
  if [[ ! -d "$dir" ]]; then
    mkdir -p "$dir" 2>/dev/null || true
    # Need execute bit for directory traversal; ugo+rw per request -> 0777
    chmod 0777 "$dir" 2>/dev/null || true
    echo "[post-start] Created local optional dir: $dir (0777)"
  fi
}

ensure_local_opt_dir "/home/vscode/.claude"
ensure_local_opt_dir "/home/vscode/.codex"
ensure_local_opt_dir "/home/vscode/.vscode"

# Report whether these are bind mounts (host) or container-local fallbacks.
report_mount_origin() {
  local path="$1"; local name="$2";
  if mountpoint -q "$path" 2>/dev/null; then
    # mountpoint returns true for any distinct mount; bind mounts qualify
    echo "[post-start] $name appears to be a host bind mount (mountpoint detected)"
  else
    echo "[post-start] $name is using container-local fallback directory"
  fi
}

report_mount_origin "/home/vscode/.claude" ".claude"
report_mount_origin "/home/vscode/.codex" ".codex"

###############################################
# Legacy volume pruning (retain host-specific volumes)
###############################################
LEGACY_VOLUMES=(
  "pvico-sql-docgen-vol"          # pre host-suffix SQL volume
  "pvico-pgsql-kmvectordb-vol"    # pre host-suffix Postgres volume
)

running_sql_container=$(docker ps --filter name=sqldocgen -q || true)

containers_using_volume() {
  local vol="$1"
  docker ps -a --filter volume="$vol" -q 2>/dev/null || true
}

prune_volume() {
  local vol="$1"
  if docker volume ls -q | grep -q "^${vol}$"; then
    local attached
    attached=$(containers_using_volume "$vol")
    if [[ -n "$attached" ]]; then
      echo "[post-start] Skipping legacy volume $vol (still referenced)"
    else
      echo "[post-start] Removing unreferenced legacy volume: $vol"
      docker volume rm -f "$vol" >/dev/null 2>&1 || true
    fi
  fi
}

for v in "${LEGACY_VOLUMES[@]}"; do prune_volume "$v"; done

# Initialize sqlPassword user secret if missing.
APPHOST_PROJ="/workspaces/aismr/src/Microsoft.Greenlight.AppHost/Microsoft.Greenlight.AppHost.csproj"
current_secret=$(dotnet user-secrets list --project "$APPHOST_PROJ" 2>/dev/null | grep '^Parameters:sqlPassword' || true)
if [[ -z "$current_secret" ]]; then
  # Prefer devcontainer provided env value; else create a strong default
  new_value="${Parameters__sqlPassword:-DevPassword123!}"
  echo "[post-start] Seeding missing user secret Parameters:sqlPassword (length=${#new_value})"
  dotnet user-secrets set Parameters:sqlPassword "$new_value" --project "$APPHOST_PROJ" >/dev/null 2>&1 || true
else
  # Mask actual length to avoid leaking; show that secret exists
  secret_len=$(echo "$current_secret" | sed -E 's/Parameters:sqlPassword\s*=\s*(.*)/\1/' | awk '{ print length }')
  echo "[post-start] Existing user secret Parameters:sqlPassword present (length=${secret_len})"
fi

# Initialize postgresPassword user secret if missing.
pg_secret=$(dotnet user-secrets list --project "$APPHOST_PROJ" 2>/dev/null | grep '^Parameters:postgresPassword' || true)
if [[ -z "$pg_secret" ]]; then
  pg_new_value="${Parameters__postgresPassword:-DevPgPassword123!}"
  echo "[post-start] Seeding missing user secret Parameters:postgresPassword (length=${#pg_new_value})"
  dotnet user-secrets set Parameters:postgresPassword "$pg_new_value" --project "$APPHOST_PROJ" >/dev/null 2>&1 || true
else
  pg_len=$(echo "$pg_secret" | sed -E 's/Parameters:postgresPassword\s*=\s*(.*)/\1/' | awk '{ print length }')
  echo "[post-start] Existing user secret Parameters:postgresPassword present (length=${pg_len})"
fi

# Surface environment parameter lengths for reference
if [ -n "${Parameters__sqlPassword:-}" ]; then
  echo "[post-start] Env Parameters__sqlPassword length: ${#Parameters__sqlPassword}"
else
  echo "[post-start] Env Parameters__sqlPassword length: 0 (unset)"
fi
if [ -n "${Parameters__postgresPassword:-}" ]; then
  echo "[post-start] Env Parameters__postgresPassword length: ${#Parameters__postgresPassword}"
else
  echo "[post-start] Env Parameters__postgresPassword length: 0 (unset)"
fi

# Install development tools if available
if ! command -v claude >/dev/null 2>&1; then
  # Install via npm (assuming Node.js is available from devcontainer features)
  if command -v npm >/dev/null 2>&1; then
    npm install -g @anthropic/claude-code >/dev/null 2>&1 || true
  fi
fi

# Silently check for user configurations without reporting
# This ensures personal development tools work if configured
[[ -d "/home/vscode/.claude" ]] >/dev/null 2>&1 || true
[[ -f "/home/vscode/.vscode/settings.json" ]] >/dev/null 2>&1 || true
[[ -f "/home/vscode/.config/Code/User/settings.json" ]] >/dev/null 2>&1 || true

echo "[post-start] Complete"
