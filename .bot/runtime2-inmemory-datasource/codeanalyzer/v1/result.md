# Code Analysis — runtime2-inmemory-datasource v1

## SqliteDataSource.cs

### OBP Violations
None. The sentinel pattern is good OBP — SqliteDataSource owns its own lifecycle. The static factory `InMemory(name)` is behavior on the owner. No external lifecycle management needed.

### Simplifications
None.

### Readability
Clean. The sentinel comment on line 56 explains why, not what.

### Behavioral Reasoning

**In-memory DB name collision across concurrent engines (informational)**

`SqliteDataSource.InMemory("system")` uses the actor name as the SQLite shared-cache DB name. SQLite shared-cache in-memory DBs are process-wide — two calls to `InMemory("system")` from different Engine instances connect to the SAME database.

The test runner runs sequentially and disposes each engine between tests, so this works. But if two engines are ever alive simultaneously (e.g., parallel PLang tests or an embedded PLang scenario), their in-memory datasources would share state silently. No test would fail, no error would surface — data just leaks between engines.

**Severity: Low.** Current usage is safe. Flag for future if parallel test execution is added.

**Mitigation if ever needed**: include Engine.Id in the name: `InMemory($"{Name}_{Engine.Id}")`.

### Deletion Test
- Lines 47-58 (in-memory constructor): Delete → 7 new DataSourceTests fail. **Covered.**
- Lines 64-65 (InMemory factory): Delete → same 7 tests fail. **Covered.**
- Lines 316-319 (sentinel dispose): Delete → `InMemory_DisposeClosesDb` test would likely become flaky (sentinel not closed, DB may persist via pool). **Partially covered** — test verifies behavior but relies on GC/pool timing.

### Verdict: CLEAN
Well-structured addition following the existing pattern.

---

## Build/this.cs

### OBP Violations
None. Follows the Debug/Test pattern exactly.

### Simplifications
1. **Line 10: `_engine` field is stored but never used.** No method or property reads it. It exists only for pattern consistency with Debug/Test (which do use their engine refs). This is a design choice, not a bug — the engine ref will be needed when the runtime2 builder is implemented. Acceptable.

### Readability
Clean.

### Deletion Test
- Delete entire file → `Actor_UsesInMemory_WhenBuildingEnabled` test fails (can't compile — `Engine.Building` doesn't exist). **Covered.**

### Verdict: CLEAN

---

## Actor.cs

### OBP Violations
None. `CreateDataSource()` navigates to `Engine.Testing.IsEnabled` and `Engine.Building.IsEnabled` — reads context from the object graph, doesn't receive flags as parameters.

### Simplifications
None.

### Readability
Clean. The guard clause at lines 72-73 is clear.

### Deletion Test
- Lines 72-73 (in-memory guard): Delete → `Actor_UsesInMemory_WhenTestingEnabled` and `Actor_UsesInMemory_WhenBuildingEnabled` fail. **Covered.**

### Verdict: CLEAN

---

## Engine/this.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. Building property follows the same pattern as Debug, Testing.

### Deletion Test
- Line 128 (`Building` property) + line 200 (constructor init): Delete → compilation error in Actor.CreateDataSource. **Covered.**

### Verdict: CLEAN

---

## Variables.cs

### OBP Violations
None. Variables owns variable navigation.

### Simplifications
None.

### Readability
The fix at lines 82-92 is more verbose than the old one-liner but correctly handles the case where the separator after the root name is `[` (array index) vs `.` (dot navigation). The old code `name[(rootName.Length + 1)..]` always skipped one character after the root, which was wrong for `items[0]` — it would skip the `[` and produce `0]` instead of `[0]`.

### Behavioral Reasoning
**This is the fix that makes `%items[0]%` work.** Before, `Variables.Get("items[0]")` produced `remaining = "0]"` (skipping the `[`), which failed to navigate. After, it correctly produces `remaining = "[0]"`.

### Deletion Test
- Lines 82-92: Revert to old one-liner → `Get_DirectArrayIndex_NavigatesCorrectly`, `Get_ArrayIndexWithProperty_NavigatesCorrectly`, `Get_MixedNotation_NavigatesComplexPath` tests fail. **Covered.**

### Verdict: CLEAN
Correct fix with good test coverage.

---

## Step/Methods.cs — FINDING #1

### OBP Violations
None.

### Behavioral Reasoning — **AfterStep now fires on failure**

**This is the most significant change on the branch.** The old code had:

```csharp
if (!result.Success) return result;  // line 74 — early return, AfterStep never runs
```

The new code removes the early return and stores the step result in Variables:

```csharp
context.Variables.Put(new Data("__stepResult", result.Value, result.Type) { Error = result.Error });
// AfterStep events now run regardless of success/failure
```

**What changed:**
- Before: AfterStep events only fire on step success
- After: AfterStep events fire on every step, including failures

**Why it was done:** The test runner registers an AfterStep handler (`TrackAssertionFailures`) to detect assertion failures. If AfterStep doesn't fire on failure, the test runner never sees the assertion error.

**What could break silently:** Any existing PLang app that has AfterStep event bindings and assumes they only run on success. After this change, those handlers will run on failure too. If a handler doesn't check `__stepResult.Success`, it might process invalid data.

**Assessment:** This is correct for the test runner, and arguably correct in general — lifecycle events should bracket the full lifecycle, including failures. The original behavior (skip AfterStep on failure) was likely an oversight, not a design choice. But it IS a behavioral contract change.

**Severity: Medium.** The risk is real but mitigated by the fact that AfterStep handlers should be resilient to failure states. The test runner's `stopOnError: false` flag is a good pattern to follow.

### Deletion Test
- Line 75 (`__stepResult` storage): Delete → test runner can't track assertion failures. PLang tests that assert failure conditions would become false-green. **Covered.**

### Verdict: NEEDS WORK — not because the code is wrong, but because the behavioral change is undocumented in the commit or architecture docs.

**Recommendation:** Add a comment in `good_to_know.md` documenting that AfterStep events fire on both success and failure, and that handlers should check `__stepResult.Success` if they need to distinguish.

---

## Test/this.cs

### OBP Violations
None. The test runner navigates `context.Step` and reads `__stepResult` from Variables — valid navigations.

### Simplifications
None.

### Readability
Clean. The two branches for `AssertionError` vs other errors at lines 148-164 are clear.

### Behavioral Reasoning
The new code at lines 148-158 catches assertion failures that bubble up to goal level (not caught by AfterStep). This is a safety net: normally `TrackAssertionFailures` catches them via AfterStep, but if the assertion error propagates past the step (e.g., because the step has no OnError handler), the goal-level check picks it up.

### Deletion Test
- Lines 148-158: Delete → would assertion errors that escape AfterStep be missed? This depends on whether any PLang test step has an assertion without OnError handling AND the assertion error bubbles past AfterStep. Likely scenario — if an assertion step immediately fails and returns the error, AfterStep fires and catches it via `__stepResult`. This code block may be redundant with the AfterStep handler. **Soft gap — hard to verify without running all PLang tests.**

### Verdict: CLEAN

---

## list/unique.cs

### OBP Violations
None.

### Simplifications
Good simplification — returns plain `List<object?>` instead of wrapping in `types.list`. Simpler is better.

### Deletion Test
- `.Cast<object?>()`: Without it, `list.Distinct()` returns `IEnumerable<object?>` which `.ToList()` converts to `List<object?>` anyway. But the `Cast` makes the type explicit and ensures the list element type is `object?` not `object`. Tests would still pass without it, but the type would be wrong. **Soft coverage.**

### Verdict: CLEAN

---

## SqliteSettingsRepository.cs

### OBP Violations
N/A — v1 code, not App.

### Behavioral Reasoning — **FINDING #2: TOCTOU in migration**

Lines 163-167:
```csharp
var oldTable = connection.Query<dynamic>("SELECT name FROM sqlite_master WHERE type='table' AND name='Settings'");
if (oldTable.Any())
{
    connection.Execute("ALTER TABLE Settings RENAME TO SettingsV1");
}
```

Two concurrent processes can both see the `Settings` table, both try to rename it. The second fails with a SQLite error. This is a TOCTOU race.

**Severity: Low.** The `CREATE TABLE IF NOT EXISTS SettingsV1` at line 169 runs regardless, so the table exists either way. The ALTER failure would throw an exception, but `CreateSettingsTable` is called from `Init()` which is called from the constructor, so the exception would propagate to startup. In practice, PLang apps are single-process, so this race is unlikely.

**Recommendation:** Wrap the check+rename in a try/catch to handle the "table already renamed" case gracefully. Or just drop the rename entirely — the new table is `SettingsV1`, and `CREATE TABLE IF NOT EXISTS` handles the case where it already exists. Only existing databases with a `Settings` table need migration.

### Verdict: NEEDS WORK (minor — race condition in migration)

---

## GlobalUsings.cs

### Verdict: CLEAN
Just documentation comments.

---

# Overall Assessment

## Summary

The in-memory datasource implementation is clean, well-tested, and OBP-compliant. The sentinel pattern is correct. The Building object follows established conventions.

Two findings:

| # | File | Severity | Description |
|---|---|---|---|
| 1 | Step/Methods.cs | Medium | AfterStep behavior change (now fires on failure) is undocumented. Correct design but a contract change that could surprise existing code. |
| 2 | SqliteSettingsRepository.cs | Low | TOCTOU race in Settings→SettingsV1 migration. Single-process PLang apps are safe, but the code should handle concurrent migration gracefully. |

## Verdict: PASS

Neither finding is a blocker. Finding #1 is an intentional design improvement with correct test coverage. Finding #2 is in v1 code and only affects existing databases during first migration.
