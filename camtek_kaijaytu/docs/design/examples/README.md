# Design Examples

> **Navigation**: [Project Root](../../../../README.md) > [camtek_kaijaytu](../../../README.md) > Design Examples
>
> **Related**: [Common](../../../src/Common/README.md) | [DMCCore](../../../src/DMCCore/README.md)

Archived concrete Data Element classes and Factory pattern implementation.

These files were the original design that used **specific classes per type** (Car, MobilePhone, Person, TV). They have been superseded by `GenericDataElement` which handles any type at runtime without code changes.

## Files

| File | Original Purpose | Why Archived |
|------|-----------------|--------------|
| `Car.cs` | Concrete class for Car type | Replaced by GenericDataElement |
| `MobilePhone.cs` | Concrete class for MobilePhone type | Replaced by GenericDataElement |
| `Person.cs` | Concrete class for Person type | Replaced by GenericDataElement |
| `TV.cs` | Concrete class for TV type | Replaced by GenericDataElement |
| `DataElementFactory.cs` | Factory to create elements by type name | No longer needed — GenericDataElement handles all types |

## Design Evolution

```text
Version 1 (these files):
  IDataElement
       ↑
  ┌────┼────┬────┐
  Car  Phone Person TV     ← One class per type (requires code change for new types)
  
  DataElementFactory       ← Maps type name → constructor (requires registration)

Version 2 (current):
  IDataElement (IKeyIdentifiable + IPrintable)
       ↑
  GenericDataElement        ← Handles ALL types with arbitrary properties
                              No code change needed for new types
```

## Value as Reference

These files still demonstrate:
- How to implement `IDataElement` with a specific class
- How strongly-typed properties work (e.g. `decimal Price`, `int Year`)
- How Factory Pattern maps type names to constructors
- How polymorphic `Print()` works with different formatting per type

If the project later needs type-specific validation or behavior, these examples show how to create specialized classes alongside `GenericDataElement`.
