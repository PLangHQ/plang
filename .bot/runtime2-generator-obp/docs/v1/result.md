# docs v1 — result.md (CHANGELOG-style)

User-visible changes on `runtime2-generator-obp` worth flagging in release notes.

## Build-time

### `PLNG001` build error — action property kinds enforced

Action handler properties must be one of:
- `Data<T>` (or plain `Data`)
- `[Provider] T`
- `[VariableName] string`

Raw `partial string` / `partial int` / etc. on an `[Action]`-attributed handler now fail the build with:

```
error PLNG001: Property '{0}' on action '{1}' must be Data<T>, [Provider], or [VariableName] string. Raw scalars are not permitted.
```

The diagnostic carries the full identifier span — IDE squiggles underline the property name.

### Source-generator file layout

Old: `PLang.Generators/LazyParamsGenerator.cs` (single 700-line file)
New: `PLang.Generators/this.cs` + `Discovery/` + `Emission/Action/` + `Emission/Property/{Data,Provider,Legacy}/`

External consumers don't reference these files directly. Internal references (other branches with in-flight work) need to update file paths.

## Runtime

### Two new `ServiceError` keys for variable resolution

When a `Data<T>` property resolves a `%var%` reference, the new contract surfaces resolution failures as `ServiceError` with one of two keys (status 400 in both cases):

| Key | Trigger |
|-----|---------|
| `VariableResolutionCycle` | A `%var%` references itself transitively (`%a%="%b%", %b%="%a%"`) |
| `ResolveDepthExceeded` | An expanding chain produces > 32 levels (each level differs from the prior, so cycle-detection alone misses) |

**Behaviour change.** Pre-v6, cycle/depth trips returned the unresolved `%var%` string as the property value, which Run() bodies often masked with default fallbacks. Post-v6, these conditions return a `Data.FromError(ServiceError)` to the dispatch caller. Handlers that previously read the unresolved string now see a typed error.

PLang developers can route these via standard `on error` modifiers:

```
- read file %config.path% to %config%
  on error key VariableResolutionCycle, call HandleCycle
  on error key ResolveDepthExceeded, call HandleDepth
```

### `[Sensitive]` masking for action parameter snapshots

Properties marked `[Sensitive]` on an `[Action]` handler are now masked in the per-error parameter snapshot (`Error.Params`, printed under "📥 Parameters at dispatch:"):

| Field | Behaviour |
|-------|-----------|
| `PrValue` | `"******"` when `.pr` literal is non-null, `null` when absent |
| `FinalValue` | `"******"` when accessed and resolved value is non-null; `null` when accessed-and-null or never accessed |

Aligns with `SensitivePropertyFilter` for JSON serialization. Same attribute, same masking convention.

### `ICodeGenerated.SnapshotParams()` (default-impl)

`ICodeGenerated` now declares `List<ParamSnapshot> SnapshotParams() => new();` with an interface default. The source generator emits per-handler overrides; handlers without parameter properties inherit the default. No public-API break — handlers don't implement `ICodeGenerated` directly (the generator does).

## Tests

C# tests: 2466 → 2471 (+5). Matrix coverage: cycle and depth assertions on Data<T> handlers (`DataWrappedStringUses_*`), sensitive snapshot null-guard, post-Run check direct deletion test.

PLang tests: pre-existing `Cannot convert String to this` failures on 7 tests (`Modules/Builder/ValidateValid`, `Modules/Error/Call`, `Modules/Error/RetryOnly`, `Modules/Event/{BeforeStep,Multiple,Wildcard}`, `Modules/Goal/Basic/GoalCall`). These are v6 surfacing real type-conversion errors that were previously masked — `.pr` files contain the literal string `"this"` with type tag `actor(...)` for handlers declaring `Data<Actor.@this>?` parameters; `TypeConverter` has no `string → Actor.@this` rule. Fix lives in the builder's Actor sentinel handling, on a different branch.

## Internal-only / no user-visible effect

- `Action.GetParameter(name, context)` — pure parameter lookup (Parameters → Defaults → NotFound). Generated handlers use it via `__ResolveData(name)`.
- `EquatableArray<T>` — value-equal wrapper around `T[]` for Roslyn incremental cache stability.
- Source generator OBP shape (Discovery + Emission/Property leaves) — refactor of the same emitted output, not a behaviour change.
