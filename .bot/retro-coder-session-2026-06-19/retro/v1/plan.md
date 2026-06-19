# Retro Plan — Coder Session 2026-06-19

## What we're fixing

Five self-corrections from today's coder session. Each one is a moment where the coder caught itself doing something wrong — these are the cleanest signal of a missing rule, because the coder already knew the right answer once corrected. The goal: get those rules into the coder's MEMORY.md so they don't need to be rediscovered next session.

## Findings (evidence already collected)

| ID | Self-correction | Root cause |
|----|-----------------|------------|
| SC1 | Relayed subagent claim that kind/strict "can't be expressed via Make.Param" without verifying | No rule: verify agent claims against source before asserting |
| SC2 | Added `PeekValue()` when `Peek()` already existed | No rule: inspect type surface via LSP before adding a method |
| SC3 | Proposed keeping `GetValue` in production code because tests needed it | No rule: check coverage — test-only callers → method belongs in test extensions |
| SC4 | Split IReader into two axes based on current usage, not real interface shape | No rule: ask "why is this here, does it follow OBP?" when a split appears |
| SC5 | Added feedback rule to a separate file, buried MEMORY.md pointer mid-list | No rule: MEMORY.md is the loaded index — rules worth following every session go there, prominently |

## Files to change

### 1. Coder's MEMORY.md — primary target
**File:** `/peer-sessions/coder/projects/-workspace-plang/memory/MEMORY.md`

Add 5 new rule entries. Currently has only 2 entries (commit hygiene, branch from main). None of the SC1–SC5 rules are there.

### 2. Proposals for coder character file — Ingi applies on Windows
**File:** `.bot/retro-coder-session-2026-06-19/retro/v1/proposals.md`

SC4 (OBP abstraction audit) is deep enough that it warrants a character-level note, not just memory. Write it as a proposal Ingi can apply to the coder character file.

### 3. My own output files
- `v1/findings.md` — evidence ledger with quotes and timestamps
- `summary.md` — session overview for future context
- `report.json` — session tracking

## How Haiku helps

Two cheap parallel jobs before I write anything:

**Haiku job A — draft MEMORY.md rule prose**
I give it the 5 structured findings; it writes clean, tight rule text for each one. I review and edit. This keeps me from wordsmithing the prose myself.

**Haiku job B — missed self-corrections scan**
One more pass over the session file looking specifically for self-corrections I might have missed (I found 22 signal hits; I read ~7 closely). Confirm or surface any others worth adding.

Then I (strong model) do the actual writes: findings.md, proposals.md, MEMORY.md edits, summary.md.

## What I do NOT change

- The coder's character file directly (read-only, orchestrator repo)
- Any PLang source code
- CLAUDE.md (proposals only, not direct edits)

## Sequence

1. [Haiku] Draft 5 rule entries + scan for missed corrections — parallel
2. [Me] Review Haiku output, decide final rule text
3. [Me] Write findings.md (evidence ledger)
4. [Me] Write MEMORY.md additions
5. [Me] Write proposals.md (character file proposals for Ingi)
6. [Me] Write summary.md + report.json
7. Commit + push
