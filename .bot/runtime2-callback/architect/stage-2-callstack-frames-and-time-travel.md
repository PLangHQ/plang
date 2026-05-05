# Stage 2: CallStack Frames + Variables Time-Travel

**Goal:** Make `App.CallStack` snapshot/restore through per-`Call` framing (with Goal-stub projection and hash-match on resume), and pin `app.Variables.SnapshotAt(error)` so the throw-time projection lives on the right `@this`. After this stage, position and throw-time variables both round-trip — the two pieces error-callbacks need before callbacks themselves get implemented.
**Scope:** *Included* — `Call.@this` Capture/Restore (with Goal-stub), `App.CallStack` Capture/Restore, `App.CallStack` diff stream queryable by timestamp, `app.Variables.SnapshotAt(error)`, the `CallbackGoalHashMismatch` error type. *Excluded* — anything callback-shaped (Stage 4), anything signing/serializer (Stage 3), error-callback materialization (Stage 4).
**Deliverables:**
- `Call.@this` implements `ISnapshotted`. Capture emits `(Goal-stub: PrPath+Hash, StepIndex, ActionIndex, …)`; Restore resolves the Goal stub against `app.Goals`, hash-matches against the loaded goal.
- `App.CallStack.@this` implements `ISnapshotted`. Capture walks the active frame chain (live Call chain up to the throw point) and emits each via `Call.Capture`. Restore reconstructs the chain. The chain is a list, not a tree — completed children are dropped.
- `App.CallStack.@this` exposes a way to query its diff stream by timestamp — minimum viable surface is "give me variable mutation events with `timestamp > T`." This is the existing diff-event recording on each Call's audit trail, surfaced as a query method on CallStack.
- `app.Variables.SnapshotAt(Error error)` — returns a `Variables.@this` snapshot of the throw-time view. Internally asks `App.CallStack` for events-since-`error.throwTime` and reverse-applies to current state. Variables owns the *projection*; CallStack owns the *data*.
- `CallbackGoalHashMismatch` error type/code — referent-integrity error raised by `Call.Restore` when `live.Hash != stub.Hash`. Hard error, no silent fallback.
- C# tests: round-trip a CallStack with multiple frames; goal-not-found on Restore (path missing); hash-mismatch on Restore (goal redeployed); `SnapshotAt(error)` correctness with various mutation patterns; auto-flip of `Flags.Diff` on the error path.
**Dependencies:** Stage 1 (`ISnapshotted`, `Snapshot.@this`, `App.Snapshot()`/`App.Restore()`).

## Design

This stage is about two `@this` types and the seam between them.

### `Call.@this` Capture/Restore

A frame's identity *as a snapshot* is `(Goal-stub, StepIndex, ActionIndex)` plus whatever else the Call carries that's worth round-tripping. The Goal is emitted as a stub — `{ PrPath, Hash }` — not the full goal. Goal stays pure (no two-mode serialization on `Goal.@this` itself); the projection lives in `Call.Capture`. On Restore, `Call` resolves the stub against the live `app.Goals` registry, hash-matches.

Hash mismatch raises `CallbackGoalHashMismatch`. This error is the only thing in this stage that the callback layer (Stage 4) will reach for by name; everything else stays internal to CallStack.

`Goal.Hash` already exists at `PLang/App/Goals/Goal/this.cs:121` (SHA-256 of name + step text). No new hashing.

### `App.CallStack.@this` Capture/Restore

The captured chain is the *active* call chain at snapshot time — Caller chain up to and including the bottom (throwing) Call. Children-as-history of completed Calls are dropped — that's runtime-only audit, not state needed for resume. Timing tier and in-flight network state also drop.

The chain is ordered: outer Calls first, throwing Call last. The bottom frame is the resume point. Outer frames let unwinding-after-resume return control through the right Calls.

### Diff stream surfacing

CallStack already records variable mutation events as part of each Call's audit trail (when `Flags.Diff` is on). This stage exposes a query method on `App.CallStack.@this`:

```csharp
public IEnumerable<DiffEvent> EventsSince(DateTime t);
```

Minimum viable shape — the consumer (Variables) just needs "events with timestamp > T." Don't pre-build a richer query API; add when a real consumer needs it.

`Flags.Diff` auto-flips on for the duration of error processing. This is a small flag-management change in the error dispatch path. Off by default everywhere else; cost is paid only when an error is actually thrown.

### `app.Variables.SnapshotAt(error)`

Variables owns the projection method:

```csharp
// On App.Variables.@this
public Variables.@this SnapshotAt(Error error);
```

Body:

1. Take the current `Variables.@this` state.
2. Ask `App.CallStack` for `EventsSince(error.throwTime)`.
3. Reverse-apply each `Set` event (restore each variable's `Before` value).
4. Return a fresh `Variables.@this` snapshot.

The seam is clean: Variables knows *how to project itself*; CallStack knows *what happened in time*. Neither knows the other's internals. Variables asks a small question; CallStack answers it.

**Key invariants:**

- *The diff stream stays on CallStack.* Don't move events onto Variables — time-ordered data belongs on the type that owns time (Call → CallStack).
- *`SnapshotAt` is pure.* Same `(error, current state)` → same result. No side effects, no caching at this stage (caching is mentioned in `plan/variable-capture.md` as a later optimisation; not now).
- *The projection produces a `Variables.@this`, not a flat dict.* It's a `@this` instance; downstream consumers (`app.Snapshot()` in Stage 4) treat it as a normal subsystem snapshot.

### OBP smells to avoid

- *Don't put `SnapshotAt` on a helper class or a static utility.* Variables owns it; that's the whole point of this stage.
- *Don't put position-resolution on `Goal.@this`.* `Goal.Resolve(stub)` would mean Goal knows about stubs, which is the centralized-translator smell. The stub lives on `Call`; `Call.Restore` does the lookup against `app.Goals`.
- *Don't expose `live.Hash != stub.Hash` as a boolean flag bubbling up to callers.* `Call.Restore` raises the typed error itself; callers either succeed or get a hard exception. No "did the resume succeed?" question outside the type.

### Test shape

- **Round-trip:** populate a CallStack with N nested frames, Capture, Restore into a fresh App with the same goals loaded, assert chain equality + each frame's Goal/Step/Action.
- **Goal not found:** Capture, then delete the goal file from disk, Restore — expect `CallbackGoalHashMismatch` (or whatever referent-integrity error is named for the path-missing case; could be a sibling type).
- **Hash mismatch:** Capture, then mutate a step's prose so `Goal.Hash` changes, Restore — expect `CallbackGoalHashMismatch`.
- **`SnapshotAt(error)`:** set `%x%=1`, throw an error, mutate `%x%=2` post-throw, read `app.Variables.SnapshotAt(error)`, assert `x == 1`. Repeat with multiple mutations, with mutations of different vars, with no mutations.
- **Diff auto-flip:** start with `Flags.Diff` off, throw an error, verify the diff stream has events for the error window; verify it's off again after the error path completes.

These are tight C# tests pinning the contract for Stage 4 to consume.
