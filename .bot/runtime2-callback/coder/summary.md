# coder summary — runtime2-callback

## Version
v2 — Stage 2 (CallStack frames + Variables time-travel)

## What this is

Stage 2 of the architect's 4-stage callback design. After this stage, position (which goal/step/action a callback resumes at) and throw-time variables both round-trip — the two pieces error-callbacks need before callbacks themselves get implemented (Stage 4).

Builds on Stage 1's snapshot foundation.

## What was done

### New types
- `RestoredFrame` (`PLang/App/CallStack/RestoredFrame.cs`) — surrogate record holding the resolved live `Action` + `Goal` + positional triple. Restored chains are read-only positional context for callbacks; not pushable into AsyncLocal Current.
- `CallbackGoalNotFound`, `CallbackGoalHashMismatch` (`PLang/App/Errors/CallbackGoalErrors.cs`) — typed referent-integrity hard errors raised by `CallStack.Restore`.

### Subsystems extended
- `App.CallStack.Call.@this` — `Capture(snap)` writes `(GoalPrPath, GoalHash, StepIndex, ActionIndex, ActionModule, ActionName, Id)`. No `Restore` on Call directly (its constructor is internal and lifetime-coupled to the live AsyncLocal); restore happens on the parent CallStack and produces `RestoredFrame` records. Excludes timing tier and in-flight network state per the architect's drop bucket.
- `App.CallStack.@this` — implements `ISnapshotted`. Capture walks the active Caller chain (outer first, bottom last). Restore resolves each captured frame's Goal against `app.Goals.Get(prPath)`; hard-errors on missing goal or hash mismatch. Adds `RestoredChain` (read-only list), `BottomFrame` (last entry of restored chain, or live `Current` translated into a `RestoredFrame`), and `EventsSince(t)` (reads from both per-Call `Diffs` and a CallStack-level diff stream).
- `App.CallStack.@this` also gains a CallStack-level diff stream: `EnableDiffStream(vars)` / `DisableDiffStream()`. Subscribes to both `OnSet` and `OnCreate` (Variables.Set fires `OnCreate` for first-time names, `OnSet` only for replace; both are mutations the diff stream cares about). Per-Call `Call.this.cs` now also subscribes to `OnCreate` so its `Diffs` capture creates as well.
- `App.Variables.@this.SnapshotAt(error)` — clones the current Variables, then reverse-applies each `Diff` from `app.CallStack.EventsSince(error.CreatedUtc)` (newest→oldest) by writing the variable back to `Diff.Before`. Pure — same `(error, current state)` → same projection.
- `App.Errors.@this.Push(error)` — auto-flips `CallStack.Flags.Diff = true` for the scope AND wires the CallStack-level diff stream against `app.Variables` so handler-time mutations land on the stream regardless of when live Calls were pushed (per-Call subscription is decided at Push time and can't backfill). Restored on Dispose. Requires Errors to know the App — wired via `internal App { get; set; }` set in App's constructor.
- `App.@this.Snapshot()/Restore()` — gain `CallStack` section.

### Tests filled
21 of the test-designer Stage-2 stubs (16 explicit [S2] + 5 supporting):

- `CallSnapshotTests` × 8 — wire-shape, positional triple, Goal-stub resolution, hard-error coverage (not-found and hash-mismatch), purity, drop-bucket exclusions
- `CallStackSnapshotTests` × 4 — outer-to-bottom ordering, drop-completed-children, restore roundtrip, BottomFrame on live stack
- `EventsSinceTests` × 2 — events filtered by timestamp, empty when none
- `FlagsDiffAutoFlipTests` × 2 — auto-flip on Errors.Push, restore on Dispose
- `SnapshotAtErrorTests` × 5 — projection type, reverse-apply, post-error mutations excluded, no-op when no mutations, idempotency

C# baseline: 80 stubs failing (post-Stage-1) → 59. PLang tests: 192/181/0fail/11stale (unchanged).

## Code example

The seam between Variables (projection) and CallStack (time-ordered data):

```csharp
public @this SnapshotAt(IError error)
{
    var clone = ShallowCloneStore();
    var stack = _context?.App?.Debug?.CallStack;
    if (stack == null) return clone;

    var events = stack.EventsSince(error.CreatedUtc).Reverse();
    foreach (var diff in events)
        clone._variables[diff.Name] = new Data.@this(diff.Name, diff.Before) { Context = _context };
    return clone;
}
```

Variables knows *how* to project itself (reverse-apply); CallStack knows *what* happened in time (events). Neither knows the other's internals.

## Next

Stage 3 — Data lazy signing + per-mimetype serializers. Will turn the `[S3]` test-designer stubs green. Per Ingi's Q1 resolution, no Data constructor change — keep settable Context property; deal with null exceptions if/when they hit.
