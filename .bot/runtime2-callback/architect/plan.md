# Callback — design spine

This branch designs the **callback mechanism** end-to-end: how PLang preserves runtime state at a failure or pause point, persists it durably, verifies it on resume, and runs it from a fresh `App` process. Two issuers — `ask user` and error-retry — share one `App.Run(callback)` entry point. Encryption and HTTP wire transport (the ask-user case in full) are deferred to a future branch — the layering is settled here so that future branch doesn't reshape this.

## The few insights that reshape this design

These cross-cut multiple topic files. If you only know these, you can navigate the rest.

1. **Bind, jump, run — never replay.** Resume jumps directly to `(goal X, step Y, action Z)` with carried state already bound, and continues from there. No prior step is re-executed. The engine has one main loop; `App.Run(goal)` and `App.Run(callback)` only differ in *where the loop's first tick lands*. No `Seek` verb.
2. **Variables are the values that survive resume. Everything else is a name.** Variables get full payload capture; provider selections, identity, datasource, modes are captured as names that resolve through their existing registries. Two trust layers gate resume: signature integrity (envelope) and referent integrity (the names still resolve).
3. **Per-type `ISnapshotted` — three buckets.** Each OBP `@this` type declares its own snapshot discipline. No central registry. Three buckets: snapshot-and-restore (variables, errors, registry-layer providers, statics), reconstruct-on-build (modules, goals, channels, cache, etc.), drop (live callstack tree, timing, network state).
4. **Signing is transparent at the Data IO boundary.** Any `Data.@this` written through the Channels serializer gets its `Signature` auto-populated by the existing `signing` module. Default = no expiry (integrity, not validity). No explicit `- sign` is needed at the issue site.
5. **`Goal.Hash` already exists** at `PLang/App/Goals/Goal/this.cs:121` — SHA-256 of `Name + concatenated step text`. We use it directly for `Callback.GoalHash`. No new hashing.
6. **Encryption is a Callback-internal concern, not a Data-layer concern.** Most Data writes (logs, files, debug) don't need encryption. Callback owns its own encryption discipline (deferred to the ask-user branch). The Data layer signs the encrypted bytes.

Combined consequence: **this design ships entirely on existing infrastructure.** No new modules, no new crypto, no new core abstractions.

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

- [encryption-layering.md](plan/encryption-layering.md) — why encryption belongs to Callback (sets up the future ask-user branch)
- [test-strategy.md](plan/test-strategy.md) — smallest meaningful first cuts (in-process and durability)
- [open-threads.md](plan/open-threads.md) — deferred items + settled rejections

## Settled rejections that span topics

- **`Data.Pause` lane.** Callback is a successful `Data` value whose payload happens to be a Callback record. Engine has one extra branch ("is the value a Callback? unwind without continuing"). No new Ok/Fail/Pause trichotomy.
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
