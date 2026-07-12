# Build Scripts

> **Navigation**: [Project Root](../../README.md) > [Scripts](../README.md) > Build
>
> **Related**: [Setup](../setup/README.md) | [camtek_kaijaytu](../../camtek_kaijaytu/README.md)

Build and clean scripts for the DMC project.

## Scripts

| Script | Purpose | Run As | Platform |
|--------|---------|--------|----------|
| `build.sh` | Auto-detect platform and dispatch to correct build script | user | Linux |
| `build_rhel.sh` | Build C++ HardwareLogic + C# DMC Server (self-contained) | user | RHEL 9.2 |
| `build.bat` | Build C# DMC Server (development build) | user | Windows |
| `clean_rhel.sh` | Clean all build artifacts | user | RHEL 9.2 |
| `clean.bat` | Clean all build artifacts | user | Windows |

## Example Output

### build.sh (on RHEL 9.2)

```
[wafer@AS93X001 build]$ ./build.sh 
=== DMC Build - Platform Detection ===
OS: Linux | Distro: rhel | Arch: x86_64

Dispatching to: build_rhel.sh
=== DMC Build Script (RHEL 9.2 / linux-x64) ===
Project root: /home/wafer/Downloads/Data-Management-Capability

--- Building C++ HardwareLogic ---
-- The CXX compiler identification is GNU 11.3.1
-- Detecting CXX compiler ABI info
-- Detecting CXX compiler ABI info - done
-- Check for working CXX compiler: /usr/bin/c++ - skipped
-- Detecting CXX compile features
-- Detecting CXX compile features - done
-- Configuring done (0.1s)
-- Generating done (0.0s)
-- Build files have been written to: /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/build/hardware
[ 50%] Building CXX object CMakeFiles/HardwareLogic.dir/src/hardware_logic.cpp.o
[100%] Linking CXX shared library libHardwareLogic.so
[100%] Built target HardwareLogic
-- Install configuration: "Release"
-- Installing: /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/build/output/lib/libHardwareLogic.so
-- Installing: /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/build/output/include
-- Installing: /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/build/output/include/hardware_logic.h
HardwareLogic built: /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/build/output/lib/libHardwareLogic.so

--- Building C# DMC Server (self-contained) ---
  Determining projects to restore...
  Restored /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/src/DMC.csproj (in 15.14 sec).
  DMC -> /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/src/bin/Release/net8.0/linux-x64/DMC.dll
  DMC -> /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/build/output/bin/
DMC Server built: /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/build/output/bin/DMC

--- Packaging native libraries ---
Package complete at: /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/build/output/bin/

=== Build Complete ===
Output directory: /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/build/output/bin/
Files:
total 35460
drwxr-xr-x 2 wafer soc       59 Jul 13 03:20 .
drwxr-xr-x 5 wafer soc       43 Jul 13 03:20 ..
-rwxr-xr-x 1 wafer soc 36040777 Jul 13 03:20 DMC
-rw-r--r-- 1 wafer soc    14628 Jul 13 03:20 DMC.pdb
-rwxr-xr-x 1 wafer soc    28024 Jul 13 03:20 libHardwareLogic.so
```

### clean_rhel.sh

```
[wafer@AS93X001 build]$ ./clean_rhel.sh 
=== DMC Clean Build Artifacts ===

  [CLEAN] C++ build: /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/build/hardware/
  [CLEAN] Output: /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/build/output/
  [CLEAN] dotnet bin: /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/src/bin/
  [CLEAN] dotnet obj: /home/wafer/Downloads/Data-Management-Capability/camtek_kaijaytu/src/obj/

=== Clean Complete ===
Source code is untouched. Run build_rhel.sh to rebuild.
```

## Build Output

After a successful build, the output directory contains:

| File | Size | Description |
|------|------|-------------|
| `DMC` | ~35 MB | Self-contained C# executable (includes .NET runtime) |
| `DMC.pdb` | ~15 KB | Debug symbols |
| `libHardwareLogic.so` | ~28 KB | C++ shared library |

## Clean Targets

| Target | What it removes |
|--------|----------------|
| `camtek_kaijaytu/build/hardware/` | C++ CMake build files |
| `camtek_kaijaytu/build/output/` | Final packaged output |
| `camtek_kaijaytu/src/bin/` | dotnet intermediate binaries |
| `camtek_kaijaytu/src/obj/` | dotnet intermediate objects |
