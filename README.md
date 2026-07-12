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
* Factory Pattern — Create different Data Element types without modifying existing code.
* Strategy Pattern — Each Data Element implements its own Print behavior; DMC does not need to know internal details.
* Observer Pattern — Clients subscribe to Data Element change notifications.

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

Data Element behavior is decomposed into two focused interfaces, combined into `IDataElement`:

```text
+-------------------+     +-------------------+
| IKeyIdentifiable  |     | IPrintable        |
+-------------------+     +-------------------+
| + GetKey(): string|     | + Print(): void   |
+-------------------+     | + ToDisplayString()|
        \                 +-------------------+
         \               /
          v             v
     +---------------------+
     |    IDataElement      |
     +---------------------+
     | + Type : string      |
     +---------------------+
              ^
              |
     +--------+---------+
     | GenericDataElement|
     +------------------+
     | + Properties      |
     | + KeyProperty     |
     +------------------+
```

| Interface | Responsibility |
|-----------|---------------|
| `IKeyIdentifiable` | Each element decides its own unique key via `GetKey()`. The server does not impose an ID. |
| `IPrintable` | Each element decides how to display itself via `Print()` and `ToDisplayString()`. |
| `IDataElement` | Combines both interfaces + exposes `Type`. |

### GenericDataElement

A single class that accepts **any type name** and **arbitrary key-value properties**. No new class is needed for new types.

```text
register → Type: Animal
           Properties: Species=Dog, Name=Buddy, Age=3
           Key property: Name
           Generated Key: "Animal:Buddy"
```

| Feature | How it works |
|---------|-------------|
| Type | User-specified string (e.g. Car, Person, Animal, Bus) |
| Properties | Arbitrary `Dictionary<string, string>` key-value pairs |
| Key | Auto-generated as `Type:KeyPropertyValue` (e.g. `Car:Toyota`) |
| Key property | User chooses which property is the unique identifier |
| Print | Outputs all properties with the key: `[Car] Make=Toyota, Year=2024 (Key: Car:Toyota)` |

### Extensibility (Open-Closed Principle)

To add a new Data Element type at runtime:

1. Enter any type name (e.g. `Bus`, `Desktop`, `Airplane`).
2. Enter key-value properties.
3. Specify which property is the unique key.

The DMC Server requires **zero modification** — it only depends on `IDataElement.GetKey()` and `IPrintable.Print()`.

## DMC Server Design

### Data Structure

```text
DMC Server
├── _instance : DMCServer              (Singleton reference)
└── _registry : Dictionary<string, IDataElement>
                 Key = element.GetKey() (e.g. "Car:Toyota")
                 Value = IDataElement instance
```

* Uses a single `Dictionary<string, IDataElement>` as a shared data pool.
* All Clients (modules) read and write to the same registry.
* Key is generated by the element itself via `GetKey()` — not imposed by the server.
* Provides O(1) lookup, insertion, and update.

### Why Shared Data Pool (Not Per-Client Isolation)

The DMC serves as a **centralized data registry** within a single equipment system. Multiple software modules (Clients) need to:

* See the same global state (e.g., module A writes inspection results, module B reads them for scheduling).
* Print out **all objects in the system** as a unified view.
* Maintain a single source of truth — avoiding conflicting copies of the same data.

Per-client isolation would turn each module into an island, breaking cross-module collaboration.

### How DMC Knows If a Data Element Exists

```text
Register(element):
    key = element.GetKey()       // Element decides its own key
    if _registry.ContainsKey(key):
        -> Route to Update
    else:
        -> Add to _registry
```

The server calls `element.GetKey()` to obtain the key, then looks it up in the dictionary. The element itself determines what makes it unique (via `IKeyIdentifiable`).

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
| 1 | Each Data Element generates a globally unique key via `GetKey()`. | Key is composed of `Type:KeyPropertyValue`, ensuring uniqueness within the system. |
| 2 | The system runs within a single process. | Allows in-memory Singleton pattern; if distributed, would need a service registry instead. |
| 3 | Data Elements are not deleted, only registered and updated. | Assessment only specifies Register/Update/Print; Delete is out of scope. |
| 4 | `Print()` outputs to console/log (text-based). | Assessment asks to "print out" without specifying serialization format. |
| 5 | All Data Element types share a composed interface (`IKeyIdentifiable` + `IPrintable` = `IDataElement`). | Required for polymorphic key generation, print, and type-agnostic storage. |
| 6 | All Clients share a single data pool (no per-client isolation). | Equipment modules need cross-module visibility; assessment requires "print all Object in System". |
| 7 | The system is thread-safe for concurrent client access. | Multiple clients may call Register/Update simultaneously; Singleton uses double-checked locking. |
| 8 | New Data Element types are added at runtime without code changes. | `GenericDataElement` accepts any type name and arbitrary properties; no recompilation needed. |

## Features

### Core Capabilities

* Data Element management

  * Register, update, and print Data Elements through a unified interface.
  * Manage object states and lifecycle without knowledge of concrete types.

* Event-driven architecture

  * Support event hooks, notifications, and asynchronous processing.

* Hardware abstraction

  * Separate low-level hardware operations from higher-level application logic through a dedicated C++ layer.

* Cross-language interoperability

  * Enable communication between native C++ modules and managed C# components through P/Invoke and gRPC.

### Reliability & Performance

* High-performance hardware control

  * Provide deterministic and low-latency interaction with semiconductor equipment.

* Persistent storage

  * Store configuration, runtime state, and operational data.

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

### Register Data Element

| Scenario | Input | Expected Result |
|----------|-------|----------------|
| Register new element | MobilePhone(id="001", Brand="Apple") | Successfully added, registry count +1 |
| Register duplicate element | MobilePhone(id="001") again | Routed to Update or returns "already exists" |
| Register different type | Car(id="002", Make="Toyota") | Successfully added as separate entry |

### Update Data Element

| Scenario | Input | Expected Result |
|----------|-------|----------------|
| Update existing element | Update id="001" Brand to "Samsung" | Property updated successfully |
| Update non-existing element | Update id="999" | Returns error or routes to Register |

### Print Data Element

| Scenario | Input | Expected Result |
|----------|-------|----------------|
| Print single element | Print(id="001") | Outputs: "MobilePhone: Apple..." |
| Print all elements | PrintAll() | Outputs all registered elements in sequence |
| Print after update | Print(id="001") after update | Reflects updated values |

### Singleton Guarantee

| Scenario | Operation | Expected Result |
|----------|-----------|----------------|
| Multiple clients access | Client A and B both get Instance | Same object reference (ReferenceEquals == true) |
| Concurrent registration | Client A and B register simultaneously | Both elements stored, no data loss |

### Open-Closed Principle

| Scenario | Operation | Expected Result |
|----------|-----------|----------------|
| Add new type | Create Desktop class, register via Factory | DMC handles it without code change |
| Print new type | PrintAll() after adding Desktop | Desktop.Print() output included |

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
│   ├── architecture/
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
    ├── integration/
    └── unit/
```

### Source Code

#### `HardwareLogic/`

* Native C++ components responsible for hardware control and real-time interaction.

#### `DMCCore/`

* C# modules responsible for equipment object management, event processing, and application logic.
* Contains the DMC Server implementation (Singleton, Registry, Factory).

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

Contains system architecture documents and design references.

* `architecture/`

  * High-level system architecture and component diagrams.

* `design/`

  * Detailed design documents and technical specifications.

### Testing

#### `tests/`

Contains automated testing components.

* `unit/`

  * Unit tests for individual modules (Register, Update, Print, Singleton).

* `integration/`

  * Integration tests for cross-module communication and system behavior.

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
* [x] Design IDataElement interface and base abstractions
* [x] Implement DMC Server with Singleton pattern
* [x] Implement Register / Update / Print operations
* [x] ~~Implement Data Element Factory~~ (replaced by GenericDataElement)
* [ ] Implement logging subsystem
* [ ] Implement configuration management
* [x] Build hardware abstraction layer
* [ ] Implement event dispatcher
* [x] Integrate gRPC communication (code complete, pending RHEL verification)
* [ ] Add unit tests for each requirement
* [ ] Add integration tests for Client-Server scenarios
* [ ] Add CI/CD pipeline

## Notes

> This repository is a personal project created for learning and portfolio purposes.
>
> Some implementation details have been simplified or anonymized.
