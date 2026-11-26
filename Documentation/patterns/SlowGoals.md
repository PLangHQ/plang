# Finding slow goals

A web page should answer in about 100 ms. This page is the pattern for finding the goals that do not: an event records every
entry goal that takes longer than a limit, and when one needs fixing, a start parameter turns on a second event that records
which steps took the time. It is plain plang, no runtime setting.

Set it up when a new app is created, with a limit of 300 ms, and lower the limit as the app gets faster.

## 1. The tables

```plang
Setup
- create table slow_runs, columns: name(string), path(string), route(string, nullable), duration_ms(number), created(datetime, default now)
- create table slow_steps, columns: goal(string), step(string), route(string, nullable), duration_ms(number), created(datetime, default now)
```

`route` is empty for a goal that does not run in a request (Start, Setup, the scheduler), so it must allow null. Write `nullable`:
without it the column can be built as `NOT NULL`, and every slow goal outside a request fails to insert.

## 2. Record slow goals

```plang
Events
- after each goal, call LogSlowGoal
- after each goal, call LogSlowSteps, only when start parameter is '--slowsteps'
```

```plang
/ events/LogSlowGoal.goal
LogSlowGoal
- calculate %!callstack.Depth% - 2, write to %idx%
- if %!callstack.Frames[idx].DurationMs% > 300 and %!Event.SourceGoal.IsSystem% == false then
    - insert into slow_runs, name=%!Event.SourceGoal.GoalName%, path=%!Event.SourceGoal.RelativeGoalPath%, route=%request!Path%, duration_ms=%!callstack.Frames[idx].DurationMs%, on error ignore
```

- `%!Event.SourceGoal%` is the goal that just ended: its name, its path, and whether it is one of plang's own goals.
- `%!callstack%` is the goals that are running, oldest first. In an `after each goal` event the last frame is the event goal
  itself and the one before it, `Depth - 2`, is the goal that just ended. Its `DurationMs` is the whole goal, including the goals it
  called.
- Each reference to `%!callstack%` builds the whole stack again, so it is used only for the duration. Name and path come from
  `%!Event.SourceGoal%`, which costs nothing.
- `IsSystem == false` leaves out plang's own goals. They run before the app's Setup, when the tables do not exist yet.
- `on error ignore`: an error in an event goal is the error of the goal it followed, so a failing insert would fail the page.
  Recording must never do that.
- `%request!Path%` is the url of the request, empty outside a request.

**Bind it to public goals only.** Without `include private goals` the event runs after the first goal of each file: a route's goal,
a page, a goal the scheduler calls. Those are the entry points, and their time includes everything they called, so a slow private
goal still shows up in the time of the goal that called it. A goal called 50 times in a loop is private and costs nothing here.
With `include private goals` the event runs after every one of them, and a page that calls many goals pays for it on every load.

A goal that ends with an error it does not handle is not recorded. The error is reported on its own.

## 3. Find the slow step

`slow_runs` says which goal is slow, not which step. Start the app with the parameter when you are fixing it, not in production:

```bash
plang --slowsteps
```

```plang
/ events/LogSlowSteps.goal
LogSlowSteps
- calculate %!callstack.Depth% - 2, write to %idx%
- if %!callstack.Frames[idx].DurationMs% > 300 and %!Event.SourceGoal.IsSystem% == false then
    - go through %!callstack.Frames[idx].ExecutedSteps%, call LogStep goalName=%!Event.SourceGoal.GoalName%

LogStep
- if %item.TotalDurationMs% > 50 then
    - insert into slow_steps, goal=%goalName%, step=%item.StepText%, route=%request!Path%, duration_ms=%item.TotalDurationMs%, on error ignore
```

Each frame keeps the steps that ran in it. Steps that ran one after another with the same text are one row, with `RepeatCount`
and `TotalDurationMs` (all runs) beside `DurationMs` (the first run), so use `TotalDurationMs`. A page that waits 300 ms and calls
a goal that waits 50 ms gives:

| goal | step | route | duration_ms |
|---|---|---|---|
| Slow | `wait 300 ms` | /slow | 316 |
| Slow | `call goal Inner` | /slow | 66 |

A `call goal X` step is slow when X is slow. When X is the first goal of its file it gets its own rows in `slow_steps` too, so
the time can be followed down one goal at a time.

## 4. Using what is recorded

`slow_runs` grows with every slow load, so the same route shows up many times. Group it to see what to fix first:

```plang
- select route, name, count(*) as loads, avg(duration_ms) as avg_ms, max(duration_ms) as max_ms
    from slow_runs where created > %Now-7days% and path not like '/events/\%'
    group by route, name order by avg_ms * loads desc, write to %slowest%
```

The goals in `events/` are recorded too: the recording goals themselves, and with `--slowsteps` their steps show up in
`slow_steps`. They are left out when reading, not when recording, because an extra condition on the path in the event makes
the `if` harder to build right. `\%` is a literal `%` in the step; a bare `%` there makes the `like` match nothing.

```json
[{"route": null, "name": "Setup", "loads": 1, "avg_ms": 2058.0, "max_ms": 2058},
 {"route": "/slow", "name": "Slow", "loads": 3, "avg_ms": 380.3, "max_ms": 409}]
```

A goal that is slow once after a start, like `Setup` on the first start of a new database, is normal. One that is slow on every
load is the one to fix.

See also: [Events](../Events.md), [Startup parameters](../StartupParameters.md), [Recommended app structure](../RecommendedAppStructure.md).
