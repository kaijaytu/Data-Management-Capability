#!/bin/bash
# =============================================================================
# DMC Full Pipeline: Uninstall Environment
# Removes all dependencies installed by this project.
# MUST be run as root.
# =============================================================================

set -e

if [ "$(id -u)" -ne 0 ]; then
    echo "ERROR: This script must be run as root."
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "=== DMC Environment Uninstall ==="
"$PROJECT_ROOT/scripts/setup/uninstall_deps_rhel.sh"
