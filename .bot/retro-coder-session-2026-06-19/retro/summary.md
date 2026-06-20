# Retro Summary — retro/coder-session-2026-06-19

**Version:** v4 (current)

## What this is

Retrospective analysis of coder and architect sessions from 2026-06-16 through 2026-06-18. Mines for self-corrections, frustration moments, and wrong-doc signals, then applies lessons directly to memory files and shared documentation. Also produced new user-facing documentation and doc-server improvements.

## What was done

**v1** analyzed `5af76d61` (coder, 21:05 UTC 2026-06-18) → SC1–SC6 applied to coder MEMORY.md and character.md. SC7 (OBP smell #8) and SC8 (.pr path format) applied from compare-redesign doc review.

**v2** analyzed `93456b3b` (coder, 15:15 UTC 2026-06-18) and `efe53299` (coder, 13:24 UTC 2026-06-18) → 3 new findings:
- **SC9** — Dispatchers dispatch; construction belongs in the type family → `CLAUDE.md` OBP smell #9 + coder MEMORY.md
- **SC10** — Fix the test, don't bend the runtime → coder MEMORY.md
- **SC11** — Establish a clean baseline before making further changes → coder MEMORY.md

**v3** analyzed 2026-06-16 coder sessions → SC12 proposed (stale procedural memory) then **declined** by Ingi ("you will fix those types of errors when you see them"). Ledger updated.

**v4** analyzed architect sessions from 2026-06-16 and 2026-06-18 → 3 new findings:
- **A1** — Verb+Noun flashing sign added to architect MEMORY.md (own naming proposals, not just reviewed code)
- **A2** — "During an OBP audit, actively apply each rule — don't skim" added to architect Small Rules
- **A3** — "Don't assert library capability limits without verifying" added to architect Small Rules

**Also in this session (non-mining work):**
- Wrote user-facing type docs in `doc/app/type/` (plain language, no C# references) with list/dict subdocs
- Fixed `&quot;` Liquid escaping bug in doc-server by assembling HTML in JS, not passing through Liquid template variables
- Added left-sidebar doc tree with active state highlighting to doc-server
- Restructured doc-server to serve from `/` instead of `/doc/` — sidebar shows `plang.is` as root link, no "Reference" label, all content resolved from `doc/` directory

## Files modified

**Lessons/rules:**
- `CLAUDE.md` — OBP smell #9
- `characters/coder/memory/MEMORY.md` — SC9, SC10, SC11
- `characters/architect/memory/MEMORY.md` — A1, A2, A3
- `~/.claude/projects/-workspace-plang/memory/MEMORY.md` — ledger updated

**Docs created:**
- `doc/app/type/start.md` — type system for PLang users
- `doc/app/type/list/start.md` — lists
- `doc/app/type/dict/start.md` — dicts
- `doc/start.md` — updated to link to type/
- `doc/app/start.md` — updated to link to type/

**Doc-server:**
- `doc-server/server.js` — sidebar tree, escaping fix, `/` routing from `doc/`
- `doc-server/templates/layout.liquid` — single `{{ content }}` output

## Code example

Doc-server now resolves all routes from `doc/`:
```js
const DOC = path.join(ROOT, 'doc');
const candidates = [
  path.join(DOC, rel, 'start.md'),   // /app/ → doc/app/start.md
  path.join(DOC, bare + '.md'),
  path.join(DOC, bare),
];
```
