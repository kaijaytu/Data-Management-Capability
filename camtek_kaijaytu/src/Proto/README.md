# Proto

> **Navigation**: [Project Root](../../../README.md) > [camtek_kaijaytu](../../README.md) > Proto
>
> **Related**: [DMCCore](../DMCCore/README.md) | [Common](../Common/README.md)

gRPC Protocol Buffers service definition for the DMC system.

## Files

| File | Purpose |
|------|---------|
| `dmc.proto` | Defines the gRPC service contract, RPC methods, and message types |

## What is Protocol Buffers (Protobuf)?

Protocol Buffers is Google's language-neutral serialization format. A `.proto` file defines:

1. **Messages** — data structures (like C# classes or C++ structs)
2. **Services** — RPC method signatures (like C# interfaces)

At build time, `Grpc.Tools` reads this `.proto` file and auto-generates:
- C# client stub (`DMCService.DMCServiceClient`)
- C# server base class (`DMCService.DMCServiceBase`)
- C# message classes (`RegisterRequest`, `RegisterResponse`, etc.)

```text
dmc.proto
    ↓ Grpc.Tools (compile time)
    ├── DMCService.DMCServiceClient    ← used by DMCClient.cs
    ├── DMCService.DMCServiceBase      ← inherited by DMCGrpcService.cs
    └── Message classes                ← RegisterRequest, DataElementMessage, etc.
```

## Why .proto Instead of Writing C# Directly?

| Approach | Pros | Cons |
|----------|------|------|
| Hand-written C# classes | Full control | Client and server must manually stay in sync |
| `.proto` + code generation | Single source of truth, cross-language | Need protobuf tooling |

With `.proto`:
- Change the contract in ONE file → regenerate → both sides update
- Can generate Java, Python, Go clients from the same `.proto`
- Binary serialization (smaller + faster than JSON)

## Service Definition Explained

### RPC Types

gRPC supports 4 communication patterns:

```text
1. Unary:           Client sends 1 request  → Server returns 1 response
2. Server streaming: Client sends 1 request  → Server returns N responses (stream)
3. Client streaming: Client sends N requests → Server returns 1 response
4. Bidirectional:   Client sends N requests ↔ Server returns N responses
```

### DMCService Methods

```protobuf
service DMCService {
  rpc Register (RegisterRequest) returns (RegisterResponse);              // Unary
  rpc Update (UpdateRequest) returns (UpdateResponse);                    // Unary
  rpc Print (PrintRequest) returns (PrintResponse);                      // Unary
  rpc PrintAll (PrintAllRequest) returns (stream DataElementMessage);     // Server streaming
  rpc BatchRegister (stream RegisterRequest) returns (BatchRegisterResponse); // Client streaming
  rpc GetCount (Empty) returns (CountResponse);                          // Unary
  rpc Contains (ContainsRequest) returns (ContainsResponse);             // Unary
}
```

| Method | Pattern | Why this pattern? |
|--------|---------|-------------------|
| `Register` | Unary | One element in, one result back |
| `Update` | Unary | Same as Register |
| `Print` | Unary | Request one element's display string |
| `PrintAll` | **Server streaming** | Server may have many elements; client receives them one by one without loading all into memory |
| `BatchRegister` | **Client streaming** | Client sends many elements; server processes them as they arrive, returns summary |
| `GetCount` | Unary | Simple query |
| `Contains` | Unary | Simple query |

### Message Structure Explained

```protobuf
message RegisterRequest {
  string type = 1;                      // Field number 1: element type name
  repeated KeyValuePair properties = 2; // Field number 2: list of key-value pairs
  string key_property = 3;              // Field number 3: which property is the key
}
```

**Key concepts:**
- `string type = 1;` — the `= 1` is a **field number**, not a default value. Used for binary encoding.
- `repeated` — means a list/array (0 or more items)
- Field numbers must be unique within a message and should never be reused (even after deletion)

### How Messages Map to C# Code

| Proto | Generated C# |
|-------|-------------|
| `string type = 1` | `public string Type { get; set; }` |
| `repeated KeyValuePair properties = 2` | `public RepeatedField<KeyValuePair> Properties { get; }` |
| `bool success = 1` | `public bool Success { get; set; }` |
| `int32 count = 1` | `public int Count { get; set; }` |

## Build Integration

In `DMC.csproj`:

```xml
<ItemGroup>
  <Protobuf Include="Proto\dmc.proto" GrpcServices="Both" />
</ItemGroup>
```

- `GrpcServices="Both"` — generate both client and server code
- `GrpcServices="Server"` — server only
- `GrpcServices="Client"` — client only

## How to Modify the Service

1. Edit `dmc.proto` (add/change messages or RPC methods)
2. Rebuild (`dotnet build`) — code is regenerated automatically
3. Update `DMCGrpcService.cs` to implement new/changed methods
4. Update `DMCClient.cs` to call new/changed methods

**Never edit the generated code directly** — it will be overwritten on next build.
