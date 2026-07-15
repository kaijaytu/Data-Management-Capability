# Tests

> **Navigation**: [Project Root](../../README.md) > [camtek_kaijaytu](../README.md) > Tests

Automated test suite for the DMC system: unit tests, native library tests, and gRPC integration tests.

## Files

| File | Language | Purpose |
|------|----------|--------|
| `DMCServerTests.cs` | C# | Unit tests — schema, set, search, concurrency, ownership, schema migration (25 tests) |
| `test_main.cpp` | C++ | Fault injection tests — directly calls `libHardwareLogic.so` API |
| `HardwareLogicConcurrencyTests.cs` | C# | Concurrency stress tests — 100 threads via P/Invoke |
| `GrpcIntegrationTests.cs` | C# | gRPC integration tests — end-to-end client-server V2 scenarios (20 tests) |
| `DMC.UnitTests.csproj` | Project | .NET 8.0 project file for unit tests (DMCServerTests) |
| `DMC.Tests.csproj` | Project | .NET 8.0 project file for concurrency tests |
| `DMC.IntegrationTests.csproj` | Project | .NET 8.0 project file for gRPC integration tests |

## Test Categories

### Unit Tests (`DMCServerTests.cs`)

Tests the DMCServer core logic directly (no network).

| # | Test | Validates |
|---|------|-----------|
| S-01 | DefineType | Schema creation (Car→[VIN]) |
| S-02 | DefineType duplicate | Returns false for already-defined type |
| S-03 | DefineType validation | Rejects empty type/keys |
| S-04 | GetTypeSchema | Returns correct IdentityKeys |
| S-05 | Set new element | CREATED with auto-increment key |
| S-06 | Set identity match (merge) | Same VIN → UPDATED, properties merged |
| S-07 | Set different identity | Different VIN → CREATED as new element |
| S-08 | Update by key (merge) | Adds new properties, keeps existing |
| S-09 | Update by key (replace) | Replaces all properties |
| S-10 | Update not found | Returns false |
| S-11 | Set without schema | Throws/rejects (DefineType required first) |
| S-12 | Set missing identity key | Rejects if IdentityKey not in properties |
| S-13 | Search by type | Returns all elements of given type |
| S-14 | Search by filter | Subset match on properties |
| S-15 | Search cross-type | Filter matches across different types |
| S-16 | Sensor merge (realistic IoT) | DeviceId-based identity, property merge |
| S-17 | Print and PrintAll | Output format correct, Contains/Count work |
| S-18 | Concurrent Set (100 threads) | All elements stored, no data loss |
| S-19 | Concurrent Set same identity | Only one created, rest updated (no duplicates) |
| S-20 | Concurrent read/write | Mixed Search + Set, no corruption |
| S-21 | Concurrent DefineType | Multiple types defined simultaneously |
| S-22 | Owner tracking | Elements record their creator |
| S-23 | Search by owner | Filters elements by owner correctly |
| S-24 | UpdateTypeSchema success | Schema migration with index rebuild |
| S-25 | UpdateTypeSchema collision | Rejects if new keys cause identity collisions |
| S-26 | UpdateTypeSchema missing property | Rejects if existing elements lack new key |
| S-27 | UpdateTypeSchema undefined type | Rejects for non-existent type |
| S-28 | Identity key with special chars | Unit separator prevents delimiter collision |
| S-29 | CommitLog write and replay | State rebuilt correctly from log |
| S-30 | CommitLog compaction | Only latest entry per key retained |
| S-31 | CommitLog schema replay | DefineType entries replayed on startup |

### C++ Fault Injection Tests (`test_main.cpp`)

| # | Test | Validates |
|---|------|-----------|
| 1 | Write before Initialize | Returns `-1`, no crash |
| 2 | Oversized `dataSize` | Stores data safely, small buffer returns `-2` |
| 2b | Invalid `bufferSize` (0, negative) | Returns `-4`, no overflow |
| 3 | Read/Write after Shutdown | Returns `-1`, no crash |
| 4 | Null pointer arguments | Returns `-3`, no segfault |
| 5 | `HW_GetLastError` thread safety | 50 threads × 10,000 iterations, no corrupted reads |

### C# Concurrency Stress Tests (`HardwareLogicConcurrencyTests.cs`)

| # | Test | Validates |
|---|------|-----------|
| C-01 | Parallel write same key | No torn writes |
| C-02 | Parallel read/write same key | No data race |
| C-03 | Parallel write different keys | Data isolation |
| C-04 | Init/Shutdown race | Only one succeeds, no UB |
| C-05 | Read storm (100+ threads) | All return correct data |
| C-06 | Read/write stress (10 seconds) | Throughput measurement, no crash |
| C-07 | `GetLastError` thread safety | No corrupted error strings |
| C-08 | `GetStatusMessage` race | Returns valid string literals |
| C-09 | Shutdown interrupts read/write | Returns `-1` after shutdown |
| C-10 | Re-initialization cycle (1000×) | No memory leak, no crash |

### gRPC Integration Tests (`GrpcIntegrationTests.cs`)

End-to-end tests using realistic IdentityKeys (VIN, DeviceId, EmployeeId).

| # | Test | Validates |
|---|------|----------|
| T-01 | DefineType (Car, Sensor, Person) | Schema creation with realistic IdentityKeys |
| T-02 | DefineType duplicate | Returns failure for already-defined type |
| T-03 | GetTypeSchema | Returns correct IdentityKeys for a type |
| T-04 | SetElement new Car (VIN-based) | CREATED with auto-increment key |
| T-05 | SetElement identity match (same VIN) | UPDATED — same VIN = same car, properties merged |
| T-06 | SetElement different VIN | CREATED — different VIN = different car (even same Make+Model) |
| T-07 | SetElement explicit update by key | Direct update by key (merge mode) |
| T-08 | SetElement not found (invalid key) | Returns NOT_FOUND |
| T-09 | SetElement without schema | Returns error (DefineType must be called first) |
| T-10 | SetElement Sensor (IoT realistic data) | DeviceId-based identity, multiple properties |
| T-11 | Search by type | Returns all elements of a given type |
| T-12 | Search by filter (subset match) | Returns elements matching filter properties |
| T-13 | PrintAll (server streaming) | Streams all elements back to client |
| T-14 | BatchSet (client streaming) | Client sends multiple elements in batch |
| T-15 | GetCount (final count) | Total count matches expected |
| T-16 | Concurrent Set (50 unique VINs) | All 50 created without duplication |
| T-17 | Concurrent Set (same identity race) | Only one created, rest updated — no duplicates |
| T-18 | Concurrent read/write mix | 100 threads (50 write + 50 search), no corruption |
| T-19 | Owner tracking | Elements record creator client-id |
| T-20 | Search by owner | Filters elements by owner correctly |

## How to Run

### Prerequisites

- `libHardwareLogic.so` must be built first (`scripts/build/build_rhel.sh` or `scripts/pipeline_start.sh`).
- For integration tests: DMC Server must be running (`/opt/dmc/run.sh --server --port 5050`).

### Option 1: Test Script (recommended)

```bash
./scripts/test/test.sh
```

Runs both C++ and C# tests automatically.

### Option 2: Manual

```bash
cd camtek_kaijaytu/tests

# C++ tests
g++ -o test_main test_main.cpp \
    -I../src/HardwareLogic/include \
    -L../build/output/lib \
    -lHardwareLogic \
    -Wl,-rpath,../build/output/lib \
    -pthread
./test_main

# C# tests
export LD_LIBRARY_PATH=../build/output/lib:$LD_LIBRARY_PATH
dotnet build
dotnet run
```

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | All tests passed |
| `1` | One or more tests failed |
| `139` (SIGSEGV) | Segmentation fault — native code bug |
| `134` (SIGABRT) | Abort — uncaught C++ exception |
