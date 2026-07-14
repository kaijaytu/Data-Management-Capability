# Common

> **Navigation**: [Project Root](../../../README.md) > [camtek_kaijaytu](../../README.md) > Common
>
> **Related**: [DMCCore](../DMCCore/README.md) | [Bridge](../Bridge/README.md) | [HardwareLogic](../HardwareLogic/README.md)

Shared interfaces and abstractions used across the entire DMC system.

## Files

| File | Type | Purpose |
|------|------|---------|
| `IDataElement.cs` | Interfaces | Defines `IIdentifiable`, `ISearchable`, `IPrintable`, and `IDataElement` |
| `DataElements/GenericDataElement.cs` | Class | Universal implementation that accepts any type |

## Interface Hierarchy (V2)

```
+----------------------+  +----------------------+  +----------------------+
|   IIdentifiable      |  |   ISearchable        |  |   IPrintable         |
|                      |  |                      |  |                      |
| IsIdenticalTo(       |  | Matches(type?,       |  | Print()              |
|   type, props,       |  |   filters): bool     |  | ToDisplayString()    |
|   identityKeys)      |  |                      |  |                      |
+----------+-----------+  +----------+-----------+  +----------+-----------+
           |                         |                          |
           +------------+------------+--------------------------+
                        |
              +---------v-----------+
              |    IDataElement     |
              |                     |
              | Type: string        |
              | Key: string         |  <-- Server-assigned
              +---------+-----------+
                        |
              +---------v-----------+
              | GenericDataElement  |  implements all
              |                     |
              | Owner: string       |  <-- Tracks creator
              | Properties: Dict    |  <-- Arbitrary key-value
              +---------------------+
```

### Why Three Interfaces? (V1 had two: IKeyIdentifiable + IPrintable)

V1's `IKeyIdentifiable.GetKey()` made the **element** generate its own key from a client-chosen property.
This caused collisions (two Toyotas → same key) and key instability (changing a property changed the key).

V2 separates identity into three concerns:

| Interface | Who calls it | Purpose |
|-----------|-------------|---------|
| `IIdentifiable` | **Server** calls during `Set()` | Does incoming data match this element? (using IdentityKeys from Type schema) |
| `ISearchable` | **Server** calls during `Search()` | Does this element match a search filter? (subset comparison) |
| `IPrintable` | **Server** calls during `Print()` | Display this element without knowing its type |

The **Server** provides the IdentityKeys (from the Type schema) to `IsIdenticalTo()`.
The **element** doesn't decide what defines its identity — it just executes the comparison.
This separation means the identity strategy can change (via `UpdateTypeSchema`) without modifying any element code.

## IIdentifiable

**Purpose**: Let the Server check if incoming data matches an existing element's identity.

```csharp
public interface IIdentifiable
{
    bool IsIdenticalTo(string type, Dictionary<string, string> properties, List<string> identityKeys);
}
```

**Why the Server provides identityKeys**: The element doesn't know its own identity schema — the schema is defined per-Type at the Server level (like a DB UNIQUE constraint). The Server passes the IdentityKeys down, and the element just compares the specified properties.

## ISearchable

**Purpose**: Let the Server filter elements by partial property match.

```csharp
public interface ISearchable
{
    bool Matches(string? type, Dictionary<string, string> filters);
}
```

**Why**: Search uses **subset matching** (filters ⊆ element properties). This is different from identity matching which uses **exact match on specific keys**. Same polymorphic pattern: Server calls the interface, element executes the logic.

## IPrintable

**Purpose**: Let each Data Element decide how to display itself.

```csharp
public interface IPrintable
{
    void Print();
    string ToDisplayString();
}
```

**Why**: The Server calls `Print()` without knowing the element's type or properties. This is how DMC "prints Data Element without knowing the Data Element" (assessment requirement).

## IDataElement

**Purpose**: Combined interface for the DMC Server to work with.

```csharp
public interface IDataElement : IIdentifiable, ISearchable, IPrintable
{
    string Type { get; }
    string Key { get; set; }    // Server-assigned, not element-generated
}
```

## GenericDataElement (V2)

**Purpose**: A single class that handles ANY type of Data Element at runtime.

```csharp
public class GenericDataElement : IDataElement
{
    string Type;                        // e.g. "Car"
    string Key;                         // Server-assigned: "Car:1" (auto-increment)
    string Owner;                       // Tracks creator: "alice"
    Dictionary<string, string> Properties;  // e.g. {VIN=ABC123, Make=Toyota, Year=2024}
}
```

| Method | What it does |
|--------|-------------|
| `IsIdenticalTo(type, props, idKeys)` | Compares only the IdentityKey properties (e.g., VIN) |
| `Matches(type?, filters)` | Returns true if all filter key-values exist in Properties (subset match) |
| `Print()` | Outputs `[Car] VIN=ABC123, Make=Toyota  (Key: Car:1 @alice)` |
| `ToDisplayString()` | Same as Print but returns string |

**Why GenericDataElement instead of specific classes (Car, Person, etc.)?**

- Assessment requires "No need to redesign to handle any new Type of Data element"
- With GenericDataElement, adding a new type (e.g. Airplane) requires zero code changes
- User just enters: Type=Airplane, properties, and picks a key property
