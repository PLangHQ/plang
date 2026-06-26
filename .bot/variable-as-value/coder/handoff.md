# Handoff — variable-as-value: unblock `plang build`

## THE GOAL (don't lose it again)
This branch exists to **make `plang build` work** — it couldn't build any goal because a
full-match `%x%` was coerced to its declared type at `.pr` load → null. Done-state =
`plang build` runs + `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green.
NOT "green C# unit slices" (that's a side-effect). Plan: `.bot/variable-as-value/coder/plan.md`.

## DONE this session (committed + pushed, HEAD = 95ab8771a)
- **The `%msg%` self-reference blocker is FIXED** (the documented "Root unknown" in
  output-redesign.md). `render … write to %msg%` → `set %msg% = %!data%` was storing a
  reference to the reused infra NAME `!data`, so it cycled. Fix: `variable.@this` now
  reports `IsRef` (a variable value IS a reference to its name), and `set`'s no-type path
  calls `Value.AsCanonical()` — the NAME-hop resolve (`%!data%` → the current Data instance,
  value NOT computed; lazy) — so the target binds to the instance, not the rebinding name.
  Commits: `91b5b225f` (self-ref), `e661dc076` (set errors on unset ref, infra `%!` excepted),
  `95ab8771a` (nav: unset index → `IndexNotSet`, self-diagnosing).
- Earlier chain (settings/serializer + nav, all green-and-pushed): `Get<T>` deferred face,
  Identity.Output, dict entry-key re-stamp, source/file/text.Navigate, interpolation
  `variable.Value()`, convert JSON-gate. Suite: Modules 124→12, Runtime 49→2, Types 24→2,
  Wire 14→5, **Data 18→0**. Foreach tests fixed.

## THE NEXT BLOCKER (start here)
The builder now runs the LLM planner (LLM IS available here — produces a real 4-step plan)
and reaches BuildStep, then dies:
```
BuildGoal/Start.goal:  foreach %plan.steps%, call BuildStep/Start planStep=%item%
BuildStep/Start.goal:6: set %step% = %goal.Steps[planStep.index]%
  → IndexNotSet(400): the index %planStep.index% is not set (resolved to null)
```
`plan.steps` items are well-formed (`{index:0, actions:[...], confidence}`); foreach +
goal-call work in unit tests — so **`%planStep%` isn't resolving in BuildStep's scope**.
Narrowed to ONE of:
  (a) the goal-call **`planStep=%item%` injection** into the called goal (App.RunGoalAsync
      param injection — see memory feedback_app_architecture_patterns), or
  (b) **`planStep.index` navigation** on the injected dict.
Next: `--debug` watch `planStep`/`item` across the foreach→BuildStep boundary; confirm
whether `%planStep%` is `(undefined)` in BuildStep (→ injection bug) or set-but-nav-misses
(→ nav bug). I could NOT get the watched value to print in the BuildStep BEFORE block —
try `{"level":"action"}` and watch the goal-call action's param resolution.

## REPRO + DEBUG (use these, not C# dumps)
```bash
dotnet build PlangConsole -c Debug         # rebuild the binary (NOT auto-rebuilt)
cd Tests
../PlangConsole/bin/Debug/net10.0/plang '--build={"files":["Scratch/Repro.goal"]}' \
  '--debug={"variables":["planStep","item"],"level":"action","maxLength":400}'  2>&1 | less
```
`Tests/Scratch/Repro.goal` is the minimal repro (set + nav). Builder goals:
`os/system/builder/{Build,BuildGoal/Start,BuildStep/Start}.goal`.

## PROCESS (Ingi's corrections — follow these)
- On a plang/builder failure: **improve the error message first** (self-diagnosing, keepable)
  and **use `plang --debug={...}`** — do NOT add `Console.Error` dumps to C#. (memory:
  feedback_debug_plang_failures). The `IndexNotSet` message this session is an example.
- Show code before changing C#; commit green chunks and push.

## Build/test
`./dev.sh build` (C#), `./dev.sh full` (all slices), `dotnet build PlangConsole` (the plang exe).
Remaining slice failures (all pre-existing, NOT regressions): Modules 12 (dict→Step `.pr`
type-object cluster + Settings-MissingKey AskError), Runtime 2, Types 2, Wire 5 — the
`data.Output` write-path migration tail (output-redesign.md P4/P7); separate from the builder.
