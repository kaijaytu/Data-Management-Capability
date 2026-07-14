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
└── tests/                  # Automated tests (fault injection, concurrency, integration)
    ├── test_main.cpp       # C++ fault injection tests
    ├── HardwareLogicConcurrencyTests.cs  # C# concurrency stress tests
    ├── GrpcIntegrationTests.cs           # gRPC integration tests
    ├── DMCServerTests.cs                 # C# unit tests (schema, set, search, concurrency, commitlog)
    ├── DMC.Tests.csproj                  # .NET test project (concurrency)
    ├── DMC.UnitTests.csproj              # .NET test project (unit tests)
    └── DMC.IntegrationTests.csproj       # .NET test project (integration)
```

## Execution Modes

The single binary supports three modes:

```bash
./DMC --server [--port 5000]                # gRPC server, listens for client connections
./DMC --client [--address http://host:port] [--client-id name]  # gRPC client with user identity
./DMC --local                               # Local interactive mode (no network)
```

| Mode | Use Case | Network |
|------|---------|---------|
| `--server` | Production: host gRPC service for remote clients | Listening on port |
| `--client` | Production: connect to a running server | Outbound connection |
| `--local` | Development/testing: direct console interaction | None |

## gRPC Service (V2)

### Schema Layer

| RPC Method | Type | Description |
|-----------|------|-------------|
| `DefineType` | Unary | Define IdentityKeys schema for a Type (one-time) |
| `GetTypeSchema` | Unary | Get IdentityKeys definition for a Type |
| `UpdateTypeSchema` | Unary | Update IdentityKeys with collision validation |

### Data Layer

| RPC Method | Type | Description |
|-----------|------|-------------|
| `SetElement` | Unary | Set a Data Element — Server decides Create/Update via IdentityKeys |
| `Search` | Unary | Search elements by type, property filters, and/or owner |
| `Print` | Unary | Get display string of one element by key |
| `PrintAll` | Server streaming | Stream all elements to client |
| `BatchSet` | Client streaming | Client sends multiple elements in one call |
| `GetCount` | Unary | Get total element count |
| `Contains` | Unary | Check if element exists by key |

### Why This Design (V1 → V2)

V1 exposed separate `Register` and `Update` RPCs, forcing the Client to decide which to call.
But the Client has no knowledge of what exists on the Server — only the Server can make this decision.

V2 replaces both with a single `SetElement` RPC:
- Client sends data, Server checks IdentityKeys to determine if the element exists
- Exists → `UPDATED` (merge or replace), Not exists → `CREATED`
- Server returns the action taken, so Client knows what happened

This follows the spec: *"Register data element (In case New Data element)"* and
*"Update data element (In case Data Element already in System)"* — both decisions belong to the Server.

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

## Logic Flow (V2)

```text
Schema Definition (one-time per Type)
      ↓
  DefineType("Car", IdentityKeys=[VIN])
      ↓
Client Input (set command)
      ↓
┌─────────────────┐
│   DMCServer     │  1. Validate schema exists for Type
│   (Singleton)   │  2. Build identity key from IdentityKeys
│                 │  3. Check identity index (O(1) lookup)
│                 │  4. Match → Update (merge/replace)
│                 │     No match → Create (auto-increment key)
│                 │  5. Return deep copy (thread-safe snapshot)
└────────┬────────┘
         ↓
┌─────────────────┐
│ IDataElement    │  IIdentifiable: identity matching via IdentityKeys
│ GenericData...  │  ISearchable: subset property filtering
│                 │  IPrintable: self-display without Server knowing type
└────────┬────────┘
         ↓
┌─────────────────┐
│ HardwareBridge  │  C# managed wrapper (P/Invoke)
│ libHardwareLogic│  C++ native hardware control
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
| **Single SetElement RPC** (replaces Register+Update) | Only the Server has the registry — only it can decide if an element is new or existing. Client should not make this decision. |
| **IdentityKeys as per-Type schema** | Identity is defined once per Type (like a DB UNIQUE constraint), not per element. Prevents inconsistent matching and enables O(1) identity index lookup. Modeled after MongoDB's `createIndex({unique:true})`. |
| **Server-generated auto-increment keys** | Keys are independent of data content — changing a property doesn't change the key. Guaranteed unique by Server. |
| **Defensive copy (CloneElement)** | `Search`/`Get`/`GetAll` return deep copies so callers can safely iterate Properties outside the lock. Prevents `Collection was modified` exceptions during concurrent access. |
| **Thread safety via `lock`** | All public DMCServer methods wrapped in `lock(_lock)`. Chosen over `ConcurrentDictionary` because `Set()` is a compound check-then-act operation that must be atomic. |
| **Merge/Replace update modes** | Merge (default) preserves properties not included in the update. Replace overwrites entirely. Same pattern as MongoDB's `$set` vs `replaceOne`. |
| **Multi-user ownership tracking** | Each element records its creator via `client-id` gRPC metadata. Shared data pool (collaborative) — anyone can read/update, but ownership is tracked for filtering and audit. |
| **`Dictionary<string, string>` for properties** | Server stores all values as strings, never interprets data types. Fulfills spec requirement: *"DMC handles all types without knowing their details."* |
| Singleton Server | All clients within one process share one data pool |
| GenericDataElement | Any type at runtime, no recompilation needed |
| gRPC communication | Remote client-server interaction with streaming support |
| Three execution modes | Flexible: network service, remote client, or standalone |
| P/Invoke Bridge | Cross-language C# ↔ C++ communication |
| Self-contained publish | No .NET runtime needed on deployment target |
| **Kafka-style CommitLog** | Append-only log is the source of truth. In-memory registry is a derived view rebuilt by replaying the log on startup. Enables crash recovery without data loss. |
| **Log Compaction** | Kafka-style: keep only the latest entry per key, reducing log size while preserving final state. |
| **Unit Separator in identity index** | Uses ASCII `\x1F` instead of `:` as delimiter in identity index keys, preventing collision when property values contain `:` (e.g., MAC addresses). |

## Persistence (Kafka-style Commit Log)

All write operations (DefineType, Set, Update, UpdateTypeSchema) are appended to a commit log before modifying in-memory state. The log is the **source of truth**.

```
Write path:  Client → CommitLog.Append() → disk write → _registry update → response
Read path:   Client → _registry (in-memory, fast)
Recovery:    Server restart → CommitLog.ReplayAll() → rebuild _registry from log
Maintenance: CompactLog() → keep only latest entry per key
```

### Data Directory

| Priority | Path | Use case |
|----------|------|----------|
| 1st | `DMC_DATA_DIR` env var | Custom path (e.g., `/var/lib/dmc`) |
| 2nd | `~/.dmc/` | Default — user home, always writable |

```bash
# Development (default)
./DMC --server --port 5050
# Commit log: ~/.dmc/dmc_commit.log

# Production (custom path)
DMC_DATA_DIR=/var/lib/dmc ./DMC --server --port 5050
# Commit log: /var/lib/dmc/dmc_commit.log
```

> **TODO**: Production deployment should use `/var/lib/dmc/` per FHS standard, with `sudo chown <user> /var/lib/dmc`.

### Log Format

One JSON object per line (append-only):

```json
{"offset":0,"timestamp":"...","operation":"DefineType","type":"Car","identityKeys":["VIN"]}
{"offset":1,"timestamp":"...","operation":"Set","type":"Car","key":"Car:1","owner":"alice","properties":{"VIN":"ABC"},"merge":true}
```

### Compaction

After many updates to the same element, the log grows. Compaction keeps only the latest entry per key:

```
Before: 51 entries (1 DefineType + 50 updates to same sensor)
After:   2 entries (1 DefineType + 1 latest sensor state)
```
