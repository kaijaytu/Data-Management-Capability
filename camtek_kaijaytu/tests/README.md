# Tests

> **Navigation**: [Project Root](../../README.md) > [camtek_kaijaytu](../README.md) > Tests

Automated test suite for the DMC HardwareLogic native library.

## Files

| File | Language | Purpose |
|------|----------|---------|
| `test_main.cpp` | C++ | Fault injection tests — directly calls `libHardwareLogic.so` API |
| `HardwareLogicConcurrencyTests.cs` | C# | Concurrency stress tests — 100 threads via P/Invoke |
| `GrpcIntegrationTests.cs` | C# | gRPC integration tests — end-to-end client-server scenarios |
| `DMC.Tests.csproj` | Project | .NET 8.0 project file for unit/concurrency tests |
| `DMC.IntegrationTests.csproj` | Project | .NET 8.0 project file for gRPC integration tests |

## Test Categories

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

| # | Test | Validates |
|---|------|-----------|
| T-01 | Register new element | Returns success + correct key |
| T-02 | Register duplicate key | Routes to Update |
| T-03 | Contains | Exists / not exists |
| T-04 | GetCount | Count is correct |
| T-05 | Print single element | Display string correct |
| T-06 | Print not found | Returns Found=false |
| T-07 | Update existing | Properties updated |
| T-08 | Update non-existing | Returns false |
| T-09 | PrintAll (server streaming) | Streams all elements |
| T-10 | BatchRegister (client streaming) | Batch of 3 elements |
| T-11 | Final count | Total ≥ 4 |
| T-12 | Error handling | Empty key doesn't crash |

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
