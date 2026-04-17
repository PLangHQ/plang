# v1 Summary — Action Modifiers Security Audit

## What this is

Security audit of the action-modifiers feature: `IModifier` fold in
`Action.RunAsync`, three modifier handlers (`timeout.after`, `cache.wrap`,
`error.handle`), `timer.sleep`, `Actions.GroupModifiers` builder path, and
the deletion of legacy step-level `OnError`/`Cache`/`Timeout`. The feature
replaces four special-cased code paths with one composable primitive, so
the attack surface shrinks on paper — but adds a new dispatch path and a
shared-state mutation opportunity in the error goal plumbing.

Baseline: `runtime2`. 36 commits on branch. Tester v4 PASS, codeanalyzer v1
PASS.

## What was done

Two-phase audit (blue team map + red team probes) across seven attack
surface areas. Verified:

- **Modifier fold**: `WrapAround` rejects non-`IModifier` handlers; params
  re-resolve per execution via source-generator set flags.
- **timeout.after**: parent token is captured before push (correct); when-
  filter distinguishes timeout from parent cancellation; `finally` pops
  stack; `using` disposes CTS.
- **cache.wrap**: MemoryCache is thread-safe; default key uses trusted
  goal path; size-bound absent but user-sovereign.
- **error.handle**: `MatchesError` handles null filters correctly;
  `MaxDepth=1000` on CallStack prevents stack-blow via error-goal
  recursion; retry delay filter `> 0` avoids Task.Delay negative crash;
  `IgnoreError` is correctly the final fallback after retry and goal.
- **GroupModifiers**: reflection-based `IsModifier`; no string parsing on
  step text; sorted by `[Modifier(Order)]`.
- **Legacy removal**: all call sites gone; .pr will be rebuilt.

Found 4 issues. Verdict: **PASS** (no critical/high open).

- **F1 Medium** — `error.handle.CallErrorGoal` mutates `goalCall.Parameters`
  and `goalCall.Action` on the shared deserialized GoalCall singleton. The
  existing `DoesNotMutateOriginalParameters` test passes because the
  original `List<Data>` reference is preserved (LINQ builds a new list) —
  but the test does not check that the shared GoalCall object itself
  isn't mutated (it is). Under concurrent execution (events, or planned
  `async.fire`/`parallel.set`, or a future request channel), two
  invocations race on `goalCall.Parameters`, and `!error` from one
  invocation leaks into the other's error goal. Not reachable on today's
  runtime; latent defect.
- **F2 Low** — `timeout.after` / `timer.sleep` don't validate negative
  `Ms`; throws through the modifier boundary instead of returning a typed
  error.
- **F3 Low** — `error.handle.Retry` unbounded `RetryCount`; DoS if bound
  to untrusted input.
- **F4 Low** — `_cancellationStack` is `Stack<T>`, not thread-safe; will
  corrupt under the planned parallel modifiers.

No issues found in: the modifier fold itself, the when-filter TOCTOU,
cache key derivation, builder grouping, legacy removal.

Files reviewed (no edits this session):
- `PLang/App/modules/IModifier.cs`
- `PLang/App/modules/ModifierAttribute.cs`
- `PLang/App/modules/timeout/after.cs`
- `PLang/App/modules/cache/wrap.cs`
- `PLang/App/modules/error/handle.cs`
- `PLang/App/modules/timer/sleep.cs`
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs`
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/Modifiers/this.cs`
- `PLang/App/Goals/Goal/Steps/Step/Actions/this.cs`
- `PLang/App/Goals/Goal/GoalCall.cs`
- `PLang/App/Actor/Context/this.cs`
- `PLang/App/Cache/MemoryStepCache.cs`
- `PLang/App/Modules/this.cs` (IsModifier / GetModifierOrder)
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` (GoalsSave)
- `PLang.Generators/LazyParamsGenerator.cs` (to confirm fresh per-call
  resolution)

## Code example

The Finding 1 pattern — shared-state mutation on a cached `Data<GoalCall>.Value`:

```csharp
// error.handle.CallErrorGoal, line 109-126
var parameters = (goalCall.Parameters ?? new())
    .Where(p => p.Name != "!error")
    .Append(new Data.@this("!error", failedResult.Error))
    .ToList();
goalCall.Parameters = parameters;           // <-- mutates shared GoalCall
...
goalCall.Action ??= context.Step?.Actions.FirstOrDefault();  // <-- same
return await context.App!.RunGoalAsync(goalCall, context);
```

Fix: don't mutate; clone.

```csharp
var parameters = (goalCall.Parameters ?? new())
    .Where(p => p.Name != "!error")
    .Append(new Data.@this("!error", failedResult.Error))
    .ToList();

var call = new GoalCall
{
    Name = goalCall.Name,
    Description = goalCall.Description,
    Parallel = goalCall.Parallel,
    Parameters = parameters,
    PrPath = goalCall.PrPath,
    Action = goalCall.Action ?? context.Step?.Actions.FirstOrDefault(),
};
return await context.App!.RunGoalAsync(call, context);
```

## Status

- Report at `.bot/runtime2-action-modifiers/security-report.json`.
- Verdict: **PASS**.
- Next: auditor. Recommend surfacing F1 to coder as a small follow-up
  before merge; F2–F4 can go to hardening backlog.

## Not done

- Did not run the test suite (tester already PASS).
- Did not write a repro test for F1 — would require a forced concurrent
  dispatch path that doesn't exist today. The finding stands on code
  reading alone.
