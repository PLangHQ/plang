# Plan v3 — Fix Event Tests (7 failures)

## Root Cause
Events ARE found and fired — confirmed with debug logging. The issue is **MemoryStack context mismatch**:

1. Test runs via `runtime.run` → RunGoal → RunStep. The execution context is from `runtime.run`'s caller (system context from test.pr).
2. `event.on` registers the handler to run on `targetActor.Context` (user actor).
3. When the event fires, TrackStep runs on the **user** actor's MemoryStack.
4. But `%stepCount%` was set on the **system** context's MemoryStack (via RunGoal).
5. TrackStep's `set %stepCount% = %stepCount% + 1` reads/writes the user MemoryStack — doesn't see or affect the test's `%stepCount%`.

## Fix
The event handler's `targetActor.Context` should point to the execution context, not the actor's original context. When `engine.execute` runs a step on the user actor, the event handler should run on the same context.

In `event.on` (line 50-51):
```csharp
Func<PLangContext, Task<Data>> handler = async ctx =>
    await ctx.Engine!.RunGoalAsync(GoalToCall, targetActor.Context, ctx.CancellationToken);
```

The `ctx` parameter is the context passed to the event handler when it fires. The handler ignores it and uses `targetActor.Context` instead. Fix: use `ctx` (the calling context) so the event goal runs on the same MemoryStack as the step that triggered it.

```csharp
Func<PLangContext, Task<Data>> handler = async ctx =>
    await ctx.Engine!.RunGoalAsync(GoalToCall, ctx, ctx.CancellationToken);
```

## Risk
- This changes event execution context for ALL events, not just test events.
- System events (debug, etc.) may break if they depend on running on the actor's context.
- The `Actor` parameter on event.on exists for explicit actor targeting — if set, should still use that actor's context.

## Implementation
1. Fix `event.on` to use `ctx` when no explicit Actor is set
2. When Actor IS set explicitly, keep using `targetActor.Context`
3. Build + test all 7 event tests
4. Verify debug events still work (plang --debug)

## Files
- `PLang/Runtime2/modules/event/on.cs` — single line change in handler lambda
