# architect — runtime2-callback

## v2 — Wire format and Phase 2 staging ([details](v2/summary.md))

The wire/sign/persistence layer above v1's snapshot/restore design, scoped to Phase 2 (error-retry only). Phase 3 (ask-user + encryption) deferred.

Three insights reshape it: **(1)** signing is transparent at the Data IO boundary — any `Data` write picks up a default no-expiry signature via the existing `signing` module, no explicit `- sign` needed at issue sites; **(2)** `Goal.Hash` already exists at `App/Goals/Goal/this.cs:121` (SHA-256 of name + step text), so `Callback.GoalHash = goal.Hash` directly with no new infra; **(3)** encryption belongs to the Callback class, invoked during its own serialization, not at the Data layer — most Data writes don't need encryption.

Combined consequence: **Phase 2 ships entirely on existing infrastructure.** No new modules, no new crypto, no new core abstractions. The work is the transparent-IO hook, schema corrections (`GoalPrPath` added, `Expiry` removed in favor of `Data.Signature.Expires`), the `SignedData` → `Signature` OBP rename, and the `App.Run(callback)` entry point. Phase 3 adds encryption (`ICryptoProvider.Encrypt/Decrypt`) only when ask-user lands.

## v1 — Callback design ([details](v1/summary.md))

Three-pass whiteboard with Ingi: design shape, subsystem walk, correction pass.

State-machine restoration via *bind state, jump to position, run* — never replay. Two issuers (`ask user`, error-retry) share one `App.Run(callback)` entry point but follow different capture policies. Per-type `ISnapshotted` (using `Snapshot.@this` and `Context.@this`). Three buckets — snapshot-and-restore, reconstruct-on-build, drop. Islands rule — values only.

Error-retry captures throw-time variables by reverse-applying the callstack's diff stream; Diff auto-flips on error. `App.Providers` is snapshot-and-restore at the registry layer (default selections + runtime registrations). Cache is **not** snapshotted — it's a hint, not state. Resume always lands **at the action** in both modes. No `Seek` verb. No `CallbackOrigin`.

Synthesis principle: **Variables are the values that survive resume. Everything else is a name.** Two trust layers gate resume: signature integrity (envelope) and referent integrity (named state still exists).
