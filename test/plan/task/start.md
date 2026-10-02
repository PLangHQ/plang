# task

## Why

`goal.call` has two flags for one idea. `Wait` (default true) throws a call's answer away when false, and `Parallel` (default false) lets the llm tool loop run tool calls side by side. A default-true flag invites the builder to write it false on a step that never mentions it (`- call goal Ble` → `Wait=false`), so `Wait` breaks the rule "a flag is false by default" (`Documentation/v0.2/conventions.md`). And a call that isn't waited for can't be waited for later: its answer is gone. With a `task`, one flag covers both: a call in parallel answers a task, the goal goes on, and a later step waits for the task, or cancels it, or never asks (fire and forget). Every running task is listed under the actor that runs it.

```
Start
- call goal FetchWeather city=%city%, in parallel, write to %weather%
- call goal FetchNews, in parallel, write to %news%
- write out "fetching…"
- wait for %weather%, %news%, write to %both%
- write out "%both[0]% / %both[1]%"
```

## Decisions (Ingi, 2026-10-02)

- **One flag: `Parallel`, false by default.** "in parallel", "don't wait" and "in the background" all set it. `Wait` goes.
- **A call in parallel answers a `task`**, a new core plang type. A plain call waits and answers the goal's result, as today.
- **Tasks live under the actor that runs them** (Ingi: "hierarchy, actor is probably the right thing"): `%!app.actor.current.task%` is the collection node, `%!app.actor.current.task.list%` every task running now. A task leaves the list when it ends; the `%task%` variable keeps it.
- **A task that fails with nobody waiting is reported** on its actor's error channel when it ends; a later `wait for` still answers the failure. A failure never vanishes.
- **A `task` module: `wait` and `cancel`.** `wait for %a%` answers a's result; `wait for %a%, %b%` answers a list of results in order; a failed task fails the wait step (`on error` works as anywhere). `cancel %task%, write to %result%` stops it.
- **The llm tool loop is one more caller:** it starts its `Parallel` tool calls as tasks and waits for all of them.

## Open (to Ingi before stage 2)

- **What `cancel %task%, write to %result%` answers.** Proposed: the task's result when it had already ended; nothing (null) when the cancel stopped it. A `wait for` on a cancelled task then fails as Cancelled.

## The shape (the coder owns the code; this is the intent)

- `task` is an item (`PLang/app/task/this.cs`, `app.task.@this`): it holds the goal call it runs and answers whether it has ended. Its members are plang types. Its result is reached through `task.wait`, one door, not a member.
- `task.list` (`PLang/app/task/list/this.cs`) is owned by the actor (`actor.Task`, as `actor.Channel` owns its channels): it starts a task, holds it while it runs, and lets it go when it ends, so the whole lifecycle lives in one type. A task's cancellation is linked to its actor's (`actor/this.cs:91–97`), so a stopped actor stops its tasks.
- `goal.call` with `Parallel` true hands its run to the runner actor's `task.list` and answers the task (`data<task>`). The fire-and-forget body (`module/goal/call.cs:67–79`) moves into the task, with its failure report (`channel.list.Report`).
- `task.wait` and `task.cancel` (`PLang/app/module/task/{wait,cancel}.cs`) are one-line doors onto the task's own `Wait` and `Cancel`.
- Risk to check: an actor property named `Task` sits beside `System.Threading.Tasks.Task` in `actor/this.cs`; the coder decides how the two read apart.

## Stages

1. **The task and the call** (coder): the `task` type and its list under the actor; `goal.call` `Parallel` answers a task; `Wait` goes; an unwatched failure is reported at its end; the llm tool loop starts its `Parallel` tools as tasks and waits for all (`llm/code/OpenAi.cs:344–358`). Plang tests below, written in formal where the builder can't pick it yet.
2. **The task module** (coder): `wait` (one, several, a failure) and `cancel`, after Ingi answers the open point.
3. **The builder** (the builder session): `call.notes.md`'s `Parallel` line ("in parallel", "don't wait", "in the background"); `call.examples.md:22` rewritten to `Parallel: true` with `write to %task%`; the task module's `description`/`notes`/`examples`, kept apart from `timer.sleep` ("wait for %task%" vs "wait 2 seconds"); goldens; measured fresh, cache off.

## Demolition

- `goal.call`'s `Wait` and its `[Default(true)]` (`module/goal/call.cs:34–37`) and the `if (await Wait…)` fork (`:64–79`).
- `call.examples.md:22`'s `"Wait": false` example (stage 3).
- `CallWaitTests` (5): rewritten to the task.
- The tool loop's own `Task.WhenAll` branch (`llm/code/OpenAi.cs:344–358`), if the tasks replace it.
- `Documentation/v0.2/conventions.md`'s "Still to fix" entry for `goal.call` `Wait`.
- The coder traces every other reader of `Wait` (C# tests, `.goal`, docs) and gives each one's disposition before building.

## Stays

- `goal.call`'s `Parallel`, now meaning "answers a task".
- `channel.list.Report`: where an unwatched failure goes.
- The actor's cancellation token: a task's cancel links to it.
- `timer.sleep`: "wait 2 seconds" stays its own action.

## How it's proven

- [start.goal](start.goal) runs the plan's tests; each lives under this folder at the path of what it tests (`module/goal/call/…`, `module/task/…`).
- At each stage's end its tests are built and run. A stage is accepted when its tests are green, the checks hold, and the architect's OBP review is clean.
- **Every test must fail without its change.**
- The plan's plang says what is wanted; the coder rewrites each test in real plang that compiles, keeping what it proves.
- Timing tests use a goal that sleeps, never a wall-clock race: "the next step ran before the task ended" is read from what the task wrote, not from durations.

## What tests can't show (checks the coder runs and reports)

- **Stage 1:** no reader of `Wait` is left (grep over C#, `.goal`, templates, docs); a 100-task run leaves the actor's list empty when all have ended.
- **Stage 3:** measured fresh, cache off, 5 each: "call X in parallel", "call X, don't wait", "call X in the background" build `Parallel=true`; "call X" builds no `Parallel`; "wait for %t%" builds `task.wait`; "wait 2 seconds" still builds `timer.sleep`.
- **Every stage:** the gate by name; PLNG001/002 at 0.

## OBP validation

| surface | plang path | C# path | file path | checks |
|---|---|---|---|---|
| the tasks | `%!app.actor.current.task%` (`.list`, `[id]`) | `actor.@this.Task` → `app.task.list.@this` | `PLang/app/task/list/this.cs` | not a naked collection: own `Start`, private backing; lifecycle in one type |
| a task | `%task%` | `app.task.@this` | `PLang/app/task/this.cs` | members are plang types; result only through `Wait` (one door) |
| wait | `wait for %t%` | `task.wait` handler | `PLang/app/module/task/wait.cs` | one-line door onto `task.Wait` |
| cancel | `cancel %t%` | `task.cancel` handler | `PLang/app/module/task/cancel.cs` | one-line door onto `task.Cancel` |
| the flag | `in parallel` | `goal.call.Parallel` | `PLang/app/module/goal/call.cs` | false by default; one flag, `Wait` gone |
