@echo off
REM =============================================================================
REM DMC Environment Teardown - Uninstall Dependencies for Windows
REM Reads the install manifest and removes ONLY what was installed by this project.
REM MUST be run as Administrator.
REM =============================================================================

setlocal enabledelayedexpansion

REM Verify running as Administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: This script must be run as Administrator.
    echo Right-click and select "Run as administrator".
    exit /b 1
)

set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%..\..
set INSTALL_MANIFEST=%PROJECT_ROOT%\.dmc_install_manifest_win

echo === DMC Environment Teardown (Windows) ===
echo.

REM Check manifest exists
if not exist "%INSTALL_MANIFEST%" (
    echo ERROR: Install manifest not found at %INSTALL_MANIFEST%
    echo Cannot determine what was installed. Nothing to do.
    exit /b 1
)

echo Reading manifest: %INSTALL_MANIFEST%
echo.

set REMOVED_COUNT=0

REM Parse manifest and uninstall
for /f "usebackq tokens=1,* delims==" %%a in ("%INSTALL_MANIFEST%") do (
    set KEY=%%a
    set VALUE=%%b

    REM Skip comments and empty lines
    if "!KEY:~0,1!" neq "#" if defined KEY (
        if "!KEY!"=="INSTALLED_WINGET" (
            echo   [REMOVE] !VALUE!
            winget uninstall !VALUE! --accept-source-agreements >nul 2>&1
            if !errorlevel! equ 0 (
                echo   Removed: !VALUE!
            ) else (
                echo   [WARN] Failed to remove !VALUE!
            )
            set /a REMOVED_COUNT+=1
        )

        if "!KEY!"=="PRE_EXISTING" (
            echo   [KEEP] !VALUE! ^(was pre-existing^)
        )

        if "!KEY!"=="MANUAL_INSTALL" (
            echo   [MANUAL] !VALUE! was manually installed - please remove via Settings ^> Apps
        )

        if "!KEY!"=="SKIPPED" (
            echo   [SKIP] !VALUE! was not installed
        )
    )
)

echo.

REM Remove manifest
echo --- Removing install manifest ---
del "%INSTALL_MANIFEST%"

echo.
echo === Environment Teardown Complete ===
echo Removed !REMOVED_COUNT! package^(s^). Pre-existing packages were not touched.

endlocal
