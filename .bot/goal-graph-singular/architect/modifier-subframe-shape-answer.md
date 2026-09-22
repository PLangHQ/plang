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

---

## ADDENDUM (2026-09-22, after coder's implementation — landed as `54ab44cd5`)

Coder implemented the ruling and reported back over the session socket. Three corrections/rulings:

1. **"Current is non-null by construction" holds in PRODUCTION only** (the fold runs inside `Run`'s Push). Tests that drive `modifier.Wrap` standalone have no frame — coder made the missing frame an explicit `InvalidOperationException` naming the modifier (not an NRE) and gave those tests a frame (`PLang.Tests/Shared/TestFrame.cs`). Accepted; the invariant above is amended to "in production; a standalone driver must provide a frame." Also accepted: `Record`'s idempotence uses an explicit `ReferenceEquals` scan, not `Contains` — a future `IError` equality override must not silently turn instance identity into value equality.

2. **The catalog Positions are inverted — the trace was right, the numbers were wrong.** Today: `timeout.after = 1` (outermost), `cache.wrap = 2`, `error.handle = 3` (innermost); `Nest` sorts ascending and the fold puts index 0 outermost. So every built `.pr` nests `timeout ⊃ cache ⊃ error.handle ⊃ action` — `on error` sits INSIDE the deadline and can never see a `Timeout` verdict; the deadline wraps the handler and discards its recovery. **Ruling — a modifier's Position follows what it bounds:**
   - `timeout.after` bounds an ATTEMPT → innermost. Each retry gets a fresh deadline; `on error` catches `Timeout` like any error.
   - `error.handle` bounds the ATTEMPTS → outermost of the three.
   - `cache.wrap` bounds the outcome of the REAL work → between: a hit skips the work and the deadline; a miss runs `timeout ⊃ action` and caches the success; a recovery result is never cached (recovery happens outside the cache).

   New Positions: **`error.handle = 1`, `cache.wrap = 2`, `timeout.after = 3`** — the exact reverse of today's pair (someone read "Order" as priority rather than nesting). Coder's hand-built acceptance test (error.handle at index 0) is then exactly what the builder produces. Add a catalog test pinning the three Positions, and a `Nest` test asserting the resulting order, so this can't silently invert again.

3. **`timeout/after.cs` — two rulings on the adjacent finds:**
   - `:41` `if (cts.IsCancellationRequested && !result.Success)` — drop the `&& !result.Success`. **The deadline is the verdict**: if the CTS fired before `next()` returned, the result is `Timeout` regardless of what the inner returned. A success arriving after the deadline is late, and late is the thing a deadline forbids. (Parent cancellation is already distinguished at `:36`.) This is also the root of the flaky green coder hit.
   - `:22` `?? 0` for a non-number `Ms` — no default, fail loud. `Ms` is a typed `Data<number>` slot; a value that isn't a number cannot be an "expire immediately" — the `Peek` seam must read a resolved number or throw naming the parameter.

Sweep honesty note (coder): any suite number taken before `4d625acc8` may be a partial run — a 15s whole-suite cap truncated Modules and an unguarded grep under `set -e` killed the sweep at Wire. Treat pre-`4d625acc8` numbers as unknown.

## ADDENDUM 2 (2026-09-22, coder landed `5c1d55b16`) — the modifier parameter-read rule

All three landed. One deviation on 3(b), accepted because its root cause corrects the doc: a `.pr`-loaded parameter is LAZY by design (values lift on the typed ask), so a sync `Peek` at `Wrap` time sees the wire form, not a number — the `?? 0` was papering over a sync/async mismatch, not an author error, and a throw there fires on correct programs (it did: `AfterActionPayloadTests` went red through a real `.pr` round-trip). Coder moved the read inside the delegate: `int ms = (await Ms.Value()).ToInt32();`.

**Rule (confirmed):** a modifier reads its parameters INSIDE the delegate it returns, through the typed ask; `IModifier.Wrap` stays sync and pure — it composes delegates and reads nothing.

Sweep: `cache/wrap.cs` is clean (no Peek). `error/handle.cs:112-114` `MatchesError` Peeks `StatusCode`/`Key`/`Message` for the filter — same bug, worse consequence (a `.pr`-built `on error key="X"` compares the unresolved wire form and silently matches wrong). `MatchesError` becomes async and reads through the typed ask. The modifier node's comment at `modifier/this.cs:32` ("Resolve populates the handler's params so IModifier.Wrap reads real values") states the false invariant — rewrite: Resolve wires the slots; values lift on the typed ask inside the delegate.

Also accepted: `ModifierRegistryTests.Order_LivesOnTheModifierType` corrected in place (it pinned the inverted numbers) rather than duplicated; `Nest` order pinned in `ModifierPositionTests`; the flaky `PathKind` pair diffed by name.
