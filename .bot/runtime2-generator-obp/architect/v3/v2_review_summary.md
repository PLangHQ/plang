# v2 — In-Conversation Re-Review (with Ingi)

## What v2 proposed

A combined refactor: extract per-property emission into a polymorphic 13-folder hierarchy AND simplify the runtime contract by introducing `App.modules.ActionBase` as a base class for all action handlers. ActionBase would own helpers (`__Resolve<T>`, `__ResolveData`, etc.), per-execution state (`__action`, `__variables`, `__app`), and a universal `ExecuteCoreAsync(action, context, run)` template. A `ParamKind` enum would drive a 4-arm runtime dispatch instead of the current 11-branch generator-side ladder.

## What changed in conversation

Three substantive shifts moved the design.

### 1. `ActionBase` is wrong — helpers belong on `Action.@this`, not on a synthetic parent.

The OBP test from CLAUDE.md is explicit: behavior belongs to the owner of the data. Helpers like `__Resolve<T>(name)` operate on `__action.Parameters`. The data owner is `Action.@this`, not a per-handler base class. ActionBase is a *plumbing parent* — it exists to share methods via inheritance, not because the handler owns the data.

Resolution: drop `ActionBase`. Methods live where data lives:

- `Action.GetParameter(name, context)` — walks Parameters, falls back to Defaults, returns Data or `Data.NotFound`. On Action because Action owns both lists.
- `Action.RunAsync(context, Func<Task<Data>>)` — universal scaffolding (callstack push/pop, try/catch/finally, context save/restore). On Action because Action is the unit being executed.
- Handlers stay flat: `partial class X : IContext, ICodeGenerated`. No inheritance from any synthetic base. The `@this`-per-folder convention stays uniform across Runtime2.

### 2. Resolution moves onto `Data.@this`, not `Variables`.

`Variables.ResolveDeep` today does the recursive walk over `IList`/`IDictionary`/typed objects to substitute `%var%` references. That logic is "any value that contains references" — not specifically a variable-store concern. By the OBP rule, behavior belongs to the data owner. The Data carries its `_value` and its `NeedsResolution` flag; Data should resolve itself.

Resolution: `Data.Value` lazy getter, when `NeedsResolution=true`, walks `_value` and substitutes references via `Context.Variables.Get(name)` for the actual lookups. The recursive traversal logic moves from `Variables.ResolveDeep` to `Data`.

This also exposes a latent bug in today's `Variables.ResolveDeep` (line 432–434 of `App/Variables/this.cs`): the typed-list branch discards the recursive call's return value. The refactor cleans this up by virtue of moving and rewriting.

### 3. The eleven-arm dispatch dissolves entirely — generator emits one shape.

Two principles combined to collapse the dispatch:

- **All action properties are `Data<T>` (or `Data` ≡ `Data<object>`).** Generator fails at build if a property is a raw scalar — handlers never declare `partial string Path` or `partial int Count`. Always `Data<string> Path`, `Data<int> Count`. The user reads `.Value` inside `Run()` to unwrap; that's a handler concern, not a runtime concern.
- **`Data.As<T>(Context)` is the existing typed-view primitive** (line 389 of `App/Data/this.cs`). It returns the existing typed Data if already correct, otherwise wraps the Value via TypeMapping or App-resolvable conversion.

With Data owning resolution and `As<T>` owning the typed view, the per-property emission collapses to one uniform line for every kind that comes from Parameters:

```csharp
get => __backing ??= __action
    .GetParameter("name", Context)
    .As<DeclaredInnerType>(Context);
```

The eleven cases (Plain, Nullable, WithDefault, VariableName, Resolvable, ResolvableWithDefault, DataPlain, DataWrapped, DataWrappedNullable, DataWrappedWithDefault, Provider) collapse to **two** in the generator: parameter-sourced (one shape) and `[Provider]` (eager init from `app.Providers`).

`[Default]`, `[VariableName]`, `[IsNotNull]`, nullable annotations, value-vs-reference distinctions — all dissolve into Data semantics or build-time validation. None of them remain as generator branching.

### Build-only validation, runtime trusts

v2 proposed a load-time validation pass walking every Action in the goal tree to check schema match. Ingi rejected this: many actions in a goal tree never run (if-branches, errors), so eager conversion/validation is wasted work. Validation lives at **build time** only — the builder produces .pr files we trust. Runtime "validation" is implicit: the type conversion at `As<T>` either succeeds or returns `Data.FromError` with a ServiceError. Errors surface naturally when the action that owns them runs.

## What v3 keeps from v2

- The hard promise: **runtime test matrix passes** as the regression contract. Byte-for-byte `.g.cs` is intentionally broken; behavior is the contract.
- The phase ordering principle: get to a green intermediate state before introducing the property hierarchy.
- The directory-as-API commitment for the generator project — but with a much smaller hierarchy (two leaves instead of ten).

## What v3 changes from v2

- **No `ActionBase`.** Methods live on `Action.@this` (`GetParameter`, `RunAsync`).
- **No `ParamKind` enum.** No runtime dispatch needed — `As<T>` handles everything.
- **Resolution moves to `Data.@this`**, not just helpers. Variables.ResolveDeep evaporates (or shrinks to a thin wrapper that delegates to Data).
- **Property hierarchy collapses to two leaves**: `DataProperty`, `ProviderProperty`. Possibly one if `Provider` becomes a flag on `DataProperty`.
- **Handler properties must be `Data<T>` or `[Provider]`** — generator fails the build for raw scalars. This is a hard contract.
- **Generated `.g.cs` shrinks further** — uniform getter shape across all parameter-sourced properties means roughly half the lines of the v2 version.
