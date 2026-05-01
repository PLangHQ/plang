# docs v2 — runtime2-generator-obp — CHANGELOG-style result

## User-visible / API surface changes (since v1 docs pass)

### New public types

- `App.Variables.Variable` — `public sealed record Variable(string Name, string RawValue, bool WasPercentWrapped) : IRawNameResolvable`. Used as the wrapped type in `Data<Variable>` for action handler properties that name a variable rather than carry its value. Defines `static implicit operator string(Variable v) => v.Name`, `override ToString() => Name`, and `static Variable Resolve(string raw, Actor.Context.@this context)` which the source generator's `Data.As<T>` raw-name dispatch invokes. Single-arg ctor (`new Variable(name)`) for direct C# composition (tests, `App.RunAction`).

- `App.Variables.IRawNameResolvable` — empty marker interface. Implementers signal to `Data.AsT_Impl` that the slot's raw string should be handed to their `static Resolve(string, Context.@this)` directly, bypassing the `%var%` substitution branch.

### Removed types

- `App.Attributes.VariableNameAttribute` — gone. Use `Data<App.Variables.Variable>` instead.

### Removed source-generator surface

- `PLang.Generators/Emission/Property/Legacy/this.cs` — file deleted.
- `__Resolve<T>`, `__StripPercent`, `__HasParam`, `RawScalarValidations` helpers and emit hooks — removed from `PLang.Generators/Emission/Action/this.cs`.
- `IsAppResolvable` detection, `ScanRawScalarValidations`, related Discovery wiring — removed.

### PLNG001 diagnostic message change

Old: *Property '{0}' on action '{1}' must be Data<T>, [Provider], or [VariableName] string. Raw scalars are not permitted.*

New: *Property '{0}' on action '{1}' must be Data<T> or [Provider]. Raw scalars are not permitted.*

### New runtime contract — `MissingRequiredParameter` for variable-name slots (v8)

Non-nullable `Data<T>` slots where `T : IRawNameResolvable` (currently only `Data<Variable>`) now get a generator-emitted pre-`Run()` validation that fires `MissingRequiredParameter` ServiceError when the parameter is absent or its `.Value == null`. Detection in Discovery (via `T : IRawNameResolvable`), plumbed through `ActionClassInfo`, emitted in the Action emitter mirroring `[IsNotNull]`. Closes the silent-NRE path through `Variable`'s implicit `string` operator. The `loop/foreach` ItemName/KeyName slots are intentionally nullable and skipped via the `!p.IsNullable` filter.

**Pre-existing diagnostic-fidelity gap**: empty-string slot values (`Name = ""`) currently pass the guard — the literal check is `?.Value == null`. Pre-v7 `string.IsNullOrEmpty(...)` covered this; tightening is an optional follow-up flagged by auditor/v3, not blocking.

### Catalog rendering change

Variable-name slots previously rendered with the `[%var% string]` type tag:
- `variable.set Name([%var% string]), Value([object])`

Now render as `[string]` with a `%var%` value at the use site:
- `variable.set Name([string] %x%), Value([object] 1)`

This affects `[Example]` attributes — string-literal `[%var% string]` references in examples would render inconsistently with the catalog. None remain in the codebase; spot check would catch any third-party module that hard-coded the old form.

## Documentation files updated

| File | Change |
|---|---|
| `CLAUDE.md` (root) | OBP shape line + Property kinds entry rewritten for two-rule contract + Variable + missing-name guard |
| `Documentation/v0.2/architecture.md` | Catalog attribute list, source-gen tree, property-kinds table, PLNG001 message, new Variable/IRawNameResolvable paragraph |
| `Documentation/v0.2/good_to_know.md` | Property-kinds section reduced to two rules; "Currently exempt" block removed; new "App.Variables.Variable — the variable-name carrier" section; `[Sensitive]` cross-reference cleaned |
| `Documentation/v0.2/action-catalog.md` | Attribute table, type-shape table, `variable.set` annotated example, two trailing string-literal type tags |
| `Documentation/Runtime2/todos.md` | 2026-04-30 entry marked RESOLVED with the actual approach taken |

## Build status

C#: 2570/2570 green (per auditor/v3).
Plang: 166/166 green (per auditor/v3).
No code edits in this docs pass.
