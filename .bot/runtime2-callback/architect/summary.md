# architect — runtime2-callback

## What this is

The **callback mechanism**, end-to-end: how PLang preserves runtime state at a failure or pause point, persists it durably (signed envelope, developer-chosen storage), verifies it on resume, and runs it from a fresh `App` process. Two issuers — `ask user` and error-retry — produce two records (`AskCallback`, `ErrorCallback`) under one tiny `ICallback` interface (`Position`, `Serialize(ctx)`, `Run(ctx) -> Task<Data>`); each impl owns its own internals.

Encryption ships in this branch as a structural pass-through (`crypto.encrypt`/`crypto.decrypt` exist as actions but return their input unchanged in v1). Real symmetric crypto follows once the named PLang runtime gaps are filled — tracked in `Documentation/Runtime2/todos.md`. HTTP wire transport for the ask-user case is the remaining piece.

## Current state

Design settled and ready for handoff:

- **`callback.Run(ctx)` is the OBP root verb.** PLang's `- run %callback%` is a thin shim. Each impl handles verify, decrypt, restore, jump, and run end-to-end inside its own body. Returns `Task<Data>` so the resumed action's result chains naturally.
- **Two records, one tiny interface.** `ICallback` exposes `Position` (a `Call` frame — same primitive both records use), `Serialize`, `Run`. `AskCallback` is slim (`Position, Actor, Variables`). `ErrorCallback` is `Snapshot App` only — the wire shape mirrors the App tree.
- **The App tree is the schema.** `app.Snapshot()` walks `@this` properties and asks each `ISnapshotted` for its capture. Restore is the dual: `app.Restore(snapshot, ctx)`. `Snapshot.@this` is the one snapshot type — no separate `AppSnapshot`.
- **CallStack is the position.** `App.CallStack` snapshots its active frame chain. Each `Call` is `@this` and emits `(Goal-stub, StepIndex, ActionIndex, …)`. Bottom frame = resume point; outer frames = unwind chain.
- **Variables owns its own time-travel.** `app.Variables.SnapshotAt(error)` is the throw-time projection method. Variables internally consults `App.CallStack` (which owns the diff stream) for events-since-T and reverse-applies. Time-ordered data on CallStack; projection method on Variables.
- **`%!error.callback%` is `app.Errors.Current.Callback`.** Lazy materialization lives on `Error.@this` as a property — no synthetic-property handler floating in the runtime.
- **Three buckets, chosen by the type.** Implement `ISnapshotted` (snapshot-and-restore: Variables, Errors, Providers registry, Statics, Build, Testing, CallStack). Don't (reconstruct-on-build: Modules, Goals, Channels, Cache). Or stay invisible (drop: live IO state, timing, history).
- **Resume mechanic:** bind, jump, run — never replay. The resumed App's main loop's first tick lands at the bottom CallStack frame.
- **Error-retry capture:** throw-time variables via diff reverse-apply (auto-flips on error). Materialization is a pure function of `(error, current state)` — cacheable.
- **Encryption owned by Callback's serializer.** `Serialize(ctx)` pipes the whole payload through `crypto.encrypt` before returning bytes; `Deserialize` reverses. The Data layer signs already-encrypted bytes; never sees plaintext. v1 crypto is identity pass-through.
- **Data signs itself; Serializers shape the wire; Channels route.** `Data.@this` carries a lazy `Signature` property that populates via the `signing` module on first read. `Serializer.@this` per mimetype family decides whether to read `data.Signature` (and trigger signing) or only `data.Value`. `Channel.@this` picks the serializer for the receiver's mimetype. No `PrepareForOutput`-style verb.
- **`application/plang+data`** — new MIME type for full-envelope wire shape. Sibling of existing `application/plang+json`.
- **`app.Callback` is config, not an `ICallback`.** `app.Callback.Signature.ExpiresInMs` reads through "App's `Callback` config holder, its `Signature` sub-config, the `ExpiresInMs` value." `Data.Signature` (wire envelope) and `app.Callback.Signature` (config) share the word but are distinct things.
- **OBP rename queued:** `App.modules.signing.SignedData` → `Signature`.

Ships on existing infrastructure plus two new `crypto` actions. No new modules, no new core abstractions.

## OBP audit pass — done in v3

The v2 → v3 review surfaced multiple OBP smells beyond the original schema fix. Findings and resolutions:

1. `AskCallback` carried `(Goal, StepIndex, ActionIndex)` primitives — collapsed to a `Call Position` field on the interface, used by both records.
2. `AppSnapshot` was an invented wrapper — collapsed to `Snapshot.@this` (same type ISnapshotted writes to).
3. `%!error.callback%` materialization had no clear home — pinned to `Error.@this.Callback` lazy property.
4. Variable throw-time projection had no clear home — pinned to `app.Variables.SnapshotAt(error)`. CallStack owns the diff stream; Variables owns the projection method.
5. Data signing was orchestrated by Channels — moved to `Data.@this` as a lazy property on `Signature`. Serializers per mimetype decide whether to read it.

All five baked into the topic files. No further audit queued.

## Where to read

- `plan.md` — spine, read end-to-end.
- `plan/<topic>.md` — focused topic files for each concern.
- `stage-N-<slug>.md` — implementation stages carved at the architect root.

## Stages

| Stage | File | Status |
|-------|------|--------|
| 1 | [Snapshot Foundation](stage-1-snapshot-foundation.md) | pending |
| 2 | [CallStack Frames + Variables Time-Travel](stage-2-callstack-frames-and-time-travel.md) | pending |
| 3 | [Data Lazy Signing + Per-Mimetype Serializers](stage-3-data-signing-and-serializers.md) | pending |
| 4 | [Callback Records and Verbs](stage-4-callback-records-and-verbs.md) | pending |

Stages 1, 2, and 4 are sequential. Stage 3 is independent in spirit and could land in parallel with 1/2 — touches every Data construction site, so plan for the rename ripple. Stage 4 depends on all three.

## Next handoff

**test-designer** picks up next — read both `plan/test-strategy.md` (integration cuts + test layer mapping) and `plan/test-coverage.md` (per-topic coverage matrix, failure matrix, new-surfaces inventory). The strategy file is the spine; the coverage file is the heavy reference. Translate into TUnit test suites and PLang `.goal` tests per the layer mapping; surface anything underspecified.

**coder** picks up Stage 1 once the test-designer's first cut is visible (or in parallel — the work doesn't block). Each stage's design narrative is written for the coder; they should read the matching `plan/<topic>.md` files alongside.

**One open architectural question Stage 3 calls out:** how `Data.@this` carries `Context` — constructor change (touches every callsite, type-system enforces) vs. additive `WithContext(ctx)` method. I lean constructor change; coder/Ingi confirm before Stage 3 begins.
