# Retro Summary — retro/coder-session-2026-06-19

**Version:** v6 (current)

## What this is

Retrospective analysis of coder sessions on the compare-redesign branch, spanning Jun 16-23 2026. Mines sessions for self-corrections, frustration moments, and wrong-doc signals, then applies lessons directly to memory files and shared documentation.

## What was done (all versions)

**v1** analyzed `5af76d61` (coder, Jun 18) → SC1–SC6 + SC7/SC8 from compare-redesign doc review → applied to coder MEMORY.md + character.md.

**v2** analyzed `93456b3b` + `efe53299` (coder, Jun 18) → SC9 (dispatchers dispatch), SC10 (fix test not runtime), SC11 (clean baseline) → coder MEMORY.md.

**v3** analyzed Jun 16 coder sessions → SC12 proposed (stale notes) then **declined** by Ingi.

**v4** analyzed architect sessions Jun 16+18 → A1 (Verb+Noun to architect), A2 (actively apply OBP during audit), A3 (verify library claims) → architect MEMORY.md.

**v5** analyzed docs sessions Jun 20 → D1 (examples are docs, OBP applies to code blocks), D2 (collection API is `.list` not `.s`), D3 (no C# types in PLang examples) → docs MEMORY.md.

**v6** (this session, Jun 23) analyzed 5 compare-redesign coder sessions (Jun 21-23). Ingi flagged coder for constant OBP violations, wrong code placement, not tracing root causes. **Top 3 patterns:**

1. **Verb+Noun every session** (5/5 sessions) — BornRow, ValueClr, NewEmpty, LoadFromFileAsync, ParseNextSegment. Rule was in MEMORY.md but only as a write-time tripwire; coder proposed them in plans. **Fix:** SC13 — flashing sign now fires at design/proposal time too.

2. **Hack-first, trace-never** (3/5 sessions) — patches symptoms without tracing root cause. Ingi: "dont just hack through this, I dont trust you." **Fix:** SC12b — new 🚩 ROOT CAUSE BEFORE CODE flashing sign.

3. **Operations in wrong type** (2/5 sessions) — Ticks in item.cs, Number.FromText named Text.FromText, prPath parsing in goal/list. **Fix:** SC18 in Coder discipline section.

Also applied: SC14 (design before code changes), SC15 (rebuild = diagnostic first), SC16 (fix at correct layer), SC17 (code over prose), SC19 (coincidental duplication), SC20 (.pr = build output).

**Also in this branch (non-mining work, v1 session):**
- User-facing type docs in `doc/app/type/`
- Doc-server sidebar, routing from `/`, Liquid escaping fix

## Files modified (v6)

- `characters/coder/memory/MEMORY.md` — SC12b, SC13 (strengthened), SC14, SC15, SC16, SC17, SC18, SC19, SC20
- `~/.claude/projects/-workspace-plang/memory/MEMORY.md` — ledger updated
- `.bot/retro-coder-session-2026-06-19/retro/v6/plan.md`, `findings.md`, `changes.md`

## Code example — SC13 strengthening

Before (write-time tripwire only):
```
While writing C#, the moment a Verb+Noun name appears... write inline comment.
```

After:
```
FIRES AT DESIGN TIME. Before writing any plan or proposal, scan ALL intended
names for Verb+Noun shape (BornRow, ValueClr, NewEmpty, LoadFromFileAsync...).
If any name is Verb+Noun, the design is wrong — fix before touching code.
```
