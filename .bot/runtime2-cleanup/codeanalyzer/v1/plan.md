# codeanalyzer v1 — runtime2-cleanup stage 1

## What's under review

`c74be34e — runtime2-cleanup stage 1: per-actor Channels.Serializers as single home`.

Five production files modified:

- `PLang/App/this.cs` — `Serializers` property deleted.
- `PLang/App/Channels/Channel/this.cs` — `Channels` back-ref added.
- `PLang/App/Channels/Channel/Stream/this.cs` — `_serializers` field + `Serializers` property removed; `WriteCore` routes through `Channels!.Serializers`.
- `PLang/App/Channels/this.cs` — `Register` sets `channel.Channels = this`; internal `sc.Serializers` sites collapse to `Serializers` (own).
- Caller sweep across 4 files (5 sites): `Goals/this.cs:320,325`, `Goals/Setup/this.cs:56`, `modules/file/providers/DefaultFileProvider.cs:99`, `Actor/Context/this.cs:172`.

Plus the test-side sweep (architect-flagged as in-scope).

## How I'm reviewing

Five passes per character file:

1. **Pass 1a — OBP rules** against project `CLAUDE.md`'s shape rules and the canonical sweep recipe.
2. **Pass 1b — shape smells** (the four-question checklist).
3. **Pass 2 — simplification.**
4. **Pass 3 — readability.**
5. **Pass 4/5 — behavioural reasoning + deletion test** on the changed lines.

Stage-20 work (drop `Channel.App` once everything navigates via `Channels`) is *out* of stage 1 scope per the architect's brief — I'll note pre-stage-20 leftovers in the report but not call them stage-1 failures.

## Verdict criteria

- **CLEAN** if the OBP shape lines up with the brief, the residual greps stay clean, no latent footguns introduced.
- **NEEDS WORK** if I find a behavioural bug or a shape regression beyond what the brief acknowledged.

I'll write findings into `report.md` and the verdict into `verdict.json` next to it.
