# v2 Summary — Type Context + Lazy Derivation (Phase 2)

## What this is

Phase 2 of the Data Envelope Architecture: Type becomes a navigator that lazily reaches Engine.Types through context for Kind, Compressible, and ClrType. Data gets a late-bound context property. Type derivation becomes lazy — no eager creation on Value set. MemoryStack and PLangContext propagate context to all Data automatically.

Previously, Type's `ClrType` was derived eagerly via static `TypeMapping.GetType()` every time a Data was constructed or its Value changed. Type had no concept of Kind or Compressible. There was no way for Data to reach Engine.Types without passing Engine through every call chain.

## What was done

### Files modified

- **`PLang/Runtime2/Engine/Types/this.cs`** — Added constructor to build `_allKinds` (HashSet of all known kinds) and `_mimeToKind` (MIME → kind reverse map). Added `KindOf(string typeValue)` method that resolves PLang type values (kind names, MIME types) to their kind.

- **`PLang/Runtime2/Engine/Memory/Data.cs`** — Three changes:
  1. **Type class**: Added `Context` property (internal), `Kind` and `Compressible` navigation properties. `ClrType` now navigates through context with fallback to static TypeMapping.
  2. **Data class**: Added `Context` property (public, JsonIgnore) that propagates to Type. Constructor stores explicit type only (no eager derivation). Value setter invalidates type (`_type = null`). Type getter lazily derives through context or static TypeMapping fallback. GetChild stamps parent's context on child.
  3. **Data<T>** and **DynamicData**: Inherit context behavior from base Data unchanged.

- **`PLang/Runtime2/Engine/Memory/MemoryStack.cs`** — Added `Context` property (internal) with setter that stamps all existing Data. Put and Set stamp context on Data they add.

- **`PLang/Runtime2/Engine/Context/PLangContext.cs`** — Constructor sets `MemoryStack.Context = this` before RegisterContextVariables, ensuring all Data gets context.

- **`PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Actions/Action/Methods.cs`** — Stamps `result.Context = context` on handler result Data before return variable binding.

### Files modified (tests)

- **`PLang.Tests/Runtime2/Types/EngineTypesTests.cs`** — Added 6 KindOf tests (known kind, case insensitive, MIME→kind, PLang name→null, unknown MIME, null/empty).

- **`PLang.Tests/Runtime2/Memory/DataTests.cs`** — Added 12 Phase 2 tests (context propagation, lazy derivation with/without context, invalidation on Value set, explicit type preserved, Type setter stamps context, Kind with/without context, Compressible, GetChild inherits context).

- **`PLang.Tests/Runtime2/Memory/MemoryStackTests.cs`** — Added 5 Phase 2 tests (PLangContext stamps MemoryStack data, Put/Set stamp context, pre-existing data gets context, clone has no context, child context stamps cloned data).

### Key decisions

- **Lazy derivation over eager**: Type is only derived when accessed. This eliminates unnecessary Type allocations when Data is constructed but Type is never read.
- **Static TypeMapping fallback**: Contextless Types (from static factories, tests, etc.) fall back to `TypeMapping.GetType()`. This keeps all existing code working without context.
- **Context stamping at MemoryStack level**: Rather than requiring every caller to stamp context, MemoryStack.Put/Set do it automatically. PLangContext stamps the MemoryStack once, which propagates to all existing Data.
- **KindOf uses `TryGetValue` on HashSet**: Returns the canonical stored value (lowercase) rather than echoing the input case.

## Code example

```csharp
// Type navigates lazily to Engine.Types through context
var data = new Data("photo", bytes, Type.FromMime("image/jpeg"));
data.Context = context; // stamps on Type too

data.Type.Kind        // → "image" (via context.Engine.Types.KindOf)
data.Type.Compressible // → false (images are already compressed)
data.Type.ClrType     // → typeof(byte[]) (via context.Engine.Types.Clr)

// Lazy derivation — no Type until first access
var data2 = new Data("count", 42);
// _type is null here — no allocation
data2.Type // lazily derives → new Type("int")

// Value change invalidates type
data2.Value = "hello";
data2.Type // lazily derives → new Type("string")

// MemoryStack stamps context automatically
context.MemoryStack.Set("x", "hello");
context.MemoryStack.Get("x").Context // → context
```

## What's next

- **Phase 3**: Data partial class split + Out view
- **Phase 4**: Envelope pipeline (Wrap/Compress/Encrypt chain)
