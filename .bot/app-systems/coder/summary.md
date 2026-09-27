# coder — app-systems

**Version:** v2

## What this is
app-systems makes every `app.X` the type X (a generic `type<X>` over its concept's `list<X>`), so the
plang path, the C# path and the file path agree. The architect's plan
(`.bot/app-systems/architect/plan.md`) lays it out in 13 stages. v1 reviewed the plan against the
code and did stage 0 (the base); v2 is stage 1: `Start` is the entry verb of everything that runs.

## What was done
- **v1 — review and stage 0** (`v1/result.md`, `v1/baseline-tests.md`): the plan's claims for stages
  1, 3, 4 checked (answers from the architect folded into plan.md). Compile re-recorded (python twin
  now refuses a missing entry per step, as C# does); 552 v0.1 `NN. stepname.pr` and, on Ingi's ruling,
  98 v0.1 `.dll`/`.pdb` deleted under `os/`.
- **v2 — stage 1, Run → Start** (`8c135f519`):
  - One verb end to end: `action.Start(context)` → `call.Start(handler, context)` →
    `ICodeGenerated.Start()` (the generated dispatcher, an explicit interface member) → the handler's
    own `Start()`. Generator: `PLang.Generators/Emission/Action/this.cs` (`EmitExecute`).
  - Renamed: 125 handlers; goal/step/step list/action/action list; `call.ExecuteAsync`;
    `ICodeGenerated.Execute`; `app.RunGoalAsync` → `app.Start(goal, context)`; setup's, the builder's
    `RunAsync` and `Executor.Run`. Kept: `App.Run<TAction>` (stage 11), the event bindings' `Run` (stage 8).
  - timer's class `Start` → `start`; test's stopwatch `Start()` → `Begin()`; `list.range`'s
    `Start`/`End` → `From`/`To`.
  - plang: `environment.run`, `test.run`, `callback.run` → `.start` with their teaching files;
    `os/system/test.goal` rebuilt by the installed builder.
  - The python twin reads `Start()`'s signature (`tools/decider/build_pr.py`); `pick_golden.json` regenerated.
  - Six suites at baseline; `plang --test` unchanged (7 pass, 317 stale).
- **Next:** stage 3 (one set of types), after the architect's go.

## Code example
The generated dispatcher and the handler's own method share the name (emitted by the generator):
```csharp
async Task<global::app.data.@this> global::app.module.ICodeGenerated.Start()
{
    if (await __Live() is { } __missing) return global::app.data.@this.FromError(__missing);
    try { return await Start(); }   // the handler's own public Start()
    ...
}
```
