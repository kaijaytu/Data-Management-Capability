#!/bin/bash
# =============================================================================
# DMC Undeploy Script for RHEL 9.2
# Removes deployed files from target directory.
# MUST be run as root.
# =============================================================================

set -e

# Verify running as root
if [ "$(id -u)" -ne 0 ]; then
    echo "ERROR: This script must be run as root."
    echo "Usage: su - root -c '$(realpath "$0") [target_dir]'"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_TARGET="${1:-/opt/dmc}"

echo "=== DMC Undeploy Script (RHEL 9.2) ==="
echo "Target: $DEPLOY_TARGET"
echo ""

if [ ! -d "$DEPLOY_TARGET" ]; then
    echo "Deployment directory does not exist. Nothing to do."
    exit 0
fi

# List what will be removed
echo "--- Files to be removed ---"
ls -la "$DEPLOY_TARGET/"
echo ""

# Confirm
read -p "Remove all files in $DEPLOY_TARGET? [y/N] " confirm
if [[ "$confirm" != "y" && "$confirm" != "Y" ]]; then
    echo "Cancelled."
    exit 0
fi

# Remove deployed files
echo "--- Removing deployed files ---"
rm -rf "$DEPLOY_TARGET"

echo ""
echo "=== Undeploy Complete ==="
echo "Deployment directory $DEPLOY_TARGET has been removed."
