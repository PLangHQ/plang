# Code Analysis v2 — system-goals-architecture

## Scope
1. Re-review of v1 fix commit (697ce1d6)
2. Fresh-eyes analysis of areas not covered in v1: Executor.cs, error/check.cs, foreach.cs, file provider, Data.Envelope, app/run.cs

---

## Fix-Introduced Finding: CommandLineParser STJ Migration Breaks Executor

### The Bug

`Executor.cs:56-73` extracts the `files` filter from `--build={"files":"test.goal"}`. After the CommandLineParser migration from Newtonsoft to System.Text.Json:

**Before (Newtonsoft):** `ParseValue` returned `JObject` → Executor line 66 matched the `is JObject` branch → `JObject.TryGetValue` extracted `filesToken` → worked.

**After (STJ):** `ParseValue` returns `Dictionary<string, object?>` via `JsonSerializer.Deserialize<Dictionary<string, object?>>()`. But STJ doesn't unwrap nested values in `object?` slots — they remain as `JsonElement`.

So at Executor line 57-64:
- `buildValue is IDictionary<string, object?>` → **true** ✓
- `filesVal is string` → **false** (it's `JsonElement`, not string)
- `filesVal is IEnumerable` → **false** (JsonElement is a struct)
- Falls through — files filter silently ignored

At Executor line 66:
- `buildValue is Newtonsoft.Json.Linq.JObject` → **false** (it's `Dictionary<string, object?>` now)
- Dead code — never reached

**Result:** `--build={"files":"test.goal"}` no longer filters to a single file. The builder scans everything. Silent behavior change.

### Fix

In `CommandLineParser.ParseValue`, the Object/Array case should unwrap JsonElements before returning. The `Data.@this.UnwrapJsonElement()` method already does exactly this. Either:

**Option A:** Use `Data.UnwrapJsonElement` in ParseValue to unwrap the deserialized dictionary.
**Option B:** In Executor, handle `JsonElement` for `filesVal`:
```csharp
if (filesVal is JsonElement je)
{
    if (je.ValueKind == JsonValueKind.String)
        engine.Building.Files.Add(new Path(je.GetString()!));
    else if (je.ValueKind == JsonValueKind.Array)
        foreach (var item in je.EnumerateArray())
            engine.Building.Files.Add(new Path(item.GetString()!));
}
```

Option A is cleaner — the fix belongs in CommandLineParser so all consumers get unwrapped values.

### Severity: **Medium** — builder runs but processes all goals instead of the filtered set

---

## Executor.cs — Dead Newtonsoft Branch

**Lines 66-73:** The `else if (buildValue is Newtonsoft.Json.Linq.JObject ...)` branch is dead code after the STJ migration. `CommandLineParser.ParseValue` now returns `Dictionary<string, object?>`, never `JObject`.

**Fix:** Delete lines 66-73 after fixing the JsonElement unwrapping above.

### Severity: **Minor** — dead code, no runtime impact

---

## Fresh-Eyes Findings

### error/check.cs: Retry hardcodes User actor context

**Line 94:** `var userContext = app.User.Context;` — The retry always runs on the User actor's context, regardless of which actor was executing the step. If a System-context step fails and retries, the retry runs on User context — different Variables, different identity.

**Fix:** Use the caller's context, not `app.User.Context`:
```csharp
// Line 94: should be
var retryContext = Context; // the execution context, not hardcoded User
```

### Severity: **Medium** — System actor steps retry with wrong context

---

### error/check.cs: Duplicate error handling logic

**Observation:** `Step.HandleErrorAsync` (Step/this.cs:182-241) and `error/check.cs` both implement the full retry + error goal + ignore + order logic. The C# path runs for built .pr files (Step.RunAsync line 140). The PLang module exists for the PLang-based runtime (system/run.pr).

Both have identical semantics but independent implementations. If one is updated (e.g., new error handling feature), the other must be updated too.

**Not a bug — but a drift risk.** Noting for architectural awareness.

### Severity: **Low** — architectural observation

---

### Executor.cs line 77: String-constructed path without AdjustPathToOs()

```csharp
var prPath = goalFile.Replace(".goal", ".pr", StringComparison.OrdinalIgnoreCase);
if (!prPath.StartsWith(".build"))
    prPath = ".build/" + prPath;
engine.System.Context.Variables.Set("goalFile", "/" + prPath.ToLowerInvariant());
```

The `".build/" + prPath` concatenation uses `/` which is correct for the internal PLang path convention (forward-slash). But `goalFile` comes from `CommandLineParser.GoalName` which could contain backslashes on Windows. The `.Replace(".goal", ".pr")` preserves backslashes. Then `".build/" + "Sub\\Dir\\Test.pr"` creates a mixed-separator path.

**Fix:** Apply `.AdjustPathToOs()` or normalize to forward-slash before concatenation.

### Severity: **Low** — only matters if someone passes `Sub\Dir\Test.goal` on Windows CLI

---

### foreach.cs: GoalCall reuse across iterations

**Line 36:** `await app.RunGoalAsync(GoalName, Context, ...)` — The same `GoalCall` object is passed for every iteration. GoalCall is mutated by `LoadFromFile` (parameters added at line 130 of GoalCall.cs). On second iteration, the GoalCall already has parameters from the first iteration, and new ones get *added* (not replaced).

If the GoalCall has Parameters, each iteration appends to the same list. After N iterations, the Parameters list has N copies of each parameter.

**Check:** Does the source generator clone GoalCall per iteration? If not, this is a parameter accumulation bug.

### Severity: **Medium** — parameter accumulation across foreach iterations

---

## v1 Fix Re-Review

### PrPath fix ✓
`AdjustPathToOs()` applied. Default changed to `/`. Tests updated. **Correct.**

### GoalCall parameter injection ✓
Moved after successful load. **Correct.** But see foreach finding above — GoalCall.Parameters is a mutable List that accumulates across calls.

### CommandLineParser STJ migration ⚠️
Migration correct for the parser itself, but introduced a regression in Executor.cs where `JsonElement` values aren't handled. See main finding above.

### Bare catch narrowing ✓
All 5 narrowed to specific exception types. **Correct.**

### Step.RunAsync catch filter ✓
Added OOM/SOE exclusion. NRE kept deliberately. **Acceptable.**

---

## Summary of Findings

| # | File | Severity | Finding |
|---|------|----------|---------|
| 1 | Executor.cs:56-64 | **Medium** | STJ migration broke files filter — JsonElement not unwrapped |
| 2 | error/check.cs:94 | **Medium** | Retry hardcodes User actor — should use caller's context |
| 3 | foreach.cs:36 + GoalCall.cs:130 | **Medium** | GoalCall.Parameters accumulates across iterations |
| 4 | Executor.cs:66-73 | Minor | Dead Newtonsoft branch after STJ migration |
| 5 | Executor.cs:77-80 | Low | String-constructed path without normalization |
| 6 | Step/error dual impl | Low | error.check and Step.HandleErrorAsync can drift |

**Critical: 0 | Medium: 3 | Minor: 1 | Low: 2**
