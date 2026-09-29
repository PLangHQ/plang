# app-systems

## Why

plang's own structure should be as navigable and as honest as a program's data. Every `%!app.X%` is a real object of type X, and three things name the same thing: the plang path (`%!app.goal%`), the C# class (`app.goal.@this`) and the file (`app/goal/this.cs`). What a thing does lives on that thing, not in helpers around it. For a plang developer, that means you can look at, and hook into, anything the app has: its types, goals, actors, modules, settings and events.

## Done

Stages 1–8h and 9a. See [done.list](done.list).

## What's left, and what each gives the developer

Each stage is a goal in [start.goal](start.goal); each behaviour there is a comment with the test that proves it.

**8h: the builder builds itself, and plain words work.** The builder is plang code, and it can now rebuild its own goals. Plain phrasings compile to what they mean: "retry once", "if done, return x", named render arguments. This is also the first time the decider is measured on the prompts the real builder renders.
- *Decisions:* no human language is read by deterministic code; a number the step writes in words is confirmed by the decider, as a visible builder step (ConfirmNumbers). The prefill offers only what is certain: a lone if takes its body, and optional properties get no hole.

**9: every value is born through its type.** A developer can hook the birth of any value: `after file create, call LoadFixture`, e.g. for tests with fake files. Inside, every module action becomes a one-line door to the object that does the work.
- *Decisions:* `type.Create` is the one async birth door and fires `on.create`; `Make` is the internal build (`on.create` fires once per value). A birth is a value coming into the program from outside the type system.
- *Stage 9 decisions (175):*
  1. Converting to another type is a birth (`set %p% as path = "a.txt"` fires `after path create`). A value's own delayed parse, or the same type re-kinded, is not.
  2. A carrier answers its own failure or hands its value whole (`Path.Use(p => p.Read(…))`), so every action is one line.
  3. `path.Read` is path's one read verb. It lands a reference; the content is that reference's own value. `channel/type/file` and `channel/type/http` are deleted, because the file and url values already are that.
  4. `signing.sign` is a one-line door to the signature's owner.
  5. The test report and failure text move to os templates later, not in this stage.
  6. The split: 9a (the `Use` door, file.read, one read verb, births through `type.Create`, the channel collapse); 9b (the other modules, one per commit); 9c (`module/action/<m>/<a>.cs` → `module/<m>/action/<a>.cs`, matching `%!app.module.<m>.action.<a>%`).

**10: the app knows its types.** Every type a program can use is listed at `%!app.type.list%`, and a type is the plang value it says it is.
- **10a: registration.** Built-in types and a plugin's types join `%!app.type.list%`; one name can't be taken twice (a clash fails loudly). `%!app.list%` is kept for the apps running under this app, later.
- **10b: typed lists.** `list<text>` is made by the type itself: a kind carries its element. A list read no longer copies itself.
- **10c: the app's facts are plang values.** `%!app.created%` is a datetime, `uptime` a duration; a channel's settings are plang values. The app's identity is read back through the same format that writes it, the store is ready when the app is, and `id`, `name` and `environment` are the app's settings.
- **10d: formats live with their owners.** A type's formats are its kinds; json's writer and reader live with the json kind, text's with text, the step notation with the action. The test report's format is a real format kind (json, junit), and each writes its own report file.
- **10e: one form of computed value.** A value computed on each read (`%Now%`, `%!event%`) has one form.
- *Decisions:* the store is born ready; `code`, `clr` and `table` live under `app.type.item`; the app's identity is read through the same format that writes it; `%!app.type.list%` lists the types (Ingi); the format machinery moves to its owners, and test's format enum dissolves into format kinds (Ingi).

**11: one door per thing.** Each thing is reached one way.
- **11a: the actor is reached one way.** `%!app.actor.system%` and `%!app.actor.user%`; no second `app.System`/`app.User`. A shortcut, if ever wanted, is a goal that returns the value, not a second property (Ingi).
- **11b: tests reach the app through its own doors.** The C# tests start actions the way plang does, not through test-only helpers.
- **11c: nothing unreached goes unnoticed.** The builder warns about goals nothing calls; the test report shows what the tests reached.

**12: mistakes you can catch precisely.** A programmer's mistake is an error with its own key (`on error key "CannotSet"`), never a generic crash.
- **12a: the exception pass.** Every `throw` in the code this branch touched is either plang itself broken, or becomes an error in the result.
- **12b: born knowing.** A goal knows where it was loaded from when it's born, not stamped on after.

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

**Every stage**
- A rebuild of the builder's own goals is byte-identical, or its differences are explained.
- The C# suites are at the baseline.
