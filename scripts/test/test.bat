@echo off
REM =============================================================================
REM DMC Test Script (Windows)
REM Runs C# concurrency stress tests.
REM Prerequisites: libHardwareLogic must be built and accessible.
REM =============================================================================

setlocal

set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%..\..
set TESTS_DIR=%PROJECT_ROOT%\camtek_kaijaytu\tests
set BUILD_LIB=%PROJECT_ROOT%\camtek_kaijaytu\build\output\lib

echo === DMC Test Suite (Windows) ===
echo.

REM ─────────────────────────────────────────────────────────────────────────────
REM C# Concurrency Stress Tests
REM ─────────────────────────────────────────────────────────────────────────────
echo ===========================================
echo   C# Concurrency Stress Tests
echo ===========================================

cd /d "%TESTS_DIR%"

set PATH=%BUILD_LIB%;%PATH%

dotnet build --nologo -q
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] Build failed.
    exit /b 1
)

echo.
dotnet run --no-build
set TEST_EXIT=%ERRORLEVEL%

echo.
echo ===========================================
echo   Test Summary
echo ===========================================

if %TEST_EXIT% EQU 0 (
    echo   ALL TESTS PASSED
) else (
    echo   [FAIL] C# Concurrency Tests ^(exit code: %TEST_EXIT%^)
)

exit /b %TEST_EXIT%
