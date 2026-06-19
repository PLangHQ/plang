# Retro Summary — v1

**Version:** v1  
**Session scanned:** coder, 2026-06-19 (`5af76d61`)

## What this is

Retrospective pass over today's coder session. Goal: find self-correction moments, identify their root cause (what rule was missing), and write proposals to fix the coder's MEMORY.md and character file so the mistakes aren't rediscovered next session.

## What was done

**Found 6 self-corrections (SC1–SC6).** All are documented in `v1/findings.md`.

**Top 3 by impact:**
1. SC1 — Relayed a subagent's "can't do this" claim without verifying source. Caused three test cases to be wrongly skipped. Rule missing: verify agent claims.
2. SC2/SC3 — Added `PeekValue()` (already existed as `Peek()`) and kept `GetValue` in production code for tests. Both caught via Ingi review. Rules missing: LSP before adding, coverage to find test-only callers.
3. SC5 — MEMORY.md is the loaded index, not just another file. Coder didn't know this, so its own feedback rules weren't actually in context.

**Files created:**
- `v1/plan.md` — plan
- `v1/findings.md` — evidence ledger with quotes and timestamps
- `v1/proposals.md` — proposed changes for coder MEMORY.md and character file (Ingi applies on Windows)
- `doc/start.md`, `doc/app/start.md`, `doc/app/goal/start.md`, `doc/app/goal/step/start.md` — new doc tree in the repo

**Files that couldn't be written (read-only filesystem):**
- `/peer-sessions/coder/projects/-workspace-plang/memory/MEMORY.md`
- `/peer-sessions/coder/CLAUDE.md`

Both are in proposals.md for Ingi to apply.

## Doc tree (`doc/`)

New folder at repo root. Structure mirrors OBP: `doc/app/goal/step/start.md`. `start.md` is the entry point for each concept. Source included via `[[path/to/file]]` — the web UI renders it inline from source. Example at `doc/app/goal/step/start.md`.

## For next run

After Ingi applies the memory/character proposals, add a note to my own MEMORY.md marking SC1–SC6 as proposed so I don't re-surface them.
