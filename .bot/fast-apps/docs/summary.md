# docs — fast-apps

**Version:** v1

## What this is
The docs pass before fast-apps merges into runtime2. The branch makes an app from an origin, with its lists as layers over it, so a sub app costs about 0.15 ms and 110 KB. The rules that keep that safe lived in a decision log. This pass writes them where a coder looks.

## What was done
- `Documentation/v0.2/apps.md` (new, indexed in `good_to_know.md`): origin and layers; a sub app's folder rule and testing from birth; the hold (`actor.on`); a run never writes a shared object; a birth that makes only what's asked; `Tools/Bench`; the packages and SDK; the rebase trap. File:line checked at the tip.
- `CLAUDE.md`: the three proposals (branch rules: boy scout, no runtime code for tests, a run never writes a shared object; a Speed record section).
- Stale: `architecture.md` named removed `CreateChild()`/`Clone()`; the plan's `start.md` said "parent", lacked stage 6 and listed deferred items as built.
- Verified, not redone: the auditor's B2 (backlog section) is in place.
- Reports: `v1/plan.md`, `v1/result.md`, `v1/verdict.json` (pass), `../docs-report.json`.

## Not done
N1–N18 (next-branch work); `start.goal` (the test-designer's); a full refresh of `architecture.md`.

## Code example
```
origin (code | actor)  →  type list, module registry, goals: layers over it
a run writes: its own layer, or its actor's context (actor.on)  —  never the shared object
```
