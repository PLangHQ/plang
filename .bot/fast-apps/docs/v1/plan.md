# docs — fast-apps v1 — plan

Brief: the architect's message (decision 62: merge when the docs bot is satisfied). Docs and CLAUDE.md only; no production code, no `.pr`, no `os/system` teaching markdown (so the teaching-edit rule is not triggered).

| Item | Where | What |
|---|---|---|
| good_to_know rules (origin, sub app folder + testing, hold, shared writes, birth, Tools/Bench, packages/SDK, rebase trap) | new `Documentation/v0.2/apps.md` + `good_to_know.md` index | each rule short, with file:line checked at the tip |
| auditor B2 | `.bot/item-doors/architect/backlog.md` | already in place (the architect's 2401279ed, item 10 says "skipped"): verified, not redone |
| auditor N19, N20 | CLAUDE.md, `apps.md` | N19 = proposal 3; N20 = the docs above |
| stale names | CLAUDE.md, Documentation, docs, plan | CLAUDE.md names none of the removed members; `architecture.md` named `CreateChild()`/`Clone()` (removed): fixed; the plan's start.md contradicted the code in five places: fixed, stage 6 added |

## CLAUDE.md proposal decisions
1. boy scout + no runtime code for tests (architect v1) — **apply**: Ingi's rules 66 and 35; both apply to every branch; attribution trimmed. Under "OBP Shape Smells" as "Branch rules".
2. speed record (architect v1 (2)) — **apply**: Ingi's 67; `Tools/Bench/run.sh` and `record.md` exist; a "Speed record" section after "Running plang Tests".
3. a run never writes an object its app shares (architect v1 (3)) — **apply**: checked in code (`actor.on`, `history.closed`, the three caches); third bullet of the branch rules.

## Left out
- N1–N18 (next-branch work), `start.goal` (the test-designer's file; N14's stage-6 note is in `start.md`).
