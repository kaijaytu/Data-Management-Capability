#!/bin/bash
# =============================================================================
# DMC Test Script
# Runs C++ fault injection tests and C# concurrency stress tests.
# Prerequisites: pipeline_start.sh (or build_rhel.sh) must have been run first.
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
TESTS_DIR="$PROJECT_ROOT/camtek_kaijaytu/tests"
BUILD_LIB="$PROJECT_ROOT/camtek_kaijaytu/build/output/lib"

echo "=== DMC Test Suite ==="
echo "Project root: $PROJECT_ROOT"
echo ""

# Verify build output exists
if [ ! -f "$BUILD_LIB/libHardwareLogic.so" ]; then
    echo "ERROR: libHardwareLogic.so not found. Run the build pipeline first."
    exit 1
fi

# ─────────────────────────────────────────────────────────────────────────────
# Step 1: C++ Fault Injection Tests
# ─────────────────────────────────────────────────────────────────────────────
echo "==========================================="
echo "  Step 1: C++ Fault Injection Tests"
echo "==========================================="

cd "$TESTS_DIR"

g++ -o test_main test_main.cpp \
    -I"$PROJECT_ROOT/camtek_kaijaytu/src/HardwareLogic/include" \
    -L"$BUILD_LIB" \
    -lHardwareLogic \
    -Wl,-rpath,"$BUILD_LIB" \
    -pthread

echo "Compiled: test_main"
echo ""

export LD_LIBRARY_PATH="$BUILD_LIB:$LD_LIBRARY_PATH"
./test_main
CPP_EXIT=$?

echo ""

# ─────────────────────────────────────────────────────────────────────────────
# Step 2: C# Concurrency Stress Tests
# ─────────────────────────────────────────────────────────────────────────────
echo "==========================================="
echo "  Step 2: C# Concurrency Stress Tests"
echo "==========================================="

cd "$TESTS_DIR"
dotnet build --nologo -q

echo ""
dotnet run --no-build
CSHARP_EXIT=$?

echo ""

# ─────────────────────────────────────────────────────────────────────────────
# Summary
# ─────────────────────────────────────────────────────────────────────────────
echo "==========================================="
echo "  Test Summary"
echo "==========================================="

if [ $CPP_EXIT -eq 0 ] && [ $CSHARP_EXIT -eq 0 ]; then
    echo "  ALL TESTS PASSED"
    exit 0
else
    [ $CPP_EXIT -ne 0 ] && echo "  [FAIL] C++ Fault Injection Tests (exit code: $CPP_EXIT)"
    [ $CSHARP_EXIT -ne 0 ] && echo "  [FAIL] C# Concurrency Tests (exit code: $CSHARP_EXIT)"
    exit 1
fi
