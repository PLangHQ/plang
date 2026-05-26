# docs — purge-systemio-from-actions — v1 plan

## Inputs

- **Auditor v2:** PASS, no findings. C# 3031/3031, PLang 206/206.
- **Coder v2 handoff:** all 7 stages landed, PLNG002 at Error severity.
- **Coder stage 7 (commit d1ddeebb)** already shipped user-facing docs:
  - `Documentation/v0.2/good_to_know.md` → new section "System.IO Is Banned
    in Production C# (use `path.@this`)" (~80 lines, full migration status).
  - `CLAUDE.md` → one-liner under engine conventions cross-referencing the
    good_to_know section.
- **Proposals on file:**
  - `.bot/purge-systemio-from-actions/claude-md-proposals.md` — one entry
    (coder/v1): "Default impl" lines under abstract types in `app-tree.md`.
  - `.bot/purge-systemio-from-actions/character-proposals.md` — none.

## Proposal triage

### CLAUDE.md proposals

| From | Target | Decision | Reason |
|---|---|---|---|
| coder/v1 | `Documentation/v0.2/app-tree.md` — "Default impl:" lines under abstract types | **defer** | The intent is sound (tracing an abstract-base call needs to land on the concrete impl), but the proposed change assumes `app-tree.md` has per-type entries with `Location:` / `Role:` sections. The actual file is a 200-line one-screen summary built around fenced tree blocks and a "What's NOT on app" table — there are no per-abstract-type sections to attach "Default impl:" lines to. The deep-dive doc at `/shared/app-tree/` is the right home for that shape. Filed under the explicit-user-request footer, so not dismissing the idea — deferring the form until either app-tree.md grows per-type sections or the proposal is re-targeted at the deep tree. Re-raise once the target file's shape supports it. |

### Character proposals

None on file. Nothing to triage.

## Docs work in this version

1. **Stale line in `/CLAUDE.md`.** The coder's stage-7 one-liner says
   *"Build-time gate: PLNG002 (currently warning; flipping to error once
   the deferred handler migrations land)"*. Stage 6 landed; PLNG002 is at
   `Error` severity now. Update the parenthetical to reflect that.

2. **XML doc on `JsonConverter` constructors.** `PLang/app/types/path/this.JsonConverter.cs`
   has a thorough class-level summary but the two public constructors are
   bare. The class-level prose distinguishes "stub" (no-Context) vs
   "Context-wired" — those should be one-line summaries on the ctors
   themselves so call-site IntelliSense surfaces the distinction without
   the reader chasing the class summary. Read/Write overrides are
   conventional `JsonConverter<T>` methods — no docs needed (overrides
   inherit).

3. **`app-tree.md` drift findings.** The app-tree checker I brought over
   from runtime2 reports two real omissions on this branch too:
   - `PLang/app/modules/Schema/` — module folder, PascalCase, not in the
     modules block. Investigate whether it's a registered action module
     (and add) or schema-tooling infrastructure (and add a skip entry +
     reason).
   - `PLang/app/data/this.Snapshot.cs` — data partial, missing from the
     Data block. Add a line.

4. **`result.md` — CHANGELOG-style entry.** User-visible runtime changes
   on this branch worth capturing for downstream:
   - New `permission.verb.Execute` (Unix r/w/x — DLL loads now prompt
     separately from "read").
   - New `path.LoadAssemblyAsync()` verb (gated by Execute).
   - New `path.ReadAsBase64()` / `path.ReadAsDataUri()` content-shape verbs.
   - `Goal.Path` / `PrPath` / `LoadedFromPrPath` / `GoalCall.PrPath` are
     `path.@this` instead of `string` — affects anyone reading `.goal`
     JSON shape externally (still serializes as the portable relative
     string via the new JsonConverter).
   - PLNG002 at Error severity — any external fork carrying System.IO
     reaches in production C# will fail to compile.

5. **`summary.md`.** Docs-bot session summary in `.bot/<branch>/docs/`.

## Files I expect to touch

- `CLAUDE.md` — one-line fix (PLNG002 status).
- `PLang/app/types/path/this.JsonConverter.cs` — two ctor summaries.
- `Documentation/v0.2/app-tree.md` — add `Schema` module line + `Snapshot`
  data partial line.
- `.bot/purge-systemio-from-actions/docs/v1/result.md` — CHANGELOG.
- `.bot/purge-systemio-from-actions/docs/summary.md` — session summary.
- `Documentation/v0.2/scripts/check-app-tree.sh` — carried from runtime2
  (already present in working tree).
- `Documentation/v0.2/app-tree.md` — maintenance note carried (already in
  working tree).

## Verdict expectation

If the four edits above land cleanly and the app-tree checker reports
clean after the two drift lines are added, this is a **PASS** —
branch ready to merge into runtime2.

If `modules/Schema/` turns out to need more than a one-line addition
(e.g. it's not actually a runtime action module and needs separate
documentation), I downgrade my own work, not the branch. The branch
itself is doc-complete on the System.IO purge — the Schema gap is
pre-existing and was just invisible until the checker landed.
