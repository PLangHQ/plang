# The test subsystem — a C# developer's guide

`plang --test` finds the `*.test.goal` files under a folder, runs each one in an
App of its own, and writes what happened to a report. This page covers the C#
that does that: the test type, the run's list and session, the report, and the
output formats. It is for the developer working *on* Runtime2 in C#.

> **Scope.** This page does not teach how to write a test goal. PLang test files
> live under `test/` at the repo root (lowercase), and a run is started from there:
> `cd test && ../PlangConsole/bin/Debug/net10.0/plang --test`. Running from the
> project root also picks up stale `.test.goal` files under `.bot/`.

```
app.test.@this          PLang/app/test/this.cs            namespace app.test   (alias Test)
app.test.list.@this     PLang/app/test/list/this.cs       namespace app.test.list
app.test.report.@this   PLang/app/test/report/this.cs     namespace app.test.report
app.test.setting.@this  PLang/app/test/setting/this.cs    namespace app.test.setting
```

---

## 1. How a run starts

`--test` is read in `PLang/Executor.cs`. When it is present, the run opens its
session before any goal runs:

```csharp
// PLang/Executor.cs
if (parameters.TryGetValue("!test", out var testValue) && testValue is not false)
{
    ...
    app.test.list.Open();
}
```

The run's goal is then replaced. When the app is testing and the goal is
`Start.goal`, `Executor` sets the goal to `/system/test.goal` (`os/system/test.goal`):

```
- set default %path% = '.'
- test.discover tests in %path% recursive, write to %tests%
- test.start tests %tests%
- test.report
```

Each of those three actions is a C# handler in `PLang/app/module/test/`
(`discover.cs`, `start.cs`, `report.cs`). They call into the test list and its
report. The actions are the plang face; the work is in the classes below.

---

## 2. `app.test` is a current, a list, and a setting

`app.test` is a `type.current` node, so it has a `list` and a `current`
(`PLang/app/this.cs`):

```csharp
public global::app.type.current.@this<global::app.test.@this, global::app.test.list.@this> test { get; }
```

| plang | C# | What it is |
|---|---|---|
| `%!app.test%` | `app.test` (the `type.current` node) | the test type's node |
| `%!app.test.list%` | `test/list/this.cs` | the run's tests, and the run's report and session |
| `%!app.test.current%` | `test.@this.Current(context)` | the test the asker's actor is running; null outside a test |
| `%!app.test.setting%` | `test/setting/this.cs` | how the run runs (see §7) |

> **`current` is a method, not a list member.** `Current(context)` reads the
> asker's actor's test channel (§3). It answers null when that actor has no test
> session open, which is every case outside a test.

The `test` type is the `Test` global alias (`PLang/app/GlobalUsings.cs`):
`global using Test = app.test.@this;`.

---

## 3. The app is testing while a session is open

The run and each test are connected by a **session**, a channel
(`PLang/app/channel/type/test/this.cs`). A session is kept open on an actor, and
an open session is what makes the app a test app:

| | The run's session | A test's session |
|---|---|---|
| opened by | `list.Open()` (Executor, §1) | `list.Open(test)` (`list/this.cs`), once per test |
| on the actor | the one the setting's `Actor` names (default `user`) | the same setting's actor, in the test's own App |
| channel name | `test` | `output` |
| holds a test | no (`Test` is null) | yes (`Test`) |
| what it keeps | nothing | what the test wrote, as `Text` |

`list.Session` is the one answer to "are we testing": it looks for the test
channel on every actor the app holds and returns the first one it finds.

```csharp
// PLang/app/test/list/this.cs
public global::app.channel.type.test.@this? Session
    => _app.actor.list.Items().Select(a => a.Channel[typeof(global::app.channel.type.test.@this)])
        .OfType<global::app.channel.type.test.@this>().FirstOrDefault();
```

`App.Mode` is derived from this, never stored. It is `Build` when the app has a
`Build`, `Test` when `test.list.Session != null`, and `Run` otherwise
(`PLang/app/this.cs`). Snapshot restore re-opens or closes the session to match a
restored mode (`PLang/app/this.Snapshot.cs`).

> **Output is captured by the session.** A test's session is registered as the
> `output` channel of the test's App. Everything the test writes out lands in
> `Text`, one line per write. `Test.Start` copies that text into `Stdout` when the
> test ends. A failure report prints it, with ANSI escapes stripped.

---

## 4. A `.test.goal` becomes a `test`

`test.discover` (`PLang/app/module/test/discover.cs`) calls
`list.Discover(root, pattern, subfolder, context)`. Its defaults are the root `.`,
the pattern `*.test.goal`, and subfolders included.

For each file, `list.Of(file)` (`PLang/app/test/list/this.cs`) decides what the
test is. It reads the goal, takes its `PrPath`, and then:

| Check | If it fails |
|---|---|
| the `.pr` exists | `Stale`, reason `no .pr` |
| the `.pr` reads (the format refuses an old one) | `Stale`, with the reader's message |
| the `.pr`'s `Hash` equals the source's `Hash` | `Stale`, reason `rebuild needed` |

A `Stale` test is never run. It is recorded, with its reason, and the run's
verdict names it (§6). A fresh one goes to `test.From(goal, context)`
(`PLang/app/test/this.cs`), which sets:

- **Reach.** The goal plus every goal its calls reach (`goal.Callee`), as they are
  now. Coverage uses this list, so a site that never runs still shows.
- **Tags.** The goal's own tag, and the capabilities each reached action requires.
- **Status.** `Skipped` when the goal is tagged `skip`, or when the setting's
  include/exclude rules leave it out. Otherwise `Ready`.

| `Status` | Set by | Meaning |
|---|---|---|
| `Ready` | `From` | discovered and fresh; will run |
| `Stale` | `list.Of` | could not load or is out of date; not run |
| `Skipped` | `From` | tagged `skip`, or left out by the setting; not run |
| `Pass` | `Complete(data)` | finished, no failure |
| `Fail` | `Complete` | finished with an error or a failed assertion |
| `Timeout` | `Start` | cancelled at the setting's timeout |

The enum is `PLang/app/test/Status.cs`. On the wire it is its name.

---

## 5. Running: `list.Start` and `test.Start`

`list.Start(given, context)` runs the tests it is given, as many at once as the
setting's `Parallel` says. A value of 0 or less means one per processor. Each test
waits on a `SemaphoreSlim` and then runs. A test's failure is its own outcome. The
others keep running.

```csharp
private async Task Run(global::app.test.@this test, actor.context.@this context)
{
    foreach (var goal in test.Reach) Report.Coverage.Add(goal);
    if (test.Status == Status.Ready)
    {
        await using var app = new global::app.@this(_app.AbsolutePath);
        app.test.list.Open(test);
        Made?.Invoke(app);
        await test.Start(app, context);
        Report.Coverage.Merge(app.test.list.Report.Coverage);
    }
    Add(test);
}
```

Each test gets **its own `app`**, rooted at the same folder, so a file is the App
boundary. The App lives as long as the run needs it.

`Test.Start(app, context)` runs the goal under a timeout:

- The timeout is the setting's `Timeout` (default 30 seconds). It rides a linked
  cancellation token that the test's actor holds, so an action that reads the
  token (`timer.sleep`, `http.request`) stops when it fires.
- The goal is loaded with `app.goal.Load` and started on the test's user actor.
- A cancellation that the timeout caused becomes `Timeout`. Any other exception
  becomes `Fail`, carrying a `ServiceError` with the code `TestRunError`. A crash
  inside the test's App is the test's `Fail`, never the run's.
- The step timings (`Timings`) come from an after-binding on the test's steps. It
  records the entry goal's own top-level steps. A sub-goal's time rolls up into the
  step that called it.

> **A test's verdict is its status.** The run never fails because a test failed.
> The run's own verdict is computed afterwards (§6), from the statuses.

---

## 6. The report

`list.Report` (`PLang/app/test/report/this.cs`) holds the run's start time, the
run's `Coverage`, and the per-status counts (`Summary()`, with every status key
present, even at 0). `Report.Write(context)` (called by `test.report`) does three
things:

1. **Console.** Unless the run is nested inside a test, it writes a summary line,
   each test with its duration and why it failed (`Test.Failure`), and the coverage
   text. The console is always written, whatever the format.
2. **The artefact.** It encodes the run with the chosen format kind, using the
   `Out` view, and writes the result to `/.test/<report>` (§7). Then it sets the
   result's properties: `format`, `reportPath`, `content`, the summary counts.
3. **The verdict.** A top-level run returns `Verdict()` as its error when it did
   not pass. `Verdict()` names every problem it finds:

| Problem | Verdict text |
|---|---|
| nothing discovered | `no tests were discovered — nothing ran.` (`NoTestsDiscovered`) |
| `Stale` tests, grouped by reason | `12 tests could not load: no .pr.` |
| `Ready` tests that never ran | `N tests did not run` |
| `Fail` or `Timeout` tests | `N tests failed` (`TestRunFailed`) |

> **Order matters.** The artefact is written before the verdict is returned, so a
> failing run still leaves its report on disk.

---

## 7. Formats and output files

The `format` setting (`test/setting/this.cs`) is a choice over the format kinds
that write a report. It is found by reflection at startup (`test/format/this.cs`),
so a new report kind needs only a `kind` with a `Report` file name.

| Format | File | Written by | Notes |
|---|---|---|---|
| `json` (default) | `/.test/results.json` | `kind/json` (`app/type/item/kind/json/this.cs`) | the run encoded by the json kind's `Encode`; the test's own `Output` writes its tagged `[Out]` fields |
| `junit` | `/.test/junit.xml` | `test/junit/this.cs` | for CI tools; a `testsuite` per goal folder |

The JUnit document maps the statuses as follows: `Fail` is a `<failure>`,
`Timeout` a `<failure type="timeout">`, `Stale` an `<error>` (never silently
skipped), and `Skipped` a `<skipped>`. A `Pass` is an empty `<testcase>`.

---

## 8. The setting

`test/setting/this.cs` is `%!app.test.setting%`. `--test={…}` sets this run's
values for it. Each field is `[Out, Store]`.

| Field | Default | What it does |
|---|---|---|
| `Actor` | `user` | the actor the run's session opens on |
| `Timeout` | 30 s | the per-test wall-clock timeout; ≤ 0 means none |
| `Parallel` | 0 | tests at once; ≤ 0 means one per processor |
| `Format` | `json` | the report format (§7) |
| `Include` / `Exclude` | empty | tag lists; exclude wins over include |

`Exclusion(test, context)` gives the reason a test is left out by these rules, or
null when the run takes it.

---

## 9. Coverage

`Coverage` (`PLang/app/test/Coverage.cs`) records what ran: which `(module, action)`
pairs fired, and which branch of each `condition.if` site was taken. Each test's
App records its own, and `Report.Coverage.Merge` folds it into the run's. The
console prints it from the module list and the goals the run walked, minus the
test files themselves.

---

## Find ...

| Find | plang | C# | Source |
|---|---|---|---|
| the run's test list | `%!app.test.list%` | `test.list.@this` | `test/list/this.cs` |
| the test a goal is | `%!app.test.current%` | `test.@this.Current` | `test/this.cs` |
| whether the app is testing | (`App.Mode` is `Test`) | `test.list.Session` | `test/list/this.cs` |
| a test's status and why | (test report) | `test.@this.Status`, `StatusReason` | `test/this.cs` |
| how a `.test.goal` becomes a test | `test.discover` | `list.Of` | `test/list/this.cs` |
| the run itself | `test.start` | `list.Start` | `test/list/this.cs` |
| the report and verdict | `test.report` | `report.Write`, `Verdict` | `test/report/this.cs` |
| the output format | `%!app.test.setting%` `Format` | `format.@this`, `junit.@this` | `test/format/this.cs`, `test/junit/this.cs` |
| the step timings | (test report) | `timing.@this` | `test/timing/this.cs` |
| the run's session | (channel `test` / `output`) | `channel.type.test.@this` | `channel/type/test/this.cs` |

---

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app root, the collection-node pattern, and reading the codebase |
| `../goal/start.md` | the goal and step subsystem: the `.goal`, the `.pr`, and goal addresses |
| `../module/start.md` | the module registry and the action handler pattern (the `test.*` actions) |
