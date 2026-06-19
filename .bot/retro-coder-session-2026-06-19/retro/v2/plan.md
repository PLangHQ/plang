# v2 Plan — Coder sessions 2026-06-18 (earlier sessions)

## Context

v1 analyzed `5af76d61` (21:05 UTC, the late session). Two earlier sessions from the same day were not yet analyzed:

- `93456b3b-b746-4951-b138-af1aa7955cc3.jsonl` (2608 lines, 15:15 UTC)
- `efe53299-2aa7-4715-bb1f-0ae5b5f50f35.jsonl` (920 lines, 13:24 UTC)

## Scope

Fan out two Haiku reader threads (one per session). Collect findings, dedup against already-applied SC1–SC8. Apply any new teachable moments to coder memory/character files.

## Batch plan

| Batch | File | Lines |
|-------|------|-------|
| A | `93456b3b-b746-4951-b138-af1aa7955cc3.jsonl` | 2608 |
| B | `efe53299-2aa7-4715-bb1f-0ae5b5f50f35.jsonl` | 920 |

Both run in parallel on Haiku. I decide on main model.

## Already applied (skip if same lesson appears)

SC1 — Verify subagent claims against source
SC2 — Inspect type surface via LSP before adding method
SC3 — Test-only callers → method belongs in test extensions
SC4 — Collapse abstractions built on usage, not domain shape
SC5 — MEMORY.md is the loaded index
SC6 — Allocate-then-transform is OBP smell
SC7 — Leaf must not decompose operand carriers
SC8 — .pr path format is .build/<goalname>.pr (flat, lowercase)
