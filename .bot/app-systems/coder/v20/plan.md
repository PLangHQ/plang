# v20 — task, stage 2: wait, cancel, the token, `%task.list%` (shape)

Plan: `test/plan/task/start.md` (a736f3883). Stage 1 accepted (c0d9cbc6b / b73906e26).

## A. The token reaches the run — the context's cancellation becomes per async flow

Today `context.CancellationToken` reads a `Stack<CancellationTokenSource>` on the context
(`actor/context/this.cs:64–77`, pushed by `on.timeout` and `test`). Every flow on an actor shares its context, so a
stack push in one flow is seen by all: a task pushing its token there would cancel its caller's steps too (and an
`on.timeout` in one parallel flow already leaks into its siblings today).

Shape: the stack becomes **per async flow** — `AsyncLocal<ImmutableStack<CancellationTokenSource>>`, the way the call
frames are (`variable/call/list`). `PushCancellation`/`PopCancellation` keep their signatures. The task's run pushes its
own token (`task.list.Start` → `run(token)`; goal.call's `Run` pushes it for the goal): `goal.Start` (`goal/this.cs:371`,
checked per goal) and `timer.sleep` (`Task.Delay(…, Context.CancellationToken)`) already read the context's token, so a
cancel stops the run in its sleep or before its next goal. Steps inside one goal: I'll check where the step loop reads
the token and make it read per step if it doesn't.

## B. The task: `Wait` and `Cancel`

```csharp
public Task<data.@this> Wait();      // as stage 1; a cancelled run answers Cancelled (499, key "Cancelled")
public Task<data.@this> Cancel();    // ended: its result (and it counts as asked: no report); running: the token
                                     // cancelled, the answer null — the run ends Cancelled, never reported
```
A cancel is asked-for, so a cancelled run is never reported as an unwatched failure.

## C. `task.wait` — one door each; the open point is "one or many"

`wait for %a%` answers a's result; `wait for %a%, %b%`, `wait for %tasks%`, `wait for %task.list%` answer a list of
results in order. One slot answering two shapes is a fork on what it was given. Options:
- **(1) Two slots, each typed:** `Task` (`task`) answers the result; `Tasks` (`list<task>`) answers the list. The builder
  writes `Task=%a%` for one and `Tasks=[%a%, %b%]` / `Tasks=%task.list%` for several. No type-switch; the answer's
  shape is the slot's. (Recommended.)
- (2) One slot (`item`), the handler asking "task or list" — a type-switch in a leaf.
`cancel` takes one task (`Task`), answering `Cancel()`'s answer.

## D. `item.Replace(previous)` and `%task.list%`

- `type.item.@this`: `public virtual ValueTask<item> Replace(Func<ValueTask<item?>> previous) => new(this);` — the previous
  value is handed **lazily**: every other type replaces as today and never reads it, so a write costs nothing more.
- Called once in the variable's write door (`variable/code/this.cs` `Set`, root and path alike, after the settle).
- The task's override: `previous()` is a task → a task holding the same run, with `list` = previous's list + itself.
  The run is the identity (one `Wait`, one `Cancel`, one listing under the actor); the item is the run plus the
  tasks it replaced. So the run's state moves into a private `task.run` the task holds (the actor's list holds the
  task it started); a task written over another is born holding the same run — no copy of fields, no stamp.
- `%task.list%` (`list<task>`): every task written to that variable, in the order written, the last `%task%` itself.
  A task written over a non-task (or nothing) is a list of one.
- Pins: a task written over a task keeps it (`%task.list%` 2, in order); a text written over a text is the text (no
  history); a dict written over a dict is the new dict; `wait for %task.list%` waits all.

## Tests (each fails without its change)
C#: cancel stops a sleeping goal (its write after the sleep never happens; the answer null; a later Wait fails
Cancelled, not reported); cancel of an ended task answers its result; a timeout pushed in one parallel flow doesn't
cancel its sibling (A); wait one / several / a list / `%task.list%` / a failure (with on error); Replace pins (D).
plang (`test/plan/task/module/task/…`): WaitAnswersResult, WaitForSeveral, WaitForList, FailedTaskFailsWait,
LateWaitGetsFailure, CancelStops, CancelAnswer, WaitOnCancelledFails, and stage 1's two that needed `wait for`
(the end of ParallelAnswersATask, TaskListedWhileRunning's "gone after").
