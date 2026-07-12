# Scripts

> **Navigation**: [Project Root](../README.md) > Scripts
>
> **Sub-pages**: [Setup](setup/README.md) | [Build](build/README.md) | [camtek_kaijaytu](../camtek_kaijaytu/README.md)

Automation scripts for the DMC project lifecycle on Windows (Win10/Win11) and RHEL 9.2 (linux-x64).

## Lifecycle Overview

```text
install → build → deploy → test → undeploy → clean → uninstall
```

## Directory Structure

```text
scripts/
├── pipeline_install.sh           # One-click: install environment (root)
├── pipeline_start.sh             # One-click: build + deploy + run (user)
├── pipeline_stop.sh              # One-click: undeploy + clean (user)
├── pipeline_uninstall.sh         # One-click: remove environment (root)
├── setup/
│   ├── install_deps.bat          # Install dependencies (Windows)
│   ├── uninstall_deps.bat        # Remove dependencies (Windows)
│   ├── install_deps_rhel.sh      # Install dependencies (RHEL 9.2)
│   └── uninstall_deps_rhel.sh    # Remove dependencies (RHEL 9.2)
├── build/
│   ├── build.bat                 # Build C# (Windows)
│   ├── clean.bat                 # Clean build artifacts (Windows)
│   ├── build.sh                  # Build (Linux, auto-detect platform)
│   ├── build_rhel.sh             # Build C++ and C# for RHEL 9.2
│   └── clean_rhel.sh             # Clean build artifacts (RHEL 9.2)
├── deploy/
│   ├── deploy.bat                # Deploy (Windows)
│   ├── deploy.sh                 # Deploy (Linux, auto-detect platform)
│   ├── deploy_rhel.sh            # Deploy to RHEL 9.2 target directory
│   └── undeploy_rhel.sh          # Remove deployed files (RHEL 9.2)
└── test/
    ├── test.bat                  # Run tests (Windows)
    └── test.sh                   # Run tests (Linux)
```

## Usage

### Quick Start (Pipeline Scripts)

```bash
# Give execute permission
chmod +x scripts/pipeline_*.sh scripts/setup/*.sh scripts/build/*.sh scripts/deploy/*.sh

# First time: install environment (as root)
su - root
./scripts/pipeline_install.sh
exit

# Build + Deploy + Run (as user, prompts for root password at deploy)
./scripts/pipeline_start.sh

# Undeploy + Clean (as user, prompts for root password at undeploy)
./scripts/pipeline_stop.sh

# Remove environment (as root)
su - root
./scripts/pipeline_uninstall.sh
```

### Pipeline Scripts

| Script | What it does | Run As |
|--------|-------------|--------|
| `pipeline_install.sh` | Install all system dependencies | root |
| `pipeline_start.sh` | Build → Deploy → Run (auto switches to root for deploy) | user |
| `pipeline_stop.sh` | Undeploy → Clean (auto switches to root for undeploy) | user |
| `pipeline_uninstall.sh` | Remove all installed dependencies | root |

### Manual Steps: Windows (Win10 / Win11)

```bat
REM Full Install → Build → Clean → Uninstall
scripts\setup\install_deps.bat          &REM Install .NET SDK, CMake
scripts\build\build.bat                 &REM Build C# (Debug)
scripts\build\clean.bat                 &REM Clean build artifacts
scripts\setup\uninstall_deps.bat        &REM Remove installed dependencies
```

### Manual Steps: RHEL 9.2

```bash
# Prerequisites
chmod +x scripts/setup/*.sh scripts/build/*.sh scripts/deploy/*.sh scripts/test/*.sh

# Full Install → Build → Deploy
./scripts/setup/install_deps_rhel.sh       # Install dotnet, cmake, gcc-c++
./scripts/build/build_rhel.sh              # Build C++ and C#
./scripts/deploy/deploy_rhel.sh [dir]      # Deploy to /opt/dmc
/opt/dmc/run.sh                            # Run

# Full Undeploy → Clean → Uninstall
./scripts/deploy/undeploy_rhel.sh [dir]    # Remove deployed files
./scripts/build/clean_rhel.sh              # Clean build artifacts
./scripts/setup/uninstall_deps_rhel.sh     # Remove installed dependencies
```

## Identity Requirements

All scripts enforce identity checks at startup:

| Script Type | Required Identity | Rejected Identity | Check Method |
|-------------|------------------|-------------------|-------------|
| setup/install, setup/uninstall | root | user | `id -u != 0` |
| deploy, undeploy | root | user | `id -u != 0` |
| build, clean | user | root | `id -u == 0` |
| Windows setup | Administrator | user | `net session` |
| Windows build, clean | user | Administrator | `net session` |

## Script Details

### setup/install_deps_rhel.sh

Installs required system packages and records what was installed in `.dmc_install_manifest`.

| What it installs | Purpose |
|-----------------|---------|
| Microsoft repo (packages-microsoft-prod) | Package source for .NET |
| dotnet-sdk-8.0 | Build and publish C# projects |
| cmake | Build C++ HardwareLogic |
| gcc-c++ | Compile C++17 code |
| make | Build system |

**Pre-existing packages are recorded but NOT removed during uninstall.**

### setup/uninstall_deps_rhel.sh

Reads `.dmc_install_manifest` and removes **only** packages that were installed by `install_deps_rhel.sh`. Packages that existed before are left untouched.

### build/build_rhel.sh

Builds the complete project:

1. C++ HardwareLogic → `libHardwareLogic.so`
2. C# DMC Server → self-contained linux-x64 executable
3. Packages both into `camtek_kaijaytu/build/output/bin/`

### build/clean_rhel.sh

Removes all build artifacts:

- `camtek_kaijaytu/build/hardware/` (C++ build)
- `camtek_kaijaytu/build/output/` (final output)
- `camtek_kaijaytu/src/bin/`, `obj/` (dotnet intermediate)

Source code is **not** affected.

### deploy/deploy_rhel.sh

Deploys built binaries to a target directory (default `/opt/dmc`):

- Copies executable and shared libraries
- Sets file permissions
- Creates `run.sh` launcher with `LD_LIBRARY_PATH`

### deploy/undeploy_rhel.sh

Removes the deployment directory. Prompts for confirmation before deleting.

## Windows Script Details

### setup/install_deps.bat

Installs required tools via `winget` and records what was installed in `.dmc_install_manifest_win`.

| What it installs | Purpose |
|-----------------|---------|
| Microsoft.DotNet.SDK.8 | Build and run C# projects |
| Kitware.CMake (optional) | Build C++ if needed |

**Pre-existing tools are recorded but NOT removed during uninstall.**

### setup/uninstall_deps.bat

Reads `.dmc_install_manifest_win` and removes **only** packages installed by `install_deps.bat`.

### build/build.bat

Builds the C# project in Debug mode for local development.

### build/clean.bat

Removes all build artifacts:

- `camtek_kaijaytu/build/win/` (Windows build output)
- `camtek_kaijaytu/build/output/`, `hardware/` (cross-compile output)
- `camtek_kaijaytu/src/bin/`, `obj/` (dotnet intermediate)

## Install Manifests

Each platform generates its own manifest file at the project root:

| Platform | Manifest File | Install Script | Uninstall Script |
|----------|--------------|----------------|------------------|
| Windows | `.dmc_install_manifest_win` | `install_deps.bat` | `uninstall_deps.bat` |
| RHEL 9.2 | `.dmc_install_manifest` | `install_deps_rhel.sh` | `uninstall_deps_rhel.sh` |

### RHEL manifest example

```text
PRE_EXISTING_RPM=cmake          # Was already installed, will NOT remove
PRE_EXISTING_RPM=gcc-c++        # Was already installed, will NOT remove
INSTALLED_RPM=dotnet-sdk-8.0    # Installed by us, WILL remove on uninstall
INSTALLED_REPO=microsoft-prod   # Installed by us, WILL remove on uninstall
```

### Windows manifest example

```text
PRE_EXISTING=dotnet-sdk         # Was already installed, will NOT remove
INSTALLED_WINGET=Kitware.CMake  # Installed by us, WILL remove on uninstall
```

This ensures both workstations can be restored to their exact pre-development state.
