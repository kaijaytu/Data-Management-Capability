# DMCCore

> **Navigation**: [Project Root](../../../README.md) > [camtek_kaijaytu](../../README.md) > DMCCore
>
> **Related**: [Common](../Common/README.md) | [Bridge](../Bridge/README.md) | [HardwareLogic](../HardwareLogic/README.md)

Core server logic — the Singleton DMC Server that manages all Data Elements.

## Files

| File | Type | Purpose |
|------|------|---------|
| `DMCServer.cs` | Class | Singleton server with Register/Update/Print operations |

## DMCServer

**Pattern**: Singleton (thread-safe, double-checked locking)

**Data Structure**: `Dictionary<string, IDataElement>` — shared data pool

```text
DMCServer (Singleton)
├── _instance: DMCServer          ← single global instance
├── _lock: object                 ← thread safety
└── _registry: Dictionary         ← Key: element.GetKey(), Value: IDataElement
```

### Public API

| Method | Signature | Behavior |
|--------|-----------|----------|
| `Instance` | `static DMCServer` | Returns the single server instance |
| `Register` | `bool Register(IDataElement)` | If key exists → Update; else → Add |
| `Update` | `bool Update(IDataElement)` | Replace element at key; false if not found |
| `Print` | `bool Print(string key)` | Call element.Print() by key |
| `PrintAll` | `void PrintAll()` | Call Print() on every element in registry |
| `Contains` | `bool Contains(string key)` | Check if key exists |
| `Count` | `int` | Number of elements in system |

### How Register Works

```text
Register(element):
    key = element.GetKey()           ← IKeyIdentifiable
    if _registry.ContainsKey(key):
        → Update(element)            ← Element already exists
    else:
        → _registry.Add(key, element) ← New element
```

### How PrintAll Works

```text
PrintAll():
    foreach element in _registry.Values:
        element.Print()              ← IPrintable (polymorphism)
```

The server never knows what type the element is. It only uses the interface methods.

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
