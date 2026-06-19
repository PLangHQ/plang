# Retro Summary — v1

**Version:** v1  
**Session scanned:** coder, 2026-06-19 (`5af76d61`)

## What this is

Retrospective pass over today's coder session. Goal: find self-correction moments, identify their root cause (what rule was missing), and write the fixes directly to the coder's MEMORY.md and character file so the mistakes aren't rediscovered next session.

## What was done

**Found 6 self-corrections (SC1–SC6).** All documented in `v1/findings.md`.

**Applied directly:**
- `characters/coder/memory/MEMORY.md` — Added SC1–SC6 bullets to "Coder discipline" section; added SC5 Memory System callout near top
- `characters/coder/character.md` — Added 4 sub-sections to OBP section: verify subagent claims (SC1), LSP before adding a method (SC2), test-only methods in test extensions (SC3), OBP usage-smell question (SC4)
- `characters/docs/memory/MEMORY.md` — Added SC5 Memory System callout (universally applicable)

**Supporting files:**
- `v1/plan.md` — plan
- `v1/findings.md` — evidence ledger with quotes and timestamps
- `v1/proposals.md` — original proposals (before direct write access was confirmed)
- `v1/changes.md` — changelog of every edit applied

## Top 3 findings by impact

1. **SC1** — Coder relayed a subagent's "can't do this" claim without verifying source. Three test cases were wrongly skipped. Rule added: verify agent claims before asserting.
2. **SC2/SC3** — Added `PeekValue()` beside existing `Peek()` and kept `GetValue` in production for test-only callers. Rules added: LSP before adding, test-only callers go in test extensions.
3. **SC5** — MEMORY.md is the loaded index, not just another file. Coder didn't know this, so its own feedback rules weren't actually in context. Callout added to MEMORY.md itself.

## Code example

Coder discipline bullets added to MEMORY.md:

```markdown
- **Verify subagent claims against source before asserting them.** Subagents can state
  constraints that sound architectural but don't exist in the code. Before telling Ingi
  "this can't be done," grep or use LSP to confirm. (SC1)
- **Inspect the type surface via LSP before adding any method.** The method you're about
  to write may already exist under a slightly different name. (SC2)
```
