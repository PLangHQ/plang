# v1 — Architect: CallStack causal frame model

## What this is

A redesign of `App/CallStack/` to (a) move ownership from `Actor.Context` to `App.Debug`, (b) make it off-by-default with auto-materialization on error, (c) introduce the causal `Cause` link distinct from `Caller`, (d) capture variable diffs through a new `Variables.Events` surface, (e) replace today's parallel `Context.Error` with a stack-walk for `%!error%`, and (f) make the data shape render-agnostic so error traces, flamegraphs, and causal graphs all project from the same Call tree.

The motivating gaps in the current code:
- Frames push/pop but `frame.Errors` is never populated, `CallStack.Errors` is empty.
- `%!error%` reads from a parallel `Context.Error` (two sources of truth).
- `IsEnabled=false` path is incoherent (Push doesn't push but Pop pops anyway).
- No causal/spawned-by link — needed for events, scheduled goals, and the upcoming Callback work.

## What was done

Multi-turn design discussion with Ingi captured the following settled decisions:
- Causal model with `Caller` + `Cause` on each Call.
- Recovery scope **Option B**: synthetic recovery frame pushed by `error.handle.Wrap`.
- Sibling-group split **Option X**: collapse consecutive identical siblings, break on goal-change or error.
- Frame renamed to **`Call`** (`App.CallStack.Call.@this`); collection is `CallStack.Calls`.
- `--debug={callstack:true}` enables live capture; off by default; reconstruct from `Action.Step.Goal.Parent` walk on error otherwise.
- Tags are written by both C# handlers and a new PLang `tag` action; capture only when a frame exists.
- Variable diffs via `Variables.Events.OnSet` (new OBP child of Variables); scalar-by-default to avoid the OOM scenario; `deepDiff:true` opt-in.
- ErrorChain (already exists) stays as-is; renderer integration deferred to a follow-on.
- Callback is out of scope — context recorded in `Documentation/Runtime2/todos.md` so future Callback design knows what callstack provides.
- Frame name conflict with C# `System.Action` and existing `App.Goals.Goal.Steps.Step.Actions.Action.@this` resolved by going with `Call` (parent name `CallStack` matches semantically; conflict with `App.modules.goal.Call` is namespace-only, no type clash).

The plan at `plan.md` lays out 10 phases with file-level edits, a risk register, and a test matrix (10 PLang tests + 4 C# test suites). Out-of-scope items explicitly listed: Callback, time-travel resume, flamegraph renderer, causal-graph renderer, LLM-rejection rendering, cycle detection enforcement.

## Code example — the new App.Run shape

```csharp
var callStack = App.Debug.CallStack;
var pushed = (callStack?.IsEnabled ?? false) ? callStack.Push(action) : null;

try
{
    var result = await handler!.ExecuteAsync(action, context);
    if (!result.Success && pushed != null)
    {
        pushed.Errors.Add(result.Error!);
        callStack!.Errors.Add(result.Error!);
    }
    return result;
}
catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
{
    var frames = pushed != null
        ? callStack!.GetCalls()
        : CallStack.Reconstruct(action);   // walk Action.Step.Goal.Parent

    var serviceErr = new ServiceError(ex.Message, step, frames, "ServiceError", 400) { Exception = ex };
    if (pushed != null) { pushed.Errors.Add(serviceErr); callStack!.Errors.Add(serviceErr); }
    return Data.@this.FromError(serviceErr);
}
finally
{
    if (pushed != null)
    {
        pushed.SnapshotVariables(context.Variables);
        await callStack!.PopAsync();
    }
    // restore context anchors
}
```

The change in shape: tracking `pushed` so the IsEnabled=false path doesn't try to pop a non-existent frame, and so `Reconstruct` only runs when there's no live stack to use.

## What's still open / next

- **test-designer** runs next to spec the test suites for Phase 9.
- **coder** then implements per the phase order in `plan.md`.
- One implementation-time decision deferred: synthetic recovery frame's `Action` reference — reuse recovery goal's first action vs. introduce a sentinel `RecoveryAction` type. Decide during coding when the OBP shape becomes concrete.

No blockers; design is complete.
