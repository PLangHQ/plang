# v3 — runtime2-builder-bootstrap (NEEDS WORK)

## What this is

Fresh-eyes review of the whole branch. v1 looked at ~22 files; v2 verified the coder's fixes to 11 of those. v3 spread out: pattern-sweeps across all 167 changed C# files, plus deep reads on what v1 deferred (Debug/this.cs, TypeMapping in full, builder modules, test infrastructure).

## What was done

Two halves:

**Half A — pattern sweeps**: searched the diff for bare catches, throws-from-Try, Clone-family methods, System.IO violations, generic casts, mutable static state, OBP outside-iteration, and locale-sensitive conversions. Each match was triaged.

**Half B — deep reads**: full 5-pass analysis on Debug/this.cs (282 changed lines), TypeMapping.cs (708 changed lines), DefaultBuilderProvider.cs (310 changed lines), Catalog/this.cs, validateResponse.cs, modules/test/{run,discover}.cs.

What I found that v1+v2 didn't catch:

- **5 more bare catches** in places v1's triage didn't open: `test/discover.cs:48`, `list/add.cs:71`, `Debug/this.cs` (×3 at 218, 614, 672), `DefaultBuilderProvider.cs:440`. v1's grep was Tier 1/2-scoped; the same anti-pattern lives in the rest of the diff.
- **Step.@this.Clone()** is missing 7 of the 18 properties on Step (PriorText, Guidance, Level, Confidence, Formal, Source, Keep — all added this branch). The method has zero production callers and one test caller that doesn't exercise the new properties. Recommendation: delete the method.
- **TypeConverter.cs:322** uses `Convert.ChangeType` without `InvariantCulture`. PLang values that round-trip through JSON as strings (`.pr` files, settings) will fail to parse on European locales (`,` vs `.` decimal separator).
- **DefaultBuilderProvider** has two silent-failure spots: `NormalizeParameterTypes` discards conversion errors (line 607), `PromoteGroups` warns to stderr but doesn't fail when steps come in as JsonElement (line 550).
- **Debug.Apply()** is not idempotent — second call doubles all event handlers. Single-shot today, hazard tomorrow.
- **Debug LLM tracing** hardcodes `OpenAiProvider`; a second provider would silently get no tracing.

Files written:
- `v3/plan.md` — scope and method
- `v3/result.md` — Half A + Half B findings, prioritized list at the end
- `v3/summary.md` — this file
- `v3/verdict.json` — `{ status: fail }`
- `v3/changes.patch` — diff vs runtime2

## Code example — the recurring pattern

Step.@this.Clone() is the cleanest illustration of the systemic issue:

```csharp
public @this Clone()
{
    return new @this
    {
        Index = Index, Text = Text, LineNumber = LineNumber,
        Indent = Indent, Comment = Comment,
        Actions = new Actions.@this(...),  // deep
        WaitForExecution = WaitForExecution,
        Goal = Goal, Intent = Intent,
        Errors = new List<Info>(Errors),
        Warnings = new List<Info>(Warnings)
        // PriorText, Guidance, Level, Confidence, Formal, Source, Keep — silently missing
    };
}
```

This is the third instance on this branch (after Context.Clone/CreateChild and Variables.Clone) of "property added, copy not updated" — the team's standing clone-family hazard. The fix isn't to chase every clone; it's to either delete the method (zero callers) or generate it.

## Verdict

**NEEDS WORK** — 5 bare catches, 1 broken Clone, 1 locale bug, 2 silent-failure paths in the builder, 1 idempotency hazard. None alone are blockers; together they're the same anti-patterns v1 already flagged, surfacing at sites v1 didn't open.

## Next step

Send back to **coder** for the priority list in `v3/result.md`. Most fixes are mechanical (the bare catches use the same shape v2 already applied; the Convert.ChangeType fix is one line). The Step.Clone deletion needs a quick test cleanup. Total work is small but covers real bugs.

After coder's response, send to tester — the locale fix in particular wants a Spanish/Italian-culture test before it ships.
