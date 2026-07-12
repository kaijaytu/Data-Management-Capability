#!/bin/bash
# =============================================================================
# DMC Environment Teardown - Uninstall Dependencies for RHEL 9.2
# Reads the install manifest and removes ONLY what was installed by this project.
# Pre-existing packages are left untouched.
# MUST be run as root.
# =============================================================================

set -e

# Verify running as root
if [ "$(id -u)" -ne 0 ]; then
    echo "ERROR: This script must be run as root."
    echo "Usage: su - root -c '$(realpath "$0")'"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
INSTALL_MANIFEST="$PROJECT_ROOT/.dmc_install_manifest"

echo "=== DMC Environment Teardown (RHEL 9.2) ==="
echo ""

# Check manifest exists
if [ ! -f "$INSTALL_MANIFEST" ]; then
    echo "ERROR: Install manifest not found at $INSTALL_MANIFEST"
    echo "Cannot determine what was installed. Nothing to do."
    exit 1
fi

echo "Reading manifest: $INSTALL_MANIFEST"
echo ""

# -----------------------------------------------------------------------------
# Step 1: Remove installed RPM packages (NOT pre-existing ones)
# -----------------------------------------------------------------------------
echo "--- Removing installed packages ---"

REMOVED_COUNT=0
while IFS='=' read -r key value; do
    # Skip comments and empty lines
    [[ "$key" =~ ^#.*$ ]] && continue
    [[ -z "$key" ]] && continue

    case "$key" in
        INSTALLED_RPM)
            echo "  [REMOVE] $value"
            dnf remove -y "$value" 2>/dev/null || echo "  [WARN] Failed to remove $value"
            REMOVED_COUNT=$((REMOVED_COUNT + 1))
            ;;
        PRE_EXISTING_RPM)
            echo "  [KEEP] $value (was pre-existing)"
            ;;
        INSTALLED_REPO)
            echo "  [REMOVE REPO] $value"
            rm -f /etc/yum.repos.d/microsoft-prod.repo
            rpm -e packages-microsoft-prod 2>/dev/null || echo "  [WARN] Failed to remove microsoft-prod RPM"
            REMOVED_COUNT=$((REMOVED_COUNT + 1))
            ;;
        PRE_EXISTING_REPO)
            echo "  [KEEP REPO] $value (was pre-existing)"
            ;;
    esac
done < "$INSTALL_MANIFEST"

echo ""

# -----------------------------------------------------------------------------
# Step 2: Clean up dnf cache
# -----------------------------------------------------------------------------
echo "--- Cleaning package cache ---"
dnf clean all 2>/dev/null || true

# -----------------------------------------------------------------------------
# Step 3: Remove manifest file
# -----------------------------------------------------------------------------
echo "--- Removing install manifest ---"
rm -f "$INSTALL_MANIFEST"

echo ""
echo "=== Environment Teardown Complete ==="
echo "Removed $REMOVED_COUNT package(s). Pre-existing packages were not touched."
