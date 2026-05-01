# Handoff to architect — replan VariableName migration

**From:** coder/v1
**Date:** 2026-05-01
**Status:** Phase 0 verified mechanically, design decision overrides plan.

## What you asked me to do (Phase 0)

Prove that for `set %x% = 5`, the Name slot Data resolves to `.Name == "x"` via
`Data.As<T>(ctx)`. Three tests, one load-bearing.

## What I did

Wrote `PLang.Tests/App/DataTests/VariableSetNameResolutionTests.cs` covering:

1. `SetX_VarMissing_NameSlotResolvesTo_x` — unset case → `.Name = "x"`, `IsInitialized = false`. **PASS.**
2. `SetX_VarExists_NameSlotResolvesTo_x_WithLiveValue` — live case via `resolved.AsT_Impl<T>` recursion → `.Name = "x"`. **PASS.**
3. `SetX_BareName_NoPercent_NameStaysAsSlotName` — LLM mistake case (bare `"x"`, no `%`) → `.Name = "Name"` (slot name leaks). **PASS** (negative — confirms the "crack").
4. `SetX_RoundTrip_ResolvedNameWritesToCorrectKey` — `Variables.Set(resolved.Name, 5)` writes to var "x", not "Name". **PASS.**

Plus the existing `NamePropagationTests` (6 tests) all pass — same mechanism, different slot names.

End-to-end: `Tests/App/StepResult/StepResult.test.goal` (`set %x% = "hello"`) `[Pass]` via `plang --test`.

So **the mechanism works exactly as you described.** No surprise on Phase 0. Architecturally the
migration was viable.

## Decision (Ingi, 2026-05-01)

**Keep `[VariableName]`. Do not delete it. The Legacy emission path is permanent, not
deprecated.**

Reasoning:

- `[VariableName]` handles **both** `value="%x%"` and bare `value="x"` (LLM mistake) natively
  via `__StripPercent`. Both write to var "x". One mechanism, no edge cases.
- `Data.As<T>().Name` only handles the `%var%` form. Bare `value="x"` silently writes to slot
  key "Name" — a real correctness regression. Test 3 above demonstrates this on the actual
  code path.
- Your plan compensated for this with `IBuildValidatable` checks on every write-target
  handler. That is *more* code added to *match* the robustness `[VariableName]` has for free.
- The "name leaks out via As<T> resolution" mechanism is clever but implicit: it relies on
  `TryFullVarMatch` plus `ConstructWrap<T>` propagating `Name` from `this` (the live var Data)
  through the recursive `AsT_Impl`. The architect-level reasoning is sound; the
  handler-author-level mental model is "the variable name flows backward through value
  resolution," which is a hard sell to anyone reading a handler in isolation.

The `[VariableName]` carve-out is not a weakness in the type system. It expresses a real
asymmetry: write-target slots semantically need a *name binding*, not a *value*. Forcing
`Data<T>` on those slots papers over that asymmetry instead of acknowledging it.

## What this means for your plan

Sections that need rewriting:

| Phase | Original | New |
|---|---|---|
| 0 — Prove `As<T>.Name` claim | Load-bearing pre-flight | **Drop.** Mechanism still works, but no longer load-bearing. Tests stay as documentation but aren't gating anything. |
| 1 — Migrate read sites | `[VariableName]` → `Data<T>` for ~16 handlers that read but don't write | **Keep.** This is still a real cleanup — `list/any`, `list/contains`, `list/count`, `list/get`, `loop/foreach`, `variable/exists`, `variable/get`, etc. They use `[VariableName]` only to call `Variables.Get(name).Value`, which `Data<T>.Value` does directly. |
| 2 — Migrate write sites + IBuildValidatable | `[VariableName]` → `Data<T>` for ~8 handlers, add bare-name validators | **Drop.** `[VariableName]` stays for write targets. No validators needed. |
| 3 — Delete `Legacy/this.cs`, `[VariableName]` attr, `__StripPercent`, `RawScalarValidations` | Final cleanup | **Drop.** All of this stays. |
| 4 — Update todos / good_to_know | Document migration done | **Reshape.** Document the dual rule (see below). |

## The new rule (for `good_to_know.md` and `/PLang/App/CLAUDE.md`)

> Action handler properties are one of:
> - `Data<T>` / plain `Data` — for value-bearing parameters (the default).
> - `[Provider] T` — for runtime-injected services.
> - `[VariableName] string` — for write-target slots that need the literal variable
>   *name*, not its value. Used by `variable.set`, `variable.clear`, `variable.remove`,
>   and the in-place mutating list handlers (`list.add`, `list.remove`, `list.set`,
>   `list.sort`, `list.reverse`).
>
> `[VariableName]` is canonical for write-target slots — not a temporary escape hatch.
> `Data.As<T>` does propagate the canonical name through `TryFullVarMatch` for `%var%`
> form, but bare names ("x" instead of "%x%") would silently route to the slot key.
> `[VariableName]` (via `__StripPercent`) handles both forms natively, which is why it
> remains the right shape for write targets.

The carve-out wording in `/PLang/App/CLAUDE.md` (currently: *"`[VariableName]` is the carve-out
for handlers that need the variable's name not its value — folded into `Data<T>` once a
`VarRef<T>` design lands"*) needs updating to reflect that this is permanent, not transitional.

## Optional Phase 5 — `Data.RawValue` (if you want it)

Side-quest, not required. Adds a `RawValue` property on `Data` that survives `As<T>` (aliased
through `ConstructWrap` like `Properties` and the event lists). Returns the unresolved string
(e.g. `"%x%"` or `"x"`) of the slot Data the handler property was bound to. Useful for:

- Diagnostics (showing what the LLM actually emitted in the .pr).
- Build-time tooling that wants to inspect raw shapes without resolving.
- Any future handler that wants `%`/no-`%` symmetry without `[VariableName]`.

Not in conflict with `[VariableName]`. Could be the first step of a future `VarRef<T>`-style
design if one ever lands. But there is no current pressure to add it — flag for later.

## What I'd ask from you next

A revised v2 plan covering:

1. Phase 1 (read-site migration) only — re-validate the ~16-handler list per-handler. Some
   may turn out to need the literal name after all (e.g. `list/get` if it later writes via
   `__data__`). Per-handler review needed.
2. The new dual-rule documentation in §good_to_know and the CLAUDE.md update.
3. Decide on Phase 5 (`Data.RawValue`) — yes/no/defer.

`tester` and `test-designer` don't have anything to pick up until Phase 1 actually lands —
this is purely an architecture replan.

## Artifacts on this branch

- `PLang.Tests/App/DataTests/VariableSetNameResolutionTests.cs` — the four proof tests. Keep
  them; they document the mechanism and would catch any future drift. Even though they're no
  longer load-bearing for a migration, they're cheap and accurate documentation of the
  `As<T>` Name-propagation contract.
