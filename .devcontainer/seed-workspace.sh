#!/bin/sh
# NOTE: Ensure this file is executable inside the container (chmod +x .devcontainer/seed-workspace.sh)
set -eu

# seed-workspace.sh
# Purpose: Populate the named volume mounted at /workspaces/aismr with the host
# source tree (mounted read‑only at /workspaces/_host-src) on first container create.
# Safe to re-run; skips existing files to avoid clobbering container-local changes.

SRC_ROOT="/workspaces/_host-src"
DEST_ROOT="/workspaces/aismr"

echo "[seed-workspace] Source (ro) : ${SRC_ROOT}"
echo "[seed-workspace] Dest   (rw) : ${DEST_ROOT}"

# Ensure destination root exists (handle prior root-owned volume initialization)
if [ ! -d "${DEST_ROOT}" ]; then
  if ! mkdir -p "${DEST_ROOT}" 2>/dev/null; then
    if command -v sudo >/dev/null 2>&1; then
      echo "[seed-workspace] Creating dest directory with sudo (previously root-owned volume)"
      sudo mkdir -p "${DEST_ROOT}" || true
    fi
  fi
fi

# If directory is not writable by current user, attempt ownership fix (only if empty or explicitly allowed)
if [ ! -w "${DEST_ROOT}" ]; then
  # Detect if directory is effectively empty (ignore lost+found on some volume types)
  NON_IGNORED_COUNT=$(find "${DEST_ROOT}" -mindepth 1 -maxdepth 1 ! -name 'lost+found' 2>/dev/null | head -n 1 | wc -l | tr -d ' ')
  if command -v sudo >/dev/null 2>&1; then
    if [ "${NON_IGNORED_COUNT}" = "0" ]; then
      echo "[seed-workspace] Taking ownership of empty root-owned destination via sudo chown"
      sudo chown -R $(id -u):$(id -g) "${DEST_ROOT}" 2>/dev/null || true
    else
      echo "[seed-workspace] WARNING: Destination not writable and not empty; attempting recursive sudo chown (may be from earlier root seeding)"
      sudo chown -R $(id -u):$(id -g) "${DEST_ROOT}" 2>/dev/null || true
    fi
  else
    echo "[seed-workspace] WARNING: Destination not writable and sudo unavailable; copy will likely fail" >&2
  fi
fi

if [ ! -d "${SRC_ROOT}" ]; then
  echo "[seed-workspace] ERROR: Source host mount not present; aborting seed." >&2
  exit 0
fi

# Simple marker file to detect prior completion
MARKER="${DEST_ROOT}/.devcontainer/.seed-complete"
if [ -f "${MARKER}" ]; then
  echo "[seed-workspace] Seed already completed (marker present). Skipping copy."
  exit 0
fi

echo "[seed-workspace] Performing initial copy (first run) ..."
# Use rsync if available for efficiency; fall back to tar | tar to preserve perms
if command -v rsync >/dev/null 2>&1; then
  # Exclusions:
  #  - .git        : git metadata not needed inside isolated volume
  #  - logs        : transient logs
  #  - node_modules: avoid copying host-installed packages (container should restore its own)
  #  - .vs         : IDE indexes / ephemeral state (often locked / permission restricted)
  #  - bin/obj     : build outputs (will be rebuilt inside container)
  rsync -a --no-owner --no-group \
    --exclude '.git/' \
    --exclude 'logs/' \
    --exclude 'node_modules/' \
    --exclude '.vs/' \
    --exclude '*/.vs/' \
    --exclude 'bin/' \
    --exclude 'obj/' \
    --exclude '*/bin/' \
    --exclude '*/obj/' \
    "${SRC_ROOT}/" "${DEST_ROOT}/"
else
  # Fallback tar pipeline with basic excludes (may not perfectly mirror rsync patterns)
  (cd "${SRC_ROOT}" && tar -cf - . \
      --exclude .git \
      --exclude logs \
      --exclude node_modules \
      --exclude .vs \
      --exclude bin \
      --exclude obj ) | (cd "${DEST_ROOT}" && tar -xf -)
  # Tar preserves numeric ownership; fix up if we are root and vscode user exists.
  if [ "$(id -u)" -eq 0 ] && id vscode >/dev/null 2>&1; then
    echo "[seed-workspace] Adjusting ownership to vscode:vscode after tar copy"
    chown -R vscode:vscode "${DEST_ROOT}" || true
  fi
fi

# Ensure .devcontainer/scripts is present
if [ ! -d "${DEST_ROOT}/.devcontainer/scripts" ] && [ -d "${SRC_ROOT}/.devcontainer/scripts" ]; then
  mkdir -p "${DEST_ROOT}/.devcontainer/scripts"
  cp -r "${SRC_ROOT}/.devcontainer/scripts" "${DEST_ROOT}/.devcontainer/"
fi

mkdir -p "${DEST_ROOT}/.devcontainer" || true
# Record abbreviated git ref & timestamp for traceability if git metadata exists in source mount
if [ -d "${SRC_ROOT}/.git" ]; then
  (cd "${SRC_ROOT}" && git rev-parse --short HEAD 2>/dev/null) > "${DEST_ROOT}/.devcontainer/.seed-git-ref" || true
fi

date -u > "${MARKER}" || true

# Final safety: if running as root (should not happen now that we use postCreateCommand) ensure ownership.
if [ "$(id -u)" -eq 0 ] && id vscode >/dev/null 2>&1; then
  echo "[seed-workspace] Final ownership normalization (root -> vscode)"
  chown -R vscode:vscode "${DEST_ROOT}" || true
fi

# Normalize line endings for shell scripts inside the devcontainer directory (Windows checkouts -> LF)
if command -v sed >/dev/null 2>&1; then
  for script in $(find "${DEST_ROOT}/.devcontainer" -maxdepth 3 -type f -name '*.sh' 2>/dev/null); do
    if grep -q $'\r' "$script" 2>/dev/null; then
      echo "[seed-workspace] Normalizing CRLF -> LF: ${script#${DEST_ROOT}/}"
      # Use sed to strip trailing CRs; write in-place
      sed -i 's/\r$//' "$script" 2>/dev/null || true
      chmod +x "$script" 2>/dev/null || true
    fi
  done
fi

echo "[seed-workspace] Copy complete. Marker written."
exit 0
