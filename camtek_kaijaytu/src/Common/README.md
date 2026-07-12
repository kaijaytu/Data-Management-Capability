# Common

> **Navigation**: [Project Root](../../../README.md) > [camtek_kaijaytu](../../README.md) > Common
>
> **Related**: [DMCCore](../DMCCore/README.md) | [Bridge](../Bridge/README.md) | [HardwareLogic](../HardwareLogic/README.md)

Shared interfaces and abstractions used across the entire DMC system.

## Files

| File | Type | Purpose |
|------|------|---------|
| `IDataElement.cs` | Interfaces | Defines `IKeyIdentifiable`, `IPrintable`, and `IDataElement` |
| `DataElements/GenericDataElement.cs` | Class | Universal implementation that accepts any type |

## Interface Hierarchy

```text
IKeyIdentifiable          IPrintable
│                         │
│ + GetKey(): string      │ + Print(): void
│                         │ + ToDisplayString(): string
└────────┐       ┌────────┘
         ↓       ↓
      IDataElement
      │
      │ + Type: string
      │
      ↑
GenericDataElement (implements all)
```

## IKeyIdentifiable

**Purpose**: Let each Data Element decide what makes it unique.

```csharp
public interface IKeyIdentifiable
{
    string GetKey();  // Element returns its own unique key
}
```

**Why**: The Server doesn't know what properties an element has. Instead of forcing a fixed "ID" field, each element computes its own key from its own data.

**Example**: A Car might use `"Car:Toyota"`, a Person might use `"Person:John"`.

## IPrintable

**Purpose**: Let each Data Element decide how to display itself.

```csharp
public interface IPrintable
{
    void Print();              // Output to console
    string ToDisplayString();  // Return as string (for logging, transport, etc.)
}
```

**Why**: The Server calls `Print()` without knowing the element's type or properties. This is how DMC "prints Data Element without knowing the Data Element" (assessment requirement).

## IDataElement

**Purpose**: Combined interface for the DMC Server to work with.

```csharp
public interface IDataElement : IKeyIdentifiable, IPrintable
{
    string Type { get; }  // The type name (e.g. "Car", "Person", "Animal")
}
```

**Why**: The Server only depends on `IDataElement`. It can:
- Store by key (`GetKey()`)
- Print without knowing internals (`Print()`)
- Query the type name (`Type`)

## GenericDataElement

**Purpose**: A single class that handles ANY type of Data Element at runtime.

```csharp
public class GenericDataElement : IDataElement
{
    string Type;                        // e.g. "Car"
    Dictionary<string, string> Properties;  // e.g. {Make=Toyota, Year=2024}
    string KeyProperty;                 // e.g. "Make"
}
```

| Method | What it does |
|--------|-------------|
| `GetKey()` | Returns `"{Type}:{Properties[KeyProperty]}"` → e.g. `"Car:Toyota"` |
| `Print()` | Outputs `[Car] Make=Toyota, Year=2024 (Key: Car:Toyota)` |
| `ToDisplayString()` | Same as Print but returns string |

**Why GenericDataElement instead of specific classes (Car, Person, etc.)?**

- Assessment requires "No need to redesign to handle any new Type of Data element"
- With GenericDataElement, adding a new type (e.g. Airplane) requires zero code changes
- User just enters: Type=Airplane, properties, and picks a key property
