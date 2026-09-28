# Frames for goal, step and action (decision 132; shape per Ingi, decision 135 + follow-up)

Ruling: `.bot/app-systems/architect/plan/frames-trace.md`. The architect's `IPlace`/`place` proposal was replaced by
Ingi: the frame simply holds what it runs, and the stack answers the goal and step in play.

## Shape

```csharp
frame.Goal / frame.Step / frame.Action     // one action per frame; a step's frame has no action
stack.Push(goal) / Push(step) / Push(action, variables)
context.CallStack.Goal => Current?.Goal     // %!goal%, goal.Current, Error, debug, file paths, fluid
context.CallStack.Step => Current?.Step     // %!step%, cache key, App.Run provenance, modifier location
frame.ToString()   // stack line: "Start.set (step 3) in /Start.goal"
frame.IsResumable  // an action its step holds
frame.Duration     // stopwatch elapsed — live in flight, fixed at pop, null with Timing off
```

- Frame loses `Synthetic` and `Start` (dispatch moved into `action.DispatchAsync`).
- `MaxDepth` 1500 (a goal level is three frames). `callstack.Overflow(ex, goal, step)` builds the depth error once.
- `goal.Start` / `step.Start` push their frame around before-bindings, body, after-bindings; `step.Resume` pushes the
  step's frame; the fake `goal.enter` action is gone.
- `context.Goal` / `context.Step` / `AnchorScope` deleted.
- Snapshot: `Capture` writes only resumable frames; live `BottomFrame` walks out to the nearest action frame.
- Timing: debug AFTER prints `Current.Duration`; test `Time()` turns Timing on in its App and reads the frame;
  `Executor` turns Timing on under `--debug` (before `--callstack`, which still wins).

## Pins
- `FramesTests`: step in play after a step that calls a goal is the caller's; nothing in play after the goal ends;
  `Duration` non-null in a step's after-binding; stack lines.
- `CallStackSnapshotTests`: round trip inside goal/step frames; `BottomFrame` from a step frame.
