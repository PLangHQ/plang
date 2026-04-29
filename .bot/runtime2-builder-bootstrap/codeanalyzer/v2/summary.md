# v2 — runtime2-builder-bootstrap (CLEAN)

## What this is

Re-review of commit `80200746` ("Address codeanalyzer v1 findings on runtime2-builder-bootstrap"). v1 returned NEEDS WORK with 10 prioritized findings; the coder closed 8, deferred 2. This pass verifies the closures are real and don't break anything.

## What was done

The 11 files touched by `80200746` were re-read in full; each fix was checked against its v1 finding (did it close the concern?) and run through Pass 4 behavioral reasoning (does the fix itself break anything?). Caller traces, memory-model semantics, and over-broad-filter audits were the primary lenses.

Key results:
- **All 8 priority fixes verified.** DiagGoal probe deletion, TypeConverter throw→Error conversion, bare-catch filtering at 5 sites + the source generator, IsDeferredActionTemplate switch to CLR identity, Variables.Set debug logging, Context Clone/CreateChild documentation, PlangTypeIndex Reset removal + volatile flag, error.handle.Wrap symmetric recovery return.
- **No regressions.** Each fix's caller surface was traced. The throw→Error change is invisible at every existing call site (all 16). The IsDeferredActionTemplate broadening is a correct expansion (catches `Actions.@this` collections that the old name-string check silently missed). The volatile flag has correct .NET memory-model semantics.
- **2 items explicitly deferred.** Three formal-syntax renderers (#9) and culture-sensitive ToString (#10) were not addressed; the coder noted these fold together into a "consolidate-formal-renderers" follow-up pass. Acceptable.
- **4 carryover sub-findings** (silent first-element-of-array in TypeConverter, null → 0 value-type default, fall-through after JSON parse failure, debug-only logging in Variables.Set) — not regressions, not on v1 priority list, worth tracking.

Files written:
- `v2/v1_review_summary.md` — what v1 said and what the coder did
- `v2/plan.md` — re-review scope and method
- `v2/result.md` — per-fix verification + carryover items
- `v2/summary.md` — this file
- `v2/verdict.json` — `{ status: pass }`
- `v2/changes.patch` — diff vs runtime2

## Code example — the kind of fix verified

The throw→Error pattern at TypeConverter, before and after. v1 caught the throw; v2 verifies the new return shape works through the call graph:

**Before:**
```csharp
if (PlangTypeIndex.IsClrTypeName(name))
    throw new InvalidOperationException(
        $"GoalCall.Name was set to a CLR type name '{name}'. ...");
```

**After:**
```csharp
if (PlangTypeIndex.IsClrTypeName(name))
    return (null, new Errors.Error(
        $"GoalCall.Name was set to a CLR type name '{name}'.",
        "ClrTypeNameInGoalSlot", 500)
        { FixSuggestion = "Build pipeline leaked a typed object's ToString() ..." });
```

The Pass 4 work was tracing all 16 callers of `TryConvertTo` to confirm none expect the throw — they all tuple-deconstruct `(value, error)` and check `error`. No regression possible.

## Next step

Tester. Two behavioral changes deserve test coverage before the branch ships:
1. The error.handle.Wrap RetryFirst-with-recovery path now flows the recovery's value (todo already in `Documentation/Runtime2/todos.md`).
2. The IsDeferredActionTemplate broadening — confirm `Actions.@this` collections don't get walked by ResolveDeep.
