@echo off
REM =============================================================================
REM DMC Environment Setup - Install Dependencies for Windows (Win10/Win11)
REM Records everything installed so it can be cleanly uninstalled later.
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

echo === DMC Environment Setup (Windows) ===
echo Install manifest: %INSTALL_MANIFEST%
echo.

REM Initialize manifest
echo # DMC Install Manifest (Windows) - %DATE% %TIME% > "%INSTALL_MANIFEST%"
echo # This file tracks all changes made by install_deps.bat >> "%INSTALL_MANIFEST%"
echo # Used by uninstall_deps.bat to restore original state >> "%INSTALL_MANIFEST%"
echo. >> "%INSTALL_MANIFEST%"

REM -----------------------------------------------------------------------------
REM Step 1: Check and install .NET SDK 8.0
REM -----------------------------------------------------------------------------
echo --- Checking .NET SDK ---

dotnet --version >nul 2>&1
if %errorlevel% equ 0 (
    for /f "tokens=*" %%v in ('dotnet --version') do set DOTNET_VER=%%v
    echo   [SKIP] .NET SDK already installed: !DOTNET_VER! ^(pre-existing^)
    echo PRE_EXISTING=dotnet-sdk >> "%INSTALL_MANIFEST%"
) else (
    echo   [INSTALL] .NET SDK 8.0
    echo.
    echo Attempting install via winget...
    winget install Microsoft.DotNet.SDK.8 --accept-source-agreements --accept-package-agreements >nul 2>&1
    if !errorlevel! equ 0 (
        echo   Installed via winget
        echo INSTALLED_WINGET=Microsoft.DotNet.SDK.8 >> "%INSTALL_MANIFEST%"
    ) else (
        echo   winget not available. Downloading installer...
        echo   Please install manually from: https://dotnet.microsoft.com/download/dotnet/8.0
        echo MANUAL_INSTALL=dotnet-sdk-8.0 >> "%INSTALL_MANIFEST%"
    )
)

echo.

REM -----------------------------------------------------------------------------
REM Step 2: Check and install CMake (optional, for C++ on Windows)
REM -----------------------------------------------------------------------------
echo --- Checking CMake ---

cmake --version >nul 2>&1
if %errorlevel% equ 0 (
    echo   [SKIP] CMake already installed ^(pre-existing^)
    echo PRE_EXISTING=cmake >> "%INSTALL_MANIFEST%"
) else (
    echo   [INSTALL] CMake
    winget install Kitware.CMake --accept-source-agreements --accept-package-agreements >nul 2>&1
    if !errorlevel! equ 0 (
        echo   Installed via winget
        echo INSTALLED_WINGET=Kitware.CMake >> "%INSTALL_MANIFEST%"
    ) else (
        echo   [SKIP] winget not available. CMake is optional on Windows.
        echo SKIPPED=cmake >> "%INSTALL_MANIFEST%"
    )
)

echo.

REM -----------------------------------------------------------------------------
REM Step 3: Verify installations
REM -----------------------------------------------------------------------------
echo --- Verifying installations ---

dotnet --version >nul 2>&1
if %errorlevel% equ 0 (
    for /f "tokens=*" %%v in ('dotnet --version') do echo   dotnet: %%v
) else (
    echo   dotnet: NOT FOUND - restart terminal or install manually
)

cmake --version >nul 2>&1
if %errorlevel% equ 0 (
    for /f "tokens=1,2,3" %%a in ('cmake --version') do echo   cmake: %%c
) else (
    echo   cmake: NOT FOUND ^(optional on Windows^)
)

echo.
echo === Environment Setup Complete ===
echo.
echo Manifest saved to: %INSTALL_MANIFEST%
echo To undo: scripts\setup\uninstall_deps.bat

endlocal
