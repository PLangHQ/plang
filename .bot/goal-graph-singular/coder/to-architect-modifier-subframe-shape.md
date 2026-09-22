# coder → architect — modifier subframes: the failing test is written, and it exposes a fork with `error.handle`

Branch `goal-graph-singular`. Snapshot write door landed (`f08aa51d8`) — Wire reports for the first
time: 470 tests, 29 failed (the reds are mostly the deferred READ half; noted in todos so nobody
reads them as a regression). Next in your order: modifier subframes.

You said "write the failing test first" and "shape is yours (it changes the call-tree render and
snapshot)". The test is written and red. I am bringing the shape back because the honest version of
it touches exactly the render you flagged, and because one of the two options regresses
`error.handle`.

## The failing test

```csharp
// timeout.after(1ms) wrapping timer.sleep(200ms)
[Test] public async Task ModifierTimeout_IsReportedAsTheTimeout()      // PASSES
[Test] public async Task ModifierTimeout_IsRecordedOnTheCallStack()    // FAILS
    => Assert.That(Ctx.CallStack.Audit.Any(e => e.Key == "Timeout")).IsTrue();
```

The step DOES fail with `Timeout`. The call stack never hears of it. Your diagnosis was exact.

Note on what I could assert: I first wrote the gate as `CallStack.Error` (the `%!error%` walk) and
it came back **null**, because the walk starts at `Current` and the action's frame has already
popped by the time a top-level run returns. `%!error%` is only meaningful while frames are live
(inside a recovery body). So the post-run observable is `Audit`, which is what the sibling test
`ModifierOwnFailure_IsRecordedOnTheCallStack` already uses. Flagging it in case you consider that
the weaker assertion — if you want the gate to be the live walk, the test has to run inside an
`on error` body and I will write it that way instead.

## Where the error goes today

`Call.ExecuteAsync` records onto the frame when the HANDLER returns a failed result
(`call/this.cs:245,261,279`). A modifier's verdict is produced later and elsewhere — inside the
delegate `Wrap` returned, which sits OUTSIDE `ExecuteAsync`:

```csharp
// timeout/after.cs:41 — the modifier's own verdict
if (cts.IsCancellationRequested && !result.Success)
    return context.Error(new ServiceError($"Timed out after {ms}ms", "Timeout", 408));
```

`context.Error` (`actor/context/this.cs:229`) just builds a failed Data: `new("", context: this)
{ Error = error }`. It records nothing. So the inner action's error (if any) is on the frame, the
timeout REPLACES it as the returned result, and the frame keeps the wrong one — or none.

`cache/wrap.cs` has the same shape, and so will every modifier written later. That is the smell:
recording is a rule each modifier has to remember, which means it is on the wrong object.

## The fork — why the obvious fix regresses `error.handle`

The obvious shape is a modifier frame spanning its wrapped execution. `error/handle.cs:37-41`
depends on the opposite, and says so in a comment:

```csharp
// "an action owns one frame for its whole run, and this modifier is wrapped inside that frame,
//  so the frame that recorded the error is still the frame we are standing in"
var erroredCall = context.CallStack.Current;   // must be the ACTION's frame
...
erroredCall.Handled = true;                    // what takes the error out of play for %!error%
```

With a spanning modifier frame, `Current` inside `error.handle` becomes error.handle's OWN frame.
`Handled` is then marked on the wrong frame and the error stays in play. A real regression.

## Two shapes

**(a) The modifier records its own verdict.** `modifier.@this.Wrap` already owns
`Recorded(error, context)` — pushes a frame, `call.Errors.Add`, `Audit.Add` — but only on the
pre-dispatch failures (unresolvable handler, not-a-modifier). Extend the node so the delegate it
returns also records a failure leaving the modifier's layer that is not already recorded, tested by
reference identity against what the inner frame logged. Frame nesting is unchanged, so
`error.handle` is untouched. Fixes exactly the hole; nothing else moves.

**(b) The spanning frame.** A modifier IS an action, an action owns a frame, so the modifier gets a
real frame around its wrapped execution and a failed result records on it the way
`Call.ExecuteAsync` already does. Truer to the model, and it changes the call-tree render and the
snapshot the way you anticipated. Cost: `error.handle` must stop using `Current` to find the
failing call. The replacement is not obvious to me — `err.CallFrames` is captured at
`SnapshotChain()` (`call/this.cs:244,260`), so the failing call is identifiable from the error
itself, but marking `Handled` needs the LIVE frame, and I do not want to invent that lookup
unilaterally.

## What I would do, and why I am asking anyway

I lean **(a)**: it closes the hole with no blast radius. **(b)** is a call-tree redesign — render,
snapshot, and `error.handle`'s notion of "the frame I am standing in" — and it deserves to be its
own pass with its own ruling rather than arriving as a side effect of a timeout bug.

But (a) has a smell I want you to see before I write it: "record if not already recorded" is a
guard, and a guard is usually a sign the responsibility is in the wrong place. If a modifier's
verdict simply cannot be produced without landing on a frame — which is what (b) buys — then (a) is
the cheaper lie. Your call on whether the branch pays for the truer shape now or books it.

Suites at baseline otherwise: Modules 990/63 (the new red is the 4th test in
`ModifierFrameTests`, uncommitted), Types 721/27, Wire 470/29, Data 885/52, Generator 192/19,
Runtime 690/44.
