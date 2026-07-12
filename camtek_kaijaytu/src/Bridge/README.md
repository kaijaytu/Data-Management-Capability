# Bridge

> **Navigation**: [Project Root](../../../README.md) > [camtek_kaijaytu](../../README.md) > Bridge
>
> **Related**: [Common](../Common/README.md) | [DMCCore](../DMCCore/README.md) | [HardwareLogic](../HardwareLogic/README.md)

P/Invoke interoperability layer connecting managed C# code to native C++ HardwareLogic.

## Files

| File | Type | Purpose |
|------|------|---------|
| `HardwareLogicNative.cs` | Static class | Raw P/Invoke declarations (`[DllImport]`) |
| `HardwareBridge.cs` | Class | Managed wrapper with error handling and state management |

## Architecture

```text
C# Application (DMCCore, Program.cs)
         ↓
┌────────────────────────┐
│    HardwareBridge      │  ← Safe C# API (Initialize, Shutdown, ReadData, WriteData)
│    (Managed Wrapper)   │
└───────────┬────────────┘
            ↓ calls
┌────────────────────────┐
│  HardwareLogicNative   │  ← [DllImport("libHardwareLogic")]
│  (P/Invoke Declarations)│
└───────────┬────────────┘
            ↓ native call
┌────────────────────────┐
│  libHardwareLogic.so   │  ← C++ shared library
│  (Native Code)         │
└────────────────────────┘
```

## HardwareLogicNative

Raw P/Invoke declarations. Maps C# method calls to C function exports.

| C# Method | C Function | Return |
|-----------|-----------|--------|
| `HW_Initialize()` | `int HW_Initialize()` | 0=success, -1=error |
| `HW_Shutdown()` | `int HW_Shutdown()` | 0=success, -1=error |
| `HW_GetStatus()` | `int HW_GetStatus()` | Status enum as int |
| `HW_GetStatusMessage()` | `const char* HW_GetStatusMessage()` | IntPtr → marshal manually |
| `HW_ReadData(id, buf, size)` | `int HW_ReadData(...)` | Bytes read, or negative on error |
| `HW_WriteData(id, data, size)` | `int HW_WriteData(...)` | 0=success |
| `HW_RunDiagnostics()` | `int HW_RunDiagnostics()` | 0=pass |
| `HW_GetLastError()` | `const char* HW_GetLastError()` | IntPtr → marshal manually |

### String Marshaling

Functions returning `const char*` use `IntPtr` return type (not `MarshalAs(LPStr)`):

```csharp
[DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr HW_GetStatusMessage();
```

**Why IntPtr instead of string?**
- C++ returns pointers to static/internal memory
- If .NET marshals as `string` with `MarshalAs(LPStr)`, it calls `CoTaskMemFree` on the pointer
- C++ didn't allocate that memory with `CoTaskMemAlloc` → crash: `free(): invalid pointer`
- Using `IntPtr` + `Marshal.PtrToStringAnsi()` copies the string without freeing the native memory

## HardwareBridge

Safe managed wrapper that adds:
- **State tracking** (`_initialized` flag)
- **Error messages** (reads `HW_GetLastError()` on failure)
- **Exception throwing** (converts error codes to exceptions)
- **Guard checks** (`EnsureInitialized()`)

| Method | What it does |
|--------|-------------|
| `Initialize()` | Calls native init, sets `_initialized = true` |
| `Shutdown()` | Calls native shutdown, sets `_initialized = false` |
| `GetStatus()` | Returns `HardwareStatus` enum |
| `GetStatusMessage()` | Marshals IntPtr → string |
| `ReadData(id)` | Reads bytes from native, returns string |
| `WriteData(id, data)` | Writes string to native store |
| `RunDiagnostics()` | Returns bool (pass/fail) |
| `GetLastError()` | Marshals last error message |

## Why Two Layers?

| Layer | Responsibility |
|-------|---------------|
| `HardwareLogicNative` | 1:1 mapping to C functions. No logic. Minimal. |
| `HardwareBridge` | Safe API for C# consumers. Handles errors, state, marshaling. |

Separating them means:
- If the C API changes, only `HardwareLogicNative` changes
- Application code only uses `HardwareBridge`, never touches raw P/Invoke
- Testing can mock `HardwareBridge` without a real native library
