# Logger

> **Navigation**: [Project Root](../../../README.md) > [camtek_kaijaytu](../../README.md) > Logger
>
> **Related**: [Config](../Config/README.md) | [DMCCore](../DMCCore/README.md)

Logging subsystem for diagnostics, debugging, and system tracing.

## Status: Not Yet Implemented

This module is reserved for future development (see [Roadmap](../../../README.md#roadmap)).

## Current State

Most output currently uses `Console.WriteLine()`. However, `DMCGrpcService.cs` already implements basic structured logging with timestamps, client-id, and peer address:

```csharp
// DMCGrpcService.cs — structured request logging
Console.WriteLine($"  [{Timestamp}] [{GetClientId(ctx)}@{Peer(ctx)}] {action,-14} {detail}");

// Program.cs — startup info
Console.WriteLine($"DMC Server initialized. Elements: {server.Count} (replayed {replayed} log entries)");

// GenericDataElement.Print() — element self-printing
Console.WriteLine(ToDisplayString());
```

This works for development but lacks a dedicated Logger module with:
- Configurable log levels (Debug/Info/Warning/Error)
- File output / log rotation
- Formal structured logging framework
- Correlation IDs for distributed tracing

## Planned Design

```text
┌───────────────┐
│  ILogger      │  Interface — abstracts logging
├───────────────┤
│ + Debug()     │
│ + Info()      │
│ + Warning()   │
│ + Error()     │
└───────┬───────┘
        ↑
   ┌────┼────────────┐
   │                 │
┌──┴──────────┐  ┌───┴──────────┐
│ ConsoleLogger│  │  FileLogger  │
└─────────────┘  └──────────────┘
```

### Log Levels

| Level | When to Use | Example |
|-------|------------|---------|
| `Debug` | Development details | `"GetKey() returned: Car:Toyota"` |
| `Info` | Normal operations | `"Element registered: Car:Toyota"` |
| `Warning` | Unexpected but handled | `"Register called for existing key, routing to Update"` |
| `Error` | Failures | `"Hardware initialization failed: timeout"` |

### Log Format

```text
[2026-07-13 04:30:15.123] [INFO]  [DMCServer] Element registered: Car:Toyota
[2026-07-13 04:30:15.456] [WARN]  [DMCServer] Key already exists, routing to Update: Car:Toyota
[2026-07-13 04:30:16.789] [ERROR] [HardwareBridge] HW_Initialize failed: device not found
```

## Why a Custom Logger Instead of Console.WriteLine?

| Console.WriteLine | Logger |
|-------------------|--------|
| No log level — everything prints | Filter by level (e.g. only errors in production) |
| No timestamp | Automatic timestamps |
| Console only | Console + file + network + any sink |
| No context | Source module, correlation ID |
| Can't turn off | Set level to `None` to suppress all |

## .NET Logging Options

| Option | Description |
|--------|-------------|
| `Microsoft.Extensions.Logging` | Built-in .NET abstraction (`ILogger<T>`) |
| `Serilog` | Popular structured logging library |
| `NLog` | Flexible, file-based logging |
| Custom `ILogger` | Full control, minimal dependencies |

## Where Logging Would Be Added

| Module | What to Log |
|--------|-------------|
| `DMCServer.Register()` | Element key, success/update routing |
| `DMCServer.PrintAll()` | Element count, timing |
| `DMCGrpcService` | Incoming requests, response status |
| `HardwareBridge` | Init/shutdown, errors, diagnostics |
| `Program.cs` | Startup mode, port, shutdown |

## Implementation Notes

When implemented:
- Inject `ILogger` via constructor or Singleton
- Read log level and output path from Config module
- Write to `/var/log/dmc/` on RHEL (user needs write permission)
- Rotate log files by size or date
- Never log sensitive data (credentials, internal addresses)
