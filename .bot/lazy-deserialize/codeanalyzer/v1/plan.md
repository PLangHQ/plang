# codeanalyzer — lazy-deserialize — v1 plan

## Task
First codeanalyzer pass over the `lazy-deserialize` feature. Coder is at v3 and reports
GREEN (272/272 goal, 4021/0 C#). Verify, then run the five-pass analysis over the
feature's C# surface.

## Scope decision
The diff vs `runtime2` base (`d96ec269f`) is 414 .cs files / ~11.5k insertions. Reviewing
every file at depth is not the right use of the pass. The lazy-deserialize *mechanism* and
the *highest-risk recent commits* (binding-clone, wire-shape reconstruction, goal-call
param clone — the v3 "substantive work") are where regressions hide. Deep-read set:

- `PLang/app/data/this.cs` — lazy `_raw`/`Value` materialize, As<T> resolution, clone family
- `PLang/app/data/this.Navigation.cs` — navigate-into-string error, lazy materialize seam
- `PLang/app/data/Wire.cs` — lazy Read/Write, hash-excludes-name, sign-if-missing
- `PLang/app/module/variable/set.cs` — the binding site (Run() ~190 lines)
- `PLang/app/module/list/add.cs` — shallow-clone-into-list
- `PLang/app/type/reader/this.cs` — reader registry (read mirror of renderer)
- `PLang/app/type/list/Conversion.cs` — wire-shape reconstruction + OwnerOf hook dispatch
- `PLang/app/goal/GoalCall.cs` — Convert/ResolveParameter

Mechanical bans (System.IO, Console, OBP #9 courier-into-.Value) grepped across the whole
changed production surface. Deterministic baseline rebuilt clean and both suites run.

## Findings → `v1/report.md`. Verdict → `v1/verdict.json`.
Not blocked.
