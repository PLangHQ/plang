# variable-as-value — follow-up after "loud value door" (4705483cf)

**Validated at:** `4705483cf` (clean C# rebuild, 0 errors).
**Predecessor:** `builder-blocked-report.md` (Regression 1 + 2).
**Verdict:** no forward progress on *building*. The commit is **diagnostic, not a fix** —
it makes Regression 1 loud (good) but introduces a **new build-time regression** (R3)
that breaks building *any* goal which references a variable in a parameter.

## What the commit did
`variable.Value` (the value door) now throws `VariableNotFoundException` when a
reference resolves to nothing, instead of nulling. Conditions opt out via a tolerant
path. Per the commit message it "surfaces Regression 1 loudly" — i.e. it does **not**
touch the compile path. The `set`/`get` `Name=null` root (Regression 1) is **still
unfixed** (diff only touches `variable/this.cs` + `condition/code/Default.cs`).

## Regression 3 (NEW) — loud door fires inside builder validation

**Repro** (from `Tests/`):
```
Scratch/Repro.goal:
  Repro
  - set %myrec% = {"id":"abc123"}
  - write out %myrec.id%

plang '--build={"files":["Scratch/Repro.goal"]}'
```
**Result:** build dies at `builder/BuildGoal/Start.goal:28` (`builder.goalsSave`):
```
VariableNotFoundException: Variable 'myrec' not found
  at app.module.builder.validateResponse.ValidateGoalState (validateResponse.cs:78)
  at ...Validate (validateResponse.cs)
```

### Root cause — resolve-before-skip ordering
`validateResponse.Validate` does a build-time convertibility check. It *intends* to skip
variable-reference parameters, but it **resolves first, skips second**:

```csharp
// validateResponse.cs:170-172
if (p.Type?.Name == null || (await p.Value()) == null) continue;          // ← throws here
if ((await p.Value()) is text refSv && refSv.StartsWith("%") && refSv.EndsWith("%")) continue;
if (ValidateResponseHelpers.IsActionRecord((await p.Value()))) continue;
```
`await p.Value()` fires the loud door on `%myrec.id%` — a runtime variable of the goal
being *built*, never in build scope — and throws before the `StartsWith("%")` skip can run.

The sibling code already does this correctly with the **non-resolving** `p.Peek()`:
```csharp
// code/Default.cs:927,947  (the right pattern)
var face = p.Peek() as text;
if (face != null && face.StartsWith("%") && face.EndsWith("%")) continue; // variable reference
```

### Suggested fix (small, C#)
In `validateResponse.cs:168-172`, gate on `p.Peek()` before any `await p.Value()`:
- detect/skip variable refs and nulls via `p.Peek()` (no door),
- only call `await p.Value()` once the parameter is known to be a concrete authored value.

Validation is **not value consumption** — same rationale the commit used to exempt
conditions. The build-time validator must never resolve a target goal's own runtime
variables.

### Blast radius
Not just `set`. **Any** goal whose parameters reference a variable (`write out %x%`,
`call X p=%y%`, …) now throws during `goalsSave` validation — i.e. essentially the whole
builder self-build, since system goals reference variables everywhere. No-variable goals
(`Hello` = `write out "hello world"`) still build + run fine. The commit's "zero new
failures" did not exercise a builder self-build over a variable-referencing goal.

## Still open
- **R1** (root) — `set`/`get` compile `Name=null`. Unfixed. Fix the compile so LHS
  `%var%` emits born-typed `Name [variable]` (ground truth: old-binary
  `os/system/builder/BuildGoal/.build/start.pr` → `variable.set Name=%buildStart% [variable]`).
- **R2** — `%!infra.member%` interpolated into a path string resolves null
  (`file/save.cs:15`). Re-verify after R1/R3.
- **R3** (this report) — resolve-before-skip in `validateResponse.cs:170-172`.

## Suggested order
1. R3 (unblocks building variable-referencing goals at all).
2. R1 (the actual compile correctness).
3. Rebuild builder (`cd os/system && plang build`), re-check R2.
4. `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

## Key files
- `PLang/app/module/builder/validateResponse.cs:170-172` (resolve-before-skip — R3)
- `PLang/app/module/builder/code/Default.cs:927,947` (correct `p.Peek()` pattern)
- `PLang/app/variable/this.cs:81` (loud value door)
- `PLang/app/module/variable/set.cs:97` (R1 born-typed guard / NRE)
- `PLang/app/module/file/save.cs:15` (R2)
