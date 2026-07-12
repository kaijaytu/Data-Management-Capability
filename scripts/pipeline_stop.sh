#!/bin/bash
# =============================================================================
# DMC Full Pipeline: Undeploy + Clean
# Handles user/root switching automatically.
# Run as regular user — script will prompt for root password when undeploying.
# =============================================================================

set -e

# Reject root — clean must run as regular user
if [ "$(id -u)" -eq 0 ]; then
    echo "ERROR: Run this script as a regular user, not root."
    echo "The script will prompt for root password when undeploying."
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "=== DMC Full Pipeline: Undeploy + Clean ==="
echo "Project: $PROJECT_ROOT"
echo ""

# Step 1: Undeploy (as root)
echo "=========================================="
echo "  Step 1/2: Undeploy (requires root)"
echo "=========================================="
su -c "cd '$PROJECT_ROOT' && '$PROJECT_ROOT/scripts/deploy/undeploy_rhel.sh'" root

echo ""

# Step 2: Clean build artifacts (as user)
echo "=========================================="
echo "  Step 2/2: Clean build artifacts (user: $(whoami))"
echo "=========================================="
"$PROJECT_ROOT/scripts/build/clean_rhel.sh"

echo ""
echo "=== Pipeline Complete: System cleaned ==="
