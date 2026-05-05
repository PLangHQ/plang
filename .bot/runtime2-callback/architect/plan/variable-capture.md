# Variable capture semantics for error-retry

`%!error.callback%` is a **synthetic, lazily materialised** PLang property. It has no cost until read, and its result is a pure function of `(error object, current state)` — same inputs, same callback.

## Where the projection method lives

The throw-time projection is a method on `App.Variables`:

```csharp
public Variables.@this SnapshotAt(Error error)
```

OBP rule: the type that owns the *result* owns the method. A `Variables` snapshot is the result; Variables owns the method. The diff stream itself stays on `App.CallStack` (each Call records the mutations that happened during it — that's the natural home for time-ordered events). `SnapshotAt` is the collaboration point: Variables asks CallStack for events-since-T, reverse-applies them to its own current state.

The seam: Variables knows *how to project itself*; CallStack knows *what happened in time*. Neither type knows the other's internals.

## Throw-time view via Diff reverse-apply

`App.Variables.SnapshotAt(error)` computes Variables-at-throw-time by:

1. Take current `App.Variables.@this`.
2. Ask `App.CallStack` for the diff events with `timestamp > error.throwTime`.
3. Reverse-apply each `Set` (restore each variable's `Before` value).
4. Return a new `Variables.@this` snapshot.

This means:

- Error handler mutations (`- set %name% = "ble"` inside the handler) appear in the live App's Variables but are *reversed* in the callback's view.
- The developer writing the error handler never has to reason about variable name collisions with the failed code.

## Diff is required on the error path

When `Flags.Diff` is off and an error occurs, the runtime auto-flips it for the duration of error processing. Cost is paid only on the error path, which is rare. No conditional code path on the consumer side.

## Providers don't get the rich treatment

Providers do **not** have diff tracking. The callback captures their registry-layer selection state at materialisation time. Convention: error handlers should not mutate provider selections. If they do, the callback reflects the post-handler selection — the runtime will not catch this. Honest asymmetry: vars get rich treatment because we have rich tooling for them; providers get the pragmatic one.

## Idempotent materialization

`%!error.callback%` (which lives at `app.Errors.Current.Callback` — see [callback-schema.md](callback-schema.md#lazy-materialization-of-errorcallback)) is a pure function of its inputs:

- The `%!error%` object (which doesn't change once thrown — it's the trigger that froze the throw-time snapshot).
- The current App state at read time (the full subtree that `app.Snapshot()` walks).

Two reads with no intervening state change produce identical callbacks. This means the runtime is free to **cache** the materialised callback by error identity and invalidate when state changes — a worthwhile optimisation if a handler reads `%!error.callback%` multiple times (e.g. once to log, once to write).

The implementation can start without caching and add it when measurement justifies it. The contract is the determinism, not the cache.
