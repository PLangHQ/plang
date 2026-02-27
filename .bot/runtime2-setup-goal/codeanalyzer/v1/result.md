# Code Analyzer v1 — runtime2-setup-goal

## Overall Verdict: NEEDS WORK

Two behavioral issues in the setup record-on-failure path. The rest is clean.

---

## Finding 1 (Behavioral — High): Failed setup steps are permanently marked as executed

**File:** `PLang/Runtime2/Engine/Goals/Goal/Steps/this.cs:39-43`

Steps.RunAsync records a step's execution **before** checking whether the error should propagate:

```csharp
var stepResult = await step.RunAsync(engine, context, cancellationToken);

// Record execution in setup table (even on tolerated errors)
if (context.Setup != null)
    await context.Setup.Record(step, engine, stepResult.Success ? null : stepResult.Error);

if (!stepResult)
{
    if (!(step.OnError?.IgnoreError ?? false))
        return stepResult;  // <-- setup fails, but step is already recorded
}
```

**What breaks:** A setup step "create table users" fails due to a transient issue (permission, disk full, network). The step is recorded as executed. Setup returns the error and the app stops. On next startup, `IsExecuted` returns true for that hash — the step is skipped forever. The table never gets created.

The only way to recover is to change the step text (so the hash changes), which is not obvious to the user.

**Fix:** Only record on success or when the error is explicitly tolerated:

```csharp
var stepResult = await step.RunAsync(engine, context, cancellationToken);

if (context.Setup != null)
{
    var shouldRecord = stepResult.Success || (step.OnError?.IgnoreError ?? false);
    if (shouldRecord)
        await context.Setup.Record(step, engine, stepResult.Success ? null : stepResult.Error);
}
```

This way, a failed step that aborts setup will NOT be recorded and WILL be retried on next startup.

---

## Finding 2 (Behavioral — Medium): Setup.Record silently swallows DataSource errors

**File:** `PLang/Runtime2/Engine/Goals/Setup/this.cs:72-86`

`Record` returns `Task`, not `Task<Data>`. If `DataSource.Set` fails (disk full, locked database), the error is silently swallowed:

```csharp
public async Task Record(Step step, Engine.@this engine, IError? error = null)
{
    if (string.IsNullOrEmpty(step.Hash)) return;
    // ...
    await engine.System.DataSource.Set(Table, step.Hash, metadata);
    // ^^^ if this fails, nobody knows
}
```

The caller in Steps.RunAsync has no way to know recording failed. Combined with Finding 1's fix, a failed Record is actually the safer failure mode (step re-runs next time). But there's still zero diagnostic information.

**Fix:** Return `Task<Data>` and let the caller decide:

```csharp
public async Task<Data> Record(Step step, Engine.@this engine, IError? error = null)
{
    if (string.IsNullOrEmpty(step.Hash)) return Data.Ok();
    // ...
    return await engine.System.DataSource.Set(Table, step.Hash, metadata);
}
```

The caller can log the failure without aborting setup.

---

## Finding 3 (Consistency — Low): EngineGoals.Count/All include setup goals, but Get excludes them

**File:** `PLang/Runtime2/Engine/Goals/this.cs:183,188`

```csharp
public IEnumerable<Goal.@this> All => _goals.Values;     // includes setup
public int Count => _goals.Count;                         // includes setup
public Goal.@this? Get(string name) { ... !goal.IsSetup } // excludes setup
```

`engine.Goals.Count` returns a number that includes setup goals, but `engine.Goals.Get(name)` can't find them. If a caller iterates `All` and then tries to `Get` each by name, setup goals silently disappear.

This isn't a bug in current usage (Setup.Goals uses All correctly), but it's a consistency trap for future callers.

**Suggestion:** Either filter setup from `All`/`Count` too, or add a `NonSetup` property and make the filtering explicit. The cheapest fix:

```csharp
public int Count => _goals.Values.Count(g => !g.IsSetup);
```

---

## Per-File Analysis

### PLang/Runtime2/Engine/Goals/Setup/this.cs
- **OBP:** Clean. Setup is a coordinator that delegates to goal.Load/RunAsync. It doesn't iterate other objects' internals.
- **Simplification:** Goals property is a LINQ query re-evaluated on each access. Fine — it's called once in RunAsync.
- **Readability:** Clear, well-documented. The try/finally pattern for context.Setup is correct.
- **Behavioral:** Findings 1 and 2 above.
- **Verdict: NEEDS WORK** — Record return type and error-on-record semantics.

### PLang/Runtime2/Engine/Goals/Goal/Steps/this.cs
- **OBP:** Clean. Steps now owns its iteration loop (moved from Goal.RunAsync). This is OBP rule 5 done right.
- **Simplification:** Clean.
- **Readability:** Clear flow. The setup check is well-placed and well-commented.
- **Behavioral:** Finding 1 above.
- **Verdict: NEEDS WORK** — record-before-error-check ordering.

### PLang/Runtime2/Engine/Goals/Goal/Methods.cs
- **OBP:** Clean. Goal.RunAsync now delegates to Steps.RunAsync instead of owning the loop. Textbook OBP rule 5.
- **Verdict: CLEAN**

### PLang/Runtime2/Engine/Goals/this.cs (EngineGoals)
- **OBP:** Clean.
- **Consistency:** Finding 3 above.
- **Verdict: NEEDS WORK** — minor, Count/All inconsistency.

### PLang/Runtime2/Engine/Context/PLangContext.cs
- **Clone family audit:** Setup is correctly preserved in Clone (line 205). CreateChild does NOT propagate Setup — this is correct because CreateChild creates a child execution context, and the child might not be in setup mode.
- **Verdict: CLEAN**

### PLang/Executor.cs (Run2 method)
- **OBP:** Clean. Run2 calls Setup.RunAsync correctly.
- **Behavioral note:** `plang p !test` skips setup entirely. This is correct — each test gets a fresh engine. Not a finding.
- **Verdict: CLEAN**

### PLang/Runtime2/Engine/Context/Actor.cs
- **OBP:** Clean. Actor owns its DataSource (lazy-created) and Context.
- **Verdict: CLEAN**

### PLang/Runtime2/Engine/DataSource/SqliteDataSource.cs
- **OBP:** Clean.
- **Behavioral:** EnsureTable is called on every operation (opens a connection just for CREATE TABLE IF NOT EXISTS). Not a bug, just wasteful on hot paths. Not flagging — performance optimization is out of scope.
- **Verdict: CLEAN**

### PLang/Runtime2/Engine/DataSource/SettingsData.cs
- **OBP:** Clean. Overrides GetChild correctly to intercept navigation.
- **Behavioral:** `.GetAwaiter().GetResult()` is safe because SqliteDataSource methods all return `Task.FromResult(...)` synchronously. Would deadlock if the backing store became truly async. Correct for current implementation.
- **Verdict: CLEAN**

### PLang/Runtime2/Engine/DataSource/IDataSource.cs
- **Verdict: CLEAN** — simple interface, well-documented.

### PLang/Runtime2/Engine/Errors/AskError.cs
- **Verdict: CLEAN**

### PLang/Runtime2/Engine/Errors/DataSourceError.cs
- **Deletion test:** ClassifyException (lines 35-53) has 4 branches, none tested. Could be replaced with a constant and no test fails.
- **Verdict: CLEAN** (the code is correct, just untested classification logic)

### PLang/Runtime2/actions/settings/get.cs, set.cs, remove.cs
- **OBP:** Clean. Handlers navigate to `Context.Engine.System.DataSource` correctly.
- **Verdict: CLEAN**

### PLang/Runtime2/actions/settings/types.cs
- **Verdict: CLEAN**

---

## Deletion Test Observations

1. **DataSourceError.ClassifyException** (lines 35-53): 4 classification branches untested. Could be deleted without test failure.
2. **Setup.Record metadata** (lines 76-83): goalPath, stepIndex, stepText, executedAt, error fields are never verified by any test. Only `Exists` is tested.
3. **AskError.FixSuggestion** (line 27): Never checked by any test.

These are test coverage gaps, not code bugs. They don't affect the verdict.

---

## Summary

| # | Severity | Finding | File |
|---|----------|---------|------|
| 1 | High | Failed setup steps permanently marked as executed | Steps/this.cs:39-43 |
| 2 | Medium | Setup.Record silently swallows errors | Setup/this.cs:72-86 |
| 3 | Low | Count/All include setup goals, Get excludes them | Goals/this.cs:183,188 |
