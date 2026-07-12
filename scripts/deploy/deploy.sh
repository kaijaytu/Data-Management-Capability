#!/bin/bash
# =============================================================================
# DMC Deploy Script - Auto-detect platform and dispatch
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Detect platform
if [ -f /etc/redhat-release ]; then
    PLATFORM="rhel"
elif [ -f /etc/os-release ]; then
    . /etc/os-release
    PLATFORM="$ID"
else
    PLATFORM="unknown"
fi

echo "=== DMC Deploy - Platform Detection ==="
echo "OS: $(uname -s) | Distro: $PLATFORM"
echo ""

case "$PLATFORM" in
    rhel|centos|rocky|alma)
        echo "Dispatching to: deploy_rhel.sh"
        exec "$SCRIPT_DIR/deploy_rhel.sh" "$@"
        ;;
    *)
        echo "ERROR: No deploy script for platform '$PLATFORM'."
        echo "Supported platforms: RHEL, CentOS, Rocky, AlmaLinux"
        exit 1
        ;;
esac