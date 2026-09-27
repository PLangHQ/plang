# Call-stack frames for goal, step and action: the architect's trace and ruling (decision 132)

Ingi, relayed by the coder (2026-09-27, before sleeping): "each step should have its own timer", through the call stack. He also said: "any questions ask architect, you can continue". The coder's sketch came in its (f) report. This trace was read at 0df80ea9a, after the sketch arrived but before ruling on it.

## What is there today

- **A frame is `callstack.call.@this`.** It holds one `Action` (`call/this.cs:36`) and is pushed in two places:
  - by `action.Start` (`goal/step/action/this.cs:183`, which spans the action's whole run);
  - by `goal.Enter`, as a **fake `goal.enter` action pinned to `Step[0]`** (`goal/this.cs:428-437`).
  
  A step pushes no frame. A goal-enter frame answers "not resumable" in `Capture` (`call/this.Snapshot.cs:23-40`).
- **The timing tier exists:** `StartedAt`, `CompletedAt`, and a stopwatch while `Timing` is on (`call/this.cs:126-130`). `Duration => CompletedAt - StartedAt` is **null until the frame is disposed** (`:87`, `:285-291`).
- **The goal and step in play are stamped on the context:**
  - `goal.Start` sets `context.Goal` and restores it in `finally` (`goal/this.cs:395-410`).
  - `step.Start` sets `context.Step = this` and **never restores it** (`goal/step/this.cs:115`).
  - `goal.call` runs the callee **in the same context** when no actor is given (`module/action/goal/call.cs:95,116`). **So after a step that calls a goal, the caller step's after-bindings see the callee's last step.** Debug's AFTER handler reads `context.Step` and `context.Goal` (`debug/this.cs:203,246`), so it prints the callee's step index under the caller's goal name.
  - `goal.Current(context) => context.Goal` (`goal/this.Item.cs:50`).
- **Readers of `frame.Action`:**
  - debug's stack lines (`debug/this.cs:228-233`);
  - `App.Run`'s provenance step (`app/this.cs:493`);
  - snapshot capture (`call/this.Snapshot.cs:25-38`, `callstack/this.Snapshot.cs:291-305`);
  - resume (`snapshot/this.Resume.cs:40`), which pushes an action frame with **no goal or step frame above it**. The synthetic actions `App.Run` builds have none either.
- **Test step timings** use their own `ConcurrentDictionary` of stopwatch timestamps, bound on the step type's `on.start` before and after (`test/this.cs:165-185`).

## Ruling

1. **Frames for goal, step and action: yes, including the goal frame.** The fake `goal.enter` action dies. It's a stand-in object for the goal, which is the smell decision 94 ruled out ("a stand-in item beside each is out").
2. **A frame holds one subject: the goal, step or action it runs.** It's named by a noun, not `Of` (a preposition). There is **no nullable `Action` beside it** and **no chain walk that tests `is goal` / `is step`**. That's the "is this X?" pattern, fixed by moving the behaviour onto the type.
3. **What a frame is asked, its subject answers as its own member:**
   - the goal and step in play: an action's is its `Step`/`Step.Goal`, a step's is itself/its `Goal`, a goal's is itself/none;
   - its stack line;
   - whether it's a resume point, and its position (only an action is one; goal and step say no, as goal-enter does today).
   
   **Why the subject and not the chain:** a resumed frame (`Resume.cs:40`) and an `App.Run` action have no goal or step frames above them. The subject still knows its place in the graph. The coder proposes the shared contract of the three runnables and shows it before coding.
4. **`context.Goal` and `context.Step` go.** Once frames exist they are stored twice. Everything in play derives from the current frame: `goal.Current`, debug's BEFORE and AFTER, and `App.Run`'s provenance. This also fixes the stale-step bug above, so pin it with a test: after a step that calls a goal, the step in play is the caller's.
5. **Timing reads the frame while it runs.** An after-binding runs *inside* the step's frame, so `Duration` is still null there. The frame answers its elapsed time whether in flight or done, as one member. Then:
   - debug's AFTER prints it;
   - test's step timings read it, so the dictionary and its bindings' stopwatch go;
   - the test run turns the timing tier on for its app, because a test always reports step times.
6. **`--debug` turns the timing tier on.** It's this run's value on `app.callstack.setting`, like the other CLI flags.
7. **Snapshot and resume:** capture stays action-positions-only, now through the subject's own answer. Resume pushes an action frame as today. Round-trip tests must cover a snapshot taken inside a step frame inside a goal frame.

## Comparison with the coder's sketch

- **Same:**
  - one frame per goal, step and action;
  - the goal frame included and `goal.enter` gone;
  - `Duration` from the existing tier;
  - `--debug` turning timing on;
  - test timings off the step frames;
  - snapshot and resume as the risk.
- **Mine adds:**
  - `context.Goal`/`context.Step` are stored twice, and `context.Step` is a live stale-step bug (the sketch kept them);
  - `Duration` is null inside the frame where the after-bindings run;
  - the subject answers goal and step, rather than a derived walk up the chain, because resumed and `App.Run` frames have no parents;
  - naming: `Of` → a noun, and no nullable `Action`.
- **The coder's adds:** the count of `frame.Action` readers (9 sites) and the order of work.
