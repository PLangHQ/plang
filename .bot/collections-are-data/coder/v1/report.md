# Coder report — collections-are-data — v1 (regression sweep)

## Summary

Stages 1–6 of collections-are-data were already committed (dict/list hold `Data`
end-to-end, F1 dead). This session closed the **runtime regressions** that the
refactor introduced, found by running the full plang suite and the builder
end-to-end. Final state:

- **C# suite: 4080 / 0**
- **plang suite: 273 / 273**
- **builder: healthy end-to-end** (was deterministically crashing)

## Root causes found and fixed (each at the owning type, not the call site)

Four regressions, all the same shape: a value that used to be a CLR
`Dictionary`/`List` now arrives as a native `dict.@this`/`list.@this`, and a
consumer only recognised the old CLR shape.

1. **Empty-string → collection conversion** (`type/list/this.Convert.cs`,
   `type/dict/this.Convert.cs`, new). `set %x% = []` serialises as
   `Value="" Type="list"`; converting `""` → `list.@this` threw. Gave each native
   type a `Convert` hook: a blank string builds an empty collection; populated
   JSON strings are declined (null) and fall through to the existing deserialize
   path. *Symptom: `/Builder/ValidateActionsOnly`.*

2. **Stale `TypeProvider.dll`** (`Tests/Cut4_RuntimeLoadAndRender/`). The committed
   fixture was built against the pre-rename `app.type.list.ITypeRenderer`; rebuilt
   against `app.type.catalog`. *Symptom: `LoadDllRegistersType`, `LoadDllOverwritesBuiltIn`.*

3. **GoalCall dropped native-list parameters** (`goal/GoalCall.cs`). The
   `parameters` slot arrives as `list.@this` of `Data`-wrapped `dict.@this`;
   `Convert` only matched `IList<object?>` of `IDictionary<string,object?>`, so it
   was silently dropped. This broke the builder's core
   `foreach %goals%, call BuildGoal goal=%item%` — `%goal%` never reached the child
   scope, surfacing as NRE in `goalsSave`. Added a `ParamEntries` normaliser
   (native OR CLR, Data-wrapped elements unwrapped).

4. **`text.Convert` rejected native collections** (`type/text/this.Convert.cs`) —
   **the deterministic builder-compile blocker.** `text` serialises structured
   values (`IDictionary`/JSON DOM/`IEnumerable`) to JSON text, but a native
   `dict.@this`/`list.@this` is none of those CLR shapes, so it hit the
   "no textual form" rejection. The builder's compile step binds the planner's
   actions list into the prompt text → every goal build died at compile with
   `TypeConversionFailed`. Routed native collections through the same
   serialization branch (their `[JsonConverter]` already renders `{}`/`[]`).

## On "stop blaming LLM non-determinism"

Confirmed correct. The builder failures looked like flaky LLM output (planner
step-count mismatch, empty compile prompt) but were **deterministic** — a trivial
one-step `set %result% = "less"` failed identically every run. The cause was #4,
reached through the data flow. Every navigable point in the builder was verified
correct (plan.steps.Count, planStep.actions, goal injection) before concluding
the bug was in conversion, not navigation.

## Fixtures rebuilt

`WhenLess.goal` / `WhenLTE.goal` helper `.pr` had a stale spurious `condition.if`
wrapping their plain `set` (an old mis-build, previously masked because the
pre-Stage-4 comparison treated `null <= "less"` as true; the architect's
"nulls sort last" contract correctly makes it false, which exposed it). With the
builder healthy again they recompile to a single `variable.set`.

## Commits (pushed to origin/collections-are-data)

- `1e5bacb98` empty-string→collection conversion + TypeProvider.dll refresh
- `bd0f17c4b` GoalCall native list.@this parameter shape
- `bdf6631d8` text.Convert serializes native dict/list — fixes builder compile
