# app-systems

> Draft by the architect; it goes at `test/plan/app-systems/start.md`, beside `start.goal`. The coder places it and deletes `checks.md` (its content is the "What tests can't show" section below).

## Why

plang's own structure should be as navigable and as honest as a program's data. Every `%!app.X%` is a real object of type X, and three things name the same thing: the plang path (`%!app.goal%`), the C# class (`app.goal.@this`) and the file (`app/goal/this.cs`). What a thing does lives on that thing, not in helpers around it. For a plang developer, that means you can look at, and hook into, anything the app has: its types, goals, actors, modules, settings and events.

## Done

Stages 1–8g, and 8h's code parts. See [done.list](done.list).

## What's left, and what each gives the developer

Each stage is a goal in [start.goal](start.goal); each behaviour there is a comment with the test that proves it.

**8h: the builder builds itself, and plain words work.** The builder is plang code, and it can now rebuild its own goals. Plain phrasings compile to what they mean: "retry once", "if done, return x", named render arguments. This is also the first time the decider is measured on the prompts the real builder renders.
- *Decisions:* no human language is read by deterministic code; a number the step writes in words is confirmed by the decider, as a visible builder step (ConfirmNumbers). The prefill offers only what is certain: a lone if takes its body, and optional properties get no hole.

**9: every value is born through its type.** A developer can hook the birth of any value: `after file create, call LoadFixture`, e.g. for tests with fake files. Inside, every module action becomes a one-line door to the object that does the work.
- *Decisions:* `type.Create` is the one async birth door and fires `on.create`; `Make` is the internal build (`on.create` fires once per value). A birth is a value coming into the program from outside the type system.

**10: the app knows its types.** `%!app.list%` lists them; a plugin's types join; one name can't be taken twice; the app's facts are plang values (`created` is a datetime); typed lists hold their type; the app's name is its setting.
- *Decisions:* the store is born ready; `code`, `clr` and `table` live under `app.type.item`; the app's identity is read through the same format that writes it.

**11: one door per thing.** `%!app.actor.system%`, not a second `%!app.system%`. The builder warns you about goals nothing calls, and the C# tests reach the app through its own doors.

**12: mistakes you can catch precisely.** A programmer's mistake is an error with its own key (`on error key "CannotSet"`), never a generic crash.

## How it's proven

- [start.goal](start.goal) runs the plan's tests; each lives under this folder at the path of what it tests.
- At the end of each stage, that stage's tests are built and run. A stage is accepted when its tests are green, the checks below hold, and the architect's OBP review is clean.
- **Every test must fail without its change:** once green, the change is reverted briefly and each test is confirmed red. (Two early tests passed while testing nothing; this is the guard.)
- Only the plan's tests are built; the rest of the test tree isn't rebuilt by this plan.

## What tests can't show

These are checks, run and reported by the coder.

**8h**
- The builder rebuilds its own goals: with the target `.pr` files moved aside and the cache off, `BuildGoal/Start.goal` and `error/show.goal` build with no step refused, and no `.pr` keeps the old nested-modifier format.
- The decider renders its common questions through the real path (`file.read` → `ui.render`), pinned by `RenderTests.Render_AReadJsonFile_IteratesItsDictAndList`.
- The eval: the pick golden is regenerated, and stage 1 scores at least the baseline; the numbers go in the decision log.

**9**
- Every existing C# test passes; each handler in the stage 9 worklist is a one-line hand-over, and the OBP review of each module is clean.
- The births moved onto `type.Create` leave no caller that drops its `ValueTask` (a temporary `[Obsolete]` on the door lists none).

**11**
- Every C# test passes with `TestApp` and `TestAction` deleted, and `App.Run<TAction>` retired.
- Building a folder with an unreached goal `Unused` emits a warning naming it.

**Every stage**
- A rebuild of the builder's own goals is byte-identical, or its differences are explained.
- The C# suites are at the baseline.
