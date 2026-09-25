# architect — builder-formal

Newest first. Branched off `get-builder-running` at `92fcae51f`; that branch's history is in `.bot/get-builder-running/architect/summary.md`.

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
