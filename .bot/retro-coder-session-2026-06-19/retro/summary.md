# Retro Summary — retro/coder-session-2026-06-19

**Version:** v2 (current)

## What this is

Retrospective analysis of three coder sessions from 2026-06-18. Mines for self-corrections, frustration moments, and wrong-doc signals, then applies the lessons directly to coder memory and shared documentation.

## What was done

**v1** analyzed `5af76d61` (21:05 UTC) → SC1–SC6 applied to coder MEMORY.md and character.md. SC7 (OBP smell #8) and SC8 (.pr path format) applied from compare-redesign doc review.

**v2** analyzed `93456b3b` (15:15 UTC, 2608 lines) and `efe53299` (13:24 UTC, 920 lines) → 3 new findings:

- **SC9** — Dispatchers dispatch; construction belongs in the type family → `CLAUDE.md` OBP smell #9 + coder MEMORY.md
- **SC10** — Fix the test, don't bend the runtime → coder MEMORY.md
- **SC11** — Establish a clean baseline before making further changes → coder MEMORY.md

5 other signals dropped as duplicates or too file-specific.

**Files modified (v2):**
- `CLAUDE.md` — new OBP smell #9
- `characters/coder/memory/MEMORY.md` — SC9, SC10, SC11 under "Coder discipline"
- `~/.claude/projects/-workspace-plang/memory/MEMORY.md` — retro ledger updated

## Code example

OBP smell #9 added to CLAUDE.md:

> **Dispatcher contains construction logic.** A dispatch function (`Lift`, a type-switch router, a `route`/`forward` method) contains `new T(...)` or builder calls that assemble a value. Dispatchers must only route — construction belongs in the type's own constructor or factory.

Real incident: `Data.Lift` contained IEnumerable construction logic for list values instead of delegating to `list/this.cs` constructor.
