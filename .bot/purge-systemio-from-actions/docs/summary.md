# docs — purge-systemio-from-actions

## v1 — 2026-05-26

**Version:** v1 (first docs pass on this branch)

**What this is:** Final docs gate for the System.IO purge. The branch
removes direct `System.IO.*` reaches from production C# under
`PLang/app/**` so every filesystem access routes through the gated
`path.@this` verb surface (and through `FilePath.AuthGate(verb)`).
Auditor v2 PASS, suite green (C# 3031/3031, PLang 206/206), branch
ready to merge into `runtime2`.

**What was done:**

The coder already shipped solid user-facing docs in stage 7 (commit
`d1ddeebb`) — full `good_to_know.md` "System.IO Is Banned" section
with migration status + one-liner under engine conventions in
`CLAUDE.md`. My pass fixed four real gaps:

1. `CLAUDE.md` — the stage-7 one-liner said "PLNG002 currently warning;
   flipping to error once the deferred handler migrations land". Stage 6
   landed; PLNG002 is at Error severity. Updated to match.
2. `PLang/app/types/path/this.JsonConverter.cs` — added one-line ctor
   summaries distinguishing the stub form from the Context-wired form.
3. `Documentation/v0.2/app-tree.md` — added `App.Parent` (new this
   branch) to the top-level tree, added `this.Snapshot.cs` to the Data
   partials list, added a paragraph clarifying that `app.Modules.Schema`
   is the LLM action catalog (not a registered action vocabulary).
4. `Documentation/v0.2/scripts/check-app-tree.sh` — carried over from
   the runtime2 sister branch, plus a `Schema` skip entry with reason.
   This is the drift checker that surfaced findings #3 above.

**Triage:** one CLAUDE.md proposal from coder/v1 (add "Default impl:"
lines under abstract-type entries in `app-tree.md`) **deferred** — the
target file's shape doesn't have per-type sections to attach those
lines to. Re-raise for `/shared/app-tree/` (the deep tree) instead.

**Code example:** the stale-line fix illustrates the pattern of what
the docs gate catches.

Before (`CLAUDE.md:22`):

> Build-time gate: PLNG002 (currently warning; flipping to error once
> the deferred handler migrations land).

After:

> Build-time gate: PLNG002 at **error** severity — PLang and
> PlangConsole build clean with zero PLNG002 warnings as of the
> `purge-systemio-from-actions` merge, and any regression fails
> compilation.

A reviewer that doesn't re-read the one-liner after stage 6 lands
ships docs that contradict the build. The pass catches it.

**Verdict:** PASS. Branch ready for `runtime2` merge.

**Files modified:**

- `CLAUDE.md`
- `PLang/app/types/path/this.JsonConverter.cs`
- `Documentation/v0.2/app-tree.md` (+ existing maintenance note carried from runtime2)
- `Documentation/v0.2/scripts/check-app-tree.sh` (carried + `Schema` skip)
- `.bot/purge-systemio-from-actions/docs/v1/plan.md`
- `.bot/purge-systemio-from-actions/docs/v1/result.md`
- `.bot/purge-systemio-from-actions/docs/v1/verdict.json`
- `.bot/purge-systemio-from-actions/docs-report.json`
- `.bot/purge-systemio-from-actions/report.json` (docs session entry)
