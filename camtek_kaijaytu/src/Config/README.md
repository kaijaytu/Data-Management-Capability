# Config

> **Navigation**: [Project Root](../../../README.md) > [camtek_kaijaytu](../../README.md) > Config
>
> **Related**: [DMCCore](../DMCCore/README.md) | [Common](../Common/README.md)

Configuration management and runtime settings for the DMC system.

## Status: Not Yet Implemented

This module is reserved for future development (see [Roadmap](../../../README.md#roadmap)).

## Planned Purpose

Manage application configuration that can be:
- Loaded at startup from files (JSON, YAML, XML)
- Overridden by environment variables
- Changed at runtime without restart
- Shared across all modules via a centralized config store

## Planned Design

```text
┌─────────────────┐
│   Config Files   │  appsettings.json, environment variables
└────────┬────────┘
         ↓
┌─────────────────┐
│  ConfigManager  │  Singleton — loads, caches, provides config values
└────────┬────────┘
         ↓
┌─────────────────┐
│  DMCServer      │  Reads server port, hardware settings, etc.
│  HardwareBridge │  Reads hardware-specific parameters
│  Logger         │  Reads log level, output path
└─────────────────┘
```

## What Would Be Configured?

| Setting | Example | Used By |
|---------|---------|---------|
| gRPC server port | `5000` | Program.cs |
| Hardware init parameters | timeout, retry count | HardwareBridge |
| Log level | `Debug`, `Info`, `Error` | Logger |
| Log output path | `/var/log/dmc/` | Logger |
| Max elements in registry | `10000` | DMCServer |

## Why It's Needed

Currently, values like the server port are hardcoded or passed via command-line args:

```bash
./DMC --server --port 5000   # port is a CLI argument
```

A Config module would allow:

```json
// appsettings.json
{
  "Server": { "Port": 5000 },
  "Hardware": { "Timeout": 3000 },
  "Logging": { "Level": "Info", "Path": "/var/log/dmc/" }
}
```

## .NET Configuration Pattern

.NET provides built-in configuration support:

```csharp
// Typical .NET pattern
var builder = WebApplication.CreateBuilder();
var port = builder.Configuration.GetValue<int>("Server:Port");
```

Sources are layered (later overrides earlier):
1. `appsettings.json` (base)
2. `appsettings.{Environment}.json` (per environment)
3. Environment variables
4. Command-line arguments

## Implementation Notes

When implemented, this module should:
- Use `IConfiguration` from `Microsoft.Extensions.Configuration`
- Provide a `DMCConfig` class with strongly-typed properties
- Validate configuration at startup (fail fast on missing required values)
- Support reload without restart for non-critical settings
