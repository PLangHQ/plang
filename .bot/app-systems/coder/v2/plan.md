# coder v2 — stage 1: Run → Start

The architect's answers to the v1 review (plang-40, 2026-09-27) settle stage 1; `callback.run` and
the v0.1 .dll/.pdb files wait for Ingi and are left alone.

## One verb end to end
`action.Start(context)` → `call.Start(handler, context)` → `ICodeGenerated.Start()` (the generated
dispatcher, an explicit interface member) → the handler's own `Start()`. The explicit member lets
the dispatcher and the handler's method share the name in one partial class; inside it, `Start()`
is the handler's.

## The renames
1. Generator (`PLang.Generators/Emission/Action/this.cs`): `Execute()` → explicit `ICodeGenerated.Start()`
   calling `Start()`; `ICodeGenerated.Execute` → `Start`; `call.ExecuteAsync` → `call.Start`.
2. Runtime objects: goal, step, step list, action, action list `Run(context)` → `Start(context)`.
3. All 125 handlers' `Run()` → `Start()` (the deferred five included: the generator emits one name).
   timer's class `Start` → `start` (a member can't share its class's name).
4. `Executor.Run`, `goal/setup RunAsync`, `build/this.cs RunAsync` → `Start`. `App.Run<…>` stays (stage 11);
   the event bindings' `Run` stays (stage 8).
5. plang actions: `environment.run` → `environment.start`, `test.run` → `test.start`, with their
   teaching files; `list.range`'s `Start` property renamed; installed `.pr` rows naming them follow.
6. `action.Return`'s `GetMethod("Run")` → `"Start"`; test's stopwatch `Start()` gets another name.
7. Tests: the 152 files, 10 `GetMethod("Run")`, 14 test doubles.

Renames go through the compiler's positions and the Edit tool. Six suites green against
`v1/baseline-tests.md`; prompt twins byte-equal or one eval run (builder-visible: the two actions,
range's property).
