# Setup Scripts

> **Navigation**: [Project Root](../../README.md) > [Scripts](../README.md) > Setup
>
> **Related**: [Build](../build/README.md)

Environment dependency management for the DMC project.

## Scripts

| Script | Purpose | Run As |
|--------|---------|--------|
| `install_deps_rhel.sh` | Install system dependencies on RHEL 9.2 | root |
| `uninstall_deps_rhel.sh` | Remove dependencies installed by this project | root |
| `install_deps.bat` | Install system dependencies on Windows | Administrator |
| `uninstall_deps.bat` | Remove dependencies installed by this project | Administrator |

## Example Output

### install_deps_rhel.sh

```
[root@AS93X001 setup]# ./install_deps_rhel.sh 
=== DMC Environment Setup (RHEL 9.2) ===
Install manifest: /home/wafer/Downloads/Data-Management-Capability/.dmc_install_manifest

--- Checking pre-existing packages ---
  [SKIP] Microsoft repo already configured

--- Installing required packages ---
  [SKIP] dotnet-sdk-8.0 already installed (pre-existing, will NOT be removed)
  [SKIP] cmake already installed (pre-existing, will NOT be removed)
  [SKIP] gcc-c++ already installed (pre-existing, will NOT be removed)
  [SKIP] make already installed (pre-existing, will NOT be removed)

--- Verifying installations ---
  dotnet: 8.0.422
  cmake:  cmake version 3.26.5
  g++:    g++ (GCC) 11.3.1 20221121 (Red Hat 11.3.1-4)

=== Environment Setup Complete ===

Manifest saved to: /home/wafer/Downloads/Data-Management-Capability/.dmc_install_manifest
To undo: ./scripts/setup/uninstall_deps_rhel.sh
```

### uninstall_deps_rhel.sh

```
[root@AS93X001 setup]# ./uninstall_deps_rhel.sh 
=== DMC Environment Teardown (RHEL 9.2) ===

Reading manifest: /home/wafer/Downloads/Data-Management-Capability/.dmc_install_manifest

--- Removing installed packages ---
  [KEEP REPO] microsoft-prod (was pre-existing)
  [KEEP] dotnet-sdk-8.0 (was pre-existing)
  [KEEP] cmake (was pre-existing)
  [KEEP] gcc-c++ (was pre-existing)
  [KEEP] make (was pre-existing)

--- Cleaning package cache ---
12 files removed
--- Removing install manifest ---

=== Environment Teardown Complete ===
Removed 0 package(s). Pre-existing packages were not touched.
```

## Output Labels

| Label | Meaning |
|-------|---------|
| `[INSTALL]` | Package was not present and has been installed. Will be removed on uninstall. |
| `[SKIP]` | Package already existed. Recorded as pre-existing and will NOT be removed. |
| `[REMOVE]` | Package is being uninstalled (was installed by this project). |
| `[KEEP]` | Package is being kept (was pre-existing before this project). |
| `[WARN]` | Removal attempted but failed. Manual cleanup may be needed. |

## Notes

- If packages were manually installed before running `install_deps_rhel.sh`, they will be recorded as `PRE_EXISTING` and will not be removed during uninstall.
- To force correct tracking, remove manually installed packages first, then run `install_deps_rhel.sh` to reinstall and record them properly.
