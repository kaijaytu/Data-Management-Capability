# ThirdParty

> **Navigation**: [Project Root](../../../README.md) > [camtek_kaijaytu](../../README.md) > ThirdParty
>
> **Related**: [HardwareLogic](../HardwareLogic/README.md) | [Bridge](../Bridge/README.md)

External libraries and third-party dependencies.

## Status: Currently Empty

All current third-party dependencies are managed via NuGet (in `DMC.csproj`) and system packages (cmake, gcc). No vendored libraries are needed yet.

## Current Dependencies (via NuGet)

These are defined in `DMC.csproj` and downloaded automatically by `dotnet restore`:

| Package | Version | Purpose |
|---------|---------|---------|
| `Grpc.AspNetCore` | 2.62.0 | gRPC server hosting (Kestrel + gRPC) |
| `Grpc.Net.Client` | 2.62.0 | gRPC client for connecting to server |
| `Google.Protobuf` | 3.26.1 | Protocol Buffers runtime (serialization/deserialization) |
| `Grpc.Tools` | 2.62.0 | Proto compiler — generates C# code from .proto files (build-time only) |

## Current Dependencies (via System Packages)

Installed by `scripts/setup/install_deps_rhel.sh`:

| Package | Purpose |
|---------|---------|
| `dotnet-sdk-8.0` | .NET SDK for building C# |
| `cmake` | Build system for C++ HardwareLogic |
| `gcc-c++` | C++17 compiler |

## When Would This Directory Be Used?

| Scenario | Example |
|----------|---------|
| Vendored C/C++ library | A hardware SDK that doesn't have a package manager |
| Pre-built .so/.dll | A proprietary binary library from equipment vendor |
| Header-only library | C++ libraries like nlohmann/json, spdlog |
| Offline deployment | NuGet packages cached locally for air-gapped builds |

## NuGet vs Vendored: When to Use Which

| | NuGet | ThirdParty/ (vendored) |
|---|-------|----------------------|
| Open source, versioned | ✅ Use NuGet | Unnecessary |
| Proprietary, no package | ❌ Not available | ✅ Put here |
| Needs modification | Awkward with NuGet | ✅ Fork and put here |
| Air-gapped / offline build | Download once, cache | ✅ Commit to repo |
| C/C++ native library | Not applicable | ✅ Include source or .so |

## If Adding a Library Here

1. Create a subdirectory: `ThirdParty/library-name/`
2. Include source code or pre-built binaries
3. Add a `README.md` explaining: version, license, why it's here, how to update
4. Update `CMakeLists.txt` or `.csproj` to reference it
5. Add license file (`LICENSE` or `NOTICE`) for compliance

## Example Structure (if populated)

```text
ThirdParty/
├── nlohmann-json/           # Header-only JSON library for C++
│   ├── README.md
│   ├── LICENSE
│   └── include/json.hpp
├── vendor-hardware-sdk/     # Proprietary hardware SDK
│   ├── README.md
│   ├── include/
│   └── lib/
│       ├── libvendor.so     # Linux
│       └── vendor.dll       # Windows
```
