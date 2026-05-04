# architect — runtime2-callback

## v1 — Callback design ([details](v1/summary.md))

Two-pass whiteboard with Ingi: the design shape, then the subsystem walk.

Locked: state-machine restoration via *seek + bind*, never replay. Two issuers (`ask user`, error-retry) share one engine-level resume primitive but follow different capture policies. Each OBP `@this` declares its own snapshot via `ISnapshotted` with three buckets (snapshot-and-restore, reconstruct-on-build, drop). Islands rule — values only, no cross-graph identity. Error-retry captures throw-time variables by reverse-applying the callstack's diff stream; Diff auto-flips on error.

Cache is audit-derived rather than `ISnapshotted`: walk Calls for `cache.set`/`cache.tryAdd`, collect keys, ask the live cache for current state of each. Pluggable-cache friendly, per-run scoped.

`App.Providers` is snapshot-and-restore at the registry layer (default selections + runtime registrations with DLL paths). Provider instances stay reconstruct-on-build; none of the built-ins hold inter-action mutable state.

Synthesis principle: **snapshots store names; they store values only when the name doesn't determine the value.** Variables and Cache are values; provider selections, identity, datasource, encryption, mode flags are names. Two trust layers gate resume: signature integrity (envelope) and referent integrity (named state still exists). Whole `Data<Callback>` is signed — no header outside the signature, tampering is all-or-nothing rejection.

No code changed — design only. Final snapshot surface is small: three `Variables` (per-actor), `Errors.Trail`, registry selections, `App._statics`, audit-derived cache tuples. Open threads (wire format, storage ergonomics, ask-user builder annotation) deferred to next sessions.
