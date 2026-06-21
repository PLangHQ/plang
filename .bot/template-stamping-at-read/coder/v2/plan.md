# v2 — Migrate PLang.Tests/Modules executor tests to the real read path

## Goal
Route hand-built goal/step EXECUTORS in PLang.Tests/Modules through the established
`TestApp.Create` + `Make.Goal`/`RealGoalLoad.ViaChannel` pattern so params born-type
like a `.pr` off disk. Copy the proven pattern (OrchestrateBranchCoverageTests,
AfterActionPayloadTests). Design nothing new.

## Migrate (executor: RunGoalAsync or step(s).RunAsync on hand-built goal/step)
- loop/ForeachTests.cs — 4 tests use `step.RunAsync` (Orchestrates, SetsItemVariable,
  IteratesDictionary, KeyIsString). The 3 `action.RunAsync` tests (Empty/Null/Cancellation)
  are handler-direct → leave.
- loop/ForeachErrorPropagationTests.cs — all 3 use `step.RunAsync`.
- loop/ForeachStringNotIterableTests.cs — all 3 use `step.RunAsync`.
- condition/StepsSubStepTests.cs — uses `steps.RunAsync` (GoalSteps). HasIndentedChildren
  test does not execute → leave.
- condition/IfErrorOrchestrationTests.cs — 2 tests use `step.RunAsync`.
- file/FileHandlerTests.cs — 3 tests use `_app.RunGoalAsync` (Read_UnregisteredScheme,
  Integration_FileExists, Integration_FileNotExists). Rest are `.Run()` handler-direct → leave.

## Leave alone (with reason)
- variable/SetTypeInferenceTests.cs, variable/settests.cs — all `action.RunAsync`/ValidateBuild (handler/builder isolation).
- ui/RenderTests.cs — all `_provider.Render(action)`; goals are callGoal registry fixtures, never RunGoalAsync.
- llm/QueryCallbackTests.cs — all `action.Run()` provider-direct.
- modifier/* (ErrorHandle, ModifierFold, TimeoutAfter, CacheWrap, Group, Registry) —
  all `action.RunAsync(Ctx)` / `modifiers.RunAsync(func)`; modifier-fold isolation, not RunGoalAsync.

## Pattern
- `new app.@this(path)` → `TestApp.Create(path)`.
- runner step → `await RealGoalLoad.ViaChannel(_app, Make.Goal(name, Make.Step(text, indent?, actions...)))`;
  capture `goal.Steps.First()` (or `goal.Steps`) and run it / `_app.RunGoalAsync(goal, ctx)`.
- var-name slots (variable.set Name, loop.foreach itemname/keyname) → `Make.Param(name, v, "variable")`.
- target/registry goals with no params stay `new Goal`.

## Verify
Build PLang.Tests/Modules clean, run suite → failed: 0. Revert+report any file that
exposes a real read-path gap.
