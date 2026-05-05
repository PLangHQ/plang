# architect — runtime2-callback

## What this is

The **callback mechanism**, end-to-end: how PLang preserves runtime state at a failure or pause point, persists it durably (signed envelope, developer-chosen storage), verifies it on resume, and runs it from a fresh `App` process. Two issuers — `ask user` and error-retry — share one `App.Run(callback)` entry point.

Encryption and HTTP wire transport (the ask-user case in full) are deferred to a future branch — the layering is settled here so that future branch doesn't reshape this.

## Current state

Design settled and ready for handoff:

- **Resume mechanic:** bind, jump, run — never replay. `App.Run` accepts a Callback as entry point; same main loop, lands at the action. No `Seek` verb.
- **Snapshot system:** per-type `ISnapshotted` interface. Three buckets — snapshot-and-restore, reconstruct-on-build, drop. Names-vs-values: variables are values that survive resume; everything else is a name. Two trust layers: signature integrity + referent integrity.
- **Error-retry capture:** throw-time variables via Diff reverse-apply (auto-flips on error). Lazy materialization of `%!error.callback%`.
- **Signing:** transparent at the Data IO boundary in `App.Channels.Serializers`. Default = no expiry. No explicit `- sign` at issue sites.
- **`GoalHash`:** reuses existing `Goal.Hash` at `App/Goals/Goal/this.cs:121`. Mismatch = hard error on resume.
- **Encryption layering:** Callback-internal (not Data-layer). Deferred to future ask-user branch.
- **OBP rename queued:** `App.modules.signing.SignedData` → `Signature`.

Ships entirely on existing infrastructure. No new modules, no new crypto, no new core abstractions.

## Where to read

- `plan.md` — spine, read end-to-end.
- `plan/<topic>.md` — focused topic files for each concern.

## Next handoff

**test-designer** picks up next — turn `plan/test-strategy.md` into a test plan and surface anything underspecified. Then **coder** implements: per-type `ISnapshotted` rollout, `Callback` record + lazy materialization, `App.Run(callback)` entry point, transparent-signing IO hook, `SignedData` → `Signature` rename, `callback.run` action, goal-hash mismatch error path.

Stages will be carved when the topic files settle. Currently the topic content carries forward verbatim from the prior whiteboard sessions; Ingi may still iterate on individual topics before stage carving.
