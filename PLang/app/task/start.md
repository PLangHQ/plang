# Tasks — a C# developer's guide

A task is a goal call that runs on its own, in the background, while the goal
that started it goes on. An actor starts it, lists it while it runs, and the
app waits for it before the run ends. This page covers the three types that
make a task, how an actor holds them, and how the run waits for them. It is
for the developer working on Runtime2 in C#.

```
app.task.@this          PLang/app/task/this.cs        namespace app.task       one task
app.task.list.@this     PLang/app/task/list/this.cs   namespace app.task.list  an actor's running tasks
app.task.run.@this      PLang/app/task/run/this.cs    namespace app.task.run   one run (internal)
actor.Task              PLang/app/actor/this.cs       the list an actor owns
```

> **Audience.** These docs are for the developer working *on* Runtime2 in C#.
> The root of the runtime is described in `../start.md`. The actor that owns the
> list is `../actor/start.md`. The frame a task's goal runs in is
> `../call/start.md`.

> **Two lists named `task`.** `actor.Task` (`task.list.@this`) is the actor's
> running tasks. `task.@this.list` is the tasks written where one task is, which
> is the `list` of §3. Read the owner before the name.

---

## 1. A task is a goal call on its own

`app.task.@this` is the value a caller holds. It is sealed, it is a plang type
(`[PlangType("task")]`), and it wraps one run. Its only public door into the run
is `Wait` and `Cancel`.

```csharp
namespace app.task;

public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public global::app.goal.@this Goal => _run.Goal;
    [Out] public global::app.type.item.text.@this Id => new(_run.Id);
    [Out] public global::app.type.item.@bool.@this Ended => new(_run.Ended);

    public Task<global::app.data.@this> Wait(CancellationToken token = default)
        => _run.Wait(token);
    public Task<global::app.data.@this> Cancel() => _run.Cancel();
}
```

The constructor is `internal`. A task is made only by the list (§2), never by a
module on its own.

| Member | What it is |
|---|---|
| `Goal` | the goal it runs |
| `Id` | its key in the actor's list, a 32-character GUID string |
| `Ended` | whether its run has ended |
| `Wait(token)` | its result, once it ends. The wait stops when `token` is cancelled; the run goes on. |
| `Cancel()` | stops the run. Returns the result if it already ended, else null. |
| `list` | every task written where this one is, in the order written |

> **Written over.** `Replace` lets a task take the place of another task in a
> variable. The new task keeps the old one, so `list` holds both, the last being
> the new one. A task written over its own run adds nothing.

---

## 2. An actor owns its task list

Each actor has one `app.task.list.@this`, created in the actor's constructor
(`PLang/app/actor/this.cs`, `_tasks = new ...(this)`). It is the actor's running
tasks, and it is nothing else. It holds a `ConcurrentDictionary` keyed by id.

```csharp
public task.@this Start(global::app.goal.@this goal,
    Func<CancellationToken, Task<global::app.data.@this>> run)
```

`Start` does four things, in this order:

1. It makes a GUID id and a `task.run.@this` for the goal and the run delegate.
2. It makes the `task.@this` and lists it under its id.
3. It begins the run. The run starts on the thread pool, once it is listed.
4. It returns the task. The caller does not wait.

A run leaves the list when it ends. So `list` answers what runs now, not what
ran. A task started inside another task's goal lists under the same actor.

`Get` on the list reads a member of the list first, then a running task by its
id. That is how `%!app.actor.current.task[id]%` reaches a task.

> **Callers.** Two call sites start tasks: the `goal.call` action with
> `in parallel` (`PLang/app/module/goal/call.cs`) and the `foreach` loop in
> parallel (`PLang/app/module/loop/foreach.cs`). Both pass the goal and a run
> delegate, and both reach the list through `actor.Task.Start`.

---

## 3. A run is isolated by its caller

`app.task.run.@this` is internal. It holds the state every task written for one
run shares: the goal, the id, the cancellation source, and whether the result
was asked for. It does not create a frame.

The isolation is made by the delegate the caller passes. The parallel branch of
`goal.call` does this, in `PLang/app/module/goal/call.cs`:

```csharp
return runs.Actor.Task.Start(goal, async token =>
{
    using var child = Context.Child(runs.Actor, token);
    await using (child.call.Isolate(null, caller: from))
        return await goal.Start(child, bound);
});
```

- The child context takes the run's token, so the goal cancels by it.
- `Isolate` makes an isolated call frame (`PLang/app/call/isolated/this.cs`).
  Every write the task makes stays in that frame. None reaches the caller.
  See `../call/start.md` §1 for the three kinds of frame.
- `bound` is the parameters, settled in the caller before the task starts. The
  task reads what the caller held at the call, not what it holds later.

The run's own `Run` does three things. It runs the delegate with the run's
token. It turns whatever the delegate throws into an error answer, through
`Error.FromException`. It leaves the list.

---

## 4. How the app waits for tasks

The app's `Start()` (`PLang/app/this.cs`) runs the goal, then `Settle()`. A run
ends only when its tasks have ended.

```csharp
private async Task Settle()
{
    while (actor.list.Items().Any(each => each.Task.list.Any()))
        foreach (var each in actor.list.Items()) await each.Task.Wait();
}
```

`Settle` loops until no actor has a task listed. A task started while it waits
is waited for too, because the loop checks again. `Wait` on the list awaits the
run's `Done` for each task listed at the time, never its result. So a failure
nobody asked for still reports itself, through the run's `Run` (§3).

The `wait for` action (`PLang/app/module/task/wait.cs`) is the other waiter. It
asks for the result, through `task.Wait(token)`. A task that failed fails the
step that waits for it.

---

## 5. A task is cancelled with its actor

Each run's cancellation source is linked to its actor's token
(`actor.CancellationToken`, `PLang/app/actor/this.cs`). So cancelling an actor
cancels its runs, and cancelling an actor cancels the actors under it, because
their tokens are linked too. A run cancelled by its actor ends with a
`Cancelled` error, status 499.

`Cancel()` on a task cancels its own source only. A run that was cancelled is
never reported as a failure. A run whose result was never asked for reports its
failure to the actor's error channel (`Channel.Report`) when it ends.

---

## Find ...

| You want | Plang | C# | Source |
|---|---|---|---|
| an actor's running tasks | `%!app.actor.current.task%` | `actor.Task` | `PLang/app/actor/this.cs` |
| one task by its id | `%!app.actor.current.task[id]%` | `list.Get(...)` | `PLang/app/task/list/this.cs` |
| start a background goal call | `goal.call` with its parallel setting on | `list.Start(goal, run)` | `PLang/app/task/list/this.cs` |
| wait for a task's result | `wait for %task%` | `Task.Wait(token)` | `PLang/app/module/task/wait.cs` |
| cancel a task | — | `Task.Cancel()` | `PLang/app/task/this.cs` |
| the run's goal and id | — | `Run.Goal`, `Run.Id` | `PLang/app/task/run/this.cs` |
| the isolated frame of a task | — | `call.Isolate(...)` | `PLang/app/module/goal/call.cs` |
| the wait for all tasks | — | `Settle()` | `PLang/app/this.cs` |

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app root, and `Start` and `Settle` |
| `../actor/start.md` | the actor that owns the task list, and its context |
| `../call/start.md` | the frames a task's goal runs in, and `Isolate` |
