# security — runtime2-callstack — v1

## What this is

First security pass on `runtime2-callstack`. Subject under audit: the
callstack refactor delivered by `architect/v1` and `coder/v2` — Phases
1-11 (AsyncLocal tree, `Call.@this`, `Cause` linkage, `App.Errors`
AsyncLocal scope, Diff capture, Tag surface, CallChainRenderer).

## Verdict

**pass.** 5 findings, none critical or high.

| # | Severity | Category | Site |
|---|---|---|---|
| 1 | medium | race-condition | `stack.Audit.Add` + `app.Errors.All.Add` unsynchronized; races under Task.WhenAll on goal.call |
| 2 | low | race-condition | `Call.Tag()` — lazy dict alloc + indexer write unsynchronized; parallel foreach hazard |
| 3 | low | concurrency-pattern | `lock(Children)` / `lock(Diffs)` lock targets are publicly-visible Lists |
| 4 | low | resource-exhaustion | `Audit` / `All` grow unbounded for App lifetime |
| 5 | low | info-disclosure | `_root` never reassigned — stale across runs; `%!callStack.Root%` mis-leads |

Two pre-existing standing findings carried forward (not new on this
branch): `Error.cs FormatVerboseValue` doesn't strip [Sensitive], and
`AssertSnapshot` writes raw values to `AssertionError.Variables`.

## What was done

- Rebuilt `PlangConsole` from clean before audit (per tester/v1's
  stale-binary lesson). Build clean, 0 errors.
- Read `architect/v1/plan.md`, `coder/v2/summary.md`, `codeanalyzer/v3`
  PASS, `tester/v1/correction.md` (PLang+C# both 100%) for prior context.
- Mapped attack surface across 8 areas — call push/pop, audit
  accumulators, Variables events, tag action, Errors AsyncLocal scope,
  CallChainRenderer, flag parser, error snapshot disclosure.
- Verified: cycle detection (`MaxDepth=1000` + `ContainsGoal` via PrPath)
  enforced at both Push sites with `CallStackOverflowException → ServiceError`
  catch in App.Run and Goal.RunAsync.
- Verified: `App.Errors.@this._current` is instance-level AsyncLocal
  (not static) — multi-App test isolation is honest.
- Verified: `Restorer.Dispose` is idempotent (`_disposed` flag).
- Verified: cycle detection PrPath path uses `OrdinalIgnoreCase` and
  walks via `_current.Value` (the stack's own AsyncLocal Current).
- Verified: `Variables.OnSet` handler is exception-free under current
  shape — `lock(Diffs!) { Diffs.Add(...) }` only touches non-null state
  set in ctor; `CaptureBefore`'s DeepClone wraps in narrow try/catch.

## Code example

The medium finding, reduced — three call sites all feed the same
unsynchronized `Audit` list:

```csharp
// PLang/App/this.cs:414, 446, 460   and   Goals/Goal/this.cs:317
stack.Audit.Add(serviceErr);   // List<IError>.Add — not thread-safe
```

Architect plan §Phase 1 says AsyncLocal `_current` "forks naturally
on Task.WhenAll." The fork branches each get their own Current, yes —
but they share the one `stack.Audit`. Under concurrent failures both
branches race the same `List<T>.Add`. Fix is one `lock(_auditLock)` (or
`ConcurrentBag<IError>` if ordering is dispensable).

Same shape on `app.Errors.All`:

```csharp
// PLang/App/Errors/this.cs:42
public IDisposable Push(IError error) {
    var previous = _current.Value;
    _current.Value = error;
    All.Add(error);                       // List.Add — not thread-safe
    return new Restorer(_current, previous);
}
```

## Recommendation

`pass` — branch is mergeable. The medium concurrency finding is a
correctness gap that **should be addressed before parallel `goal.call`
ships**, since that is what makes it reproducible. It's a 3-line fix and
nothing about the surrounding refactor needs to change.

Suggest sending back to **coder** for the small concurrency fixes
(Findings 1 + 2 are simple), then **auditor** for the final pre-merge
pass. If Ingi prefers to merge as-is and queue the lock with parallel
goal.call work, that's a defensible call — verdict stands either way.

## Files written

- `.bot/runtime2-callstack/security-report.json`
- `.bot/runtime2-callstack/security/v1/plan.md`
- `.bot/runtime2-callstack/security/v1/verdict.json`
- `.bot/runtime2-callstack/security/v1/summary.md`
- `.bot/runtime2-callstack/security/summary.md`
