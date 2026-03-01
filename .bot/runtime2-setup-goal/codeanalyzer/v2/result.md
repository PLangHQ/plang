# Code Analyzer v2 — runtime2-setup-goal

## Overall Verdict: CLEAN

All three v1 findings addressed correctly. No new issues introduced.

---

## v1 Finding 1 (High): Failed setup steps permanently marked as executed
**Status: FIXED**

`Steps/this.cs:41-48` — Record now only happens on success or tolerated error:
```csharp
if (context.Setup != null)
{
    var tolerated = stepResult.Success || (step.OnError?.IgnoreError ?? false);
    if (tolerated)
        await context.Setup.Record(step, engine, stepResult.Success ? null : stepResult.Error);
}
```

Test `RunAsync_FailedStepNotRecorded` verifies a failed step is NOT recorded. Test `RunAsync_ToleratedErrorStepIsRecorded` verifies a tolerated error IS recorded. Both test the exact behavioral boundary.

## v1 Finding 2 (Medium): Setup.Record silently swallows errors
**Status: FIXED**

`Setup/this.cs:72` — Record now returns `Task<Data>`:
```csharp
public async Task<Data> Record(Step step, Engine.@this engine, IError? error = null)
```

The caller in Steps.RunAsync doesn't check the return value (line 47), which is acceptable — if recording fails, the step re-runs on next startup (safe failure mode).

## v1 Finding 3 (Low): Count/All include setup goals but Get excludes them
**Status: FIXED**

`Goals/this.cs:178-193`:
- `AllIncludingSetup` (internal) for Setup.Goals to use
- `All`, `Count`, `Value` now filter `!g.IsSetup`, consistent with `Get()`

Minor note: `Names` (line 173) still returns all keys including setup goals. This is a trivial inconsistency — `Names` is rarely used and doesn't participate in goal lookup. Not worth a finding.

---

## New Code Review

The fixes themselves are clean:
- No new OBP violations
- No new behavioral issues
- Test coverage on both sides of the record boundary (success → recorded, failure → not recorded, tolerated → recorded)
- `AllIncludingSetup` is correctly scoped as `internal`
