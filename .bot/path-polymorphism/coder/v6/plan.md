# Coder v6 — plan

Responds to the builder bot's `.bot/path-polymorphism/builder/report.md` (3
classes of self-rebuild regressions). Evidence base: the raw broken `.pr`
snapshot at `.bot/path-polymorphism/builder/rebuilt-pr/`.

## Validation (done before planning)

The builder bot has low C# understanding — validated each claim against code:

- **Class 1a — slash-qualified `goal.call` names: REAL.** `GoalCall.GetGoalAsync`
  step 3 derives `.build/buildgoal/start.pr` for `BuildGoal/Start`; the file is
  `BuildGoal/.build/start.pr`. Deterministic; hit live earlier this session.
- **Class 1b — bare names nested in `error.handle.Actions`: NOT a bug.**
  `error.handle.RunRecovery` (`handle.cs:166-174`) stamps the enclosing step
  onto each recovery action, so nested bare-name `goal.call`s resolve at
  dispatch like any other. `prPath:null` on them is benign — the audit
  over-flags. **Skip** the "recurse into list<action>" fix (Ingi confirmed).
- **Class 2 — `builder.actions … write to %x%` mis-compile: REAL, LLM-caused.**
  Snapshot confirms (`tail Value='%planStep.actions%'`). Validator defense.
- **Class 3 — not in snapshot, intermittent.** Cheap validator rule; include.
- **Side bug: `builder/this.cs:110`** inverted `File.Exists` — confirmed.

## Baseline

C# 2882/2882; PLang `--test` 203/203/0 stale; build clean. HEAD `d34064188`.

## Changes

1. **`PLang/app/goals/goal/GoalCall.cs` — `GetGoalAsync`.** Slash-qualified
   name handling: split `Folder/Leaf`, resolve `{folder}/.build/{leaf}.pr`
   walking the caller's ancestor folders, then root-relative. Bare-name path
   unchanged.
2. **`PLang/app/modules/builder/code/Default.cs` — `ResolveGoalCallsInAction`.**
   When `PrPath` is set on a slash-named call, strip `Name` to the leaf so the
   dispatcher's literal name-match against the loaded `.pr` succeeds.
3. **`PLang/app/modules/builder/code/Default.cs` — `Validate`.** Two structural
   checks, surfacing through `LlmFixer`:
   - Class 2: a step whose text contains `write to %x%` must end in
     `variable.set` with `Name=%x%` and `Value=%!data%`.
   - Class 3: `variable.set` with `Type=json` and a string `Value` containing
     a `%var%` reference is rejected (safe shape is a native list/dict).
4. **`PLang/app/modules/builder/this.cs:110`** — `File.Exists` → `!File.Exists`.

## Verification

Build + both suites. Then a `cache:false` self-rebuild of the builder and the
audit script over the output — Class 1 (deterministic) must show zero
slash/prPath issues.
