# [VariableName] Migration — v1 Plan

## What this is

Finish the migration deferred from the previous branch (todos.md §2026-04-30). The
Legacy property emission path (`PLang.Generators/Emission/Property/Legacy/this.cs`)
exists only to keep ~25 handlers building while they still use:
- `[VariableName] partial string Foo` (write-target slots)
- raw `partial int / bool / string` (non-Data primitives)

Goal: every action handler property is `Data<T>`, plain `Data`, or `[Provider] T`.
Delete `Legacy/this.cs`, delete `[VariableName]` attribute, delete the helper
methods that backed the legacy path. PLNG001 already gates non-Data properties at
build time — this finishes what PLNG001 was designed for.

## Why now

- The previous branch landed PLNG001 with Legacy as a temporary escape hatch.
  Phase 5 was always meant to delete Legacy.
- The auditor/v1 ServiceError contract fix is currently asymmetric — both
  emission paths honor it, but only because Legacy still exists. Single emission
  path collapses the asymmetry.
- ~25 handlers carry the cost of two shapes (Data world for new code, raw-string
  world for these). Future handler authors hit the inconsistency.

## The Name resolution claim (validate first)

The migration hinges on a claim about `Data.As<T>` semantics. For
`set %x% = 5`, the .pr parameter slot is `{"name":"Name", "value":"%x%"}`.

Claim: when the source-generated property getter does
`__ResolveData("Name").As<string>(Context)`:
- If `x` doesn't exist → returns `Data<string>` with `.Name = "x"` (from
  `TryFullVarMatch` extracting "x" out of "%x%").
- If `x` exists → recursion onto live variable's Data; result has `.Name = "x"`
  (preserved through `WrapAs<T>` → `ConstructWrap<T>` which uses `this.Name`).

So the handler can read `Name.Name` to get the literal variable name **without**
the `[VariableName]` attribute — `As<T>` resolution itself surfaces the canonical
name.

The crack: LLM emits bare `"x"` (no `%`) → no `TryFullVarMatch` → `.Name` stays
as the slot name "Name". Closed at build time by `IBuildValidatable` on the
write-target handlers.

### Phase 0 — Prove the claim

Before any handler change, lock the claim into a test. **Coder writes
`PLang.Tests/App/DataTests/VariableSetNameResolutionTests.cs`** — three tests:

1. `SetFromJson_VarMissing_BindsToVarName` — deserialize the .pr JSON for
   `set %x%=5`, run, assert `Variables.GetValue("x") == 5`.
2. `SetFromJson_VarExists_OverwritesByVarName` — pre-populate `x=10`, run,
   assert `x == 5` AND `Variables.Get("Name").IsInitialized == false` (negative —
   slot name didn't leak).
3. `NameSlot_ResolvedAsString_HasCanonicalVarName` — pull the Name slot Data
   directly from the deserialized Action.Parameters, call `.As<string>()`,
   assert `.Name == "x"`.

Test 3 is the load-bearing assertion. If it fails, the migration as designed
won't work and we need a different approach (build-time normalization, or a
different field).

The .pr JSON used in tests:
```json
{
  "module": "variable",
  "action": "set",
  "Parameters": [
    { "name": "Name", "value": "%x%" },
    { "name": "Value", "value": 5 }
  ]
}
```

Coder verifies the deserialization path. If `JsonSerializer.Deserialize<Action.@this>`
doesn't round-trip cleanly, use `app.Channels.Serializers.Deserialize` (the path
the runtime uses for .pr files) — see `PLang/App/Goals/this.cs:320-326`.

If all three tests pass: Phase 0 done, proceed to Phase 1.
If test 3 fails: STOP, report back, redesign.

## Phase 1 — Migrate read sites (no name needed)

Handlers that read a variable's value but don't need its literal name. They
declare `[VariableName] partial string ListName` today only to call
`Context.Variables.Get(ListName).Value` — they don't actually use the name once
they have the value.

- `PLang/App/modules/list/any.cs`
- `PLang/App/modules/list/contains.cs`
- `PLang/App/modules/list/count.cs`
- `PLang/App/modules/list/first.cs`
- `PLang/App/modules/list/flatten.cs`
- `PLang/App/modules/list/get.cs`
- `PLang/App/modules/list/group.cs`
- `PLang/App/modules/list/indexof.cs`
- `PLang/App/modules/list/join.cs`
- `PLang/App/modules/list/last.cs`
- `PLang/App/modules/list/range.cs`
- `PLang/App/modules/list/split.cs` (if reads a list)
- `PLang/App/modules/list/unique.cs`
- `PLang/App/modules/loop/foreach.cs`
- `PLang/App/modules/variable/exists.cs`
- `PLang/App/modules/variable/get.cs`

Pattern:
```csharp
// before
[VariableName] public partial string ListName { get; init; }
...
var list = Context.Variables.Get(ListName).Value as List<object?>;

// after
public partial Data.@this<List<object?>> List { get; init; }
...
var list = List.Value;
```

Per-handler review needed: some "read" handlers may also write back (e.g.
`list/split` produces a new list and stores it). Coder reads each handler before
applying the pattern. If the handler also writes, it's Phase 2 not Phase 1.

## Phase 2 — Migrate write-target sites (need literal name)

Handlers that write back to a variable. They need the literal name to call
`Context.Variables.Set(name, ...)`.

- `PLang/App/modules/list/add.cs`
- `PLang/App/modules/list/remove.cs`
- `PLang/App/modules/list/reverse.cs` (if in-place)
- `PLang/App/modules/list/set.cs`
- `PLang/App/modules/list/sort.cs` (if in-place)
- `PLang/App/modules/variable/clear.cs`
- `PLang/App/modules/variable/remove.cs`
- `PLang/App/modules/variable/set.cs`

Pattern:
```csharp
// before
[VariableName] public partial string Name { get; init; }
...
Context.Variables.Set(Name, value, ...);

// after
public partial Data.@this<string> Name { get; init; }
...
Context.Variables.Set(Name.Name, value, ...);
```

Each handler needs a `IBuildValidatable.ValidateBuild` check (or extension to an
existing one) that rejects bare-name parameters:
```csharp
if (Name.Value is string s && !s.Contains('%'))
    return $"Parameter 'Name' must reference a variable like %{s}%, got bare '{s}'.";
```

This catches LLM emissions that didn't wrap in `%`. Without this, `Name.Name`
falls back to the slot name and silently writes to the wrong key.

## Phase 3 — Delete the dead code

Once Phases 1 and 2 are done and `dotnet build` succeeds:

1. Delete `PLang.Generators/Emission/Property/Legacy/this.cs`.
2. Delete `[VariableName]` attribute from `PLang/App/modules/Attributes.cs`.
3. Delete from `PLang.Generators/Emission/Action/this.cs`:
   - `__StripPercent` helper (lines ~290-298).
   - `RawScalarValidations` (around lines 250-295) and the `if (__resolutionError != null) return __resolutionError;` pre-Run check (line 232 — see todos.md for context).
4. Delete `PLang.Generators/Discovery/this.cs` references to `[VariableName]`
   detection (around the `IsVariableName` flag).
5. Run `plang p build` from project root — should succeed with no
   PLNG001 diagnostics.
6. Run `dotnet run --project PLang.Tests` — all tests pass.
7. Run `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` — PLang
   tests pass.

If anything breaks, do NOT add a fallback. The whole point is to remove the
escape hatch.

## Phase 4 — Update todos and good_to_know

After Phase 3 lands:
1. Mark `Documentation/Runtime2/todos.md` §2026-04-30 as DONE (delete or move
   to a "completed" section per existing convention).
2. Add to `Documentation/v0.2/good_to_know.md`: "Handler properties are always
   `Data<T>`, plain `Data`, or `[Provider] T`. To get the literal variable name
   in a write-target slot, use `Data.Name` after resolution — `As<T>` propagates
   the canonical name from `%var%` form via `TryFullVarMatch`."

## Handoff order

1. **Coder** writes Phase 0 tests, runs them. Reports PASS/FAIL.
2. If PASS: **Coder** does Phase 1, then Phase 2, then Phase 3 (each phase a
   separate commit so reviewers can bisect).
3. **test-designer** considers PLang-level tests for the migrated handlers (esp.
   the new `IBuildValidatable` checks).
4. **codeanalyzer / tester / security / auditor** review.
5. **docs** updates good_to_know and applies any CLAUDE.md proposals.

## Open questions / decisions to revisit if Phase 0 surprises us

- If test 3 fails, the fallback design is build-time normalization: the .pr
  loader rewrites bare `"x"` → `"%x%"` for slots marked as variable references.
  This would need a slot-level marker (an attribute) — defeating part of the
  goal but smaller than keeping `[VariableName]`.
- If a handler turns out to need both name AND value resolution behaviors
  simultaneously (edge case in `list/sort` with key extraction by var name?),
  document and decide per-handler — don't introduce a new general mechanism
  unless ≥2 handlers need it.

## Architectural note

This migration also collapses a long-standing inconsistency: today, the LLM-form
variability for variable-name parameters is patched by handler-side stripping
(`__StripPercent` reads `_value` and trims `%`). After the migration, that
variability is normalized by `Data.As<T>` itself via `TryFullVarMatch` — the
*same* mechanism that resolves `%var%` to a value also surfaces the canonical
name. One mechanism, one truth. The build-time `IBuildValidatable` check on
write-target handlers closes the only remaining crack (LLM emits bare names).
