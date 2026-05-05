# Coder v2 — Stage 2: CallStack Frames + Variables Time-Travel

Implements architect's `stage-2-callstack-frames-and-time-travel.md`. Builds on Stage 1's snapshot foundation.

## Stage 2 deliverables

| Architect deliverable | File | Notes |
|---|---|---|
| `Call.@this` ISnapshotted | `PLang/App/CallStack/Call/this.Snapshot.cs` (partial) | Capture: `(GoalPrPath, GoalHash, StepIndex, ActionIndex, ActionModule, ActionName, Id)`. Restore: NOT on Call.@this directly (it has private constructor). Returns a *RestoredFrame* surrogate via the parent CallStack restore path. |
| `App.CallStack.@this` ISnapshotted | `PLang/App/CallStack/this.Snapshot.cs` (partial) | Captures `Current.SnapshotChain()` reversed (outer first, bottom last). Restore reconstructs a chain of `RestoredFrame` records and exposes via `RestoredChain`/`BottomFrame`. |
| `EventsSince(t)` | `PLang/App/CallStack/this.cs` (extension) | `IEnumerable<Diff> EventsSince(DateTimeOffset t)` — walks Root tree (or current chain) collecting Diffs after the cutoff. |
| `Variables.SnapshotAt(error)` | `PLang/App/Variables/this.SnapshotAt.cs` (partial) | Asks CallStack for `EventsSince(error.CreatedUtc)`, reverse-applies each `Set` event onto a clone, returns the clone. |
| `CallbackGoalHashMismatch` | `PLang/App/Errors/CallbackGoalHashMismatch.cs` | Hard error for hash divergence. Goal-not-found uses a sibling type `CallbackGoalNotFound`. |
| `Flags.Diff` auto-flip | `PLang/App/Errors/this.cs` (Push) | When an error is pushed, the scope momentarily turns `Flags.Diff = true` on the App's CallStack and restores on Dispose. Requires Errors to have an App reference — new constructor parameter. |
| C# tests for [S2] stubs | `PLang.Tests/App/CallStackTests/*` + `VariablesTests/SnapshotAtErrorTests.cs` | ~16 tests across Call/CallStack/EventsSince/FlagsDiff/SnapshotAt. |

## Design choices

### `Call.@this` Snapshot is one-way

Call.@this has a private/internal constructor and lots of runtime-only state (Stopwatch, AsyncLocal, OnSet handler). Restoring INTO a Call would break its lifetime contract. Instead:

- `Call.Capture(snap)` writes the positional triple + lookup keys into the snapshot.
- There is **no static `Call.Restore`**. Instead, `App.CallStack.@this.Restore` reads the captured frames and produces `RestoredFrame` records that hold the resolved live `Action.@this` plus the resolved `Goal.@this`.

This matches the architect's intent ("the chain is a list, not a tree — completed children are dropped") and avoids forging a fake `Call.@this` that nobody can dispose properly.

```csharp
public sealed record RestoredFrame(
    Action.@this Action,
    Goal.@this Goal,
    int StepIndex,
    int ActionIndex,
    string Id);
```

`CallStack.@this.RestoredChain` is `IReadOnlyList<RestoredFrame>` populated by Restore; null on a fresh App. `BottomFrame` returns the last entry.

### `BottomFrame` semantics

The architect calls out `BottomFrame` as identifying the throwing Call. Two angles:

- On a **live** CallStack, `BottomFrame` is the deepest active frame — i.e., `Current` translated into the same `RestoredFrame` shape (so the API is uniform whether live or restored).
- On a **restored** CallStack, `BottomFrame` is the last entry in `RestoredChain`.

Implementation: `BottomFrame` returns either `RestoredChain[^1]` if a restore happened, else a `RestoredFrame` synthesized from `Current` if there is one. Returns null if neither.

### `EventsSince(t)` shape

```csharp
public IEnumerable<Diff> EventsSince(DateTimeOffset t)
```

Walks the Call tree (Root → Children recursively, plus current chain to capture in-flight Calls whose Diffs haven't been popped), filters each Call's `Diffs` by `Diff.At > t`. Returns enumerable of `Diff` ordered as encountered. The architect's note: "minimum viable shape — the consumer (Variables) just needs 'events with timestamp > T.'" Order isn't required to be globally-sorted; reverse-apply is order-independent for last-write-wins.

### `Variables.SnapshotAt(error)`

```csharp
public Variables.@this SnapshotAt(IError error)
{
    var clone = ShallowCloneAll();              // start from current state
    var events = _context!.App.Debug.CallStack.EventsSince(error.CreatedUtc);
    foreach (var diff in events.Reverse())       // reverse-apply newest→oldest
        clone.Set(diff.Name, diff.Before);
    return clone;
}
```

`ShallowCloneAll()` is a private helper that builds a fresh `Variables.@this` containing the same Data instances (clone-on-set semantics keep the original safe). The walk reverses each Set, undoing handler mutations.

### `Flags.Diff` auto-flip on Errors.Push

`Errors.@this.Push(error)` currently returns an IDisposable that pops the AsyncLocal current. Extend:

1. Capture current `app.Debug.CallStack.Flags.Diff` value.
2. Set it to `true` for the scope.
3. On Dispose, restore the prior value.

This requires `Errors.@this` to know about `App.@this`. Add `internal App.@this? App { get; set; }` property; App constructor sets it after construction.

The diff that gets captured between Push and Dispose is what `SnapshotAt(error)` reverse-applies.

## Test fixtures

Use the existing `CallStackTestHelpers.MakeAction` to build action/step/goal triples. Push frames onto a real CallStack, capture, then restore on a fresh App (with the same Goals registered) and assert the chain.

For hash-mismatch: load a goal with hash X, capture, then mutate one step's text, restore — expect `CallbackGoalHashMismatch`. The mutation invalidates the cached `_hash` field (Hash getter caches; mutating Text doesn't auto-invalidate). I'll either reset `_hash` via a test seam or build a new Goal instance with the mutated text and re-add to App.Goals.

## Workflow

1. Add `RestoredFrame` record.
2. Add `Call.this.Snapshot.cs` with Capture only.
3. Add `CallStack.this.Snapshot.cs` with Capture + Restore (returning RestoredFrames).
4. Add `EventsSince` + `BottomFrame` to CallStack.
5. Add `CallbackGoalHashMismatch` + `CallbackGoalNotFound` errors.
6. Make Errors aware of App; add Flags.Diff auto-flip in Push.
7. Add `Variables.SnapshotAt(error)`.
8. Wire CallStack into App.Snapshot()/Restore() (new section "CallStack").
9. Fill the 16 [S2] test bodies.
10. Verify all [S1] still green; PLang tests no regressions.
11. Commit.
