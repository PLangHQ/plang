# v26 — Stage 26: Types keystone

Combined keystone per architect's spec. Three static surfaces collapsed into one instance-shaped `app.Types` subsystem.

## Plan

1. **Types/Registry.cs** (new partial) — absorbs `Utils/PlangTypeIndex`. Instance fields: locks, `_nameToType`, `_typeToName`, `_runtimeNameToType`, `_clrTypeFullNames`, `Assemblies`. Instance methods: `IsClrTypeName`, `ResolveName`, `ResolveType`, `KnownTypes`, `RegisterRuntime`. Pure-logic helpers (`IsThisClass`, `InferName`, `SafeGetTypes`) stay private static.

2. **Types/Choices/this.cs** (new sub-`@this`) — absorbs `App/Choices/this.cs`, instance class. Mounted as `app.Types.Choices`.

3. **Types/this.cs** (rewrite as primary partial) — absorbs `Utils/TypeMapping` public surface. Instance methods (state-touching): `Get` (was `GetType`), `Clr`, `GetTypeName`, `Name`, `Register`, `RegisterDomainTypes`, `GetValidValues`, `ValidValues`, `BuildTypeEntries`, `ComplexSchemas`, `GetBuilderTypeNames`, `BuilderNames`. Static methods (pure logic / kept reachable from static-context callers): `ClrFromMime`, `IsScalarPlangType`, `IsPrimitive`, `ConvertTo`, `Populate`, `TryConvertTo`, `GetPrimitiveOrMime`, `GetPrimitiveName`, `GetTypeNameStatic`. The `Primitives`/`PrimitiveNames` lookup tables stay `private static readonly` (constant data).

4. **Plumbing changes:**
   - **`Modules.@this` gains `App` back-reference** (`public global::App.@this? App { get; internal set; }`), set by App constructor right after Modules construction. Lets `Modules.Describe`, `Modules.GetDefaults`, and `Schema.Build` reach `app.Types` for type-name lookups. Without this, those instance methods on a Modules instance had no path to the Types instance.
   - **Schema/Render `LookupParamTypeName`** changed from `private static` to `private` (instance) — uses `_modules.App?.Types.GetTypeName(...)` with static fallback.
   - **Modules `DescribeReturnType`** changed from `private static` to `private` instance for the same reason.
   - **TypeConverter** (still in App.Utils for stage 27) — its two `PlangTypeIndex.IsClrTypeName(name)` calls become `context?.App.Types.IsClrTypeName(name) ?? false`; `TypeMapping.IsPrimitive(...)` becomes `global::App.Types.@this.IsPrimitive(...)` (kept static).
   - **`validateResponse.Validate`** gains an `App? app` parameter so callers (`Run` via `Context.App`, `ValidateGoalState` via `goal.App`) can supply navigation when `goal.App` is null.

5. **Test compatibility:** add `PLang.Tests/Support/TypeMappingTestFacade.cs` declaring `namespace App.Utils; internal static class TypeMapping { ... }` — preserves existing `TypeMapping.X(...)` test ergonomics by routing through a shared per-process App fixture. ~150 test call sites unchanged.

6. **Static-friendly helpers** added on `Types.@this` to plug the holes where instance navigation isn't available:
   - `GetPrimitiveOrMime(string)` — primitives + MIME lookup, no registry. Used by `Data.Type.ClrType` fallback when Context is null.
   - `GetPrimitiveName(Type)` — reverse primitive lookup. Used by `Data.@this.Type` lazy-derivation fallback.
   - `GetTypeNameStatic(Type)` — full pure-reflection variant of `GetTypeName` (handles primitives, generics, arrays, `Data<T>`, [PlangType] attribute, @this convention) for callers without an App. Used by `Modules.Describe` fallback when the Modules instance has no App backing (test fixtures that do `new AppModules()` directly).

7. **Sweep callers** (~17 production files + 7 test files) — already done before final test run.

8. **Delete** `Utils/TypeMapping.cs`, `Utils/PlangTypeIndex.cs`, `App/Choices/` (whole folder).

## Brief deviations

- Brief table listed `IsScalarPlangType`, `IsPrimitive`, `ConvertTo`, `Populate`, `TryConvertTo` as instance methods. **Kept static** — they're either pure-logic (no state) or forwarders to the still-static `TypeConverter` (stage 27 will absorb that). The `static-context callers` admission in the brief covers this.
- Brief said `Primitives`/`PrimitiveNames` would become instance fields. **Kept `private static readonly`** — they're constant lookup tables, no per-App divergence, fits Rule C exception class for const-style data.
- Brief's `ResolveName`/`ResolveType` overlap question (vs. `Get`): kept as separate methods. `Get` does primitives + generics + registry + MIME (the rich entry path); `ResolveType`/`ResolveName` are bare registry lookups (used internally by `Get` and `GetTypeName`).
- Added `Modules.App` back-reference (the brief implies but doesn't mandate this — without it, the instance-Types methods can't be reached from Schema/Render/Modules without changing every caller).
