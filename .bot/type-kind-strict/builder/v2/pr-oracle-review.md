# v2 — Review of committed builder `.pr` (the proposed oracle)

Ingi asked me to read the committed builder `.pr` files before trusting them as the
self-build oracle. Verdict: **mostly correct, but ≥2 frozen mis-maps** — so the git `.pr`
is NOT a clean oracle as-is. It needs hand-correction first.

Scanned every `os/system/builder/**/.build/*.pr`, step text → mapped `module.action`.

## Confirmed frozen bugs

### 1. `BuildGoal/.build/llmfixer.pr` — `LlmFixer[0]` is `event.on`, should be `goal.call`
```
text: call /system/builder/EmitBuildEvent kind="planner-validation-failed", error=...
got : event.on        WRONG
want: goal.call
```
Leading verb is `call` → always `goal.call` (Plan.llm 49–67 exists to prevent exactly
this). The *identical* `call …/EmitBuildEvent` pattern maps correctly EVERYWHERE else
(BuildGoal/start[0], RefineActions[5], FixValidation[4], HandleStepFailure[0], all of
EmitSummary). Same construct, different result = the flakiness, frozen in. Also note this
mis-map **breaks the planner-failure recovery path** in the running builder.

### 2. `.build/build.pr` — `Build[10]` carries a hallucinated `variable.set` peer
```
text  : save %traceGoals% to file '/.build/traces/%!trace.id%/manifest.json'   (no "write to")
formal: file.save(Path=..., Value=%traceGoals%) , variable.set(Name=%path%, Value=%!data%)
```
The `variable.set(Name=%path%, Value=%!data%)` is invented (no capture clause) and
**clobbers `%path%`**, the build root set in Build[0]. This is the
`%!data%`-into-an-unrequested-slot failure Compile.llm's `%!data%` section warns about.

### 3. `.build/build.pr` — `Build[2]` variable.set mis-targets `Name`/`Value`
```
text  : set default %!build.summary% = true
got   : variable.set(Name=%default%, Value=%!build.summary%, Type=bool, AsDefault=true)   WRONG
want  : variable.set(Name=%!build.summary%, Value=true, AsDefault=true)
```
The variable name `%!build.summary%` was shoved into `Value`; a bogus `%default%` became
`Name`. Steps [0] (`%path%`) and [1] (`%!build.cache%`) use the identical `set default %x%
= true` construct and map correctly — [2] is the odd one out. The word "default" in the
step text leaked into the value/name binding. **Found only on the second, full pass.**

> **Method note:** my first pass used pattern-specific greps (event.on / setDefault /
> file.save+call) and declared "everything else correct" — which MISSED bug #3. A
> per-step Name/Value/GoalName dump caught it. Lesson: review the oracle step-by-step,
> not by grepping for known-bad shapes.

## After fixing all three — the rest is clean
Full per-step pass (text vs module.action AND key params Name/Value/GoalName/Left/Right)
across all 11 builder `.pr`:
- `goal.call | error.handle` for `call X, on error call Y` (plan[5], start[3], start[7], BuildStep Compile[10], [14]).
- `loop.foreach, goal.call` for foreach+call; `condition.if, goal.call` for if+call (all of EmitSummary, BuildStep/validate).
- `llm.query, variable.set(%!data%)` for `llm.query …, write to %x%`.
- `builder.*` actions, `math.subtract/divide + capture`, `file.save`, `list.add`, `channel.set` — all correct targets.
- All 11 files parse as valid JSON after the patches.

**Conclusion: with bugs #1–#3 fixed, the committed builder `.pr` is a trustworthy oracle.**

## Implications
1. **Oracle must be hand-corrected before use.** Fix #1 and #2 in the `.pr` as a deliberate
   bootstrap patch (allowed per building-the-builder.md), producing a trusted reference set.
   Then "self-build reproduces the reference mapping" is a valid gate.
2. **These two are concrete prompt targets** for levers 1/3 — the real fix is making the
   prompt reliably map `call X`→goal.call and stop stubbing `%!data%`, so a rebuild keeps
   reproducing the corrected reference instead of re-freezing the bug.
3. The fact that the "stable" committed builder already contains a broken recovery path
   (#1) may itself contribute to self-build instability.
