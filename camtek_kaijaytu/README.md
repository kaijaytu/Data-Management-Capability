# camtek_kaijaytu

> **Navigation**: [Project Root](../README.md) > camtek_kaijaytu
>
> **Sub-pages**: [Common](src/Common/README.md) | [DMCCore](src/DMCCore/README.md) | [Bridge](src/Bridge/README.md) | [HardwareLogic](src/HardwareLogic/README.md) | [Design Examples](docs/design/examples/README.md) | [Scripts](../scripts/README.md)

Core source code and build environment for the DMC (Data Management Capability) system.

## Directory Structure

```text
camtek_kaijaytu/
├── src/                    # Source code
│   ├── Program.cs          # Entry point — server/client/local modes
│   ├── DMC.csproj          # .NET project file
│   ├── Proto/              # gRPC service definition (.proto)
│   ├── Common/             # Interfaces and shared abstractions
│   ├── DMCCore/            # Server logic (Singleton, gRPC service, Client)
│   ├── Bridge/             # P/Invoke bridge to C++ layer
│   ├── HardwareLogic/      # C++ native hardware control
│   ├── Config/             # (reserved) Configuration management
│   ├── Logger/             # (reserved) Logging subsystem
│   └── ThirdParty/         # (reserved) External libraries
├── docs/                   # Design documents and examples
│   └── design/examples/    # Archived concrete class examples
├── build/                  # Generated build output (not committed)
└── tests/                  # Automated tests (fault injection + concurrency)
    ├── test_main.cpp       # C++ fault injection tests
    ├── HardwareLogicConcurrencyTests.cs  # C# concurrency stress tests
    └── DMC.Tests.csproj    # .NET test project
```

## Execution Modes

The single binary supports three modes:

```bash
./DMC --server [--port 5000]                # gRPC server, listens for client connections
./DMC --client [--address http://host:port]  # gRPC client, connects to a running server
./DMC --local                               # Local interactive mode (no network)
```

| Mode | Use Case | Network |
|------|---------|---------|
| `--server` | Production: host gRPC service for remote clients | Listening on port |
| `--client` | Production: connect to a running server | Outbound connection |
| `--local` | Development/testing: direct console interaction | None |

## gRPC Service

| RPC Method | Type | Description |
|-----------|------|-------------|
| `Register` | Unary | Register a new Data Element |
| `Update` | Unary | Update an existing Data Element |
| `Print` | Unary | Get display string of one element |
| `PrintAll` | Server streaming | Stream all elements to client |
| `BatchRegister` | Client streaming | Client sends multiple elements in one call |
| `GetCount` | Unary | Get total element count |
| `Contains` | Unary | Check if element exists by key |

## Important Notes

### Singleton Scope

`DMCServer.Instance` is a **per-process** Singleton. It guarantees:

- Within ONE process: all gRPC requests share the same `_registry`
- Multiple `--client` connections to one `--server` → all see the same data ✅

It does **NOT** guarantee:

- Two separate `./DMC --server` processes share data ❌ (each has its own registry)
- `--local` and `--server` on the same machine share data ❌ (different processes)

### Correct Deployment

```text
✅ Correct: 1 Server + N Clients
┌──────────────────┐
│  ./DMC --server  │  ← ONE process, ONE registry
└────────┬─────────┘
         │ gRPC
    ┌────┼────┐
    ↓    ↓    ↓
Client Client Client   ← All see same data

❌ Wrong: 2 Servers
┌──────────────────┐   ┌──────────────────┐
│ DMC --server :5000│   │ DMC --server :5001│  ← TWO registries, data split
└──────────────────┘   └──────────────────┘
```

### Port Conflict

Do not start two `--server` instances on the same port. The second one will fail with "address already in use".

## Logic Flow

```text
User Input (stdin)
      ↓
┌─────────────────┐
│   Program.cs    │  Reads commands, builds GenericDataElement
└────────┬────────┘
         ↓
┌─────────────────┐
│   DMCServer     │  Calls element.GetKey() → stores in Dictionary
│   (Singleton)   │  Calls element.Print() → outputs to console
└────────┬────────┘
         ↓
┌─────────────────┐
│ IDataElement    │  Interface contract (IKeyIdentifiable + IPrintable)
│ GenericData...  │  Concrete implementation — any type, any properties
└────────┬────────┘
         ↓
┌─────────────────┐
│ HardwareBridge  │  C# managed wrapper
│ (P/Invoke)      │  Calls native C++ functions
└────────┬────────┘
         ↓
┌─────────────────┐
│ libHardwareLogic│  C++ shared library (.so)
│ (Native)        │  Hardware init, read, write, diagnostics
└─────────────────┘
```

## Build

```bash
# Linux (RHEL 9.2) — full build
../scripts/build/build.sh

# Windows — C# only (development)
..\scripts\build\build.bat
```

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Singleton Server | All clients within one process share one data pool |
| Interface separation (IKeyIdentifiable + IPrintable) | Each element controls its own identity and display |
| GenericDataElement | Any type at runtime, no recompilation needed |
| gRPC communication | Remote client-server interaction with streaming support |
| Three execution modes | Flexible: network service, remote client, or standalone |
| P/Invoke Bridge | Cross-language C# ↔ C++ communication |
| Self-contained publish | No .NET runtime needed on deployment target |
