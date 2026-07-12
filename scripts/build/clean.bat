@echo off
REM =============================================================================
REM DMC Clean Build Artifacts (Windows)
REM Removes all generated files from build process, keeping source code intact.
REM MUST be run as regular user (NOT Administrator).
REM =============================================================================

setlocal

REM Reject Administrator — clean should run as regular user
net session >nul 2>&1
if %errorlevel% equ 0 (
    echo ERROR: Do not run clean as Administrator. Use a regular user account.
    exit /b 1
)

set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%..\..
set SRC_DIR=%PROJECT_ROOT%\camtek_kaijaytu\src
set BUILD_DIR=%PROJECT_ROOT%\camtek_kaijaytu\build

echo === DMC Clean Build Artifacts (Windows) ===
echo.

REM Clean Windows build output
if exist "%BUILD_DIR%\win" (
    echo   [CLEAN] Windows build: %BUILD_DIR%\win\
    rmdir /s /q "%BUILD_DIR%\win"
)

REM Clean Linux build output (if cross-compiled)
if exist "%BUILD_DIR%\output" (
    echo   [CLEAN] Output: %BUILD_DIR%\output\
    rmdir /s /q "%BUILD_DIR%\output"
)

if exist "%BUILD_DIR%\hardware" (
    echo   [CLEAN] C++ build: %BUILD_DIR%\hardware\
    rmdir /s /q "%BUILD_DIR%\hardware"
)

REM Clean dotnet intermediate files
if exist "%SRC_DIR%\bin" (
    echo   [CLEAN] dotnet bin: %SRC_DIR%\bin\
    rmdir /s /q "%SRC_DIR%\bin"
)

if exist "%SRC_DIR%\obj" (
    echo   [CLEAN] dotnet obj: %SRC_DIR%\obj\
    rmdir /s /q "%SRC_DIR%\obj"
)

echo.
echo === Clean Complete ===
echo Source code is untouched. Run build.bat to rebuild.

endlocal
