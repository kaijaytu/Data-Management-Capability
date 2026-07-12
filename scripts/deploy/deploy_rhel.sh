#!/bin/bash
# =============================================================================
# DMC Deploy Script for RHEL 9.2
# Deploys the self-contained DMC package to target directory
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
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BUILD_OUTPUT="$PROJECT_ROOT/camtek_kaijaytu/build/output/bin"
DEPLOY_TARGET="${1:-/opt/dmc}"

echo "=== DMC Deploy Script (RHEL 9.2) ==="
echo "Source: $BUILD_OUTPUT"
echo "Target: $DEPLOY_TARGET"
echo ""

# Check build output exists
if [ ! -d "$BUILD_OUTPUT" ]; then
    echo "ERROR: Build output not found. Run build_rhel.sh first."
    exit 1
fi

# Create target directory
echo "--- Creating deployment directory ---"
mkdir -p "$DEPLOY_TARGET"

# Copy files
echo "--- Copying files ---"
cp -r "$BUILD_OUTPUT"/* "$DEPLOY_TARGET/"

# Set permissions
echo "--- Setting permissions ---"
chmod +x "$DEPLOY_TARGET/DMC"
chmod 755 "$DEPLOY_TARGET/libHardwareLogic.so"

# Set library path
echo "--- Configuring library path ---"
cat > "$DEPLOY_TARGET/run.sh" << 'EOF'
#!/bin/bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export LD_LIBRARY_PATH="$SCRIPT_DIR:$LD_LIBRARY_PATH"
exec "$SCRIPT_DIR/DMC" "$@"
EOF
chmod +x "$DEPLOY_TARGET/run.sh"

echo ""
echo "=== Deployment Complete ==="
echo ""
echo "To run the DMC Server:"
echo "  $DEPLOY_TARGET/run.sh"
echo ""
echo "Or manually:"
echo "  export LD_LIBRARY_PATH=$DEPLOY_TARGET:\$LD_LIBRARY_PATH"
echo "  $DEPLOY_TARGET/DMC"
