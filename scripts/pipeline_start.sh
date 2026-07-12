#!/bin/bash
# =============================================================================
# DMC Full Pipeline: Build + Deploy
# Handles user/root switching automatically.
# Run as regular user — script will prompt for root password when deploying.
# =============================================================================

set -e

# Reject root — build must run as regular user
if [ "$(id -u)" -eq 0 ]; then
    echo "ERROR: Run this script as a regular user, not root."
    echo "The script will prompt for root password when deploying."
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "=== DMC Full Pipeline: Build + Deploy ==="
echo "Project: $PROJECT_ROOT"
echo ""

# Step 1: Build (as user)
echo "=========================================="
echo "  Step 1/3: Build (user: $(whoami))"
echo "=========================================="
"$PROJECT_ROOT/scripts/build/build.sh"

echo ""

# Step 2: Deploy (as root)
echo "=========================================="
echo "  Step 2/3: Deploy (requires root)"
echo "=========================================="
su -c "cd '$PROJECT_ROOT' && '$PROJECT_ROOT/scripts/deploy/deploy.sh'" root

echo ""

# Step 3: Run (as user)
echo "=========================================="
echo "  Step 3/3: Launch DMC Server"
echo "=========================================="
DEPLOY_TARGET="${1:-/opt/dmc}"
"$DEPLOY_TARGET/run.sh"
