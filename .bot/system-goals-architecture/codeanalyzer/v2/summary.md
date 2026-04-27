# Summary v2 — Re-review + Fresh Eyes

## What this is
Re-review of coder's v1 fixes plus fresh-eyes analysis of Executor.cs, error/check.cs, foreach.cs, file module, Data.Envelope, and app/run.cs.

## What was done
Verified all 5 v1 fixes (correct), then traced fix-introduced behavior through the Executor. Found that the STJ migration in CommandLineParser produces `JsonElement` values that Executor doesn't handle — breaking `--build={"files":"test.goal"}`. Also found the Newtonsoft branches in Executor are now dead code.

Fresh-eyes pass found two additional medium issues: error.check retry hardcodes User actor (wrong context for System-actor steps), and GoalCall.Parameters accumulates across foreach iterations because the same GoalCall object is reused and Parameters are appended, not replaced.

## Key findings

### Medium (3)
1. **Executor file filter broken** — STJ `JsonSerializer.Deserialize<Dictionary<string, object?>>` produces `JsonElement` values. Executor checks `is string` and `is IEnumerable` — both false for JsonElement. Fix: unwrap JsonElements in CommandLineParser.ParseValue using Data.UnwrapJsonElement pattern.

2. **error.check retry hardcodes User actor** (`error/check.cs:94`) — `var userContext = app.User.Context` should be `Context` (the caller's context). System-actor steps retry with wrong Variables.

3. **GoalCall parameter accumulation** — foreach.cs reuses the same GoalCall. GoalCall.LoadFromFile appends to Parameters list. After N iterations, list has N copies. Fix: clear Parameters before each call, or clone GoalCall per iteration.

## Code example — JsonElement regression
```csharp
// CommandLineParser.ParseValue returns Dictionary<string, object?>
// STJ puts JsonElement in the object? slots:
//   {"files": "test.goal"} → Dict { "files": JsonElement("test.goal") }
//
// Executor.cs:60-62:
if (filesVal is string singleFile)      // false — it's JsonElement
else if (filesVal is IEnumerable ...)   // false — JsonElement is struct
// → files filter silently ignored
```

## Verdict: NEEDS WORK
Recommend sending all 3 medium findings to the coder.
