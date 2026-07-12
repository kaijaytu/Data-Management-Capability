#!/bin/bash
# =============================================================================
# DMC Environment Setup - Install Dependencies for RHEL 9.2
# Records everything installed so it can be cleanly uninstalled later.
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

echo "=== DMC Environment Setup (RHEL 9.2) ==="
echo "Install manifest: $INSTALL_MANIFEST"
echo ""

# Initialize manifest
echo "# DMC Install Manifest - $(date)" > "$INSTALL_MANIFEST"
echo "# This file tracks all changes made by install_deps_rhel.sh" >> "$INSTALL_MANIFEST"
echo "# Used by uninstall_deps_rhel.sh to restore original state" >> "$INSTALL_MANIFEST"
echo "" >> "$INSTALL_MANIFEST"

# -----------------------------------------------------------------------------
# Step 1: Record pre-existing packages
# -----------------------------------------------------------------------------
echo "--- Checking pre-existing packages ---"

check_and_install_rpm() {
    local pkg="$1"
    if rpm -q "$pkg" &>/dev/null; then
        echo "  [SKIP] $pkg already installed (pre-existing, will NOT be removed)"
        echo "PRE_EXISTING_RPM=$pkg" >> "$INSTALL_MANIFEST"
    else
        echo "  [INSTALL] $pkg"
        dnf install -y "$pkg"
        echo "INSTALLED_RPM=$pkg" >> "$INSTALL_MANIFEST"
    fi
}

# Microsoft repo
if [ -f /etc/yum.repos.d/microsoft-prod.repo ]; then
    echo "  [SKIP] Microsoft repo already configured"
    echo "PRE_EXISTING_REPO=microsoft-prod" >> "$INSTALL_MANIFEST"
else
    echo "  [INSTALL] Microsoft repo"
    rpm -Uvh https://packages.microsoft.com/config/rhel/9.0/packages-microsoft-prod.rpm
    echo "INSTALLED_REPO=microsoft-prod" >> "$INSTALL_MANIFEST"
fi

echo ""

# -----------------------------------------------------------------------------
# Step 2: Install required packages
# -----------------------------------------------------------------------------
echo "--- Installing required packages ---"

check_and_install_rpm "dotnet-sdk-8.0"
check_and_install_rpm "cmake"
check_and_install_rpm "gcc-c++"
check_and_install_rpm "make"

echo ""

# -----------------------------------------------------------------------------
# Step 3: Verify installations
# -----------------------------------------------------------------------------
echo "--- Verifying installations ---"

echo -n "  dotnet: "; dotnet --version 2>/dev/null || echo "NOT FOUND"
echo -n "  cmake:  "; cmake --version 2>/dev/null | head -1 || echo "NOT FOUND"
echo -n "  g++:    "; g++ --version 2>/dev/null | head -1 || echo "NOT FOUND"

echo ""
echo "=== Environment Setup Complete ==="
echo ""
echo "Manifest saved to: $INSTALL_MANIFEST"
echo "To undo: ./scripts/setup/uninstall_deps_rhel.sh"
