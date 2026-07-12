#!/bin/bash
# =============================================================================
# DMC Full Pipeline: Install Environment
# Sets up all system dependencies.
# MUST be run as root.
# =============================================================================

set -e

if [ "$(id -u)" -ne 0 ]; then
    echo "ERROR: This script must be run as root."
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "=== DMC Environment Install ==="
"$PROJECT_ROOT/scripts/setup/install_deps_rhel.sh"
