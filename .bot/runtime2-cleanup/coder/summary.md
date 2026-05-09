# coder — runtime2-cleanup

## Version
v29 — `[Provider]` attribute renamed to `[Code]` to match the runtime
escape-hatch (`app.Code.Get<T>()`).

## What this is

The runtime escape-hatch was renamed `App.Providers` → `App.Code` in an
earlier sweep — call sites already use `app.Code.Get<T>()`. The attribute
name lagged, leaving a contradictory shape:

```csharp
[Provider] IFoo Foo { get; init; }    // resolves through app.Code
```

v29 closes the gap so the attribute, the registry, and the generator's
emitted call all use the same word.

## What was done

### Attribute + usage sweep
- `PLang/App/modules/Attributes.cs:48`: class `ProviderAttribute` → `CodeAttribute`.
  XML doc rewritten — names `app.Code.Get<T>()` explicitly.
- 50 call sites in `PLang/App/modules/**`: `[Provider]` → `[Code]` (sed sweep).
- 2 reflection short-name lookups missed by the sweep
  (`PLang/App/Modules/this.cs:252`, `PLang/App/modules/builder/code/Default.cs:300`):
  `<modules.ProviderAttribute>` → `<modules.CodeAttribute>`.

### Source generator
- `PLang.Generators/Discovery/this.cs`:
  - Short-name attribute match `"ProviderAttribute"` → `"CodeAttribute"` at
    both call sites (BuildProperty + IsValidActionProperty).
  - PLNG001 title + messageFormat: `[Provider]` → `[Code]`.
  - Local variable `isProvider` → `isCode`; alias
    `using ProviderProperty = ...Provider.@this` →
    `using CodeProperty = ...Code.@this`.
  - Doc comments swept.
- `PLang.Generators/Emission/Property/Provider/` →
  `PLang.Generators/Emission/Property/Code/` (`git mv`); namespace
  `PLang.Generators.Emission.Property.Provider` →
  `PLang.Generators.Emission.Property.Code`.
- `PLang.Generators/Emission/Action/this.cs`: alias and `OfType<...>`
  references switched from `ProviderProperty` to `CodeProperty`.
- `PLang.Generators/Emission/Property/this.cs`: doc reference swept.

### Test fixtures
- Two generator-test stubs that synthesised an `App.modules.ProviderAttribute`
  for in-memory compilation (`Plng001PostMigrationTests.cs:44`,
  `IncrementalCacheTests.cs:158`) → `CodeAttribute`.
- `PLang.Tests/Generator/Matrix/Provider/Handlers.cs`: `[App.modules.Provider]`
  → `[App.modules.Code]` (left the folder name alone — it's a test scenario
  label, not a code name).
- `GeneratorValidationTests.cs`:
  - `ProviderProperty_BuildsSuccessfully` → `CodeProperty_BuildsSuccessfully`,
    file path checks updated to `Emission/Property/Code/`.
  - `PropertyHierarchy_TwoLeavesOnly` and
    `ActionPropertyRecord_NoSymbolLeaks_IncrementalSafe`: directory + file
    checks `Provider/` → `Code/`.
- All `[Provider]` references in test comments swept.

### Docs
- `Documentation/v0.2/architecture.md` (4 references), `good_to_know.md` (4),
  `action-catalog.md` (1), `builder-self-rebuild-plan.md` (1) — all updated.
- `CLAUDE.md` line 39: NOT edited directly (docs-owned, per project rule).
  Proposal appended to `.bot/runtime2-cleanup/claude-md-proposals.md`.

## Code example

Before:

```csharp
[Action("query")]
public partial class Query : IContext
{
    [Provider]
    public partial ILlmProvider Llm { get; init; }   // attribute lies; resolves via app.Code
}
```

After:

```csharp
[Action("query")]
public partial class Query : IContext
{
    [Code]
    public partial ILlmProvider Llm { get; init; }   // matches the emitted app.Code.Get<ILlmProvider>()
}
```

## Verification

- Clean rebuild: `rm -rf */bin */obj && dotnet build PlangConsole` → 0 errors
  (after one round of fixing 2 reflection-string misses + the test-fixture
  stubs the sweep didn't reach).
- C# tests: `dotnet run --project PLang.Tests` → **2752 / 2752**.
- PLang tests: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`
  → **199 / 199**.

## What's next

Branch ready for re-audit + merge. v28 + v29 are bundled — auditor should
re-clear both at once.
