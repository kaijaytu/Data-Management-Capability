# HardwareLogic

> **Navigation**: [Project Root](../../../README.md) > [camtek_kaijaytu](../../README.md) > HardwareLogic
>
> **Related**: [Common](../Common/README.md) | [DMCCore](../DMCCore/README.md) | [Bridge](../Bridge/README.md)

Native C++17 shared library providing low-level hardware control operations.

## Files

| File | Type | Purpose |
|------|------|---------|
| `CMakeLists.txt` | Build config | CMake build definition for shared library |
| `include/hardware_logic.h` | Header | C API exported functions |
| `src/hardware_logic.cpp` | Implementation | Hardware simulation and state management |

## Build Output

```text
libHardwareLogic.so   (Linux shared library)
```

## API (C Interface)

All functions are exported as `extern "C"` for P/Invoke compatibility.

| Function | Signature | Description |
|----------|-----------|-------------|
| `HW_Initialize` | `int ()` | Initialize hardware. Returns 0 on success. |
| `HW_Shutdown` | `int ()` | Shutdown and clear state. Returns 0 on success. |
| `HW_GetStatus` | `int ()` | Returns status: 0=Uninitialized, 1=Ready, 2=Busy, -1=Error |
| `HW_GetStatusMessage` | `const char* ()` | Returns human-readable status string |
| `HW_ReadData` | `int (id, buf, size)` | Read element data into buffer. Returns bytes read. |
| `HW_WriteData` | `int (id, data, size)` | Write element data. Returns 0 on success. |
| `HW_RunDiagnostics` | `int ()` | Run self-test. Returns 0 on pass. |
| `HW_GetLastError` | `const char* ()` | Returns last error message (thread-safe snapshot) |

### Error Codes

| Code | Meaning |
|------|--------|
| `0` | Success |
| `-1` | Operation failed (not ready, not found, etc.) |
| `-2` | Buffer too small |
| `-3` | Null pointer argument |
| `-4` | Invalid buffer/data size (zero or negative) |

## Internal Design

```text
┌─────────────────────────────────────────┐
│           Global State (anonymous ns)    │
├─────────────────────────────────────────┤
│ g_status : HWStatus (enum)              │
│ g_lastError : std::string               │
│ g_dataStore : unordered_map<string,string> │
│ g_mutex : std::mutex                    │
└─────────────────────────────────────────┘
```

- **Thread safety**: All functions lock `g_mutex` before accessing shared state
- **`HW_GetLastError`**: Uses `thread_local` buffer to return a safe snapshot, preventing pointer invalidation from concurrent writes
- **Input validation**: Null pointer checks (return `-3`) and size validation (return `-4`) are performed before acquiring the lock
- **Data store**: In-memory `unordered_map` simulates hardware storage
- **Status machine**: Uninitialized → Ready → (operations) → Shutdown

## Status Flow

```text
         HW_Initialize()
              ↓
Uninitialized ──→ Ready
                    ↓
              ReadData / WriteData / RunDiagnostics
                    ↓
         HW_Shutdown()
              ↓
           Uninitialized (reset)
```

## Build

```bash
mkdir build && cd build
cmake ../HardwareLogic -DCMAKE_BUILD_TYPE=Release
cmake --build .
# Output: libHardwareLogic.so
```

## Why C++?

- Hardware control requires deterministic, low-latency execution
- Direct memory and peripheral access
- Thread-safe mutex locking for concurrent access from managed code
- The C API (`extern "C"`) ensures compatibility with P/Invoke from C#
