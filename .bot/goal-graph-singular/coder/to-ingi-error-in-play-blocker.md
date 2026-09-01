# `%!error%` off the call stack — one thing is in the way

**Branch:** `goal-graph-singular` · **Commit:** `0c2c74fab` · Needs a yes before I write code.

You asked: *"why isn't the error on the current call on the stack? then we reach
for the error there?"* — and that is exactly right. The walk is built and green:

```csharp
// callstack/this.cs — the walk
public IError? Error
{
    get
    {
        for (var node = _current.Value; node != null; node = node.Caller)
            if (node.Error is { } error) return error;
        return null;
    }
}

// call/this.cs — each frame answers for itself
public IError? Error => Handled ? null : Errors.Newest;
```

Five tests pass: null when nothing failed · a live frame answers · an inner frame
shadows its caller and un-shadows on pop · `Handled` stops a frame answering while
the entry stays for the audit view.

**One test is red, and it is the whole ballgame.**

---

## The problem, as a trace

```
action.Run(context)
│
├─ execute = () => DispatchAsync(context)
├─ for each modifier:  execute = modifier.Wrap(execute)     ← error.handle wraps the OUTSIDE
│
└─ await execute()
   └─ handle.Wrap's lambda:
      ├─ var result = await next();          ← this is DispatchAsync
      │     ├─ call = CallStack.Push(this)         frame is born
      │     ├─ call.ExecuteAsync(...)              error recorded on THIS frame
      │     └─ dispose                             ✗ FRAME IS GONE
      │
      └─ RunRecovery(...)                    ← %!error% is read HERE
                                                the frame holding it popped one line ago
```

`error.handle` already knows this — it has a workaround:

```csharp
// handle.cs:91 — comment is verbatim from the file
// Failing Call comes from the error's CallFrames snapshot — App.Run pushed and
// popped the action's Call inside next(), so we can't read it from stack.Current
// anymore. CallFrames[0] is the failing Call itself (post-Push snapshot).
var erroredCall = result.Error is Error errWithFrames && errWithFrames.CallFrames.Count > 0
    ? errWithFrames.CallFrames[0] : null;
```

So the error is not "not on the stack". It is on a frame that **died one line too
early** — because the Call wraps *dispatch only*, while the modifiers wrap
*around dispatch*.

I did not reroute `%!error%`. It would have gone null everywhere:
`os/system` reads it 32 times and `Tests/App/CallStack/*` pins the shadowing.

---

## Three ways out. I recommend A.

### A — the action owns ONE frame, covering its modifiers too

Move the Push from `DispatchAsync` up into `action.Run`, outside the modifier fold:

```csharp
public async Task<data.@this> Run(actor.context.@this context)
{
    // An action owns ONE frame for its whole run — its dispatch AND the modifiers
    // wrapped around it. A modifier recovering from a failure then runs INSIDE the
    // frame that failed, which is where the error already is.
    var call = context.CallStack.Push(this, context.Variable);
    await using var _ = call;

    ... lifecycle → modifier fold → dispatch, unchanged ...
}
```

What falls out for free:

- `%!error%` during recovery = `CallStack.Current.Error`. No walk needed for the
  common case; the walk still covers "my caller failed".
- `Handled` means what it says: recovery succeeded **on this frame**. The
  `CallFrames[0]` reach-into-a-dead-object goes away —
  `erroredCall` becomes `context.CallStack.Current`.
- Nesting is automatic: the inner action's frame is current during inner recovery.
- `app.Error`, the `Push`/`Restorer` AsyncLocal scope, and `error.trail` all delete.

What changes, and you should say if you dislike it:

- **Retry shares one frame.** Today each retry attempt pushes a fresh Call. After A,
  three retries are three `Errors` entries on one frame. I think that is more correct —
  one action, one frame, three observations — but it changes what the call tree renders.
- The frame also spans the Before/After lifecycle events, not just dispatch.
- The `CallStackOverflow` catch moves from `DispatchAsync` to `Run`.

### B — copy the error onto the caller frame at unwind

Keeps dispatch as-is; on pop, an unrecovered error is added to `Caller.Errors`.

Why I don't like it: `Handled` is a frame-level bool, so once an error rides up to
the caller's frame, marking it handled there would also suppress a later, unrelated
failure of that caller. Fixing that means moving `Handled` onto the *error* — a
change to `IError` to work around a lifetime bug. And it stores one fact in N frames,
which is the thing we are trying to stop doing.

### C — re-enter the dead frame for the duration of recovery

```csharp
using (context.CallStack.Reenter(erroredCall))
    recoveryResult = await RunRecovery(actions, context);
```

This is the `app.Error.Push` scope again with a new name. It keeps the dead-object
reach alive as the mechanism. No.

---

## What I want

**Yes to A?** It is a real restructure of `action.Run` / `DispatchAsync` — maybe 40
lines across two files plus the deletions — so I am not starting it on my own read.

The one thing worth your opinion regardless of A/B/C: **is one frame per action
right, or one frame per dispatch attempt?** A makes it per action. Everything else
follows from that answer.

---

## Also fixed on the way (already committed)

Four `ErrorHandleTests` have been silently red since `dd7e28ef2` renamed
`error.handle.Actions` → `Action`. The tests still passed `("actions", ...)`, so the
parameter never bound, `hasRecovery` was always false, and **recovery never ran in
any of them**. The runtime was right; the tests were stale. Modules 67 → 64.

That is also how I found the gate: my new tests failed the same way, and the
existing ones were not there to catch it.
