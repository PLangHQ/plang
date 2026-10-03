# builder — summary (plang-os-stable)

## Version
v1 (2026-10-03) — diagnosis-only session. Issue 41 regression after the `window.callGoal` →
`window.call` rename (decision 576, item 3; `86493dce1`).

## What this is
On `plang-os-stable`, `call Show text=%text% in %writer%` (and even `in %window%`) was building as
**goal.call** instead of **window.call** — the window variable absorbed as a plain argument. Two
actions now share the word "call" (`goal.call` and `window.call`). The architect asked me to measure,
check teaching, and bring the decider trace if a module-level pick is the cause.

## What was done (diagnosis only — nothing committed to source)
Measured on a fresh binary, cache:skip, in a scratch folder outside `test/` (`/shared/w41`).

- `call Show … in %writer%` → goal.call **0/5**; `call Show … in %window%` → goal.call **5/5**;
  `call Finalize` (guard) → goal.call 5/5. So it is **not** var-name specific — even literal
  `in %window%` failed.
- **Decider trace (untyped):** `window.call 0.98 (module window 0.56, goal 0.40) (possible),
  goal.call 0.93 => formal: goal.call(Name)`. The decider knows window.call fits (0.98 within the
  module) but the **module-choice stage** only reaches window 0.56 (< 0.90), so window.call is
  `(possible)` and the certain common-action goal.call owns the `=> formal:` starting line, which the
  writer follows.
- Teaching is already correct (decider.json carries the `in %window%`→window.call counter-case;
  window.call.examples say "not goal.call"). Strengthening the goal.call common-action false-side
  (imperative + general `in %<any window>%`) moved `in %window%` only **0→1/5** and `in %writer%`
  stayed 0/5 — a text counter-case can't lift the 0.56 module embedding. **Reverted** (no source change).
- **Typed probe** (the lever): born as a real window, the pick flips.
  ```
  TypedW
  - open window 'start.html' in %browser%, write to %writer%
  - call Show text=%text% in %writer%
  ```
  The walk types it (`=> types: %writer% window`) and `call Show … in %writer%` → **window.call 3/5**;
  the 2/5 misses are the writer choosing `goal.call(… Window=%writer%)` → *"goal.call has no property
  Window"* → StepsRefused. So the type helps (0→3/5) but doesn't settle it, because the **stage-1
  module question is step-text only and never sees `%writer% : window`**.

## Root cause / routing
Core, not teaching: the stage-1 module-choice question ignores a step variable's known type. The
architect routed it to the coder as **issue 63** — feed the step's known variable types
(`%writer% is a window`) into the module question ("explain, don't hide"), with a deterministic pick
hint only if that doesn't hold. Same shape as 45's typed offers.

## What to do next (post-63 verification — this is the whole remaining task here)
1. Rebuild the binary after the coder lands 63.
2. Re-measure, fresh/cache:skip, in `/shared/w41`:
   - `TypedW` (above) → step 1 should be **window.call 5/5**.
   - guard `call Finalize` → **goal.call 5/5** (plain call unchanged).
   - optionally untyped `call Show … in %window%` to see whether the type-born path is what matters.
3. If green, window.call's teaching can go back to normal and PlangOS's Edit step can stay formal
   (per the architect). No builder-source change is expected to be needed here.

Probe files live in `/shared/w41/` (TypedW.goal, W1/W2/W3.goal) and are recreated from the text above.
