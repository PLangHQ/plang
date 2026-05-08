# Stage 9 — coder plan (`catalog-dissolve-to-modules-schema`)

## What

Dissolve `App/Catalog/` into `App/Modules/Schema/`. The Catalog concept is
"Modules describing itself to the LLM" — Modules owns the data, Schema is a
property of Modules. After: `app.Modules.Schema.Build()` and
`app.Modules.Schema.Render(spec)` (Rule E — caller stops decomposing
modules to pass it in).

## File map

### New (`PLang/App/Modules/Schema/`)

- `this.cs` — `Schema.@this` class. Holds `_modules` ref. Properties
  `PrimitiveNames`, `Types`, `TypeNames`, `TypeSchemas`. Method `Build()` (instance).
- `Render.cs` — `partial class @this` with `Render(Spec.Example spec)` instance method
  + private helpers `RenderActionFormal`, `RenderValueFormal`, `BuildActionRecord`,
  `ConvertValueForJson`, `LookupParamTypeName`, `UnwrapDataAndNullable` — all converted
  to instance methods that read `_modules`.
- `Entry.cs` — `Entry` class (was `TypeEntry`), `Field` class, `EntryKind` enum (was `TypeKind`).
- `Spec/Action.cs` — `Action` record (was `ActionSpec`).
- `Spec/Example.cs` — `Example` record (was `ExampleSpec`).

### Deleted (`PLang/App/Catalog/`)

The whole folder: `this.cs`, `ActionSpec.cs`, `ExampleSpec.cs`, `TypeEntry.cs`,
`ExampleRenderer.cs`, `ExampleHelpers.cs`.

### Modified — production

- `PLang/App/Modules/this.cs`:
  - `public Schema.@this Schema { get; }` property (lazy-host, see "Lazy semantics" below).
  - Allocated in ctor: `Schema = new Schema.@this(this);`
  - Lines 289–294: `App.Catalog.ExampleSpec[]` → `App.Modules.Schema.Spec.Example[]`; the static `App.Catalog.ExampleRenderer.Render(s, this)` becomes `Schema.Render(s)` (uses the host's `_modules`).
- `PLang/App/Types/this.cs:372` — `Dictionary<string, App.Catalog.TypeEntry>` → `Dictionary<string, App.Modules.Schema.Entry>`.
- `PLang/App/Utils/TypeMapping.cs` — sweep 10 sites: `App.Catalog.TypeEntry` → `App.Modules.Schema.Entry`; `App.Catalog.TypeKind.*` → `App.Modules.Schema.EntryKind.*`; `App.Catalog.Field` → `App.Modules.Schema.Field`. The `BuildTypeEntries(modules)` static method stays where it is (it's the producer of `Entry` objects called from `Schema.Build()`); only the type names rename.
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs:37` — `App.Catalog.@this.Build(action.Context.App.Modules)` → `action.Context.App.Modules.Schema.Build()`.

### Modified — handler authors (6 files)

For each: drop `using static App.Catalog.ExampleHelpers;`; add
`using App.Modules.Schema.Spec;` (for `Action`, `Example`); rewrite each
`Example("intent", ...)` call to `new Example("intent", new[] { ... })`;
rewrite each `Action("module.action", params, modifiers)` call to
`new Action("module", "action", params, modifiers)` (split on the dot
manually — record positional ctor doesn't auto-split).

Files: `error/handle.cs`, `math/{add,multiply,subtract,divide,power}.cs`.

### Modified — tests

- `PLang.Tests/App/Catalog/CatalogTests.cs` → relocate to
  `PLang.Tests/App/Modules/Schema/SchemaTests.cs` (folder + namespace match
  production). `App.Catalog.@this.Build(_app.Modules)` → `_app.Modules.Schema.Build()`.
- `PLang.Tests/App/Modules/builder/ComplexTypeDiscoveryTests.cs` — type-name sweep.
- `PLang.Tests/App/Modules/builder/GetTypeInfoTests.cs` — `App.Catalog.@this` → `App.Modules.Schema.@this`.

## Lazy semantics (Risk #4 in brief)

Today `Catalog.Build(modules)` is called explicitly by the builder provider
on demand. To preserve that:

- `app.Modules.Schema` returns the unbuilt host instance (allocated in
  Modules ctor). Its `PrimitiveNames` / `Types` are empty arrays.
- `app.Modules.Schema.Build()` returns a *new* fully-built Schema instance
  with PrimitiveNames + Types populated. Held by no one — caller keeps it.
- `Render(spec)` is an instance method that only needs `_modules` (not
  PrimitiveNames/Types), so it works on the host without `Build()` being
  called first.

This matches the architect's "Schema holds `_modules` and exposes
`Render(spec)` as instance" lean.

## Verification

- `find PLang/App/Catalog -type f` → empty.
- `grep -rn "App\.Catalog\." PLang/ PLang.Tests/ Tests/ --include='*.cs'` → 0.
- `grep -rn "ExampleHelpers" PLang/ PLang.Tests/ Tests/ --include='*.cs'` → 0.
- C# 2755/2755; PLang 199/199; build clean.
