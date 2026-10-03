# Issue 41 — `call X in %window%` builds goal.call, not window.callGoal

From the os bot, via the architect. On plang-os-stable (window module lives only there) a
`cache:"skip"` rebuild of PlangOS's Screen.goal mis-built two plain-word steps.

## Diagnosis (plang-os-stable 0920ac82b, clean binary, faithful probes in /shared/plangos)

Each step isolated, 5 fresh builds, `--build={"cache":"skip"}`, jq-counted step 0.

- `call ShowFiles files=%files% in %browser.desktop%` (ShowFiles reachable): window.callGoal
  **0/5** — 3 SAVED as goal.call (wrong), 2 NO-SAVE (`goal.call has no property Window`).
- same step, ShowFiles NOT reachable (control for d2aa1358d's reachable-goals offer):
  window.callGoal **1/5**. No meaningful difference → the offers batch is **not** the lever.
- `open window 'start.html' in %browser%, write to %writer%`: window.open + Browser kept
  **5/5**. The reported Browser-drop did **not** reproduce in isolation — context-sensitive;
  needs the real Screen.goal. (os bot's guess: the window.navigate example just before it.)

**Root cause (confirmed against the model's input, `--debug llm.system+user+response`):**
the decider's stage-1 state prompt already carries the exact counter-example
(`call ShowFiles … → window.callGoal, not goal.call: in %window% puts the goal in the
window's page`), yet goal.call still wins. goal.call is a stage-1 **common action asked by
name** (`decider.json` common set); its yes/no fires certain on any `call <Name>` step, and a
near-certain common action is marked Certain with its module skipping stage 2
(`pick/list/this.cs:297` Mark.Certain, `:522` From.Common). window.callGoal is an ordinary
window-module action, not common — it can't displace the certain goal.call. The os bot's
trace: module question = window (0.70), goal.call yes/no = 0.94 (enforced over the module
answer).

So: **teaching gap in the decider's yes/no, not the writer's examples.** goal.call's
common-action question had a `true` side (`call SendMail to=%x%`, …) and no counter-case for
`call X in %window%`.

## Fix (teaching lever — the architect's first choice)

`os/system/builder/llm/decider.json`, goal.call common-action `false` side, add a generic
counter-case (placeholders, not the literal ShowFiles):

> "the step calls no goal now; a call that says WHERE the goal runs — `call X in %window%`,
> `… in %browser%` — runs a page's own function (window.callGoal), not a goal of this app; …"

**Counts after (plang-os-stable, clean binary, cache:skip, jq):**
- `call ShowFiles … in %browser.desktop%` → window.callGoal **5/5** (was 0/5).
- guard plain `call Finalize` → goal.call **5/5**.
- guard 32(b) `call goal Render module=%files%` → goal.call **5/5**.

Re-validated on app-systems (clean rebuild): `call Finalize`→goal.call, `call goal Render
module=%files%`→goal.call, `compare %a% > %b%, write to %isGreater%`→condition.compare +
variable.set (Operator=">"). No regression. Edit applied on app-systems (decider.json is
branch-independent prompt text; plang-os-stable left pristine).

## Also fixed — `condition/compare.notes.md` dangling reference

Line 2 said "see if.notes' table"; notes render only for the picked action, so a compare step
never receives if.notes. Inlined the operator table into compare.notes.md (self-contained).

## Structural note (for architect/Ingi, not blocking — teaching fixed it)

The module question named window yet goal.call's common-action yes/no was enforced over it.
The clean structural option: the pick should not skip stage 2 (nor mark a common action
Certain) when the module question names a different module. The writer also reaches for
`goal.call(…, Window=…)` — a signal goal.call wants to own where-it-runs.
