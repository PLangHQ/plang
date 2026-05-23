# v1 plan — doc inconsistency fixes

User-driven session (not pipeline). Ingi asked for an inconsistency scan, then approved fixes to the five factual findings (#1, #2, #3 — environment-rename — was a non-issue in architecture.md, but #1 architecture Providers, #2 .pr JSON, #3 callbacks Providers, #4 broken variables.md links, #5 builder index actions are the five approved).

## Fixes

1. `Documentation/v0.2/architecture.md` — `.Providers` overview → `.Code`; `PlangSerializer` binary → JSON.
2. `Documentation/v0.2/callbacks.md` — `Providers registrations` → `Code registrations`.
3. `Documentation/v0.2/variables.md` — repair all broken `goal-result.md` / `contexts.md` / `app.md` / `call-stack.md` / `modules.md` links.
4. `docs/modules/index.md` — builder row extended with `validateResponse, enrichResponse, promoteGroups, merge`.

## Not fixed (Ingi's intent-based-language reframing)

`into`/`write to`, `equals`/`is`, `before each goal` / `before goal`, Goals/goal casing — all leave as-is. PLang's intent-mapping absorbs phrasing variation; only factual contradictions matter.

## CLAUDE.md proposals

None this session — no canonical rules discovered worth proposing.

## Character proposals

None.
