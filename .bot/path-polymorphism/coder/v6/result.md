# Coder v6 — result

Responds to `.bot/path-polymorphism/builder/report.md`. The builder bot has low
C# understanding (per Ingi) — every claim was validated against code/evidence
before acting.

## Done & verified

### Class 1a — slash-qualified `goal.call` names  *(REAL bug — fixed)*

`GoalCall.GetGoalAsync` step 3 flattened `BuildGoal/Start` to
`.build/buildgoal/start.pr`; the file is `BuildGoal/.build/start.pr` (`.build`
sits *inside* the folder). Fix: slash-qualified names resolve as
`{folder}/.build/{leaf}.pr`, walking the caller's ancestor folders (the target
folder can be a sibling), then root/context. `GoalCall.LoadFromFile` leaf-matches
a slash-qualified `Name` against the loaded goal's own (unqualified) `Name` so a
pre-resolved `prPath` + `BuildGoal/Start` no longer 404s.

**Verified:** self-rebuild — all 3 slash-qualified calls (`BuildGoal/Start`,
`BuildStep/Start` ×2) now resolve with correct `prPath`; zero slash failures.

### Class 1b — bare names nested in `error.handle.Actions`  *(NOT a bug — no change)*

`error.handle.RunRecovery` stamps the enclosing step onto each recovery action
(`handle.cs:166-174`), so nested bare-name `goal.call`s resolve at dispatch
normally. `prPath:null` on them is benign. The builder bot's "recurse into
list<action>" fixes a non-problem — skipped (Ingi confirmed). The 6 remaining
`prPath:null` entries in a rebuild are all this benign class.

### Side bug — inverted `File.Exists`  *(fixed)*

`builder/this.cs:110` fired the "no app found" branch when the marker *existed*.
Flipped to `!File.Exists`. **Verified:** self-rebuild now runs without the
`--app={"create":true}` workaround.

## Re-diagnosed — needs an Ingi decision (Class 2)

The builder bot's Class 2 ("LLM drops the `Include` param of `builder.actions`")
is **wrong**. `builder.actions` (`GetActions`) has **no parameters at all** —
`Builder.Actions` returns the full unfiltered catalog (`Modules.Describe()`).
The `include=%planStep.actions%` in `BuildStep/Start.goal:16` is **dead text** —
there is no such parameter. The real defect: the LLM non-deterministically
mis-compiles `write to %x%` steps. Two distinct shapes seen:

- snapshot run: `Compile:s1` (`builder.actions`) trailing `variable.set` got
  `Value=%planStep.actions%` instead of `%!data%`.
- verification run: `Compile:s0` (`builder.validateStepActions … write to
  %planStep.actions%`) compiled to **zero actions**.

A structural validator is the right defense — but `builder.validate` only
receives the action list, **not the step text**, so it cannot tell a step was a
`write to %x%` step. The fix needs the step text reachable. Open question for
Ingi — see summary / the message thread.

## Class 3 — unvalidated

Not present in the snapshot; intermittent. Couldn't reproduce. Pending the same
validator-location decision as Class 2.

## Files (Class 1 + inverted)

- `PLang/app/goals/goal/GoalCall.cs` — `GetGoalAsync` slash resolution + `LoadFromFile` leaf-match
- `PLang/app/modules/builder/code/Default.cs` — `ResolveGoalCallsInAction` comment (sets `PrPath`, no longer mutates `Name`)
- `PLang/app/modules/builder/this.cs` — inverted `File.Exists`
