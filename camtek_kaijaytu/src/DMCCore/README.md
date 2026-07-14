# DMCCore

> **Navigation**: [Project Root](../../../README.md) > [camtek_kaijaytu](../../README.md) > DMCCore
>
> **Related**: [Common](../Common/README.md) | [Bridge](../Bridge/README.md) | [HardwareLogic](../HardwareLogic/README.md)

Core server logic — the Singleton DMC Server that manages all Data Elements.

## Files

| File | Type | Purpose |
|------|------|---------|
| `DMCServer.cs` | Class | Singleton server with Schema (DefineType) + Data (Set/Search/Print) operations |
| `CommitLog.cs` | Class | Kafka-style append-only commit log for persistence |
| `DMCGrpcService.cs` | Class | gRPC service implementation (maps RPCs to DMCServer) with structured logging |
| `DMCClient.cs` | Class | Interactive gRPC client with client-id support |

## DMCServer (V2)

**Pattern**: Singleton (thread-safe, double-checked locking)

**Architecture**: Two-layer design — Schema layer + Data layer

```text
DMCServer (Singleton)
├── _instance: DMCServer              ← single global instance
├── _lock: object                     ← thread safety (all public methods)
├── _typeSchemas: Dictionary          ← Type → IdentityKeys (per-Type schema)
├── _typeCounters: Dictionary         ← Type → auto-increment counter
├── _identityIndex: Dictionary        ← "Type:val1:val2" → Key (O(1) lookup)
└── _registry: Dictionary             ← Key → IDataElement (data storage)
```

### Why This Data Structure?

| Structure | Purpose | Why |
|-----------|---------|-----|
| `_typeSchemas` | Store IdentityKeys per Type | Schema defined once, enforced on every Set. Like MongoDB's `createIndex({unique:true})` |
| `_identityIndex` | Fast identity lookup | Without index: O(N) scan on every Set. With index: O(1) dictionary lookup |
| `_typeCounters` | Auto-increment key generation | Keys independent of data content (Car:1, Car:2...). Changing properties never changes the key |
| `_registry` | Element storage | The actual data pool. Key is server-generated, value is IDataElement |

### Public API — Schema Layer

| Method | Signature | Behavior |
|--------|-----------|----------|
| `DefineType` | `bool DefineType(type, identityKeys)` | Define IdentityKeys for a Type. Must be called before Set(). Returns false if already defined |
| `GetTypeSchema` | `List<string>? GetTypeSchema(type)` | Returns IdentityKeys for a Type, or null |
| `UpdateTypeSchema` | `(bool, List) UpdateTypeSchema(type, newKeys)` | Validate collisions → update schema + rebuild index. Returns conflicts if rejected |

### Public API — Data Layer

| Method | Signature | Behavior |
|--------|-----------|----------|
| `Set` | `SetResult Set(type, props, key?, merge?, owner?)` | No key: identity match → Update or Create. With key: explicit update |
| `Update` | `bool Update(key, props, merge?)` | Update by key. Merge (default) or Replace |
| `Search` | `IEnumerable Search(type?, filters, owner?)` | Subset match via ISearchable + optional owner filter. Returns deep copies |
| `Get` | `IDataElement? Get(key)` | Get single element by key. Returns deep copy |
| `GetAll` | `IEnumerable GetAll()` | Get all elements. Returns deep copies |
| `Print` | `bool Print(key)` | Print single element |
| `PrintAll` | `void PrintAll()` | Print all elements |

### How Set Works (V2)

```text
Set(type, properties, owner):
    1. Validate schema exists: _typeSchemas[type]
    2. Validate all IdentityKeys present in properties
    3. lock(_lock):
       a. Build identity key: "Car:Toyota:Camry" (from IdentityKeys)
       b. Check _identityIndex: O(1) lookup
          → Match found: UpdateProperties (merge/replace) → return UPDATED
          → No match: GenerateKey (auto-increment) → Add to registry + index → return CREATED
    4. Return SetResult { Action, Key }
```

**Why lock instead of ConcurrentDictionary?**
Set() is a compound check-then-act: check index → create if not found. With ConcurrentDictionary, two threads could both see "not found" and both create → duplicate. Lock ensures the entire operation is atomic.

### Deep Copy (Defensive Copy)

`Search`, `Get`, `GetAll` return **cloned elements** with copied Properties dictionaries.

```text
Without clone: Search() → lock → return reference → unlock → iterate Properties
               Another thread: lock → modify same Properties → unlock → 💥 Collection modified!

With clone:    Search() → lock → copy Properties → unlock → iterate safely on own copy ✅
```

Lock protects the *access*, not the *usage*. Data leaving the lock must be a snapshot.

## CommitLog (Kafka-style Persistence)

**Design principle**: Log is the source of truth, _registry is a derived view.

```text
CommitLog
├── _logPath: string              ← ~/.dmc/dmc_commit.log
├── _currentOffset: long          ← monotonically increasing
├── _fileLock: object             ← file write safety
│
├── Append(operation, type, ...)  ← Write before memory (WAL)
├── ReadAll()                     ← Replay on startup
└── Compact()                     ← Keep latest per key (Kafka-style)
```

### Integration with DMCServer

Every write operation follows the WAL pattern:

```text
Set():
    lock(_lock):
        1. _commitLog.Append(...)   ← disk write first (persist)
        2. _registry.Add(...)       ← memory write second (derived)
        3. return result

Crash at step 2? → Log has the entry, replay recovers it.
Crash at step 1? → Nothing written, nothing to recover. Consistent.
```

### Replay on Startup

```text
EnableCommitLog(path):
    for each entry in CommitLog.ReadAll():
        switch entry.Operation:
            DefineType    → _typeSchemas[type] = identityKeys
            Set           → identity match → create or update element
            Update        → update properties
            UpdateSchema  → update schema + rebuild identity index
    SyncCounter() → ensure auto-increment doesn't collide after restart
```

## DMCGrpcService (V2)

Maps gRPC RPC calls to `DMCServer` operations. Includes structured logging for every request.

**Log format**: `[timestamp] [client-id@peer] ACTION detail`

| RPC | Delegates To | Log Example |
|-----|-------------|-------------|
| `DefineType` | `DMCServer.DefineType()` | `[18:30:01] [alice@ipv4:...] DEFINE_TYPE OK Car[VIN]` |
| `SetElement` | `DMCServer.Set()` | `[18:30:02] [alice@...] SET Created key=Car:1 type=Car owner=alice` |
| `Search` | `DMCServer.Search()` | `[18:30:03] [alice@...] SEARCH type=Car filters=Make=Toyota → 2 found` |
| `Print` | `DMCServer.Get()` → `ToDisplayString()` | `[18:30:04] [alice@...] PRINT key=Car:1 → found` |
| `PrintAll` | `DMCServer.GetAll()` → stream | `[18:30:05] [alice@...] PRINT_ALL streaming 3 element(s)` |
| `BatchSet` | `DMCServer.Set()` per element | `[18:30:06] [alice@...] BATCH_SET done: received=3 created=3` |

**Client identity**: Extracted from gRPC metadata header `client-id`. Defaults to `"anonymous"`.

## DMCClient (V2)

Interactive console client that connects to a running DMC gRPC server.

**Commands**:

| Category | Command | Description |
|----------|---------|-------------|
| Schema | `define-type` | Define IdentityKeys for a Type |
| Schema | `update-schema` | Update IdentityKeys (validates collisions) |
| Schema | `schema` | View current IdentityKeys for a Type |
| Data | `set` | Set element (Server decides create/update) |
| Data | `search` | Search by type and/or properties |
| Data | `print` | Print single element by key |
| Data | `printall` | Stream all elements |
| Data | `batch` | Batch set multiple elements |
| Utility | `count` | Element count |
| Utility | `contains` | Check if key exists |

**Client identity**: Set via `--client-id` flag or auto-generated as `client-{PID}`.

### Singleton Guarantee

```csharp
// Thread-safe double-checked locking
public static DMCServer Instance
{
    get
    {
        if (_instance == null)
            lock (_lock)
                if (_instance == null)
                    _instance = new DMCServer();
        return _instance;
    }
}
```

All clients calling `DMCServer.Instance` get the same object reference.

### Why Singleton?

Assessment requirement: "How to make sure all Clients are access to the same DMC Server."

- Private constructor → cannot create new instances externally
- Static Instance property → everyone gets the same one
- Lock → safe for multiple threads/clients
