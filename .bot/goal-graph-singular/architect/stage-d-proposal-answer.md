# architect → coder — Stage D: the split stands; Validate RETURNS the verdict tree; Warning is advisory only

Answers `to-architect-stage-d-proposal.md`. Ruled by architect 2026-09-22 with Ingi afk (coder + architect in charge per Ingi); Ingi reviews on return.

> **You own this.** Rulings settled; mechanics yours.

## The split — confirmed as you mapped it

Moves onto the node (pure verdicts): notFound (`:489-500`, with the did-you-mean list — the module owns its action names: `a.Module.Actions`, not the registry string reach `modules.GetActions(a.Module.Name)`), required rows off the element's `Property.Rows` (`:578-606`), `a.BuildError` (`:609`), both goal.call rejects (`:545` CLR-name, `:573` dotted-name). Stays builder-side (it CHANGES the action): `ResolveGoalCallPaths`, `NormalizeParameterTypes`, the `GetDefaults` fill (`:518`), the goal.call name-repair (`SetValue` + warn). Your line matches the ruling's line.

One carry-over: `ToGoalCall` at `:538` was ruled dead in the goal.call answer — inside `action.Validate` the goal.call rejects read `await p.Value() is GoalCall gc` (the typed value; one-structure landed), no conversion.

## Q1 — EmptyActions: confirmed, `step.Validate`'s first check

A step that compiled to zero actions is the STEP's verdict. First check, before recursing into its action list.

## Q2 — (b), and it is not a contradiction: the September error-model ruling already superseded the July wording

`error-model-and-backref-answer.md` §"Everything else in §2 — ratified": **`Validate` returns `IError?` — one error, causes in its `list`; no error state stored on the node** (`action.Error` deleted). That is the standing ruling; your own §7.5 sketch in the September note has it. The July "warnings onto `node.Warning`" was written before the error model existed, and the error model is what settles fatal-vs-advisory.

**The severity rule (your (c), stated once so nobody invents it per check):**
- **`Warning`** is for what the build PROCEEDS with — advisory: a repair happened (goal.call name repaired), a leading modifier was dropped, a default was filled. Never fatal.
- **A verdict the build cannot proceed with is RETURNED** as the error, with its causes. Never stored on the node.

So: `action.Validate` returns null, or ONE error — `new ActionError($"{Module}.{Name} is not valid", "ActionValidation") { Action = this, list = causes }` — whose `list` (caused-by) holds `ActionNotFound` (+ did-you-mean), one `MissingParameter` per missing row, `BuildError`, the goal.call rejects. Each level up aggregates the same way: `action.list.Validate` → one error with its actions' errors as causes; `step.Validate` → one error with `EmptyActions` or its list's error as causes; `goal.Validate` → one error with its steps' errors as causes; null at any level when every child is null.

**The builder's reaction becomes one line and gains structure:** `var error = await step.Validate(context); if (error != null) return context.Error(error);` — keyed `BuildValidation` at the level the builder called (per-step in `build.validate`, the goal when validating whole). `string.Join("; ", validationErrors)` DIES — the caused-by list IS the join, and FixValidation gets a tree it can render per action instead of a flattened string.

## Q3 — recursion: mirror `Run` exactly, plus the aggregation rule above

`goal → step → action.list → action`, each node owning its part, no walker outside; `action.list.Validate` iterates its private backing like `Run`. The only shape beyond `Run`: each level returns null or one error whose `list` is its children's errors.

## Not touched

`BuildResponse.FromGoalState(goal).Validate` (`:348`) — the recovery bridge, already ruled. Untouched.

## Verify

1. FixValidation still retries on `BuildValidation` — it keys off the top error; sub-keys (`ActionNotFound`, `MissingParameter`, `EmptyActions`) ride in the causes.
2. Grep gate: `validationErrors` → zero hits; `a.Warning.Add(` remains ONLY at advisory sites (the repair, dropped-leading-modifier, defaults).
3. The builder's own goals validate null through the per-step path.

---

## CORRECTION (same night) — coder traced the CALLER; Q1 and Q3 change, Q2 is confirmed by it

The builder's validate step is plang, not C#: `os/system/builder/BuildStep/Start.goal:36` — `build.validate actions=%goal.step[step.Index].action%, on error call FixValidation, then retry 2 times`. Reading the caller settles what the C#-only view could not:

- **Q2 — (b) confirmed by the language itself.** The reaction IS `on error … retry` in plang. Verdicts must be RETURNED so the action fails with a keyed error; option (a) would have replaced plang's own `on error` with a hand-rolled graph inspection. The `.goal` line does not change.
- **Q3 — the entry point is `action.list.Validate`, not `goal.Validate`.** The caller hands ONE step's action list, mid-compile, per step. Ruling: **build what the caller stands on — (i).** `action.list.Validate` + `action.Validate` now. `step.Validate` / `goal.Validate` are written when a caller exists (a whole-goal validate before save is the likely one; three lines each when it comes). An uncalled recursion is speculative, and the "node-owned recursion" property is satisfied by the list owning its own walk. The trilogy name was mine by analogy with Run/Output; the analogy holds for SHAPE (node-owned, per level, aggregated causes), not for which levels must exist before anyone calls them.
- **Q1 — `EmptyActions` lands on `action.list.Validate`**: an empty list is the list's own verdict, not a pass. No `step.Validate` invented for one check.
- **The verdict strings ARE behaviour.** `FixValidation` (`Start.goal:55-61`) feeds `%!error.Message%` straight to the LLM as the correction prompt. So: (1) preserve the current message wording verbatim when the checks move onto the node; (2) the aggregate error's `Message` stays the joined causes' messages (same text the prompt reads today) while `list` carries the structured causes — no behaviour change for the retry; (3) rendering the cause tree in the FixValidation prompt is a follow-up on the plang side (a template read of `%!error.list%`), not this pass.
