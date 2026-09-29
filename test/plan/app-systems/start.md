# app-systems

## Why

plang's own structure should be as navigable and as honest as a program's data. Every `%!app.X%` is a real object of type X, and three things name the same thing: the plang path (`%!app.goal%`), the C# class (`app.goal.@this`) and the file (`app/goal/this.cs`). What a thing does lives on that thing, not in helpers around it. For a plang developer, that means you can look at, and hook into, anything the app has: its types, goals, actors, modules, settings and events.

## Done

Stages 1–12, except 10b's (C). See [done.list](done.list).

## What's left, and what each gives the developer

Each stage is a goal in [start.goal](start.goal); each behaviour there is a comment with the test that proves it.

**10b (C): a typed list holds its type.** `list<text>` knows its element type when read back. Its plan test, `TypedListHoldsItsType`, waits on Ingi: whether a typed list's elements are born eagerly (when the list is made) or lazily (when each is read), and the `list<path>` teaching.

## How it's proven

- [start.goal](start.goal) runs the plan's tests; each lives under this folder at the path of what it tests.
- At the end of each stage, that stage's tests are built and run. A stage is accepted when its tests are green, the checks below hold, and the architect's OBP review is clean.
- **Every test must fail without its change:** once green, the change is reverted briefly and each test is confirmed red. (Two early tests passed while testing nothing; this is the guard.)
- For a change in the builder, the revert check is its deterministic pin (LineTwinTests, MatchTests, RenderTests, the formal golden): reverting the change turns the pin red. A plan test built before the revert keeps its compiled `.pr`, and an LLM rebuild isn't deterministic, so the plan test can't show it.
- Only the plan's tests are built; the rest of the test tree isn't rebuilt by this plan.
- The plan's plang says what is wanted; the coder writes each test in real plang that compiles, keeping what it proves.

## What tests can't show

These are checks, run and reported by the coder.

**8h**
- The builder rebuilds its own goals: with the target `.pr` files moved aside and the cache off, `BuildGoal/Start.goal` and `error/show.goal` build with no step refused, and no `.pr` keeps the old nested-modifier format.
- The decider renders its common questions through the real path (`file.read` → `ui.render`), pinned by `RenderTests.Render_AReadJsonFile_IteratesItsDictAndList`.
- The eval: the pick golden is regenerated, and stage 1 scores at least the baseline; the numbers go in the decision log.

**9**
- Every existing C# test passes; each handler in the stage 9 worklist is a one-line hand-over, and the OBP review of each module is clean.
- The births moved onto `type.Create` leave no caller that drops its `ValueTask` (a temporary `[Obsolete]` on the door lists none).

**10d**
- The report formats close on their C# pins, not a plan goal. A goal run by `plang --test` runs in an app that shares the outer run's root, so a `test.report` inside it would write `.test/junit.xml` / `.test/results.json` into the folder the outer run owns. Pinned instead: `ReportFormatTests` (the formats are json and junit, each a kind's own answer; a kind that writes no report is refused), `ReportActionTests` (junit writes `junit.xml`, json writes `results.json`, one per run), and `SensitivePropertyFilterTests` (a secret shows masked in an assertion message and a debug dump; the Out view omits it).

**11b–c**
- Every C# test passes with `TestApp` and `TestAction` deleted, and `App.Run<TAction>` retired.
- Building a folder with an unreached goal `Unused` emits a warning naming it.

**12b**
- Born knowing closes on its C# pins, not a plan goal: what a program sees (a relative file resolving against its goal's folder) isn't new, only where the folder comes from. Pinned: `PrLoadTests` (a goal and its sub-goal are born holding the `.pr` they were read from; a nested reader's refusal names the file), both mutation-checked (the reference's origin dropped turns them red). The os `Build.goal` build loads and runs the builder's own goals unchanged.

**Every stage**
- A rebuild of the builder's own goals is byte-identical, or its differences are explained.
- The C# suites are at the baseline.
