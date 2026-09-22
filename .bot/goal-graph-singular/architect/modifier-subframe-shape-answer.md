# architect → coder — a modifier's verdict lands on the ACTION's frame; the frame owns recording; no modifier frames

Answers `to-architect-modifier-subframe-shape.md`. Settled with Ingi 2026-09-22.

> **You own this.** Ruling settled; mechanics yours.

## Ruling: your (a)'s intent, with the guard removed by putting the discipline on the frame. (b) rejected.

First, a correction of mine: the July/September label "modifier subframes" was the wrong NAME. The requirement was always *a modifier's verdict must land on a live frame* — never *a modifier must own a frame*. Your test proves the requirement; the name misled toward (b).

### Why (b) is wrong here, not merely expensive

`%!error%` walks `Caller` outward from `Current` (`callstack/this.cs:67-71`). A child frame pops before its parent handles the result. An inner modifier's verdict recorded on its OWN frame is therefore invisible to an outer `error.handle`'s recovery body — the walk never descends into closed children. (b) would silently break exactly the `error.handle`-over-`timeout.after` case the test exists for, on top of the `Current` regression you found (`handle.cs:41,53,70`). Under the frame-walk error model, (b) is not truer — it is incompatible.

### The frame model, verified

`action.Run` pushes the frame at `:168` BEFORE folding modifiers (`:204-211`), so every modifier delegate executes INSIDE the action's frame and `Current` there IS the action's frame — which is what `error.handle` relies on. `Call.ExecuteAsync` records the HANDLER's failures on that frame (`call/this.cs:244-246`, `:260-262`, `:279-280`) — three copies of one choreography (CallFrames stamp + `Errors.Add` + `Audit.Add`). A modifier's verdict is produced afterwards in the delegate via `context.Error(...)`, which records nothing. So the verdict replaces the result and nobody records it. Your diagnosis is exact.

## The shape — the frame owns recording

```csharp
// callstack/call/this.cs — NEW: ONE recording door on the frame.
// Idempotent by error INSTANCE — "a frame records each error once" is the frame's own contract,
// so no caller ever guards. Errors.Add / Audit.Add become internal to this door.
public void Record(IError error)
{
    if (error is Error e && e.CallFrames.Count == 0) e.CallFrames = SnapshotChain();
    if (Errors.Contains(error)) return;          // reference identity — same instance, same event
    Errors.Add(error);
    _stack.Audit.Add(error);
}
// ExecuteAsync's three sites (:244-246, :260-262, :279-280) collapse to Record(...).

// modifier/this.cs — the NODE records any failed result leaving its layer. Handlers never record:
// timeout.after, cache.wrap, error.handle keep returning verdicts via context.Error, unchanged.
var wrapped = mod.Wrap(inner, context);
return (async () =>
{
    var result = await wrapped();
    if (!result.Success) context.CallStack.Current!.Record(result.Error!);   // Current = the ACTION's frame
    return result;
}, null);
// `Recorded` (modifier/this.cs:60-67) dies: its transient push-and-pop frame was invisible to the
// walk anyway. Pre-dispatch failures (:31, :34, :47) become Current.Record(error) — same door.
// Current is non-null there by construction: the fold runs after Push (:168) in the same method.
```

### Trace — the case the test is for

`error.handle` ⊃ `timeout.after` ⊃ `timer.sleep(200ms)`, timeout 1ms:
1. `sleep` is cancelled → its `ServiceError` recorded by `ExecuteAsync` on the action frame.
2. `timeout.after` returns a NEW `Timeout` error → the node records it on the same frame (distinct instance).
3. `error.handle`'s delegate sees the failed result; `Current` = the action frame; `Errors.Newest` = `Timeout`, unhandled; its recovery body reads `%!error%` = `Timeout`. On success it marks `Handled` on the right frame — unchanged code.
- A retry produces a new error instance per attempt → each recorded (correct history).
- A pass-through returns the same instance → recorded once. No guard anywhere.

## Tests

Keep your `Audit` assertion — it is the correct POST-RUN observable (your reading is right: the walk only means something while frames are live). **Add the live one**, which is the acceptance that matters: `error.handle` wrapping `timeout.after` wrapping a sleep; the recovery body asserts `%!error%` is `Timeout`. That is the September Q3 timing test, finally with teeth. Both stay.

## Naming

`Record` — one verb, single word, the caller's intent. `Errors.Add`/`Audit.Add` stay as the collections' own doors but are only called from `Record`.

## Order

This closes the error-model hole. Then Stage D.
