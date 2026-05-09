# coder — runtime2-cleanup

## Version
v26 — Stage 26: Types keystone (TypeMapping + PlangTypeIndex + Choices collapsed into `app.Types`).

## What this is
The Tier 5 keystone. Three Rule C sites under one realignment:

- `Utils/TypeMapping.cs` — full static class with two static dictionaries and a flat surface of static methods. Today's `Types/this.cs` was a delegating wrapper around it; the wrapper existed *because* TypeMapping was static.
- `Utils/PlangTypeIndex.cs` — full static class with 6 static fields including locks and lazy-init guards. Process-global indexing state.
- `App/Choices/this.cs` — the static `@this` shape (already a contradiction). Two static fields with lazy-init via lock-double-check.

After: one instance-shaped `app.Types` subsystem with three pieces: primary partial (`Types/this.cs`), Registry partial (`Types/Registry.cs`), and `Types/Choices/` sub-`@this`. Three folders/files vanish. Unblocks stage 27 (Utils empty-out — TypeConverter → Types/Conversion.cs partial; Utils/Json disperse).

## What was done

### New shape
- `Types/this.cs` — REWRITTEN as primary partial. Absorbs TypeMapping public surface. State-touching methods are instance (Get/Clr, GetTypeName/Name, Register, GetValidValues/ValidValues, BuildTypeEntries/ComplexSchemas, GetBuilderTypeNames/BuilderNames, RegisterDomainTypes). Pure-logic helpers stay static (ClrFromMime, IsScalarPlangType, IsPrimitive, ConvertTo/Populate/TryConvertTo forwarders to TypeConverter, GetPrimitiveOrMime, GetPrimitiveName, GetTypeNameStatic).
- `Types/Registry.cs` — NEW partial. Absorbs PlangTypeIndex internals as instance state. Public methods all instance: IsClrTypeName, ResolveName, ResolveType, KnownTypes, RegisterRuntime.
- `Types/Choices/this.cs` — NEW sub-`@this` (relocated from App/Choices/). Static class becomes instance; mounted as `app.Types.Choices`.
- `Utils/TypeMapping.cs`, `Utils/PlangTypeIndex.cs`, `App/Choices/` — DELETED.

### Plumbing changes

- **`Modules.@this` gains `App` back-reference** — `public global::App.@this? App { get; internal set; }`. App constructor sets `_modules.App = this` after Modules construction. Without this, `Modules.Describe`, `Modules.GetDefaults`, `Schema.Build`, `Schema/Render.LookupParamTypeName` couldn't reach `app.Types` from their instance methods.
- **`Modules.DescribeReturnType` and `Schema/Render.LookupParamTypeName`** — converted from `private static` to instance to use `App?.Types.GetTypeName(...)`.
- **`validateResponse.Validate`** — gains `App? app` parameter so callers with `Context.App` (Run) or `goal.App` (ValidateGoalState) can supply navigation when `goal.App` is null.
- **`TypeConverter` (still in App.Utils for stage 27)** — its `PlangTypeIndex.IsClrTypeName(name)` calls become `context?.App.Types.IsClrTypeName(name) ?? false`; `TypeMapping.IsPrimitive(...)` becomes `global::App.Types.@this.IsPrimitive(...)` (kept static).
- **3 static-friendly helpers added on Types.@this** for the few callers that legitimately have no App in scope:
  - `GetPrimitiveOrMime(string)` — primitives + MIME (no registry). Used by `Data.Type.ClrType` fallback when Context is null.
  - `GetPrimitiveName(Type)` — reverse primitive lookup. Used by `Data.@this.Type` lazy-derivation fallback.
  - `GetTypeNameStatic(Type)` — full pure-reflection variant of `GetTypeName` (handles primitives, generics, arrays, `Data<T>`, [PlangType] attribute read, @this convention). Used by `Modules.Describe` fallback when Modules has no App backing (test fixtures that do `new AppModules()` directly).

### Test compatibility

Added `PLang.Tests/Support/TypeMappingTestFacade.cs` declaring `namespace App.Utils; internal static class TypeMapping { ... }` — preserves the legacy `TypeMapping.X(...)` test ergonomics by routing through a shared per-process App fixture. ~150 test call sites unchanged.

### Caller sweep

17 production files touched (Data/this, Settings/Sqlite, Variables/this, Debug/this, Modules/this, Modules/Schema/this, Modules/Schema/Render, Utils/TypeConverter, modules/builder/code/Default, modules/builder/validateResponse, modules/file/code/Default, modules/test/discover, modules/variable/set, Executor — plus the three deletes and the App.cs / Modules.cs plumbing). Test-side: only the new facade file (no per-test rewrite).

## Brief deviations

1. Brief table listed `IsScalarPlangType`, `IsPrimitive`, `ConvertTo`, `Populate`, `TryConvertTo` as instance methods. **Kept static** — pure logic or forwarders to still-static TypeConverter (stage 27 will absorb that). The brief's "static-context callers" admission covers this.
2. Brief implied `Primitives`/`PrimitiveNames` would become instance fields. **Kept `private static readonly`** — they're constant lookup tables, fits Rule C exception class for const-style data.
3. Brief left `Get`/`ResolveType` overlap as a coder call. **Kept separate**: `Get` is the rich entry path (primitives + generics + registry + MIME); `ResolveType`/`ResolveName` are bare registry lookups (used internally).
4. **Added `Modules.App` back-reference** — the brief's caller sweep implicitly required this for instance-Types reach from Schema/Render/Modules without changing every caller.

## Stage closure

- C# tests green: 2752/2752
- PLang tests green: 199/199
- `find PLang/App/Utils/TypeMapping.cs PLang/App/Utils/PlangTypeIndex.cs` — both gone
- `find PLang/App/Choices` — directory absent
- `find PLang/App/Types/Registry.cs PLang/App/Types/Choices/this.cs` — both present
- `grep -rn "App\.Utils\.TypeMapping\b\|PlangTypeIndex\b\|App\.Choices\.@this" PLang/ --include='*.cs'` — only doc-comment mentions remain
- Behaviour change: none. Pure shape change. Three Rule C sites closed in one realignment.

Stage 27 (TypeConverter → Types/Conversion.cs partial; Utils/Json disperse) now unblocked.
