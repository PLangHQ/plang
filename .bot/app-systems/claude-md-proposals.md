## architect — app-systems — 2026-09-26
**Target:** /CLAUDE.md (PLang Syntax)
**Why:** Ingi: "comment above says what next code line does". The parser follows it (lines above a goal's name are the goal's comment, lines above a step are that step's), but 356 of 594 `.goal` files in `os/` and `Tests/` describe the goal under its name, so the description lands on step 0. Bots writing `.goal` files copy that shape.
**Proposed change:**
```
- A comment line (`/ …`) describes the NEXT code line. A goal's description goes above its name; a step's comment goes above the step. Never put a goal's description under its name: it would become the first step's comment.
- A folder's docs in a plang app are `start.md` (as `Start.goal` is the entry), not `readme.md`.
```
