# Callback — design spine

This branch designs the **callback mechanism** end-to-end: how PLang preserves runtime state at a failure or pause point, persists it durably, verifies it on resume, and runs it from a fresh `App` process. Two issuers — `ask user` and error-retry — produce two concrete callback types (`AskCallback`, `ErrorCallback`) under one `ICallback` interface. The shared verb is `callback.Run(ctx)` — Callback owns the resume orchestration end-to-end. Encryption ships in this branch as a structural pass-through (real symmetric crypto follows once missing runtime features land); HTTP wire transport for the ask-user case is the remaining piece.

## The few insights that reshape this design

These cross-cut multiple topic files. If you only know these, you can navigate the rest.

1. **Bind, jump, run — never replay.** Resume lands at the captured `(goal, step, action)` with state already bound, and continues. No prior step is re-executed. The engine has one main loop; resume is a different entry point, not a different loop. No `Seek` verb.
2. **`callback.Run(ctx)` is the OBP root verb.** `ICallback` exposes two methods: `Serialize(ctx)` and `Run(ctx)`. PLang's `- run %callback%` is a thin shim that dispatches into Run. Each impl owns verify, decrypt, restore, jump, and run — end-to-end.
3. **Two records, one tiny interface.** `ICallback` exposes `Position` (a `Call` frame), `Serialize(ctx)`, and `Run(ctx) -> Task<Data>`. `AskCallback` is slim and explicit: `Position, Actor, Variables`. `ErrorCallback` is `Snapshot App` only — *everything* lives inside the snapshot, including the call chain whose bottom frame answers `Position`.
4. **The App tree is the schema.** `app.Snapshot()` walks `@this` properties and asks each `ISnapshotted` for its capture into a `Snapshot.@this`. Restore is the dual: `app.Restore(snapshot, ctx)`. Adding a new App component? Implement `ISnapshotted` or don't — no `ErrorCallback` schema change.
5. **CallStack is the position.** `App.CallStack` snapshots its active frame chain. Each `Call` is `@this` and emits `(Goal-stub, StepIndex, ActionIndex, …)`. Bottom frame = resume point; outer frames = unwind chain. Goal-hash mismatch on any frame's stub during restore = hard error.
6. **Variables owns its own time-travel.** `app.Variables.SnapshotAt(error)` is the throw-time projection method. Variables knows *how to project itself*; it asks `App.CallStack` (which owns the diff stream as part of its audit trail) for events-since-T and reverse-applies. Time-ordered data on CallStack; projection method on Variables; clean seam between the two.
7. **Three buckets — chosen by the type, not a classifier.** Implement `ISnapshotted` (snapshot-and-restore: Variables, Errors, Providers registry, Statics, Build, Testing, CallStack). Don't (reconstruct-on-build: Modules, Goals, Channels, Cache, etc.). Or stay invisible to the snapshot (drop: live IO state, timing, history).
8. **Data signs itself; Serializers shape the wire; Channels route.** `Data.@this` carries a lazy `Signature` property — first access populates via the `signing` module. `Serializer.@this` per mimetype family (`JsonSerializer`, `PlangDataSerializer`, etc.) decides whether to read `data.Signature` (and thus trigger signing) or only `data.Value`. `Channel.@this` picks the serializer for the receiver's mimetype and orchestrates. No `PrepareForOutput` verb, no Channel-side signing logic. New MIME type `application/plang+data` introduced for full-envelope wire shape.
9. **Encryption is owned by Callback's serializer.** `ICallback.Serialize(ctx)` pipes the whole payload through `crypto.encrypt` before returning bytes; `Deserialize` reverses. `Data.Signature` (lazily populated) signs already-encrypted bytes — Data never sees plaintext. v1 ships `crypto.encrypt`/`crypto.decrypt` as identity pass-through (real AES-GCM tracked in `Documentation/Runtime2/todos.md`).
10. **`app.Callback` is config, not an `ICallback`.** `app.Callback.Signature.ExpiresInMs` reads through "the App's `Callback` config holder, its `Signature` sub-config, the `ExpiresInMs` value." Default `null`. Data's lazy signature getter reads it from `Context.App.Callback.Signature.ExpiresInMs` *only when the wrapped value is an `ICallback`*. Two distinct things share the word *Signature* — keep them straight: `Data.Signature` is wire envelope; `app.Callback.Signature` is config.

Combined consequence: **this design ships on existing infrastructure plus two new `crypto` actions** (encrypt/decrypt, identity pass-through in v1). No new modules, no new core abstractions.

## Topic files

The mechanism:

- [resume.md](plan/resume.md) — bind-jump-run, two issuers + their capture policies, position semantics, the issue/resume movie
- [snapshotted-system.md](plan/snapshotted-system.md) — `ISnapshotted` interface, three buckets, full subsystem inventory, providers two-layer snapshot, callstack as positional context
- [variable-capture.md](plan/variable-capture.md) — error-retry's throw-time vars via Diff reverse-apply
- [plang-surfaces.md](plan/plang-surfaces.md) — `%!error.callback%`, `- run %callback%`, ask-user `vars:` annotation
- [callback-schema.md](plan/callback-schema.md) — the `Callback` record shape

The durability layer:

- [transparent-signing.md](plan/transparent-signing.md) — the IO hook in `App.Channels.Serializers`
- [signature-rename.md](plan/signature-rename.md) — `SignedData` → `Signature` OBP cleanup
- [resume-mechanics.md](plan/resume-mechanics.md) — the issue/resume flow across the serialization boundary, goal lookup, `Goal.Hash` gate

Forward-compat + housekeeping:

- [encryption-layering.md](plan/encryption-layering.md) — encryption owned by Callback's serializer; v1 ships `crypto.encrypt`/`crypto.decrypt` as identity pass-through
- [test-strategy.md](plan/test-strategy.md) — integration cuts (in-process resume + durability round-trip) + test layer mapping
- [test-coverage.md](plan/test-coverage.md) — coverage matrix per topic, failure matrix, new-surfaces inventory
- [open-threads.md](plan/open-threads.md) — deferred items + settled rejections

## Settled rejections that span topics

- **`Data.Pause` lane.** A callback is a successful `Data` value whose payload happens to be an `ICallback`. Engine has one extra branch ("is the value an ICallback? unwind without continuing"). No new Ok/Fail/Pause trichotomy.
- **Replay-style resume.** Never. Bind + jump only.
- **Ref-capture across islands.** Brainstormed, no real use case, closed. Values only. Serializer-level blob dedup is the answer if size becomes a problem.
- **Cache in the snapshot.** Rejected. Cache is a hint, not state. If it must survive resume, it's a Variable.
- **Memory channel buffers carried across resume.** Channels are I/O, not inter-step state.
- **Capturing `Identity` object instead of name.** Names + referent integrity is the contract; objects are never carried. Same for every selection-shaped piece of state.
- **"Header" outside the signed envelope.** Whole `Data<Callback>` is signed; nothing meaningful sits outside the signature.
- **A `Seek` verb on the engine.** No new verb. Same main loop, different entry point.
- **`CallbackOrigin` typed item on the resumed root Call.** Rejected — telemetry stitching is a log-layer concern, correlated by callback identity.
- **`Cause` extension to cross-process.** Stays same-process only.
- **Auto-verification on Data read.** Rejected. Reads do not verify automatically; the consumer that cares (`- run %callback%`, `- verify %x%`) explicitly invokes verify.
- **Default expiry on auto-signed Data.** Rejected. Default = no expiry. Integrity, not validity, is what the default protects.
- **Encryption at the Data layer.** Rejected. Belongs to Callback; most Data writes don't need encryption.
- **Built-in `callback.store` / `callback.load` actions.** Rejected. Storage is the developer's concern via existing `file.*`, `db.*`, channel actions.
