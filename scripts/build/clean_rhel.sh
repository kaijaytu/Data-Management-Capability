#!/bin/bash
# =============================================================================
# DMC Clean Build Artifacts
# Removes all generated files from build process, keeping source code intact.
# MUST be run as regular user (NOT root).
# =============================================================================

set -e

# Reject root — clean should run as regular user
if [ "$(id -u)" -eq 0 ]; then
    echo "ERROR: Do not run clean as root. Use a regular user account."
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BUILD_DIR="$PROJECT_ROOT/camtek_kaijaytu/build"
SRC_DIR="$PROJECT_ROOT/camtek_kaijaytu/src"

echo "=== DMC Clean Build Artifacts ==="
echo ""

# Clean C++ build artifacts
if [ -d "$BUILD_DIR/hardware" ]; then
    echo "  [CLEAN] C++ build: $BUILD_DIR/hardware/"
    rm -rf "$BUILD_DIR/hardware"
fi

# Clean C# build artifacts
if [ -d "$BUILD_DIR/output" ]; then
    echo "  [CLEAN] Output: $BUILD_DIR/output/"
    rm -rf "$BUILD_DIR/output"
fi

# Clean dotnet intermediate files
for dir in bin obj; do
    if [ -d "$SRC_DIR/$dir" ]; then
        echo "  [CLEAN] dotnet $dir: $SRC_DIR/$dir/"
        rm -rf "$SRC_DIR/$dir"
    fi
done

# Clean any NuGet caches for this project
NUGET_HTTP="$SRC_DIR/.nuget"
if [ -d "$NUGET_HTTP" ]; then
    echo "  [CLEAN] NuGet cache: $NUGET_HTTP/"
    rm -rf "$NUGET_HTTP"
fi

echo ""
echo "=== Clean Complete ==="
echo "Source code is untouched. Run build_rhel.sh to rebuild."
