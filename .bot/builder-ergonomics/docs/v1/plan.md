# Docs v1 plan — builder-ergonomics

**Verdict-class so far:** coder v2 ✓, tester v2 PASS, security v1 PASS, auditor v1 PASS. Branch is ready to merge once docs gates close.

## What this branch actually shipped (for the docs lens)

Two architectural facts and three builder-pipeline improvements:

1. **Channel recursion guard — `Channel.Goal.@this.IsExecuting`** (commit `827d34e19`)
   The `Actor.FoundationalChannels` / `PushChannelsOverride` / `FreezeFoundational` /
   `AppChannels.Snapshot` mechanism is **deleted from source**. Replaced by a
   private `AsyncLocal<bool> _executing` on each goal-backed channel. Set true
   while the goal body runs; the registry's `Get(name)` treats an executing
   goal-channel as not-found.
2. **Conversion error chaining — root cause first** (commit `4c37ad582`)
   When `Conversion.TryConvertTo` receives an `errors.Error` as input and
   `Convert.ChangeType` throws, the conversion wrapper is **appended to the
   source error's chain** and the source is returned. Display shows the real
   cause, not the recovery handler's reformatting failure.
3. **Builder pipeline ergonomics** (commits `27ad03927`, `9f53f1809`, `6e210f4c5`)
   - Per-step `confidence` (VeryHigh|High|Medium|Low|VeryLow) emitted by all
     four LLM passes (Plan, Compile, RefineActions, FixValidation). Low/VeryLow
     surfaces as `⚠ planner|compiler <level>: …` in build output.
   - Named `"builder"` channel registered at top of `Build.goal`, backed by
     `BuilderChannel.goal`. All build-time output routes through
     `EmitBuildEvent.goal` → templated render → write to `"builder"`.
   - LLM-prompt hardening: `Plan.llm` "verb rule" (action set comes from the
     verb, not parameter values) + `goal/call.notes.md` "Actor must come from
     step text". Five `llm.query` schemas migrated to `list<T>` from `[T]`.

Per the coder/tester/auditor reports, F4 (UploadFile Http 502) and F5
(`y/n/a` permission-prompt leak) are environmental/hygiene — deferred,
not a docs concern. P1/P2/P3/P5/P7 of the user-feedback are closed out
in the tester handoff with reasons; not chasing.

## Documentation gaps to close

### G1 — `io-channels.md` is stale on the recursion mechanism (critical)

`Documentation/v0.2/io-channels.md` currently describes the deleted
`FoundationalChannels` / `PushChannelsOverride` / `FreezeFoundational`
override mechanism in three places: the `Channel.Goal.@this` description
(lines ~135), the actor-channel-resolution code block (lines ~177-185),
and the "PLang — replace `output` with a goal channel" example (lines
~261-271). A reader following these lands on APIs that no longer exist.

**Fix:** rewrite all three sections to describe the per-channel
`IsExecuting` guard, cite the actual file (`PLang/app/channels/channel/goal/this.cs`),
and link the cycle-A→B→A walk-through (sound under cross-file tracing per
auditor + security). Drop the "actor channel resolution & overrides"
code block — the overlay surface is gone; `Channels` is now just the
direct registry. Tests-capture-stderr example stays (unaffected).

### G2 — `app-tree.md` lists `FreezeFoundational()` as actor surface (major)

`Documentation/v0.2/app-tree.md:103` shows `FreezeFoundational()` as an
actor method. It is not. Remove the line. Also remove the matching skip
regex in `Documentation/v0.2/scripts/check-app-tree.sh:89`
(`actor_skip_regex='^(FoundationalChannels)$'`) — it was the
canary-skip for an API that's gone, and leaving it covers future
genuine drift.

### G3 — Architecture doc says "Channels are reconstructed at App build" (minor — consistent)

`Documentation/v0.2/architecture.md:493` and `snapshots.md:19` both list
`Channels` in the not-snapshotted bucket. That's still true — the
recursion guard is per-call AsyncLocal state, not subsystem state — so
no change needed. Noted to confirm no regression.

### G4 — No `good_to_know.md` entry on "value owns its discipline" (minor — preventive)

This branch's load-bearing change is a worked example of an OBP
discipline: **the type holding the data owns the discipline that guards
it.** The deleted approach pushed an overlay onto the actor and made the
registry resolve "as if I weren't here" — discipline lived outside the
type. The replacement is one private `AsyncLocal` on the goal-channel and
one branch in `AppChannels.Get`. Worth a short entry that future authors
can grep for when tempted to add a parallel "context filtering layer"
instead of pushing the rule onto the owning type.

### G5 — `build.md` doesn't explain the builder channel + confidence (major)

The current `build.md` documents the `--build` properties and the
"why didn't the planner pick my action?" diagnostic recipe (added on
this branch's first docs commit, `659909d4f`). It doesn't:

- Describe the **per-step confidence** that the planner and compiler now
  emit, what each level means, or how to react to a `Low`/`VeryLow`.
- Describe the builder's **own output channel** (`"builder"` channel +
  `BuilderChannel.goal` + `EmitBuildEvent.goal` + `templates/output/build-output.template`)
  and that it's intentionally a redirection seam for future
  log-file / structured-stream / TUI consumers.

**Fix:** add two new sections — "Confidence per step" (one paragraph +
the warning shape + table of levels) and "Builder output routing"
(short — what registers what, where to redirect).

### G6 — User-facing modules docs (`docs/modules/*.md`) unaffected

The branch's PLang surface additions (`channel.set "name" call Goal`)
were already documented as the standard `channel.set` shape in
`Documentation/v0.2/io-channels.md`. The builder-channel registration is
internal-to-the-builder, not a user feature. `docs/modules/` does not
mention `channel.set` today, so no stale references; adding an entry is
beyond branch scope. No change.

## CLAUDE.md / character proposals

No `claude-md-proposals.md` or `character-proposals.md` exists on this
branch. Nothing to apply or reject.

## Order of work

1. G2 — drop one line in app-tree.md, drop one line in check-app-tree.sh
2. G1 — rewrite three sections of io-channels.md
3. G5 — extend build.md with confidence + builder channel
4. G4 — add good_to_know.md entry
5. Bookkeeping — summary.md, docs-report.json, verdict.json, report.json session, commit, push

No PLang `.goal` examples to write (no new user-facing modules). No XML
doc gaps detected: `channel.goal.@this.IsExecuting` is already
documented at the C# site, and the actor's `Channels` getter has a
pointer to it.

## Verdict track

Expected PASS — all gaps are docs-rewrites or additions, no flagging
back to coder/tester needed.
