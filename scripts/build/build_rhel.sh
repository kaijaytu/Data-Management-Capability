#!/bin/bash
# =============================================================================
# DMC Build Script for RHEL 9.2 (linux-x64)
# Builds both C++ HardwareLogic and C# DMCCore as self-contained package
# MUST be run as regular user (NOT root).
# =============================================================================

set -e

# Reject root — build should run as regular user
if [ "$(id -u)" -eq 0 ]; then
    echo "ERROR: Do not run build as root. Use a regular user account."
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SRC_DIR="$PROJECT_ROOT/camtek_kaijaytu/src"
BUILD_DIR="$PROJECT_ROOT/camtek_kaijaytu/build"

echo "=== DMC Build Script (RHEL 9.2 / linux-x64) ==="
echo "Project root: $PROJECT_ROOT"
echo ""

# -----------------------------------------------------------------------------
# Step 1: Build C++ HardwareLogic (.so)
# -----------------------------------------------------------------------------
echo "--- Building C++ HardwareLogic ---"

HW_BUILD_DIR="$BUILD_DIR/hardware"
mkdir -p "$HW_BUILD_DIR"
cd "$HW_BUILD_DIR"

cmake "$SRC_DIR/HardwareLogic" \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_INSTALL_PREFIX="$BUILD_DIR/output"

cmake --build . --config Release
cmake --install .

echo "HardwareLogic built: $BUILD_DIR/output/lib/libHardwareLogic.so"
echo ""

# -----------------------------------------------------------------------------
# Step 2: Build C# DMCCore (self-contained, linux-x64)
# -----------------------------------------------------------------------------
echo "--- Building C# DMC Server (self-contained) ---"

cd "$SRC_DIR"

dotnet publish -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -o "$BUILD_DIR/output/bin"

echo "DMC Server built: $BUILD_DIR/output/bin/DMC"
echo ""

# -----------------------------------------------------------------------------
# Step 3: Copy native library alongside the executable
# -----------------------------------------------------------------------------
echo "--- Packaging native libraries ---"

cp "$BUILD_DIR/output/lib/libHardwareLogic.so" "$BUILD_DIR/output/bin/"

echo "Package complete at: $BUILD_DIR/output/bin/"
echo ""

# -----------------------------------------------------------------------------
# Summary
# -----------------------------------------------------------------------------
echo "=== Build Complete ==="
echo "Output directory: $BUILD_DIR/output/bin/"
echo "Files:"
ls -la "$BUILD_DIR/output/bin/"
