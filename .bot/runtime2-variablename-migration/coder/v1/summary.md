# Coder v1 — Phase 0 verification + design decision

## What this is

Architect's plan called for migrating ~24 handler properties from `[VariableName] partial
string Foo` to `Data<T> Foo` and reading `Name.Name` (the resolved Data's name) to recover
the literal variable name. Phase 0 of the plan asked coder to lock the `As<T>` Name-
propagation claim into tests before any handler change.

The mechanism works exactly as architect described — proven mechanically and end-to-end.
But Ingi flagged that the bare-name case (LLM emits `"x"` instead of `"%x%"`) silently
routes to the slot key under the new path, while the existing `[VariableName]` / `__StripPercent`
path handles both forms natively. After discussion, we kept `[VariableName]` and sent the
plan back to architect for replanning.

## What was done

Wrote 4 unit tests in `PLang.Tests/App/DataTests/VariableSetNameResolutionTests.cs`. All
pass. Confirmed via `PLang.Generators/Emission/Property/Data/this.cs:50` that the source
generator's getter for `Data<T>` properties emits exactly the call my tests exercise
(`__ResolveData("Name").As<T>(Context)`). Confirmed real `.pr` shape matches the test
fixture by reading `Tests/App/StepResult/.build/stepresult.test.pr`. Ran
`Tests/App/StepResult/StepResult.test.goal` (`set %x% = "hello"`) end-to-end via `plang
--test` — `[Pass]`.

Files modified:

- `PLang.Tests/App/DataTests/VariableSetNameResolutionTests.cs` — proof artifact, 4 tests.
- `.bot/runtime2-variablename-migration/coder/v1/plan.md` — this session's plan.
- `.bot/runtime2-variablename-migration/coder/v1/architect-handoff.md` — handoff with the
  decision and a request for v2 plan.
- `.bot/runtime2-variablename-migration/coder/v1/summary.md` — this file.
- `.bot/runtime2-variablename-migration/coder/summary.md` — bot-root summary.
- `.bot/runtime2-variablename-migration/report.json` — coder session entry appended.

No production C# changed. No `.goal` changed. The test file is additive — it documents the
`As<T>` Name-propagation contract whether or not the migration ever ships.

## Decision

`[VariableName]` is canonical for write-target slots. The Legacy emission path is
permanent, not deprecated. Migration scope reduces to read-site cleanup only (architect
to replan).

## Code example

The exact shape that proves the architect's claim — and the same shape that exposes the
bare-name regression:

```csharp
// Slot Data shape, verbatim from the .pr deserialization for `set %x% = 5`:
var nameSlot = new Data("Name", "%x%") { Context = ctx };

// Architect's claim — confirmed:
var resolved = nameSlot.As<string>(ctx);
Assert.That(resolved.Name).IsEqualTo("x");        // ✓ TryFullVarMatch extracted "x"

// The crack — bare LLM emission:
var bare = new Data("Name", "x") { Context = ctx };
var resolvedBare = bare.As<string>(ctx);
Assert.That(resolvedBare.Name).IsEqualTo("Name"); // ✗ slot name leaks
```

The first assertion is what makes the migration architecturally viable. The second is
what makes it a robustness regression vs `[VariableName]`. Hence the decision to keep
`[VariableName]`.

## Next

Architect picks up the handoff in `architect-handoff.md` and produces v2 plan covering
read-site migration only. Tester / test-designer / codeanalyzer have nothing to do until
v2 lands.
