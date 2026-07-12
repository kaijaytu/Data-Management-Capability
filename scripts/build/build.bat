@echo off
REM =============================================================================
REM DMC Build Script for Windows (Development Build)
REM Builds C# only. C++ HardwareLogic is built separately on Linux.
REM MUST be run as regular user (NOT Administrator).
REM =============================================================================

setlocal

REM Reject Administrator — build should run as regular user
net session >nul 2>&1
if %errorlevel% equ 0 (
    echo ERROR: Do not run build as Administrator. Use a regular user account.
    exit /b 1
)

set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%..\..
set SRC_DIR=%PROJECT_ROOT%\camtek_kaijaytu\src
set BUILD_DIR=%PROJECT_ROOT%\camtek_kaijaytu\build\win

echo === DMC Build Script (Windows / Development) ===
echo.

REM Check dotnet is available
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found. Install from https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b 1
)

REM Build C# project
echo --- Building C# DMC Server ---
cd /d "%SRC_DIR%"
dotnet build -c Debug -o "%BUILD_DIR%"

if errorlevel 1 (
    echo.
    echo BUILD FAILED
    exit /b 1
)

echo.
echo === Build Complete ===
echo Output: %BUILD_DIR%
echo.
echo To run: %BUILD_DIR%\DMC.exe
echo Note: HardwareLogic (C++) is not available on Windows.
echo       The program will skip hardware initialization.

endlocal