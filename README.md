# Data Management Capability (DMC) Project

This repository contains the architecture, source code, and documentation for the Data Management Capability (DMC) system, designed for high-performance semiconductor equipment control.

## Tech Stack

### Languages

* C++17
* C# (.NET)

### Communication

* P/Invoke
* gRPC

### Design Patterns

* Factory Pattern
* Strategy Pattern
* Observer Pattern

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

## Features

### Core Capabilities

* Equipment object management

  * Manage equipment objects, states, and lifecycle.

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

#### `Bridge/`

* Interoperability layer connecting native C++ components and managed C# components using P/Invoke and gRPC.

#### `Common/`

* Shared utilities, common data structures, and reusable components.

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

  * Unit tests for individual modules.

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
* [ ] Implement logging subsystem
* [ ] Implement configuration management
* [ ] Build hardware abstraction layer
* [ ] Implement event dispatcher
* [ ] Integrate gRPC communication
* [ ] Add unit tests
* [ ] Add CI/CD pipeline

## Notes

> This repository is a personal project created for learning and portfolio purposes.
>
> Some implementation details have been simplified or anonymized.
