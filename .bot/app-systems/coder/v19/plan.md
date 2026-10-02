# v19 — task, stage 1: the task and the call (shape)

Plan: `test/plan/task/start.md`. Stage 1 only; stage 2 (task.wait / task.cancel module) after acceptance.

## The types

```csharp
// PLang/app/task/this.cs — a goal call running on its own
namespace app.task;
[global::app.Attributes.PlangType("task")]
public sealed class @this : global::app.type.item.@this
{
    // members (plang types): what it runs, which one it is, whether it has ended
    public global::app.goal.@this Goal { get; }
    public global::app.type.item.text.@this Id { get; }          // %!app.actor.current.task[id]%
    public global::app.type.item.@bool.@this Ended { get; }      // its run has finished
    // the result: one door, not a member
    public Task<data.@this> Wait();   // marks it watched, answers the run's result (a failure as itself)
    // born only by its list: private run, private linked CancellationTokenSource
}
```
- At its end a failed task nobody has asked `Wait` of is **reported** on its actor's error channel
  (`channel.list.Report`). Exactly one of the two happens: one `Interlocked` flag set by `Wait` (before it awaits) or
  by the end (before it reports). A `Wait` after the report still answers the failure.

```csharp
// PLang/app/task/list/this.cs — the actor's running tasks (as channel.list is its channels)
namespace app.task.list;
public sealed class @this : global::app.type.item.@this
{
    private readonly ConcurrentDictionary<string, task.@this> _running;   // private backing
    public task.@this Start(goal.@this goal, Func<CancellationToken, Task<data.@this>> run);  // born, listed, run
    public IEnumerable<task.@this> list => _running.Values;               // running now
    public override ValueTask<data.@this> Get(data.@this parent, string key); // own members, then [id]
}
```
- The whole lifecycle is in this type: `Start` makes the task (its token linked to the actor's), lists it, runs it on
  the thread pool; the task's end removes it. `%task%` keeps the task after it leaves the list.
- `actor.@this`: `public global::app.task.list.@this Task => _tasks;`, beside `Channel`.

**`actor.Task` beside `System.Threading.Tasks.Task`:** in a type position (`Task<x>` return types) C# ignores a
property, so signatures are unaffected; in an expression position (`Task.Run`, `Task.CompletedTask`) inside the actor
class the property would win. `actor/this.cs` has no `Task` expression today (it uses `ValueTask`), so nothing changes
there; any later one inside the actor class writes `System.Threading.Tasks.Task`. Everywhere else `actor.Task` is member
access and can't be confused. Module code reaches the type as `global::app.task.@this` (inside `app.module.*` a bare
`task` would resolve to the `app.module.task` namespace that stage 2 adds).

## goal.call

```csharp
[Default(false)] Parallel   // "answers a task" — the one flag; Wait and its [Default(true)] go
public async Task<data.@this> Start()
{
    if (await Name.Value() is not { } goal) return Name;
    return await Context.App.actor.list.Use(Actor, Context, async runner => await Parallel.ToBooleanAsync()
        ? Context.Ok<global::app.task.@this>(runner.Task.Start(goal, _ => Run(goal, runner.Context)))
        : await Run(goal, runner.Context));
}
```
- The fire-and-forget body (Task.Run + catch + Report) moves into `task.list.Start` / the task's end.
- `Start` stays bare `Task<data.@this>` (a polymorphic forwarder: the goal's result, or the task).
- The call frame is AsyncLocal (`call.list._current`) and disposing a frame only restores the parent's pointer, so a
  task keeps the frame it was started in (the tool loop's `Isolate` frame included).

## The llm tool loop (OpenAi.cs:344–366 and ExecuteToolAsync)

- The `allParallel` scan and the `Task.WhenAll` / sequential fork go. Each tool call is made **in order** through
  its held goal.call: a non-Parallel tool runs to its end, a Parallel tool's call answers a task at once. Then the
  loop waits every task, in order, and the results go back in call order.
- `ExecuteToolAsync` splits at the held call: starting (OnToolCall "starting", arguments, `Isolate`, `Held.Start`) and
  ending (for a Parallel tool, `Use<task>(t => t.Wait())`; then OnToolCall "completed" and the json encode).
- **Behaviour change, mixed flags:** today one non-Parallel tool makes the whole batch sequential. After, each call's
  own `Parallel` decides: a Parallel tool runs on while the next tool runs. No test pins the old rule
  (`Query_MixedParallelFlags_ForcesSequential` asserts success only); it is renamed to what it proves.

## Readers of `Wait` — disposition

| reader | disposition |
|---|---|
| `module/goal/call.cs:34–37` `Wait` + `[Default(true)]` | removed |
| `module/goal/call.cs:64–79` the fork + fire-and-forget body | → `Parallel` ? `runner.Task.Start` : run; the body moves into the task |
| `goal/step/serializer/Reader.cs:66` comment "(goal.call does: Wait)" | reworded to `Parallel` |
| `CallWaitTests`: `TheStep_GoesOnBeforeTheGoalEnds`, `AFailure_NothingWaitsFor_GoesToTheErrorChannel`, `TheActorNamed_RunsIt` (the 3 that set `Wait=false`) | rewritten to `Parallel` + the task, into a new `TaskTests` |
| `CallWaitTests`: the other 6 (error channel fallback, error goal channel, goal channel message, dict parameters, list-given-dict, older `.pr`) | not about `Wait`: stay; the class is renamed `CallTests` |
| `Documentation/v0.2/conventions.md:86,90` | :90's `goal.call Wait` item removed from "Still to fix" (four left); :86's example stays (it explains the rule with Wait as the case) |
| `os/system/modules/goal/call.examples.md:22` (`"Wait": false`) | stage 3 (builder) |
| `os/system/modules/goal/call.notes.md:4` (Parallel line) | stage 3 (builder) |
| built `.pr` files | none writes `Wait` (checked over os/, test/, Tests/ `.build`) |
| `PLang.Tests/Shared/Make.cs:114` `Make.Tool(parallel:)` | stays (`Parallel`) |

## Tests (each fails without the change)

C# (Runtime, `TaskTests`):
- a Parallel call answers a `task`; the caller's next step writes `%order%` "caller" while the task's goal sleeps then
  appends "task": after `await task.Wait()`, `%order%` is `caller,task` (read from what was written, not timed).
- a plain call answers the goal's result (no task).
- a running task is in `actor.Task.list` and found by `[id]`; after it ends it isn't, and the `%task%` variable still
  holds it (`Ended` true).
- an unwatched failure reaches the error channel; a `Wait` begun before the end gets it and nothing is reported.
- the actor named runs it (rewritten `TheActorNamed_RunsIt`).
- 100 Parallel calls, all waited: the actor's list is empty.
QueryToolTests: two Parallel tools each write a mark into the caller's %kept% dict; both marks are there and both
results go back in call order.

plang (`test/plan/task/module/goal/call/`, formal where the builder can't pick it yet): ParallelAnswersATask,
PlainCallWaits, TaskListedWhileRunning, UnwatchedFailureReported, ToolLoopRunsTasks (the C# pin above, a goal noting it).

## Checks reported at the end
- grep: no `Wait` reader left (C#, `.goal`, templates, docs) beyond stage 3's two os files.
- a 100-task run leaves the actor's list empty.
- the gate by name; PLNG001/002 at 0.
