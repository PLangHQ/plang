# v1 Summary — Engine.Types (Phase 1)

## What this is

Phase 1 of the Data Envelope Architecture: a new `Engine.Types` class that consolidates all type knowledge from three scattered sources into a single live instance on the Engine root. This is the foundation for Phases 2-4 (Type context + lazy derivation, Data partial class split, envelope pipeline).

Previously, type knowledge was spread across:
- `TypeMapping` (static, Engine/Utility) — PLang names ↔ CLR types
- `TypeMapping` (instance, FileModule) — file extension → category/MIME
- `MimeTypeHelper` (static, Utils) — redundant MIME mapping

## What was done

Created `Engine.Types` as a new `@this` class following OBP conventions:

### Files created
- **`PLang/Runtime2/Engine/Types/this.cs`** — New class owning all type knowledge with OBP-compliant API (noun names: `Clr`, `Name`, `Kind`, `Mime`, `Compressible`). Absorbs PLang name ↔ CLR type dictionaries, file extension → Kind mappings, extension → MIME mappings, and Kind compressibility data. Supports runtime extensibility via `Add`/`Remove`.
- **`PLang.Tests/Runtime2/Types/EngineTypesTests.cs`** — 62 TUnit tests covering all public methods.

### Files modified
- **`PLang/Runtime2/Engine/this.cs`** — Added `Types` property, initialized in constructor.
- **`PLang/Runtime2/GlobalUsings.cs`** — Added `EngineTypes` global alias.
- **`PLang.Tests/GlobalUsings.cs`** — Added `EngineTypes` global alias for tests.

### Key decisions
- **Additive, no breaking changes** — Old static `TypeMapping` stays as-is. Data.cs has no Engine access yet (Phase 2 wires them together).
- **Stateless helpers stay static** — `ConvertTo()`, `IsPrimitive()` remain on `TypeMapping` since they don't need runtime extensibility.
- **`.key` conflict resolved** — "certificate" wins over "presentation" (architect's decision).
- **OBP compliance** — One-word noun API names, instance on Engine, no `Get` prefix.

### What's next
- **Phase 2**: Type gets context + lazy derivation. Data gets late-bound context. Data's constructor/Value setter switch from static `TypeMapping` to `Engine.Types` via context navigation.
- **Phase 3**: Data partial class split + Out view.
- **Phase 4**: Envelope pipeline (Wrap/Compress/Encrypt).

## Code example

```csharp
// Engine.Types API
engine.Types.Clr("string")         // → typeof(string)
engine.Types.Clr("list<int>")      // → typeof(List<int>)
engine.Types.Name(typeof(byte[]))   // → "bytes"
engine.Types.Kind(".jpg")           // → "image"
engine.Types.Mime(".xlsx")           // → "application/vnd.openxmlformats-..."
engine.Types.Compressible("text")   // → true
engine.Types.Compressible("image")  // → false

// Runtime extensibility
engine.Types.Add(".plx", "plang-extension", "application/x-plang");
engine.Types.Remove(".plx");
```
