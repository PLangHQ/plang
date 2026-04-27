# v1 — Summary

## What this is

Codeanalyzer's first review of `runtime2-builder-bootstrap`. The branch lands the v2 PLang builder as a self-hosting pipeline: a Catalog system, structured Examples, per-build Trace ids, ParamSnapshots on errors, typed BuildResponse end-to-end, and a CLR-type-name leak guard. The actual scope is much larger than the coder handover described — the handover named only 3 small "gap fixes" while the branch contains a full diagnostics + module sweep squashed into one large commit (`50351d8b`) plus 5 follow-ups.

## What was done

Scoped the review to the highest-risk new and changed C# files in `PLang/` (~14 files, ~2k lines deep dive + ~12 lighter scans). Skipped `.goal` / `.pr` files, `.build/` outputs, docs, and modules outside the focused list.

Findings split by severity:
- **MAJOR** — 5 instances of bare `catch` swallowing `Exception` (TypeConverter:88,190; Variables/this:170; FluidProvider:140; Errors/Error:292) plus the source generator emitting unfiltered catches in generated `ExecuteAsync`. Per project memory, silent error swallowing is always critical.
- **MAJOR** — `TypeConverter.TryConvertTo` throws `InvalidOperationException` from a `Try*` method (lines 266, 296). Contract violation: the method signature is `(value, error)` and it must return errors, not throw them.
- **MAJOR** — `DefaultBuilderProvider` has a live diagnostic probe (lines 174–198 `DiagGoal`) that walks every goal/step/action on every save, hardcodes the parameter name "GoalName", writes debug output unconditionally. The leak-hunt that motivated it is fixed per the recent commits. Pure deletion-test win.
- **MEDIUM** — `Data.@this.IsDeferredActionTemplate` key-name heuristic (matches PLang type name string `"action"`/`"list<action>"`). Fragile to renames and user-declared aliases; should be structural.
- **MEDIUM** — `Actor.Context.@this` Clone vs CreateChild propagate different state. New properties (Trace, Error, Test) are not propagated through either consistently. Classic Clone/Copy family hazard.
- **MEDIUM** — `error.handle.Wrap` returns the recovery value on the GoalFirst path (line 96) but drops it on the RetryFirst path (line 109). Asymmetric — likely a bug or needs documentation.
- **MEDIUM** — `PlangTypeIndex.Reset()` clears 2 of its 4 caches; `_clrTypeFullNamesInitialized` flag is non-volatile DCL.
- **MINOR** — Three places implement "formal syntax" rendering (ExampleRenderer, FluidProvider, DefaultBuilderProvider) — guaranteed drift point.
- **MINOR** — Culture-sensitive `ToString()` calls in formal-syntax renderers.

10 actionable items prioritized for the coder.

The Catalog system, PlangType attribute, ParamSnapshot, BuildResponse, and trace plumbing are otherwise well-designed — the architecture is solid, the issues are bug-hardening.

## Code example — the deletion-test win

`PLang/App/modules/builder/providers/DefaultBuilderProvider.cs`, lines 171–198:
```csharp
// Probe — for every GoalName param, log value runtime type, dict keys, and
// BOTH a direct JsonSerializer.Serialize(value, PrWrite) AND a serialize-of-
// the-Data wrapper. Compare the two paths to find where PascalCase appears.
void DiagGoal(App.Goals.Goal.@this g)
{
    foreach (var step in g.Steps)
        foreach (var act in step.Actions)
            foreach (var p in act.Parameters)
                if (p.Name == "GoalName")
                {
                    /* … log keys, directJson, dataJson via Debug.Write … */
                }
    foreach (var sub in g.Goals) DiagGoal(sub);
}
DiagGoal(goal);
```
This is leak-hunt instrumentation from commit `ada1901a`. Per commit `711c2107`, the bug is fixed. If deleted, no test breaks. Same pattern (live probes left behind after the hunt) is worth searching for elsewhere.

## Code example — the contract violation

`PLang/App/Utils/TypeConverter.cs`, lines 261–270 (also 295–300):
```csharp
if (value is string goalName)
{
    if (PlangTypeIndex.IsClrTypeName(goalName))
        throw new InvalidOperationException(
            $"GoalCall.Name was set to a CLR type name '{goalName}' from a string source. " +
            $"Build pipeline leaked a typed object's ToString() into a goal-name slot.");
    return (new App.Goals.Goal.GoalCall { Name = goalName }, null);
}
```
The method's contract is `(value, Errors.Error?)` — never throw. The intent (defensive tripwire) is sound; should be:
```csharp
if (PlangTypeIndex.IsClrTypeName(goalName))
    return (null, new Errors.Error(
        $"GoalCall.Name was set to a CLR type name '{goalName}'.",
        "ClrTypeNameInGoalSlot", 500)
        { FixSuggestion = "Build pipeline leaked a typed object's ToString() into a goal-name slot." });
```
Same fix applies to lines 295–300 (the dict path).

## What to do next

1. Send `result.md` to the coder for the 10 prioritized fixes.
2. After fixes: re-run codeanalyzer v2 to verify (especially the bare-catch sweep).
3. Tester pass on the test suite once the throws are converted to returns — the GoalCall guard tests need to assert `Error` returns now.
