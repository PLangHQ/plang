# architect — builder-formal

Newest first. Branched off `get-builder-running` at `92fcae51f`; that branch's history is in `.bot/get-builder-running/architect/summary.md`.

## 2026-09-25 (evening) — Ingi AFK: the architect is in charge ("you can answer everything, your are in charge")

**Decisions log while Ingi is away. Read this first when he's back.**
- **Formal notation settled** (vision.md §4): the `.pr` is formal only, in a thin JSON envelope; types in the formal (`Name: type = value`, written always, optional on input); a frozen default is `Name: type ?= value`; a modifier WRAPS its action with `{ }` (Ingi's shape) and error.handle's recovery becomes `Recovery=[…]`; `{ }` always = the actions an action contains.
- **What the LLM is shown** (§4b): picks ≥ 0.5 with scores; ≥ 0.9 pre-filled; 0.5–0.9 listed as "possible". Ingi suggested hiding < 0.9; kept because that band holds the 12 correct write-to → variable.set. `?` for a value to fill (not `%Prop%`: real variable syntax, silent if unfilled; `build.goals path=%path%` is a real Path=%path%); known values pre-filled (write to → `Value=%!data%`).
- **Decider:** v5 (`e83acd9b1`) is the shape (2 runs: 44/43 exact at 0.9, 57/56 at 0.5, nothing ≥ 0.9 wrong). Stop tuning write-to at the decider; stage 3's double check measures it. output.write gets example step texts.
- **Not touched (parked by Ingi):** snapshot, security, app-systems, runtime registrations, the action-as-value and timeout drafts, prompt B's A-vs-B run on get-builder-running.
- **Next, in order:** formal notation revision (2b) → prompt C + double check, C vs B on both models, 1 run × 2–3 rounds, then pick one model → stage 4 (the plang builder: formal .pr, the parser in C#, the catalog filter, the decider in Decide.goal).

## 2026-09-25 — decider v3 (`fc72d3083`); v4 queued (architect, delegated by Ingi)

v3 with the clause off: 51/58 exact at 0.5 (5 missed, 2 extra), 36/58 at 0.9 (none wrong), 150 KB. else/elseif asked by name works (0.95–0.98). The long clause on the common questions hurt (46/58, 7 extras), so it's off by default. The runner-up rule names the right action, but its score is the module's ~50% share of one choice. The write-to step texts had no effect when shown only under the module. **v4 (my calls; queued after plan stage 2):** (1) a runner-up module ≥ 0.2 gets a yes/no "does step N also use X?", and that is the action's score; (2) each common action's example step texts shown beside it; (3) an action held as a value (an action-typed property, a modifier's recovery) is not a decider pick, and the builder includes goal.call's definition when a picked action takes actions, so the golden expectations drop those goal.calls; (4) action- and goal-typed properties reach the LLM (only `clr` hidden; C# in stage 4). Coder is on plan stage 2 (formal notation + parser + writer).

## 2026-09-25 — decider v2; the story (vision.md); v3 + stage 2 sent

Decider v1 (`ade8d1cdd`) asked 37 yes/no per step (a 142 KB request for checkout); Ingi: "think something is wrong". v2 (`d902a4502`) is v0.1's shape: one choice per step for the main module, bare names, descriptions once in the state, plus yes/no for the 6 common actions, with steps numbered from 0. It's 5× smaller and ~2× faster (724 → 149 KB, 8.2 → 4.6 s), 2 steps worse (51/58 exact at 0.5; everything ≥ 0.9 still right). Prompt C was hand-written for checkout in `/shared/coder/2.0/`. **The story** ([vision.md](vision.md)): Ingi told the start: programmers who read docs; a step is a list of module.action(parameters) in any language; `write to` is variable.set; the `.pr` carries both JSON and formal; modifiers on the next line; unsure → warning. Then he handed over ("figure the rest out"). My calls: the action writes itself in formal; a `.goal` step written in formal is parsed directly (no decider/LLM) as the escape hatch; the `.pr` is never hand-edited; a certain contradiction fails loudly; the LLM never names types. Sent to coder (Ingi: "and send to coder"): decider v3 (runner-up rule, the clause back on common questions, write-to example texts, condition per action) → re-measure → plan stage 2 (formal notation + parser + the formal writer, round-trip, no LLM).

## 2026-09-25 — plan approved; stage 1 sent to coder

Ingi answered the five questions as proposed: the common actions start as the top 5–8 by count and are adjusted from measurement ("you will learn"); near-certain = 0.9 to start; the LLM sees scores; picks ≥ 0.9 are pre-filled, 0.5–0.9 are "possible", and below 0.5 fails loudly; a disagreement is told back to the LLM, and if it insists the build fails loudly. Coder has the go on stage 1 (decider with common actions + scores, measured alone on the 5 goals).

## 2026-09-25 — branch opened; plan drafted for Ingi's read

Ingi's direction: the decider (stages 1–2) picks each step's actions, with scores, answering a few common actions directly in stage 1 so stage 2 is skipped where it isn't needed; stage 3 gets the picks as a pre-filled formal (`file.read(Path=?)`) and answers in a compact formal notation instead of JSON; the builder parses it; the LLM and the decider must agree ("double validation, two llm are reading over the code"). Plan: [plan.md](plan.md) — python first (decider, parser round-trip, prompt C vs B on the 5 goals), the plang builder after C wins. Five open questions for Ingi (common actions, the near-certain threshold, scores shown, decider misses, disagreement retry). Coder is holding until Ingi has read the plan; prompt B on `get-builder-running` is paused, pushed and safe.

| Stage | Status |
|---|---|
| 1 Decider with common actions + scores (python) | pending — plan under review |
| 2 Formal notation + parser, round-trip (python) | pending |
| 3 Prompt C + double check, eval vs B (python) | pending |
| 4 Into the plang builder (after C wins) | pending |
