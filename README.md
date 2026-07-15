# Data Management Capability (DMC) Project

> **Sub-pages**: [camtek_kaijaytu](camtek_kaijaytu/README.md) | [Scripts](scripts/README.md) | [Common](camtek_kaijaytu/src/Common/README.md) | [DMCCore](camtek_kaijaytu/src/DMCCore/README.md) | [Bridge](camtek_kaijaytu/src/Bridge/README.md) | [HardwareLogic](camtek_kaijaytu/src/HardwareLogic/README.md) | [Design Examples](camtek_kaijaytu/docs/design/examples/README.md)

This repository contains the architecture, source code, and documentation for the Data Management Capability (DMC) system, designed for high-performance semiconductor equipment control.

## Tech Stack

### Languages

* C++17
* C# (.NET)

### Communication

* P/Invoke
* gRPC

### Design Patterns

* Singleton Pattern — Ensure all clients access the same DMC Server instance.
* Strategy Pattern — Each Data Element implements its own Print behavior; DMC does not need to know internal details.
* Interface Segregation (ISP) — IIdentifiable + ISearchable + IPrintable composed into IDataElement.
* Open-Closed Principle (OCP) — GenericDataElement with Dictionary<string, string> handles any type without code changes.
* Kafka-style Commit Log — Append-only log as source of truth; in-memory state is a derived view.
* Bridge Pattern — HardwareBridge decouples managed C# from native C++ via P/Invoke.

### Build System

* CMake
* MSBuild

### Testing

* Unit Test
* Integration Test

### Platform

* Windows
* Linux

## Dependencies

* C++17 compatible compiler
* .NET SDK
* CMake 3.20+
* gRPC
* Visual Studio 2022
* GCC / Clang

## Architecture Diagram

### System Layer Architecture

```text
+-----------------------+
|     User Interface    |
+-----------+-----------+
            |
            v
+-----------------------+
|       DMCCore         |
|      (C# Layer)       |
+-----------+-----------+
            |
            v
+-----------------------+
|        Bridge         |
|   (P/Invoke / gRPC)   |
+-----------+-----------+
            |
            v
+-----------------------+
|    HardwareLogic      |
|      (C++ Layer)      |
+-----------------------+
```

### Client-Server Architecture

```text
+----------+   +----------+   +----------+
| Client A |   | Client B |   | Client C |
+----+-----+   +----+-----+   +----+-----+
     |              |              |
     +--------------+--------------+
                    |
                    v
          +--------------------+
          |    DMC Server      |  <-- Singleton Instance
          | (Object Registry)  |
          +--------------------+
          | - Register Element |
          | - Update Element   |
          | - Print Element(s) |
          +--------+-----------+
                   |
        +----------+----------+
        |                     |
+-------+-------+    +-------+-------+
|  Data Store   |    | Print Engine  |
| (Dictionary)  |    | (Polymorphism)|
+---------------+    +---------------+
```

## DMC Overview

The DMC is a capability identified as part of a systems infrastructure. It acts as a **Server** responsible for handling all kinds of objects (Data Elements) input by **Clients**, without needing to know the internal details of those objects.

### Responsibilities

* Storage of Data Elements with a well-defined data structure.
* Allowing multiple users (Clients) to input and manage their data.
* Providing the ability to print out all objects currently in the system.

## DMC Requirements

| # | Requirement | Description |
|---|-------------|-------------|
| 1 | Register Data Element | Accept and store a new Data Element if it does not already exist in the system. |
| 2 | Update Data Element | Modify an existing Data Element if it is already registered. |
| 3 | Print Data Element | Output the contents of a Data Element without the server knowing its concrete type. |
| 4 | Multi-type Storage | Allow storage and management of different types of Data Elements per user. |
| 5 | Open for Extension | No redesign required when adding a new type of Data Element. |

## Data Element Design

### Interface Architecture

Data Element behavior is decomposed into three focused interfaces, combined into `IDataElement`:

```text
+----------------------------+   +----------------------------+   +-------------------+
| IIdentifiable              |   | ISearchable                |   | IPrintable        |
+----------------------------+   +----------------------------+   +-------------------+
| + IsIdenticalTo(type,      |   | + Matches(type?, filters)  |   | + Print(): void   |
|     props, identityKeys)   |   |   : bool                   |   | + ToDisplayString()|
|   : bool                   |   +----------------------------+   +-------------------+
+----------------------------+               |                          /
              \                              |                         /
               +-----------+-----------------+------------------------+
                           |
                           v
                  +---------------------+
                  |    IDataElement      |
                  +---------------------+
                  | + Type : string      |
                  | + Key  : string      |
                  +---------------------+
                           ^
                           |
                  +--------+-----------+
                  | GenericDataElement  |
                  +--------------------+
                  | + Properties        |
                  | + Owner             |
                  +--------------------+
```

| Interface | Responsibility |
|-----------|---------------|
| `IIdentifiable` | Each element knows how to compare its own identity using IdentityKeys (subset of properties defined per Type). |
| `ISearchable` | Each element knows how to match against search filters (subset match on properties). |
| `IPrintable` | Each element decides how to display itself via `Print()` and `ToDisplayString()`. |
| `IDataElement` | Combines all three interfaces + exposes `Type` and `Key`. |

### GenericDataElement

A single class that accepts **any type name** and **arbitrary key-value properties**. No new class is needed for new types.

```text
DefineType("Car", IdentityKeys=["VIN"])

Set → Type: Car
      Properties: VIN=1HGBH41JXMN109186, Make=Toyota, Model=Camry, Year=2024
      → CREATED, Key: Car:1  (auto-increment)

Set → Type: Car
      Properties: VIN=1HGBH41JXMN109186, Color=Red
      → UPDATED, Key: Car:1  (same VIN = same car, Color merged)

Set → Type: Car
      Properties: VIN=5YJSA1DN0DFP14555, Make=Tesla, Model=Model3
      → CREATED, Key: Car:2  (different VIN = different car)
```

| Feature | How it works |
|---------|-------------|
| Type | User-specified string (e.g. Car, Person, Sensor, Bus) |
| Properties | Arbitrary `Dictionary<string, string>` key-value pairs — Server never interprets data types |
| Key | Server-generated auto-increment: `Type:N` (e.g. `Car:1`, `Car:2`) |
| IdentityKeys | Per-Type schema defined via `DefineType()`. Server uses these to determine Create vs Update |
| Owner | Optional client identifier, enabling multi-user tracking and search by owner |
| Print | Outputs all properties: `[Car] VIN=1HGBH41JXMN109186, Make=Toyota  (Key: Car:1 @ClientA)` |

### Extensibility (Open-Closed Principle)

To add a new Data Element type at runtime:

1. `DefineType("Bus", IdentityKeys=["PlateNumber"])` — declare what makes a Bus unique.
2. `Set(type="Bus", properties={PlateNumber=ABC-1234, Route=42, Capacity=50})` — store data.
3. Done. The Server handles Create/Update automatically via identity matching.

The DMC Server requires **zero modification** — it relies on `IIdentifiable.IsIdenticalTo()` for identity matching and `IPrintable.Print()` for display. New types are handled by schema definition, not code changes.

## DMC Server Design

### Data Structure (V2 — Schema Layer + Data Layer)

```text
DMC Server (Singleton)
├── _instance : DMCServer              (Singleton reference)
├── _typeSchemas : Dictionary<string, List<string>>
│                Type → IdentityKeys (per-Type schema, e.g. Car → [VIN])
├── _typeCounters : Dictionary<string, int>
│                Type → auto-increment counter (for key generation)
├── _identityIndex : Dictionary<string, string>
│                "Type\x1FVal1\x1FVal2" → Key (O(1) identity dedup lookup)
├── _registry : Dictionary<string, IDataElement>
│                Key (e.g. "Car:1") → IDataElement instance
└── _commitLog : CommitLog
                 Kafka-style append-only log (source of truth)
```

| Structure | Purpose | Why |
|-----------|---------|-----|
| `_typeSchemas` | Store IdentityKeys per Type | Schema defined once via `DefineType()`, enforced on every `Set()`. Like MongoDB `createIndex({unique:true})` |
| `_identityIndex` | Fast identity dedup lookup | Without index: O(N) scan. With index: O(1) dictionary lookup |
| `_typeCounters` | Auto-increment key generation | Keys are independent of data content (Car:1, Car:2...). Changing properties never changes the key |
| `_registry` | Element storage | The actual data pool. Key is server-generated, value is IDataElement |
| `_commitLog` | Persistence | Append-only JSON log. On startup, state is rebuilt by replaying the log |

### Why Shared Data Pool (Not Per-Client Isolation)

The DMC serves as a **centralized data registry** within a single equipment system. Multiple software modules (Clients) need to:

* See the same global state (e.g., module A writes inspection results, module B reads them for scheduling).
* Print out **all objects in the system** as a unified view.
* Maintain a single source of truth — avoiding conflicting copies of the same data.

Per-client isolation would turn each module into an island, breaking cross-module collaboration.

### How DMC Knows If a Data Element Exists

```text
Set(type, properties, owner):
    1. Validate: _typeSchemas[type] exists (DefineType must be called first)
    2. Validate: all IdentityKeys present in properties
    3. lock(_lock):
       a. Build identity key: "Type\x1FIdentityVal1\x1FIdentityVal2"
       b. Lookup _identityIndex[identityKey]
          → Match found: UpdateProperties (merge or replace) → return UPDATED
          → No match: GenerateKey(type) → add to _registry + _identityIndex → return CREATED
```

The server uses per-Type IdentityKeys (defined via `DefineType()`) to build a composite identity string, then looks it up in O(1) via the identity index. The element itself participates through `IIdentifiable.IsIdenticalTo()` for validation.

### How DMC Prints Without Knowing the Data Element

The server calls `Print()` through the `IPrintable` interface. Each element provides its own implementation (polymorphism).

```text
PrintAll():
    for each element in _registry.Values:
        element.Print()   // Dispatched via IPrintable
```

The DMC Server never casts to a concrete type — it relies entirely on the `IPrintable` interface contract.

### How All Clients Access the Same DMC Server (Singleton)

```text
class DMCServer:
    private static _instance : DMCServer = null
    private static _lock : object = new object()

    public static Instance:
        get:
            if _instance == null:
                lock(_lock):
                    if _instance == null:
                        _instance = new DMCServer()
            return _instance
```

* Private constructor prevents external instantiation.
* Thread-safe double-checked locking ensures a single instance.
* All Clients call `DMCServer.Instance` to get the same server reference.

## Design Assumptions

The following assumptions are made and validated for each design step:

| # | Assumption | Justification |
|---|-----------|---------------|
| 1 | Each Type declares IdentityKeys that uniquely identify an instance within that Type. | Identity is composed from a subset of properties (e.g. VIN for Car). Server builds a composite key for O(1) dedup lookup. |
| 2 | The system runs within a single process. | Allows in-memory Singleton pattern; if distributed, would need a service registry instead. |
| 3 | Data Elements are not deleted, only registered and updated. | Assessment only specifies Register/Update/Print; Delete is out of scope. |
| 4 | `Print()` outputs to console/log (text-based). | Assessment asks to "print out" without specifying serialization format. |
| 5 | All Data Element types share a composed interface (`IIdentifiable` + `ISearchable` + `IPrintable` = `IDataElement`). | Required for polymorphic identity matching, subset search, print, and type-agnostic storage. |
| 6 | All Clients share a single data pool (no per-client isolation). | Equipment modules need cross-module visibility; assessment requires "print all Object in System". |
| 7 | The system is thread-safe for concurrent client access. | Multiple clients may call Set/Search simultaneously; all public methods use lock(_lock) for atomicity. |
| 8 | New Data Element types are added at runtime without code changes. | `GenericDataElement` accepts any type name and arbitrary properties; `DefineType()` declares schema at runtime. |

## Features

### Core Capabilities

* Data Element management

  * Register, update, and print Data Elements through a unified interface.
  * Manage object states and lifecycle without knowledge of concrete types.

* Hardware abstraction

  * Separate low-level hardware operations from higher-level application logic through a dedicated C++ layer.

* Cross-language interoperability

  * Enable communication between native C++ modules and managed C# components through P/Invoke and gRPC.

### Reliability & Performance

* High-performance hardware control

  * Provide deterministic and low-latency interaction with semiconductor equipment.

* Fault handling

  * Detect, report, and recover from hardware and communication failures.

### Development & Maintenance

* Modular architecture

  * Separate business logic, hardware logic, communication, and infrastructure layers.

* Automated testing

  * Support unit tests and integration tests.

* Automated deployment

  * Simplify build, test, and deployment workflows through scripts.

## Test Scenarios

### Register Data Element (Set — Create)

| Scenario | Input | Expected Result |
|----------|-------|----------------|
| Register new element | `DefineType("Car", ["VIN"])` then `Set("Car", {VIN=1HGBH41JXMN109186, Make=Toyota})` | CREATED, Key: `Car:1`, registry count +1 |
| Register same identity | `Set("Car", {VIN=1HGBH41JXMN109186, Color=Red})` | UPDATED, Key: `Car:1` (same VIN = same car, Color merged) |
| Register different identity | `Set("Car", {VIN=5YJSA1DN0DFP14555, Make=Tesla})` | CREATED, Key: `Car:2` (different VIN = different car) |

### Update Data Element

| Scenario | Input | Expected Result |
|----------|-------|----------------|
| Update existing (merge) | `Update("Car:1", {Year=2025})` | Property added, existing properties kept |
| Update existing (replace) | `Update("Car:1", {Make=Honda}, merge=false)` | All old properties replaced with new |
| Update non-existing | `Update("Car:999", {Color=Blue})` | Returns false (element not found) |

### Print Data Element

| Scenario | Input | Expected Result |
|----------|-------|----------------|
| Print single element | `Print("Car:1")` | Outputs: `[Car] VIN=1HGBH41JXMN109186, Make=Toyota  (Key: Car:1)` |
| Print all elements | `PrintAll()` | Outputs all registered elements in sequence |
| Print after update | `Print("Car:1")` after update | Reflects updated values |

### Singleton Guarantee

| Scenario | Operation | Expected Result |
|----------|-----------|----------------|
| Multiple clients access | Client A and B both get Instance | Same object reference (ReferenceEquals == true) |
| Concurrent Set | 100 threads call Set simultaneously | All elements stored correctly, no data loss or duplicate keys |

### Open-Closed Principle

| Scenario | Operation | Expected Result |
|----------|-----------|----------------|
| Add new type | `DefineType("Desktop", ["SerialNumber"])` then `Set("Desktop", {...})` | DMC handles it without code change |
| Print new type | `PrintAll()` after adding Desktop | Desktop element output included |
| Schema migration | `UpdateTypeSchema("Car", ["VIN", "PlateNumber"])` | IdentityKeys updated, existing data validated for collisions |

## Project Structure Overview

The project is organized into multiple directories to provide clear separation between source code, documentation, and automation tools.

```text
Data-Management-Capability/
├── camtek_kaijaytu/      # Core source code and build environment
├── misc/                 # Documentation and reference materials
└── scripts/              # Build, test, and deployment automation
```

## Directory Details

### 1. `camtek_kaijaytu/` (Core Package)

This directory contains the main source code and development environment.

```text
camtek_kaijaytu/
├── build/
├── docs/
│   └── design/
├── src/
│   ├── Bridge/
│   ├── Common/
│   ├── Config/
│   ├── DMCCore/
│   ├── HardwareLogic/
│   ├── Logger/
│   └── ThirdParty/
└── tests/
    ├── test_main.cpp                    # C++ fault injection tests
    ├── HardwareLogicConcurrencyTests.cs # C# concurrency stress tests
    ├── GrpcIntegrationTests.cs          # gRPC client-server integration tests
    ├── DMCServerTests.cs                # Unit tests (schema, set, search, concurrency, ownership)
    ├── DMC.Tests.csproj                 # .NET test project (concurrency)
    ├── DMC.UnitTests.csproj             # .NET test project (unit tests)
    └── DMC.IntegrationTests.csproj      # .NET test project (integration)
```

### Source Code

#### `HardwareLogic/`

* Native C++ components responsible for hardware control and real-time interaction.

#### `DMCCore/`

* C# modules responsible for equipment object management, event processing, and application logic.
* Contains the DMC Server implementation (Singleton, Registry) and gRPC service.

#### `Bridge/`

* Interoperability layer connecting native C++ components and managed C# components using P/Invoke and gRPC.

#### `Common/`

* Shared utilities, common data structures, and reusable components.
* Contains the `IDataElement` interface and base abstractions.

#### `Config/`

* Configuration management and runtime settings.

#### `Logger/`

* Logging subsystem for diagnostics, debugging, and system tracing.

#### `ThirdParty/`

* External libraries and third-party dependencies.

### Documentation

#### `docs/`

Contains design documents and technical references.

* `design/`

  * Detailed design documents, examples, and technical specifications.

### Testing

#### `tests/`

Contains automated testing for the HardwareLogic native library.

* **C++ Fault Injection Tests** (`test_main.cpp`)

  * Null pointer handling, invalid buffer sizes, illegal state operations
  * `HW_GetLastError` thread-safety validation (multi-threaded)

* **C# Concurrency Stress Tests** (`HardwareLogicConcurrencyTests.cs`)

  * 100-thread parallel read/write, Init/Shutdown races
  * Read storm, throughput measurement, re-initialization cycles

* **gRPC Integration Tests** (`GrpcIntegrationTests.cs`)

  * End-to-end DefineType, SetElement (identity dedup), Search, Print, PrintAll
  * Server streaming, client streaming (BatchSet), concurrent gRPC calls
  * Realistic scenarios: VIN-based Car, DeviceId-based Sensor, multi-user via metadata

Run all tests: `./scripts/test/test.sh`

### Build Output

#### `build/`

Stores generated binaries and build artifacts.

---

## 2. `misc/` (Documentation Archive)

Contains supporting documents, external specifications, assessment reports, and reference materials.

```text
misc/
├── assessment/
└── specifications/
```

### `assessment/`

* Evaluation reports, feasibility studies, and design assessments.

### `specifications/`

* Hardware specifications and external reference documents.

---

## 3. `scripts/` (Automation and Deployment)

Contains automation scripts used throughout the development lifecycle.

```text
scripts/
├── build/
├── deploy/
└── test/
```

### Build Scripts

Located in:

```text
scripts/build/
```

Examples:

* `build.sh`
* `build.bat`

Compile the complete solution for Linux and Windows environments.

### Test Scripts

Located in:

```text
scripts/test/
```

Examples:

* `test.sh`
* `test.bat`

Execute unit tests and integration tests.

### Deployment Scripts

Located in:

```text
scripts/deploy/
```

Examples:

* `deploy.sh`
* `deploy.bat`

Deploy binaries and configuration files to target environments.

## Getting Started

### Clone the Repository

```bash
git clone https://github.com/kaijaytu/Data-Management-Capability-DMC.git
```

### Build on Windows

```bash
cd scripts/build
build.bat
```

### Build on Linux

```bash
cd scripts/build
chmod +x build.sh
./build.sh
```

## Roadmap

* [x] Initialize repository structure
* [x] Design IDataElement interface and base abstractions (IIdentifiable + ISearchable + IPrintable)
* [x] Implement DMC Server with Singleton pattern (V2: Schema + Data two-layer)
* [x] Implement Set / Update / Search / Print / PrintAll operations
* [x] Implement DefineType / GetTypeSchema / UpdateTypeSchema (Schema layer)
* [x] Implement identity-based deduplication with O(1) index lookup
* [x] Implement Kafka-style CommitLog persistence (append-only, compaction, replay)
* [x] Implement multi-user ownership tracking
* [x] Implement GenericDataElement (runtime extensible, any type)
* [x] ~~Implement Data Element Factory~~ (replaced by GenericDataElement)
* [ ] Implement logging subsystem
* [ ] Implement configuration management
* [x] Build hardware abstraction layer (C++17 + P/Invoke Bridge)
* [ ] Implement event dispatcher
* [x] Integrate gRPC communication (10 RPCs: Unary + Server/Client Streaming)
* [x] Add fault injection tests (C++ null pointer, buffer overflow, illegal state)
* [x] Add concurrency stress tests (C# 100-thread parallel R/W, race conditions)
* [x] Add unit tests (31 tests: schema, set, search, concurrency, ownership, migration, commitlog)
* [x] Add integration tests (20 tests: end-to-end gRPC, realistic scenarios)
* [ ] Add CI/CD pipeline

## Notes

> This repository is a personal project created for learning and portfolio purposes.
>
> Some implementation details have been simplified or anonymized.
