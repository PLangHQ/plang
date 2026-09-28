# app-systems: checks that can't be a .test.goal

The plan's validation is `start.goal` beside this file: its tests (under `test/plan/app-systems/`, each at the path of what it tests) plus these checks. A stage is done when its own tests are built and green, these checks hold, and its OBP review is clean.

## Stage 8h

- **The builder rebuilds its own goals.** With the target `.pr` files moved aside and the LLM cache off, `plang build` of `os/system/builder/BuildGoal/Start.goal` and `os/system/error/show.goal` refuses no step. Afterwards neither `BuildGoal/.build/start.pr` nor `error/.build/show.pr` has a `"modifier"` key, and the TRANSITIONAL reader (`goal/step/action/serializer/Reader.cs`) is deleted.
- **The decider really asks stage 1's common questions.** A C# render test goes through `file.read` → `ui.render` (not an injected dict; `RenderTests.Render_AReadJsonFile_IteratesItsDictAndList`), and the rendered `decider1` lists every `decider.json` common action.
- **The eval measures the real prompts:** the pick golden is regenerated, and stage 1 with the common questions scores at least the baseline. Record the numbers in the decision log.

## Stage 9

- Every existing C# test passes.
- Each handler in `plan/stage-9-worklist.md` is a one-line hand-over to its owner, and the architect's OBP review of each module is clean.
- The births move onto `type.Create`: a temporary `[Obsolete]` on the door lists no remaining caller that drops its `ValueTask` (the 8e trap).

## Stage 11

- Every C# test passes with `TestApp` and `TestAction` deleted, and `App.Run<TAction>` retired.
- The builder warns about a goal no public goal reaches: building a folder with an unreached goal `Unused` emits a build warning naming it.

## Every stage

- At the stage's end, that stage's tests in `test/plan/app-systems/` (its sub-goal in `start.goal`) are built and run, and each is green, or red with why: a real bug, or a spec that doesn't compile to what it means. Only the plan's tests are built; the rest of the test tree stays unbuilt ("no .pr").
- The builder check: a rebuild of the builder's own goals is byte-identical, or its differences are explained.
- The C# suites are at the baseline (known failures listed in the decision log).
