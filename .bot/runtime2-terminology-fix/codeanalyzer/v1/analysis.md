# Code Analyzer v1 — runtime2-terminology-fix

## Scope

Higher-level cross-concern analysis of the terminology rename: `actions/` → `modules/`, `IClass` → `IAction`, Library internal variables `handler` → `action`, error key `"HandlerError"` → `"ActionError"`.

## Verification Summary

| Surface | Status | Notes |
|---------|--------|-------|
| Namespace (`App.modules`) | Clean | All production + test code updated |
| Interface (`IAction`) | Clean | No remaining `IClass` references |
| Library field (`_actions`) | Clean | No remaining `_handlers` |
| Tuple field (`Action`) | Clean | All call sites use destructuring, no `.Handler` access |
| Error key (`"ActionError"`) | Clean in production | See Finding #1 for test straggler |
| Source generator string literals | Clean | 3 literals in LazyParamsGenerator verified |

## Findings

### Finding #1 — Stale "HandlerError" in ErrorInfoTests (Minor)

**File:** `PLang.Tests/App/Errors/ErrorInfoTests.cs:198,204`

```csharp
var error2 = new Error("Handler error", step, "HandlerError", 500);
// ...
await Assert.That(formatted).Contains("HandlerError(500)");
```

The `Format_IncludesErrorChain` test uses `"HandlerError"` as test data for error formatting. This is not a functional bug — the test is about formatting, not the specific key. But it contradicts the coder summary's claim of "Zero remaining references to HandlerError in production/test/generator code."

**Severity:** Minor. Test still passes. But it's stale terminology that should be updated for consistency.

**Fix:** Change `"HandlerError"` → `"ActionError"` and `"Handler error"` → `"Action error"` in the test, update assertion to `"ActionError(500)"`.

### Finding #2 — Scaffolder skeleton files have stale namespace (Non-blocking)

**Files:** 4 files in `.bot/runtime2-settings/scaffolder/v1/` still reference `App.actions`:
- `skeletons/ArchiveSettings.cs`
- `skeletons/archive_settings.cs`
- `skeletons/archive_types.cs`
- `tests/csharp/ModuleViewTests.cs`

These are scaffolder output from the `runtime2-settings` branch — they'll be stale when that branch rebases onto this one. Not blocking, but worth noting for whoever merges.

**Severity:** Non-blocking. These are `.bot/` artifacts, not production code.

## Cross-Concern Analysis (Applying Learnings)

### "A fix can introduce the same class of bug" — N/A
The rename is purely mechanical. No new logic was introduced, so no new bugs of the same class.

### "Trace data origins" — Verified
The `"ActionError"` key is produced at one site: `Libraries/this.cs:53`. It flows into `ActionError` constructor (default key). No external consumers check for the old `"HandlerError"` string — it was purely internal.

### "Review fixes against full type surface" — Complete
All rename surfaces verified: namespaces (134 files), interface name, file names, variable names (`_actions`, `action`), tuple names (`Action`), error key, source generator string literals (3 occurrences).

### "Deletion tests" — Confirmed
The old entity `IAction.cs` interface was dead code. Deleting it caused no build failure, confirming it was truly unused. Good hygiene.

### Tuple rename breaking change — Mitigated
The `Handler` → `Action` tuple field rename could have broken call sites using `.Handler`. All 10+ call sites verified — they all use tuple destructuring (`var (action, error) = ...`), so the named field change is transparent.

## Test Results

Coder reports 1423 pass, 0 fail. No new tests were needed — this was a rename, not a behavior change.

## Conclusion

The rename is clean and complete in production and test code. One minor straggler in test data (`"HandlerError"` in ErrorInfoTests) should be updated for consistency.
