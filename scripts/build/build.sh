#!/bin/bash
# =============================================================================
# DMC Build Script - Auto-detect platform and dispatch
# MUST be run as regular user (NOT root).
# =============================================================================

set -e

# Reject root — build should run as regular user
if [ "$(id -u)" -eq 0 ]; then
    echo "ERROR: Do not run build as root. Use a regular user account."
    exit 1
fi

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

ARCH="$(uname -m)"

echo "=== DMC Build - Platform Detection ==="
echo "OS: $(uname -s) | Distro: $PLATFORM | Arch: $ARCH"
echo ""

case "$PLATFORM" in
    rhel|centos|rocky|alma)
        echo "Dispatching to: build_rhel.sh"
        exec "$SCRIPT_DIR/build_rhel.sh" "$@"
        ;;
    *)
        echo "WARNING: No specific build script for '$PLATFORM'."
        echo "Falling back to generic Linux build (C# only)."
        echo ""

        PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
        SRC_DIR="$PROJECT_ROOT/camtek_kaijaytu/src"
        BUILD_DIR="$PROJECT_ROOT/camtek_kaijaytu/build/linux"

        cd "$SRC_DIR"
        dotnet build -c Debug -o "$BUILD_DIR"

        echo ""
        echo "=== Build Complete ==="
        echo "Output: $BUILD_DIR"
        ;;
esac